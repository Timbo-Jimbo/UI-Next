using System.Collections.Generic;
using System.IO;
using TimboJimbo.UINext;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Label = UnityEngine.UIElements.Label;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// The UI Next toolbar's States popup: previews states on the selected node, in the Scene and Game views
    /// (<see cref="UIStatePreview"/>), with nothing saved. A button for each interaction state, mode and state of your own
    /// in the project, in those groups, lit while it is active for the selection; a press turns one on or off. Two with
    /// the same name are told apart by their folder. Clear preview takes back everything the preview turned on.
    /// </summary>
    internal sealed class StatesPopup : ToolbarPopup
    {

        private static readonly (string Name, InteractionStates State)[] s_interaction =
        {
            ("Pressed", InteractionStates.Pressed),
            ("Hovered", InteractionStates.Hovered),
            ("Focused", InteractionStates.Focused),
            ("Focus within", InteractionStates.FocusWithin),
            ("Selected", InteractionStates.Selected),
            ("Disabled", InteractionStates.Disabled),
        };

        private Label _target;
        private Button _clear;
        private readonly List<(Button Button, System.Func<UINode, bool> Active)> _buttons = new();

        protected override float Width => 330f;

        public override void OnOpen()
        {
            base.OnOpen();
            Selection.selectionChanged += Refresh;
            UIStatePreview.Changed += Refresh;
        }

        public override void OnClose()
        {
            Selection.selectionChanged -= Refresh;
            UIStatePreview.Changed -= Refresh;
        }

        // What it previews on, then the interaction states, the modes and the states of your own, each a group.
        protected override void Build()
        {
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            _target = new Label { style = { flexGrow = 1, flexShrink = 1, unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal } };
            header.Add(_target);
            _clear = new Button(UIStatePreview.Clear) { text = "Clear preview", tooltip = "Take back every state the preview turned on." };
            header.Add(_clear);
            Content.Add(header);

            var row = Group("Interaction", "States a button gives what is inside it as it is used.");
            foreach (var (name, state) in s_interaction)
            {
                var flag = state;
                AddButton(row, name, () => UIStatePreview.Toggle(Selected(), flag), node => UIStatePreview.IsActive(node, flag));
            }

            var modes = new List<UIState>();
            var states = new List<UIState>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(UIState)))
            {
                var state = AssetDatabase.LoadAssetAtPath<UIState>(AssetDatabase.GUIDToAssetPath(guid));
                if (state == null) continue;
                (state.Kind == StateKind.Mode ? modes : states).Add(state);
            }
            AddStates("Modes", "Environment modes (Dark, a size class): tokens switch on them.", modes);
            AddStates("States", "Game states of your own, turned on for a node and everything inside it.", states);

            var note = new Label("Previews show in the Scene and Game views; nothing is saved.")
            {
                style = { marginTop = 6, opacity = 0.6f, whiteSpace = WhiteSpace.Normal, fontSize = 10 },
            };
            Content.Add(note);
            Refresh();
        }

        // A group's heading, and the row its buttons wrap in.
        private VisualElement Group(string title, string tip)
        {
            Content.Add(new Label(title) { tooltip = tip, style = { marginTop = 6, marginBottom = 1, opacity = 0.75f, fontSize = 10 } });
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = UnityEngine.UIElements.Wrap.Wrap } };
            Content.Add(row);
            return row;
        }

        private void AddStates(string title, string tip, List<UIState> states)
        {
            if (states.Count == 0) return;
            states.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var row = Group(title, tip);
            foreach (var state in states)
            {
                var s = state;
                var button = AddButton(row, Name(s, states), () => UIStatePreview.Toggle(Selected(), s), node => UIStatePreview.IsActive(node, s));
                button.tooltip = AssetDatabase.GetAssetPath(s);
            }
        }

        // A state's name, with its folder's after it when another of the same name is there too.
        private static string Name(UIState state, List<UIState> all)
        {
            foreach (var other in all)
            {
                if (other == state || other.name != state.name) continue;
                var folder = Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(state)));
                return string.IsNullOrEmpty(folder) ? state.name : state.name + " · " + folder;
            }
            return state.name;
        }

        private Button AddButton(VisualElement row, string text, System.Action pressed, System.Func<UINode, bool> active)
        {
            var button = new Button(pressed) { text = text };
            row.Add(button);
            _buttons.Add((button, active));
            return button;
        }

        // What it previews on, and the buttons of what is active for it lit.
        private void Refresh()
        {
            if (Content == null) return;
            var node = Selected();
            _target.text = node != null ? "Previewing on: " + node.name : "Select a UI node to preview states on it";
            _clear.SetEnabled(UIStatePreview.IsPreviewing);
            foreach (var (button, active) in _buttons)
            {
                button.SetEnabled(node != null);
                bool lit = node != null && active(node);
                button.style.backgroundColor = lit ? new StyleColor(UINextToolbar.Lit) : new StyleColor(StyleKeyword.Null);
            }
        }

        private static UINode Selected()
        {
            var go = Selection.activeGameObject;
            return go != null ? go.GetComponent<UINode>() : null;
        }
    }
}
