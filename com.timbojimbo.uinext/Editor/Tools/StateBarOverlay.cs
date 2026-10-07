using System.Collections.Generic;
using TimboJimbo.UINext;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;
using Label = UnityEngine.UIElements.Label;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// The state bar: a Scene view overlay that previews states on the selected node, in the Scene and Game views
    /// (<see cref="UIStatePreview"/>). Its buttons are the interaction states and every state and mode in the project,
    /// each lit while it is active for the selection; a press turns one on or off. Clear takes the preview back.
    /// </summary>
    [Overlay(typeof(SceneView), Id, "UI Next States", true)]
    public sealed class StateBarOverlay : Overlay
    {
        private const string Id = "timbojimbo-uinext-state-bar";

        private static readonly (string Name, InteractionStates State)[] s_interaction =
        {
            ("Pressed", InteractionStates.Pressed),
            ("Hovered", InteractionStates.Hovered),
            ("Focused", InteractionStates.Focused),
            ("Selected", InteractionStates.Selected),
            ("Disabled", InteractionStates.Disabled),
        };

        private static readonly Color s_lit = new(0.17f, 0.36f, 0.53f);

        private VisualElement _root;
        private Label _title;
        private readonly List<(Button Button, System.Func<UINode, bool> Active)> _buttons = new();

        public override VisualElement CreatePanelContent()
        {
            _root = new VisualElement { style = { minWidth = 220, paddingTop = 2, paddingBottom = 2 } };
            Build();
            Selection.selectionChanged -= Refresh;
            Selection.selectionChanged += Refresh;
            UIStatePreview.Changed -= Refresh;
            UIStatePreview.Changed += Refresh;
            return _root;
        }

        public override void OnWillBeDestroyed()
        {
            Selection.selectionChanged -= Refresh;
            UIStatePreview.Changed -= Refresh;
            base.OnWillBeDestroyed();
        }

        // The rows: the selection's name, the interaction states, the modes, the states of your own, and Clear.
        private void Build()
        {
            _root.Clear();
            _buttons.Clear();
            _title = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 } };
            _root.Add(_title);

            var interaction = Row();
            foreach (var (name, state) in s_interaction)
            {
                var flag = state;
                AddButton(interaction, name, () => UIStatePreview.Toggle(Selected(), flag), node => UIStatePreview.IsActive(node, flag));
            }

            var modes = new List<UIState>();
            var states = new List<UIState>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(UIState)))
            {
                var state = AssetDatabase.LoadAssetAtPath<UIState>(AssetDatabase.GUIDToAssetPath(guid));
                if (state == null) continue;
                (state.Kind == StateKind.Mode ? modes : states).Add(state);
            }
            AddStates(modes);
            AddStates(states);

            var clear = new Button(UIStatePreview.Clear) { text = "Clear" };
            clear.style.marginTop = 4;
            _root.Add(clear);
            Refresh();
        }

        private void AddStates(List<UIState> states)
        {
            if (states.Count == 0) return;
            states.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var row = Row();
            foreach (var state in states)
            {
                var s = state;
                AddButton(row, s.name, () => UIStatePreview.Toggle(Selected(), s), node => UIStatePreview.IsActive(node, s));
            }
        }

        private VisualElement Row()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = UnityEngine.UIElements.Wrap.Wrap } };
            _root.Add(row);
            return row;
        }

        private void AddButton(VisualElement row, string text, System.Action pressed, System.Func<UINode, bool> active)
        {
            var button = new Button(pressed) { text = text };
            row.Add(button);
            _buttons.Add((button, active));
        }

        // Lights the buttons of what is active for the selection.
        private void Refresh()
        {
            if (_root == null) return;
            var node = Selected();
            _title.text = node != null ? node.name : "Select a UI node";
            foreach (var (button, active) in _buttons)
            {
                button.SetEnabled(node != null);
                bool lit = node != null && active(node);
                button.style.backgroundColor = lit ? new StyleColor(s_lit) : new StyleColor(StyleKeyword.Null);
            }
        }

        private static UINode Selected()
        {
            var go = Selection.activeGameObject;
            return go != null ? go.GetComponent<UINode>() : null;
        }
    }
}
