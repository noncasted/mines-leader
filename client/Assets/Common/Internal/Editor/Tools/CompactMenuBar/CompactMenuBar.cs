using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    /// <summary>
    /// On Linux the editor main menu is a native GtkMenuBar: a separate row above the main toolbar.
    /// The native plugin next to this file (source in Tools~/GtkMenuBarHider) hides it, and a toolbar
    /// element shows the same menus in place of the Unity logo. Enabled by default, toggled per user
    /// in Tools/Compact Menu Bar. Other platforms are not touched.
    /// </summary>
    // Statics are reflection handles of editor types, valid across code reloads.
    [InitializeOnLoad]
    [NoAutoStaticsCleanup]
    public static class CompactMenuBar
    {
        private const string ElementPath = "Editor Controls/Menu Bar";
        private const string ToggleMenu = "Tools/Compact Menu Bar";
        private const string EnabledPrefsKey = "MinesLeader.CompactMenuBar.Enabled";
        private const string ProductCaption = "ToolbarProductCaption";
        private const string ContextMenu = "CONTEXT";

        // Built-in menus keep the native order; menus of packages and project code go between them, as natively.
        private static readonly string[] Leading = { "File", "Edit", "Assets", "GameObject", "Component" };
        private static readonly string[] Trailing = { "Window", "Help" };

        // Menu items of native code are invisible to TypeCache, only the internal menu API lists them.
        private static readonly MethodInfo GetMenuItems =
            typeof(Menu).GetMethod("GetMenuItems", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(Menu), "GetMenuItems");

        // Elements outside Unity's own preset start hidden, and with the native menu hidden
        // a hidden element leaves the editor without a menu, so it is displayed explicitly.
        private static readonly MethodInfo TryGetOverlay =
            typeof(MainToolbar).GetMethod("TryGetOverlay", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(MainToolbar), "TryGetOverlay");

        static CompactMenuBar()
        {
            if (IsSupported == false)
                return;

            Apply(IsEnabled);
        }

        private static bool IsSupported => Application.platform == RuntimePlatform.LinuxEditor;
        private static bool IsEnabled => EditorPrefs.GetBool(EnabledPrefsKey, true);
        private static bool IsActive => IsSupported == true && IsEnabled == true;

        [DllImport("gtk-menubar-hider", EntryPoint = "menubar_hider_set_hidden")]
        private static extern int SetNativeMenuHidden(int hidden);

        [MenuItem(ToggleMenu)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(EnabledPrefsKey, IsEnabled == false);
            Apply(IsEnabled);
        }

        [MenuItem(ToggleMenu, true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked(ToggleMenu, IsEnabled);
            return IsSupported;
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Left, defaultDockIndex = -100)]
        private static MainToolbarElement Create()
        {
            return new MainToolbarCustomElement(CreateBar) { displayed = IsActive };
        }

        private static void Apply(bool enabled)
        {
            if (SetNativeMenuHidden(enabled == true ? 1 : 0) < 0)
                throw new InvalidOperationException("Native menu bar hider failed to patch GtkMenuBar");

            // The toolbar does not exist yet while the editor loads.
            EditorApplication.delayCall += () =>
            {
                if (enabled == true)
                    DisplayElement();

                MainToolbar.Refresh(ElementPath);
            };
        }

        private static void DisplayElement()
        {
            var args = new object[] { ElementPath, null };

            if ((bool)TryGetOverlay.Invoke(null, args) == false)
                throw new InvalidOperationException($"Main toolbar has no {ElementPath} overlay");

            ((Overlay)args[1]).displayed = true;
        }

        private static VisualElement CreateBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;

            foreach (var menu in CollectMenus())
            {
                EditorToolbarButton button = null;
                button = new EditorToolbarButton(menu, () => EditorUtility.DisplayPopupMenu(button.worldBound, menu, null));
                bar.Add(button);
            }

            bar.RegisterCallback<AttachToPanelEvent>(OnAttached);
            return bar;
        }

        private static void OnAttached(AttachToPanelEvent evt)
        {
            var caption = evt.destinationPanel.visualTree.Q(ProductCaption)
                ?? throw new InvalidOperationException($"Main toolbar has no {ProductCaption} element");

            caption.style.display = IsActive == true ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static IReadOnlyList<string> CollectMenus()
        {
            var known = new HashSet<string>(Leading.Concat(Trailing)) { ContextMenu };
            var extra = new Dictionary<string, int>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<MenuItem>())
            {
                foreach (MenuItem attribute in method.GetCustomAttributes(typeof(MenuItem), false))
                {
                    var menu = attribute.menuItem.Split('/')[0];

                    if (known.Contains(menu) == true || extra.ContainsKey(menu) == true)
                        continue;

                    // Hidden menus (internal: without developer mode) have no items.
                    if (TryGetMinPriority(menu, out var priority) == false)
                        continue;

                    extra.Add(menu, priority);
                }
            }

            var ordered = extra
                .OrderBy(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key);

            return Leading.Concat(ordered).Concat(Trailing).ToList();
        }

        private static bool TryGetMinPriority(string menu, out int priority)
        {
            var items = (Array)GetMenuItems.Invoke(null, new object[] { menu, false, false });
            priority = int.MaxValue;

            if (items.Length == 0)
                return false;

            var priorityProperty = items.GetValue(0).GetType().GetProperty("priority")
                ?? throw new MissingMemberException("ScriptingMenuItem", "priority");

            foreach (var item in items)
                priority = Math.Min(priority, (int)priorityProperty.GetValue(item));

            return true;
        }
    }
}
