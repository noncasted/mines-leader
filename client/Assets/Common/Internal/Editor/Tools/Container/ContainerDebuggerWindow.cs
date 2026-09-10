using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    /// <summary>
    /// Read-only view of live containers. Data comes only from
    /// <see cref="ContainerRegistryDebug"/> / <see cref="IContainerDiagnostics"/>.
    /// </summary>
    public class ContainerDebuggerWindow : EditorWindow
    {
        private const string UssPath = "Assets/Common/Internal/Editor/Tools/Container/ContainerDebuggerWindow.uss";

        private readonly List<RegistrationInfo> _registrations = new();
        private readonly List<ResolveRecord> _history = new();
        private readonly Dictionary<int, IContainerDiagnostics> _diagnosticsById = new();
        private readonly Dictionary<int, RegistrationInfo> _registrationsBySlot = new();
        private readonly HashSet<IContainerDiagnostics> _visited = new();

        private Lifetime _lifetime;
        private IContainerDiagnostics _selectedContainer;
        private int _selectedSlot = -1;
        private int _nextId = 1;
        private bool _historyEnabled;
        private bool _isRefreshing;

        private VisualElement _root;
        private Label _statusLabel;
        private Label _emptyTreeLabel;
        private Label _emptyDetailsLabel;
        private Label _containerTitle;
        private Toggle _historyToggle;
        private TreeView _treeView;
        private MultiColumnListView _registrationsView;
        private MultiColumnListView _historyView;
        private VisualElement _details;
        private VisualElement _graphBody;
        private VisualElement _buildBody;
        private Label _historyHint;

        [MenuItem("Tools/Container Debugger")]
        public static void Open()
        {
            var window = GetWindow<ContainerDebuggerWindow>();
            window.titleContent = new GUIContent("Container Debugger");
            window.minSize = new Vector2(800f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _lifetime?.Terminate();
            _lifetime = new Lifetime();

            try
            {
                ContainerRegistryDebug.Changed.Advise(_lifetime, Refresh);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _lifetime?.Terminate();
            _lifetime = null;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            _root = new VisualElement();
            _root.AddToClassList("container-debugger-root");
            _root.style.flexGrow = 1;
            rootVisualElement.Add(_root);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            if (styleSheet != null)
                _root.styleSheets.Add(styleSheet);

            BuildHeader();
            BuildToolbar();
            BuildBody();
            BuildFooter();
            Refresh();
        }

        private void OnPlayModeChanged(PlayModeStateChange _)
        {
            EditorApplication.delayCall += Refresh;
        }

        private void Refresh()
        {
            if (this == null || _root == null || _isRefreshing)
                return;

            _isRefreshing = true;

            try
            {
                RefreshTree();
                ApplyHistoryFlag();
                RebuildDetails();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("Failed to read container diagnostics", true);
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("header");

            var title = new Label("Container Debugger");
            title.AddToClassList("header-title");
            header.Add(title);

            _root.Add(header);
        }

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            var refresh = new Button(Refresh) { text = "Refresh" };
            refresh.AddToClassList("toolbar-button");
            toolbar.Add(refresh);

            _historyToggle = new Toggle("Resolve history")
            {
                value = _historyEnabled
            };
            _historyToggle.AddToClassList("history-toggle");
            _historyToggle.tooltip = "When on, writes Resolve timing into IContainerDiagnostics.History";
            _historyToggle.RegisterValueChangedCallback(OnHistoryToggled);
            toolbar.Add(_historyToggle);

            _root.Add(toolbar);
        }

        private void BuildBody()
        {
            var body = new VisualElement();
            body.AddToClassList("body");

            body.Add(BuildTreePane());
            body.Add(BuildDetailsPane());

            _root.Add(body);
        }

        private VisualElement BuildTreePane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("tree-pane");

            var title = new Label("Containers");
            title.AddToClassList("pane-title");
            pane.Add(title);

            _treeView = new TreeView
            {
                fixedItemHeight = 22,
                selectionType = SelectionType.Single,
                autoExpand = true
            };
            _treeView.AddToClassList("container-tree");
            _treeView.makeItem = () =>
            {
                var label = new Label();
                label.AddToClassList("tree-item");
                return label;
            };
            _treeView.bindItem = BindTreeItem;
            _treeView.selectedIndicesChanged += OnTreeSelectionChanged;
            pane.Add(_treeView);

            _emptyTreeLabel = new Label();
            _emptyTreeLabel.AddToClassList("empty-state");
            pane.Add(_emptyTreeLabel);

            return pane;
        }

        private VisualElement BuildDetailsPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("details-pane");

            _emptyDetailsLabel = new Label();
            _emptyDetailsLabel.AddToClassList("empty-state");
            pane.Add(_emptyDetailsLabel);

            _details = new VisualElement();
            _details.AddToClassList("details");

            _containerTitle = new Label();
            _containerTitle.AddToClassList("container-title");
            _details.Add(_containerTitle);

            _details.Add(BuildRegistrationsSection());

            var lower = new VisualElement();
            lower.AddToClassList("lower");
            lower.Add(BuildGraphSection());
            lower.Add(BuildOrderSection());
            _details.Add(lower);

            _details.Add(BuildHistorySection());
            pane.Add(_details);

            return pane;
        }

        private VisualElement BuildRegistrationsSection()
        {
            var section = new VisualElement();
            section.AddToClassList("section");
            section.AddToClassList("registrations-section");

            var title = new Label("Registrations");
            title.AddToClassList("section-title");
            section.Add(title);

            var columns = new Columns();
            columns.Add(MakeRegistrationColumn("slot", "Slot", 56, false, info => info.Slot.ToString()));
            columns.Add(MakeRegistrationColumn("implementation", "Implementation", 220, false, info => FormatType(info.ImplementationType)));
            columns.Add(MakeRegistrationColumn("services", "Service types", 240, true, info => FormatServiceTypes(info.ServiceTypes)));
            columns.Add(MakeRegistrationColumn("lifetime", "Lifetime", 90, false, info => info.Lifetime.ToString()));
            columns.Add(MakeRegistrationColumn("instantiated", "Instantiated", 90, false, info => FormatFlag(info.IsInstantiated)));
            columns.Add(MakeRegistrationColumn("generated", "Generated", 90, false, info => FormatFlag(info.IsGenerated)));

            _registrationsView = new MultiColumnListView(columns)
            {
                itemsSource = _registrations,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All,
                showBorder = true,
                fixedItemHeight = 22
            };
            _registrationsView.AddToClassList("table");
            _registrationsView.selectedIndicesChanged += OnRegistrationSelected;
            section.Add(_registrationsView);

            return section;
        }

        private Column MakeRegistrationColumn(
            string name,
            string title,
            float width,
            bool stretchable,
            Func<RegistrationInfo, string> read)
        {
            return new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = 48,
                stretchable = stretchable,
                resizable = true,
                sortable = false,
                makeCell = MakeCell,
                bindCell = (element, index) => BindRegistrationCell(element, index, read)
            };
        }

        private VisualElement BuildGraphSection()
        {
            var section = new VisualElement();
            section.AddToClassList("section");
            section.AddToClassList("graph-section");

            var title = new Label("Dependencies");
            title.AddToClassList("section-title");
            section.Add(title);

            var graphScroll = new ScrollView(ScrollViewMode.Vertical);
            graphScroll.AddToClassList("section-body");
            _graphBody = new VisualElement();
            graphScroll.Add(_graphBody);
            section.Add(graphScroll);

            return section;
        }

        private VisualElement BuildOrderSection()
        {
            var section = new VisualElement();
            section.AddToClassList("section");
            section.AddToClassList("build-section");

            var title = new Label("Build order");
            title.AddToClassList("section-title");
            section.Add(title);

            var buildScroll = new ScrollView(ScrollViewMode.Vertical);
            buildScroll.AddToClassList("section-body");
            _buildBody = new VisualElement();
            buildScroll.Add(_buildBody);
            section.Add(buildScroll);

            return section;
        }

        private VisualElement BuildHistorySection()
        {
            var section = new VisualElement();
            section.AddToClassList("section");
            section.AddToClassList("history-section");

            var title = new Label("Resolve history");
            title.AddToClassList("section-title");
            section.Add(title);

            _historyHint = new Label();
            _historyHint.AddToClassList("hint");
            section.Add(_historyHint);

            var columns = new Columns();
            columns.Add(MakeHistoryColumn("slot", "Slot", 56, false, record => record.Slot.ToString()));
            columns.Add(MakeHistoryColumn("type", "Implementation", 220, true, record => TypeNameForSlot(record.Slot)));
            columns.Add(MakeHistoryColumn("milliseconds", "ms", 80, false, record => record.Milliseconds.ToString("F3")));
            columns.Add(MakeHistoryColumn("frame", "Frame", 70, false, record => record.Frame.ToString()));

            _historyView = new MultiColumnListView(columns)
            {
                itemsSource = _history,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All,
                showBorder = true,
                fixedItemHeight = 22
            };
            _historyView.AddToClassList("table");
            _historyView.selectedIndicesChanged += OnHistorySelected;
            section.Add(_historyView);

            return section;
        }

        private Column MakeHistoryColumn(
            string name,
            string title,
            float width,
            bool stretchable,
            Func<ResolveRecord, string> read)
        {
            return new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = 48,
                stretchable = stretchable,
                resizable = true,
                sortable = false,
                makeCell = MakeCell,
                bindCell = (element, index) => BindHistoryCell(element, index, read)
            };
        }

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("footer");

            _statusLabel = new Label();
            _statusLabel.AddToClassList("status-label");
            footer.Add(_statusLabel);

            _root.Add(footer);
        }

        private void RefreshTree()
        {
            var roots = SafeList(() => ContainerRegistryDebug.Roots);
            var items = BuildTreeItems(roots);

            _treeView.SetRootItems(items);
            _treeView.Rebuild();
            _treeView.ExpandAll();

            var hasRoots = items.Count > 0;
            _treeView.style.display = hasRoots ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyTreeLabel.style.display = hasRoots ? DisplayStyle.None : DisplayStyle.Flex;
            _emptyTreeLabel.text = "No containers";

            var selectedId = FindId(_selectedContainer);

            if (selectedId < 0 && items.Count > 0)
            {
                selectedId = items[0].id;
                _selectedContainer = items[0].data;
                _selectedSlot = -1;
            }

            if (selectedId < 0)
                _selectedContainer = null;

            if (selectedId >= 0)
                _treeView.SetSelectionById(selectedId);
            else
                _treeView.ClearSelection();

            SetStatus(FormatStatus(roots.Count), false);
        }

        private List<TreeViewItemData<IContainerDiagnostics>> BuildTreeItems(
            IReadOnlyList<IContainerDiagnostics> roots)
        {
            _diagnosticsById.Clear();
            _visited.Clear();
            _nextId = 1;

            var items = new List<TreeViewItemData<IContainerDiagnostics>>();

            foreach (var root in roots)
            {
                if (root == null)
                    continue;

                items.Add(BuildTreeNode(root));
            }

            return items;
        }

        private TreeViewItemData<IContainerDiagnostics> BuildTreeNode(IContainerDiagnostics diagnostics)
        {
            var id = _nextId++;
            _diagnosticsById[id] = diagnostics;

            var children = new List<TreeViewItemData<IContainerDiagnostics>>();

            if (_visited.Add(diagnostics) == false)
                return new TreeViewItemData<IContainerDiagnostics>(id, diagnostics, children);

            foreach (var child in SafeList(() => diagnostics.Children))
            {
                if (child == null)
                    continue;

                children.Add(BuildTreeNode(child));
            }

            return new TreeViewItemData<IContainerDiagnostics>(id, diagnostics, children);
        }

        private void BindTreeItem(VisualElement element, int index)
        {
            var label = (Label)element;

            try
            {
                var diagnostics = _treeView.GetItemDataForIndex<IContainerDiagnostics>(index);
                label.text = FormatContainerLabel(diagnostics);
                label.tooltip = FormatPath(diagnostics);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                label.text = "(error)";
                label.tooltip = exception.Message;
            }
        }

        private void OnTreeSelectionChanged(IEnumerable<int> indices)
        {
            if (_isRefreshing)
                return;

            var index = FirstIndex(indices);
            IContainerDiagnostics next = null;

            if (index >= 0)
            {
                try
                {
                    next = _treeView.GetItemDataForIndex<IContainerDiagnostics>(index);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            if (ReferenceEquals(_selectedContainer, next))
                return;

            _selectedContainer = next;
            _selectedSlot = -1;
            ApplyHistoryFlag();
            RebuildDetails();
        }

        private void RebuildDetails()
        {
            var hasContainer = _selectedContainer != null;
            _details.style.display = hasContainer ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyDetailsLabel.style.display = hasContainer ? DisplayStyle.None : DisplayStyle.Flex;
            _emptyDetailsLabel.text = EmptyMessage();

            if (hasContainer == false)
            {
                _registrations.Clear();
                _registrationsBySlot.Clear();
                _history.Clear();
                _registrationsView?.Rebuild();
                _historyView?.Rebuild();
                _graphBody?.Clear();
                _buildBody?.Clear();
                return;
            }

            _containerTitle.text = FormatPath(_selectedContainer);
            _containerTitle.tooltip = _containerTitle.text;

            _registrations.Clear();
            _registrationsBySlot.Clear();

            foreach (var info in SafeList(() => _selectedContainer.Registrations))
            {
                _registrations.Add(info);
                _registrationsBySlot[info.Slot] = info;
            }

            _registrationsView.Rebuild();

            var selectedIndex = IndexOfSlot(_selectedSlot);

            if (selectedIndex >= 0)
                _registrationsView.selectedIndex = selectedIndex;
            else
            {
                _selectedSlot = -1;
                _registrationsView.ClearSelection();
            }

            RebuildGraph();
            RebuildBuildOrder();
            RebuildHistory();
        }

        private void OnRegistrationSelected(IEnumerable<int> indices)
        {
            if (_isRefreshing)
                return;

            var index = FirstIndex(indices);
            _selectedSlot = index >= 0 && index < _registrations.Count
                ? _registrations[index].Slot
                : -1;

            RebuildGraph();
            RebuildBuildOrder();
        }

        private void RebuildGraph()
        {
            _graphBody.Clear();

            if (_selectedSlot < 0)
            {
                _graphBody.Add(Hint("Select a registration to inspect its dependencies."));
                return;
            }

            if (_registrationsBySlot.TryGetValue(_selectedSlot, out var selected) == false)
            {
                _graphBody.Add(Hint($"Slot {_selectedSlot} is not in this container."));
                return;
            }

            _graphBody.Add(MakeCaption("Depends on"));

            var dependencies = selected.Dependencies;
            var hasDependencies = dependencies != null && dependencies.Count > 0;

            if (hasDependencies == false)
                _graphBody.Add(Hint("No dependencies."));
            else
            {
                foreach (var slot in dependencies)
                    _graphBody.Add(MakeSlotChip(slot, slot == _selectedSlot));
            }

            _graphBody.Add(MakeCaption("Selected"));
            _graphBody.Add(MakeSlotChip(_selectedSlot, true));

            _graphBody.Add(MakeCaption("Used by"));

            var usedBy = 0;

            foreach (var info in _registrations)
            {
                var slots = info.Dependencies;

                if (slots == null)
                    continue;

                foreach (var slot in slots)
                {
                    if (slot != _selectedSlot)
                        continue;

                    _graphBody.Add(MakeSlotChip(info.Slot, false));
                    usedBy++;
                    break;
                }
            }

            if (usedBy == 0)
                _graphBody.Add(Hint("Nothing in this container depends on it."));
        }

        private void RebuildBuildOrder()
        {
            _buildBody.Clear();

            if (_selectedContainer == null)
            {
                _buildBody.Add(Hint("No build order."));
                return;
            }

            var order = SafeList(() => _selectedContainer.BuildOrder);

            if (order.Count == 0)
            {
                _buildBody.Add(Hint("No build order."));
                return;
            }

            for (var i = 0; i < order.Count; i++)
            {
                var slot = order[i];
                var row = MakeSlotChip(slot, slot == _selectedSlot);
                row.text = $"{i + 1}. {row.text}";
                _buildBody.Add(row);
            }
        }

        private void RebuildHistory()
        {
            if (_historyView == null || _historyHint == null)
                return;

            _history.Clear();

            if (_historyEnabled == false || _selectedContainer == null)
            {
                _historyHint.style.display = DisplayStyle.Flex;
                _historyHint.text = "Turn on Resolve history to record Resolve calls. Structure is always available.";
                _historyView.style.display = DisplayStyle.None;
                _historyView.Rebuild();
                return;
            }

            foreach (var record in SafeList(() => _selectedContainer.History))
                _history.Add(record);

            _historyView.Rebuild();
            _historyView.style.display = DisplayStyle.Flex;

            if (_history.Count == 0)
            {
                _historyHint.style.display = DisplayStyle.Flex;
                _historyHint.text = "No resolve records yet.";
            }
            else
            {
                _historyHint.style.display = DisplayStyle.None;
                _historyHint.text = string.Empty;
            }
        }

        private void OnHistorySelected(IEnumerable<int> indices)
        {
            if (_isRefreshing || _historyEnabled == false)
                return;

            var index = FirstIndex(indices);

            if (index < 0 || index >= _history.Count)
                return;

            SelectSlot(_history[index].Slot);
        }

        private void OnHistoryToggled(ChangeEvent<bool> evt)
        {
            _historyEnabled = evt.newValue;
            ApplyHistoryFlag();
            RebuildHistory();
        }

        private void ApplyHistoryFlag()
        {
            foreach (var diagnostics in _diagnosticsById.Values)
                SetHistoryEnabled(diagnostics, _historyEnabled);
        }

        private void SelectSlot(int slot)
        {
            _selectedSlot = slot;

            var index = IndexOfSlot(slot);

            if (index >= 0 && _registrationsView.selectedIndex != index)
                _registrationsView.selectedIndex = index;

            RebuildGraph();
            RebuildBuildOrder();
        }

        private Button MakeSlotChip(int slot, bool selected)
        {
            var hasInfo = _registrationsBySlot.TryGetValue(slot, out var info);
            var title = hasInfo ? FormatType(info.ImplementationType) : "(missing)";
            var lifetime = hasInfo ? info.Lifetime.ToString() : string.Empty;
            var text = string.IsNullOrEmpty(lifetime)
                ? $"[{slot}] {title}"
                : $"[{slot}] {title} · {lifetime}";

            var button = new Button(() => SelectSlot(slot)) { text = text };
            button.AddToClassList("slot-chip");

            if (selected)
                button.AddToClassList("slot-chip--selected");

            if (hasInfo)
            {
                button.tooltip =
                    $"Slot {slot}\n{FormatType(info.ImplementationType)}\n{FormatServiceTypes(info.ServiceTypes)}\n" +
                    $"Lifetime {info.Lifetime}\nInstantiated {FormatFlag(info.IsInstantiated)}\nGenerated {FormatFlag(info.IsGenerated)}";
            }
            else
            {
                button.tooltip = $"Slot {slot} is not in Registrations";
            }

            return button;
        }

        private void BindRegistrationCell(VisualElement element, int index, Func<RegistrationInfo, string> read)
        {
            var label = (Label)element;

            if ((uint)index >= (uint)_registrations.Count)
            {
                label.text = string.Empty;
                label.tooltip = string.Empty;
                return;
            }

            var info = _registrations[index];
            label.text = read(info);
            label.tooltip = $"{FormatType(info.ImplementationType)}\n{FormatServiceTypes(info.ServiceTypes)}";
        }

        private void BindHistoryCell(VisualElement element, int index, Func<ResolveRecord, string> read)
        {
            var label = (Label)element;

            if ((uint)index >= (uint)_history.Count)
            {
                label.text = string.Empty;
                label.tooltip = string.Empty;
                return;
            }

            var record = _history[index];
            label.text = read(record);
            label.tooltip = $"Slot {record.Slot} · {record.Milliseconds:F3} ms · frame {record.Frame}";
        }

        private int FindId(IContainerDiagnostics diagnostics)
        {
            if (diagnostics == null)
                return -1;

            foreach (var pair in _diagnosticsById)
            {
                if (ReferenceEquals(pair.Value, diagnostics))
                    return pair.Key;
            }

            return -1;
        }

        private int IndexOfSlot(int slot)
        {
            if (slot < 0)
                return -1;

            for (var i = 0; i < _registrations.Count; i++)
            {
                if (_registrations[i].Slot == slot)
                    return i;
            }

            return -1;
        }

        private string TypeNameForSlot(int slot)
        {
            return _registrationsBySlot.TryGetValue(slot, out var info)
                ? FormatType(info.ImplementationType)
                : "(missing)";
        }

        private void SetStatus(string message, bool isError)
        {
            if (_statusLabel == null)
                return;

            _statusLabel.text = message;
            _statusLabel.EnableInClassList("status-label--error", isError);
        }

        private static Label MakeCell()
        {
            var label = new Label();
            label.AddToClassList("table-cell");
            return label;
        }

        private static Label MakeCaption(string text)
        {
            var label = new Label(text);
            label.AddToClassList("graph-caption");
            return label;
        }

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.AddToClassList("hint");
            return label;
        }

        private static void SetHistoryEnabled(IContainerDiagnostics diagnostics, bool enabled)
        {
            if (diagnostics == null)
                return;

            try
            {
                if (diagnostics.IsHistoryEnabled == enabled)
                    return;

                diagnostics.IsHistoryEnabled = enabled;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static string FormatContainerLabel(IContainerDiagnostics diagnostics)
        {
            var name = ContainerName(diagnostics);
            var count = SafeList(() => diagnostics.Registrations).Count;
            return $"{name}  ({count})";
        }

        private static string FormatPath(IContainerDiagnostics diagnostics)
        {
            var parts = new List<string>();
            var current = diagnostics;
            var guard = 0;

            while (current != null && guard < 64)
            {
                parts.Add(ContainerName(current));
                current = SafeParent(current);
                guard++;
            }

            parts.Reverse();
            return string.Join(" / ", parts);
        }

        private static string ContainerName(IContainerDiagnostics diagnostics)
        {
            if (diagnostics == null)
                return "(none)";

            try
            {
                return string.IsNullOrEmpty(diagnostics.Name) ? "(unnamed)" : diagnostics.Name;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return "(error)";
            }
        }

        private static IContainerDiagnostics SafeParent(IContainerDiagnostics diagnostics)
        {
            try
            {
                return diagnostics.Parent;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        private static IReadOnlyList<T> SafeList<T>(Func<IReadOnlyList<T>> read)
        {
            try
            {
                return read() ?? Array.Empty<T>();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Array.Empty<T>();
            }
        }

        private static string FormatType(Type type)
        {
            if (type == null)
                return "—";

            if (type.IsGenericType == false)
                return type.Name;

            var tick = type.Name.IndexOf('`');
            var name = tick >= 0 ? type.Name.Substring(0, tick) : type.Name;
            var arguments = type.GetGenericArguments();
            var inner = new string[arguments.Length];

            for (var i = 0; i < arguments.Length; i++)
                inner[i] = FormatType(arguments[i]);

            return $"{name}<{string.Join(", ", inner)}>";
        }

        private static string FormatServiceTypes(IReadOnlyList<Type> types)
        {
            if (types == null || types.Count == 0)
                return "—";

            if (types.Count == 1)
                return FormatType(types[0]);

            var names = new string[types.Count];

            for (var i = 0; i < types.Count; i++)
                names[i] = FormatType(types[i]);

            return string.Join(", ", names);
        }

        private static string FormatFlag(bool value)
        {
            return value ? "yes" : "no";
        }

        private string FormatStatus(int rootCount)
        {
            if (rootCount == 0)
                return EmptyMessage();

            var registrations = 0;

            foreach (var diagnostics in _diagnosticsById.Values)
                registrations += SafeList(() => diagnostics.Registrations).Count;

            return $"{rootCount} root(s) · {_diagnosticsById.Count} container(s) · {registrations} registration(s)" +
                   (EditorApplication.isPlaying ? " · Play Mode" : " · Edit Mode");
        }

        private static string EmptyMessage()
        {
            return EditorApplication.isPlaying
                ? "No containers yet. Waiting for a container Build."
                : "No containers. Enter Play Mode to inspect a built graph.";
        }

        private static int FirstIndex(IEnumerable<int> indices)
        {
            foreach (var index in indices)
                return index;

            return -1;
        }
    }
}
