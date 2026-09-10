using UnityEngine.UIElements;

namespace Internal
{
    public abstract class ProjectToolsTab
    {
        protected IProjectToolsHost Host { get; private set; }

        public abstract string Title { get; }

        public VisualElement Build(IProjectToolsHost host)
        {
            Host = host;

            var content = new VisualElement();
            content.AddToClassList("tab-content");
            BuildContent(content);

            return content;
        }

        public virtual void OnEnable()
        {
        }

        public virtual void OnDisable()
        {
        }

        protected abstract void BuildContent(VisualElement parent);

        protected static VisualElement BuildSubSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            var label = new Label(title);
            label.AddToClassList("section-title");
            section.Add(label);

            return section;
        }
    }
}