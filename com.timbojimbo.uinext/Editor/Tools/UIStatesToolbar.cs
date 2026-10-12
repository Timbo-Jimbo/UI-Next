using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// The UI States toolbar, among the Scene view's overlays: one dropdown opening the popup that previews states on the
    /// selected node (<see cref="StatesPopup"/>), lit, with how many, while any are previewed. Its icon is Unity's own,
    /// unless States.png is in the package's Editor/Icons folder (a 16-point icon, with States@2x.png beside it for
    /// high-DPI screens). The debug views and the stats are in the Rendering Debugger's UI Next panel.
    /// </summary>
    [Overlay(typeof(SceneView), Id, "UI States", true)]
    public sealed class UIStatesToolbar : ToolbarOverlay
    {
        private const string Id = "timbojimbo-uinext-states";

        public UIStatesToolbar() : base(StatesDropdown.Id)
        {
            collapsedIcon = StatesDropdown.Icon();
        }
    }

    // UI States: opens the popup; lit, with how many, while any states are previewed.
    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class StatesDropdown : EditorToolbarDropdown
    {
        public const string Id = "TimboJimbo.UINext/States";

        private const string Name = "UI States";

        // Lit while states are previewed.
        internal static readonly Color Lit = new(0.17f, 0.36f, 0.53f);

        public StatesDropdown()
        {
            icon = Icon();
            text = Name;
            tooltip = "UI Next: preview interaction states, modes and states of your own on the selected node, in the Scene and Game views.";
            clicked += () => UnityEditor.PopupWindow.Show(worldBound, new StatesPopup());
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                UIStatePreview.Changed -= Refresh;
                UIStatePreview.Changed += Refresh;
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => UIStatePreview.Changed -= Refresh);
        }

        private void Refresh()
        {
            int count = UIStatePreview.Count;
            text = count > 0 ? Name + " · " + count : Name;
            style.backgroundColor = count > 0 ? new StyleColor(Lit) : new StyleColor(StyleKeyword.Null);
        }

        // States.png in the package's Editor/Icons folder if it is there, else Unity's state icon (the dark skin's where
        // the editor wears it; FindTexture is quiet about a name it lacks).
        internal static Texture2D Icon()
        {
            var own = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.timbojimbo.uinext/Editor/Icons/States.png");
            if (own != null) return own;
            var builtIn = EditorGUIUtility.isProSkin ? EditorGUIUtility.FindTexture("d_AnimatorState Icon") : null;
            return builtIn != null ? builtIn : EditorGUIUtility.FindTexture("AnimatorState Icon");
        }
    }
}
