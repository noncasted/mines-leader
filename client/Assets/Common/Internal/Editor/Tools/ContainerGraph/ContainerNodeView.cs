using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    public sealed class ContainerNodeView : NodeView<ContainerNode>
    {
        private const string UssPath =
            "Assets/Common/Internal/Editor/Tools/ContainerGraph/ContainerGraph.uss";

        private VisualElement _panel;

        public override void OnViewBuilt()
        {
            _panel = BuildPanel();
            View.Root.Add(_panel);
        }

        public override void OnCullingChanged(bool cullingEnabled)
        {
            if (cullingEnabled == false && _panel != null)
                View.Root.Add(_panel);
        }

        private VisualElement BuildPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("container-node-panel");
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.marginTop = 4;
            panel.style.paddingLeft = 8;
            panel.style.paddingRight = 8;
            panel.style.paddingBottom = 6;
            panel.style.maxWidth = 360;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            if (sheet != null)
                panel.styleSheets.Add(sheet);

            panel.Add(BuildSection("External dependencies", Node.ExternalDependencies));
            panel.Add(BuildSection("Services", Node.Services));
            panel.Add(BuildSection("Loaded assets", Node.LoadedAssets));
            return panel;
        }

        private static VisualElement BuildSection(string title, System.Collections.Generic.IReadOnlyList<string> lines)
        {
            var section = new VisualElement();
            section.AddToClassList("container-node-section");
            section.style.marginTop = 4;

            var header = new Label(title);
            header.AddToClassList("container-node-section-title");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 11;
            header.style.color = new Color(0.82f, 0.82f, 0.82f);
            section.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 72;
            section.Add(scroll);

            if (lines == null || lines.Count == 0)
            {
                var empty = new Label("(none)");
                empty.AddToClassList("container-node-empty");
                empty.style.color = new Color(0.55f, 0.55f, 0.55f);
                empty.style.fontSize = 10;
                scroll.Add(empty);
                return section;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                var row = new Label(lines[i]);
                row.AddToClassList("container-node-row");
                row.style.fontSize = 10;
                row.style.whiteSpace = WhiteSpace.Normal;
                row.tooltip = lines[i];
                scroll.Add(row);
            }

            return section;
        }
    }
}
