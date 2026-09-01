using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    /// <summary>
    /// Водопад трассы загрузки: список скоупов деревом слева и их отрезки на общей
    /// шкале времени справа. Трассы пишет <see cref="ProfilerTraceStorage"/> в плей-моде,
    /// окно их только читает.
    /// </summary>
    public class ProfilerTraceWindow : EditorWindow
    {
        private const float NameColumnWidth = 380f;
        private const float RowHeight = 22f;
        private const float MinRowHeight = 14f;
        private const float ValueFontSize = 10f;
        private const float TicksCount = 4f;

        private static readonly Color[] _barColors =
        {
            new(0.29f, 0.72f, 0.71f),
            new(0.36f, 0.63f, 0.80f),
            new(0.55f, 0.68f, 0.45f),
            new(0.85f, 0.66f, 0.38f),
            new(0.72f, 0.52f, 0.75f),
            new(0.83f, 0.47f, 0.47f)
        };

        private readonly HashSet<int> _collapsed = new();

        private float _rowHeight = RowHeight;

        private List<string> _files = new();
        private string _external;
        private ProfilerTraceData _trace;
        private string _selectedFile;

        private ToolbarMenu _tracesMenu;
        private ToolbarButton _copyPath;
        private Label _summary;
        private VisualElement _axis;
        private ScrollView _rows;

        [MenuItem("Tools/Startup Profiler")]
        public static void Open()
        {
            var window = GetWindow<ProfilerTraceWindow>();
            window.titleContent = new GUIContent("Startup Profiler");
            window.minSize = new Vector2(720f, 300f);
            window.Show();
        }

        private void OnEnable()
        {
            ProfilerTraceStorage.Saved += OnTraceSaved;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            ProfilerTraceStorage.Saved -= OnTraceSaved;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        /// <summary>
        /// Если из плей-мода вышли, не дойдя до меню, трасса так и осталась открытой —
        /// закрываем её сами, чтобы недогруженный запуск тоже было видно в окне.
        /// </summary>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode)
                return;

            if (GameProfiler.IsRunning)
                GameProfiler.Finish();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = new Color(0.16f, 0.17f, 0.19f);

            root.Add(CreateToolbar());

            _summary = new Label
            {
                style =
                {
                    color = new Color(0.72f, 0.74f, 0.78f),
                    paddingLeft = 8f,
                    paddingTop = 6f,
                    paddingBottom = 6f,
                    fontSize = 12f
                }
            };

            root.Add(_summary);

            _axis = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    height = 22f,
                    borderBottomWidth = 1f,
                    borderBottomColor = new Color(0.3f, 0.31f, 0.34f)
                }
            };

            root.Add(_axis);

            _rows = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { flexGrow = 1f }
            };

            // Высота строки считается от высоты списка, поэтому пересобираем его на каждом
            // изменении размера окна.
            _rows.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (Mathf.Abs(evt.newRect.height - evt.oldRect.height) < 1f)
                    return;

                RebuildRows();
            });

            root.Add(_rows);

            Refresh();
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new Toolbar();

            var refresh = new ToolbarButton(Refresh) { text = "Refresh" };
            toolbar.Add(refresh);

            _tracesMenu = new ToolbarMenu { text = "Traces" };
            toolbar.Add(_tracesMenu);

            // Сбор кадров держит запись профайлера включённой весь запуск — это заметная плата,
            // поэтому его видно и выключается он тут же.
            var frames = new ToolbarToggle { text = "Frames", value = ProfilerFrameCapture.Enabled };
            frames.tooltip = "Снимать сэмплы Unity-профайлера по кадрам трассы";
            frames.RegisterValueChangedCallback(evt => ProfilerFrameCapture.Enabled = evt.newValue);
            toolbar.Add(frames);

            toolbar.Add(new ToolbarButton(ExpandAll) { text = "Expand all" });
            toolbar.Add(new ToolbarButton(CollapseAll) { text = "Collapse all" });

            toolbar.Add(new ToolbarButton(() =>
            {
                if (EditorUtility.DisplayDialog("Startup Profiler", "Удалить все сохранённые трассы?", "Удалить", "Отмена") == false)
                    return;

                ProfilerTraceStorage.Clear();
                _trace = null;
                _selectedFile = null;
                Refresh();
            }) { text = "Clear" });

            toolbar.Add(new ToolbarButton(() => EditorUtility.RevealInFinder(ProfilerTraceStorage.Directory)) { text = "Open folder" });

            // Путь к открытой трассе нужен, чтобы утащить её в чат или в другой инструмент:
            // каталогов два, а имя файла — метка времени, руками такое не набирают.
            _copyPath = new ToolbarButton(CopySelectedPath) { text = "Copy path" };
            toolbar.Add(_copyPath);

            // Трасса может приехать с другой машины или с устройства — тогда её просто
            // открывают файлом, мимо обоих известных каталогов.
            toolbar.Add(new ToolbarButton(() =>
            {
                var path = EditorUtility.OpenFilePanel("Open trace", ProfilerTraceStorage.Directory, "json");

                if (string.IsNullOrEmpty(path))
                    return;

                _external = path;
                _selectedFile = path;

                Refresh();
            }) { text = "Open file…" });

            return toolbar;
        }

        private void OnTraceSaved(ProfilerTraceData trace)
        {
            _selectedFile = null;
            EditorApplication.delayCall += Refresh;
        }

        private void Refresh()
        {
            if (_rows == null)
                return;

            _files = ProfilerTraceStorage.List()
                                        .Where(file => string.IsNullOrEmpty(file) == false)
                                        .ToList();

            if (string.IsNullOrEmpty(_external) == false && _files.Contains(_external) == false)
                _files.Insert(0, _external);

            if (string.IsNullOrEmpty(_selectedFile) || _files.Contains(_selectedFile) == false)
                _selectedFile = _files.FirstOrDefault();

            _trace = _selectedFile == null ? ProfilerTraceStorage.Last : ProfilerTraceStorage.Load(_selectedFile);

            RebuildTracesMenu();
            RebuildAxis();
            CollapseSpans();
            RebuildRows();

            // Трасса из ProfilerTraceStorage.Last живёт в памяти и файла за собой не имеет.
            _copyPath?.SetEnabled(string.IsNullOrEmpty(_selectedFile) == false);
        }

        private void CopySelectedPath()
        {
            if (string.IsNullOrEmpty(_selectedFile))
                return;

            var path = Path.GetFullPath(_selectedFile);

            EditorGUIUtility.systemCopyBuffer = path;
            ShowNotification(new GUIContent("Path copied"));
            Debug.Log($"[Profiler] Trace path copied: {path}");
        }

        private void RebuildTracesMenu()
        {
            _tracesMenu.menu.ClearItems();

            if (_files.Count == 0)
            {
                _tracesMenu.text = "No traces";
                return;
            }

            _tracesMenu.text = Describe(_selectedFile);

            foreach (var file in _files)
            {
                var captured = file;

                _tracesMenu.menu.AppendAction(
                    Describe(file),
                    _ =>
                    {
                        _selectedFile = captured;
                        Refresh();
                    },
                    _ => captured == _selectedFile ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
        }

        /// <summary>
        /// Трассы из билда лежат в persistentDataPath и в списке идут вперемешку с
        /// редакторными, поэтому источник пишем прямо в пункте меню.
        /// </summary>
        private static string Describe(string file)
        {
            // Пустой путь роняет Path.GetDirectoryName, а прийти он может и из списка,
            // и из отменённого диалога открытия файла.
            if (string.IsNullOrEmpty(file))
                return "trace";

            var name = Path.GetFileNameWithoutExtension(file);
            var directory = Path.GetDirectoryName(file);

            if (string.IsNullOrEmpty(directory))
                return name;

            if (directory == ProfilerTraceStorage.Directory)
                return $"editor · {name}";

            return Path.GetFullPath(directory).Contains("ProfilerTraces")
                ? $"build · {name}"
                : $"file · {name}";
        }

        private void RebuildAxis()
        {
            _axis.Clear();

            var title = new Label("Scope")
            {
                style =
                {
                    width = NameColumnWidth,
                    flexShrink = 0f,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.78f, 0.8f, 0.84f),
                    paddingLeft = 8f,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            _axis.Add(title);

            var timeline = new VisualElement { style = { flexGrow = 1f } };
            _axis.Add(timeline);

            var duration = _trace?.DurationMs ?? 0d;

            for (var i = 0; i <= TicksCount; i++)
            {
                var fraction = i / TicksCount;

                var label = new Label(FormatDuration(duration * fraction))
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = Length.Percent(fraction * 100f),
                        color = new Color(0.6f, 0.62f, 0.66f),
                        fontSize = 10f,
                        marginLeft = i == TicksCount ? -44f : 2f,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        top = 4f
                    }
                };

                timeline.Add(label);
                timeline.Add(CreateGridLine(fraction, 0.24f));
            }
        }

        private static VisualElement CreateGridLine(float fraction, float alpha)
        {
            return new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = Length.Percent(fraction * 100f),
                    top = 0f,
                    bottom = 0f,
                    width = 1f,
                    backgroundColor = new Color(1f, 1f, 1f, alpha)
                }
            };
        }

        private void RebuildRows()
        {
            _rows.Clear();

            if (_trace == null || _trace.Spans.Count == 0)
            {
                _summary.text = "Трасс нет. Запусти игру — профайлер сохранит замер загрузки на выходе в меню.";
                return;
            }

            var depth = _trace.Spans.Max(span => span.Depth) + 1;

            _summary.text = $"{_trace.Name}    •    Start {_trace.StartedAt}    •    Duration {FormatDuration(_trace.DurationMs)}" +
                            $"    •    Frames {_trace.Frames}    •    Spans {_trace.Spans.Count}    •    Depth {depth}" +
                            (_trace.Cold ? "    •    COLD (первый запуск после перекомпиляции)" : string.Empty);

            var children = new Dictionary<int, List<ProfilerSpanData>>();
            var byId = _trace.Spans.ToDictionary(span => span.Id);

            foreach (var span in _trace.Spans)
            {
                if (children.TryGetValue(span.ParentId, out var list) == false)
                {
                    list = new List<ProfilerSpanData>();
                    children.Add(span.ParentId, list);
                }

                list.Add(span);
            }

            var visible = new List<ProfilerSpanData>();

            foreach (var span in _trace.Spans)
            {
                if (IsVisible(span))
                    visible.Add(span);
            }

            _rowHeight = FitRowHeight(visible.Count);

            for (var index = 0; index < visible.Count; index++)
            {
                var span = visible[index];
                _rows.Add(CreateRow(span, children.ContainsKey(span.Id), index));
            }

            return;

            bool IsVisible(ProfilerSpanData span)
            {
                var current = span;

                while (current.ParentId >= 0 && byId.TryGetValue(current.ParentId, out var parent))
                {
                    current = parent;

                    if (_collapsed.Contains(current.Id))
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Развёрнутая трасса — это под сотню строк, и в вертикальном скроле водопад
        /// читать нечем: пропадает общая картина запуска. Поэтому строки ужимаются под
        /// высоту окна, пока это остаётся читаемым.
        /// </summary>
        private float FitRowHeight(int count)
        {
            if (count <= 0)
                return RowHeight;

            var available = _rows.resolvedStyle.height;

            // До первой раскладки высоты ещё нет — тогда рисуем в полный размер,
            // а пересоберёмся по GeometryChangedEvent.
            if (float.IsNaN(available) || available <= 1f)
                return RowHeight;

            return Mathf.Clamp(Mathf.Floor(available / count), MinRowHeight, RowHeight);
        }

        private VisualElement CreateRow(ProfilerSpanData span, bool hasChildren, int index)
        {
            var duration = Math.Max(_trace.DurationMs, 0.001d);
            var scale = _rowHeight / RowHeight;
            var barHeight = Mathf.Max(4f, Mathf.Round(10f * scale));
            var barTop = Mathf.Round((_rowHeight - barHeight) * 0.5f);
            var labelTop = Mathf.Round((_rowHeight - ValueFontSize * 1.4f) * 0.5f);

            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    height = _rowHeight,
                    backgroundColor = index % 2 == 0
                        ? new Color(0f, 0f, 0f, 0f)
                        : new Color(1f, 1f, 1f, 0.025f)
                }
            };

            var name = new VisualElement
            {
                style =
                {
                    width = NameColumnWidth,
                    flexShrink = 0f,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    overflow = Overflow.Hidden,
                    paddingLeft = 8f + span.Depth * Mathf.Round(14f * scale)
                }
            };

            if (hasChildren)
            {
                var isCollapsed = _collapsed.Contains(span.Id);

                var arrow = new Label(isCollapsed ? "▶" : "▼")
                {
                    style =
                    {
                        width = 14f,
                        fontSize = 8f,
                        color = new Color(0.65f, 0.67f, 0.7f),
                        unityTextAlign = TextAnchor.MiddleLeft
                    }
                };

                name.Add(arrow);

                // Клик по всей строке имени, а не только по стрелке: попадать в 14 пикселей
                // на глубокой вложенности неудобно.
                name.RegisterCallback<MouseDownEvent>(_ =>
                {
                    if (_collapsed.Remove(span.Id) == false)
                        _collapsed.Add(span.Id);

                    RebuildRows();
                });
            }
            else
            {
                name.Add(new VisualElement { style = { width = 14f } });
            }

            name.Add(new VisualElement
            {
                style =
                {
                    width = 3f,
                    height = Mathf.Max(6f, Mathf.Round(14f * scale)),
                    marginRight = 6f,
                    backgroundColor = ColorFor(span.Depth)
                }
            });

            name.Add(new Label(span.Name)
            {
                tooltip = span.Name,
                style =
                {
                    color = new Color(0.86f, 0.88f, 0.9f),
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    flexGrow = 1f
                }
            });

            row.Add(name);

            var timeline = new VisualElement { style = { flexGrow = 1f } };

            for (var i = 0; i <= TicksCount; i++)
                timeline.Add(CreateGridLine(i / TicksCount, 0.06f));

            var left = (float)(span.StartMs / duration * 100d);
            var width = Mathf.Max(0.15f, (float)(span.DurationMs / duration * 100d));

            var bar = new VisualElement
            {
                tooltip = $"{span.Name}\nstart {FormatDuration(span.StartMs)}\nduration {FormatDuration(span.DurationMs)}\nframes {span.Frames}",
                style =
                {
                    position = Position.Absolute,
                    left = Length.Percent(left),
                    width = Length.Percent(width),
                    top = barTop,
                    height = barHeight,
                    backgroundColor = ColorFor(span.Depth),
                    borderTopLeftRadius = 2f,
                    borderTopRightRadius = 2f,
                    borderBottomLeftRadius = 2f,
                    borderBottomRightRadius = 2f
                }
            };

            timeline.Add(bar);

            // Длинный отрезок упирается в правый край, поэтому его длительность пишем внутри бара.
            var isWide = left + width > 82f;

            // Кадры рядом с длительностью: отрезок, который ждал кадры, и отрезок,
            // который жёг процессор, выглядят на шкале одинаково, а чинятся по-разному.
            var text = FormatDuration(span.DurationMs) +
                       (span.Frames > 0 ? $"  ({span.Frames} fr)" : string.Empty) +
                       (span.Unfinished ? "  (unfinished)" : string.Empty);

            var value = new Label(text)
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = Length.Percent(isWide ? left : Mathf.Min(left + width, 99f)),
                    marginLeft = 6f,
                    top = labelTop,
                    fontSize = ValueFontSize,
                    color = isWide
                        ? new Color(0.1f, 0.12f, 0.14f)
                        : new Color(0.68f, 0.7f, 0.74f)
                }
            };

            timeline.Add(value);
            row.Add(timeline);

            return row;
        }

        private void ExpandAll()
        {
            _collapsed.Clear();
            RebuildRows();
        }

        private void CollapseAll()
        {
            CollapseSpans();
            RebuildRows();
        }

        /// <summary>
        /// Состояние по умолчанию: трасса открывается свёрнутой, дальше её разворачивают
        /// по тем этапам, которые оказались долгими.
        /// </summary>
        private void CollapseSpans()
        {
            _collapsed.Clear();

            if (_trace == null)
                return;

            foreach (var span in _trace.Spans)
            {
                if (span.Depth >= 1)
                    _collapsed.Add(span.Id);
            }
        }

        private static Color ColorFor(int depth)
        {
            return _barColors[Mathf.Clamp(depth, 0, _barColors.Length - 1)];
        }

        private static string FormatDuration(double ms)
        {
            if (ms < 1d)
                return $"{ms * 1000d:F0}µs";

            if (ms < 1000d)
                return $"{ms:F2}ms";

            return $"{ms / 1000d:F2}s";
        }
    }
}
