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
    /// The UI States toolbar's popup: previews states everywhere, in the Scene and Game views (<see cref="UIStatePreview"/>),
    /// with nothing saved, and records edits into one of them. A button for each interaction state, mode and state of
    /// your own in the project, in those groups, lit while it is on; a press turns one on or off. Two with the same name
    /// are told apart by their folder. Record turns recording on: while it is, a lit state's dot says whether edits go to
    /// it (red), and a click on another's records into that one instead. Clear preview turns them all off. Built with UI
    /// Toolkit and handed to Unity's popup window from CreateGUI, which sizes the window to it.
    /// </summary>
    internal sealed class StatesPopup : PopupWindowContent
    {
        private const float Width = 330f;

        private static readonly (string Name, InteractionStates State)[] s_interaction =
        {
            ("Pressed", InteractionStates.Pressed),
            ("Hovered", InteractionStates.Hovered),
            ("Focused", InteractionStates.Focused),
            ("Focus within", InteractionStates.FocusWithin),
            ("Selected", InteractionStates.Selected),
            ("Disabled", InteractionStates.Disabled),
        };

        private VisualElement _content;
        private Button _record;
        private Button _clear;
        private Label _hint;
        private readonly List<(PreviewState State, Button Button, Button Dot)> _buttons = new();

        public override VisualElement CreateGUI()
        {
            _content = new VisualElement
            {
                style = { width = Width, paddingLeft = 8, paddingRight = 8, paddingTop = 6, paddingBottom = 8 },
            };
            Build();
            return _content;
        }

        // Drawn by UI Toolkit (CreateGUI): nothing for IMGUI.
        public override void OnGUI(Rect rect) { }

        public override void OnOpen() => UIStatePreview.Changed += Refresh;

        public override void OnClose() => UIStatePreview.Changed -= Refresh;

        // Record and Clear preview, then the interaction states, the modes and the states of your own, each a group.
        private void Build()
        {
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            _record = new Button(() => UIStatePreview.Recording = !UIStatePreview.Recording)
            {
                text = "● Record",
                tooltip = "Record edits in the inspector into a state: the one turned on last, or the one whose dot you click.",
            };
            header.Add(_record);
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            _clear = new Button(UIStatePreview.Clear) { text = "Clear preview", tooltip = "Turn every state the preview turned on off." };
            header.Add(_clear);
            _content.Add(header);
            _hint = new Label { style = { opacity = 0.75f, whiteSpace = WhiteSpace.Normal, fontSize = 10 } };
            _content.Add(_hint);

            var row = Group("Interaction", "States a button gives what is inside it as it is used.");
            foreach (var (name, state) in s_interaction)
                AddButton(row, name, new PreviewState(state));

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

            var note = new Label("Previews show everywhere, in the Scene and Game views; nothing is saved.")
            {
                style = { marginTop = 6, opacity = 0.6f, whiteSpace = WhiteSpace.Normal, fontSize = 10 },
            };
            _content.Add(note);
            Refresh();
        }

        // A group's heading, and the row its buttons wrap in.
        private VisualElement Group(string title, string tip)
        {
            _content.Add(new Label(title) { tooltip = tip, style = { marginTop = 6, marginBottom = 1, opacity = 0.75f, fontSize = 10 } });
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = UnityEngine.UIElements.Wrap.Wrap } };
            _content.Add(row);
            return row;
        }

        private void AddStates(string title, string tip, List<UIState> states)
        {
            if (states.Count == 0) return;
            states.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var row = Group(title, tip);
            foreach (var state in states)
                AddButton(row, Name(state, states), new PreviewState(state)).tooltip = AssetDatabase.GetAssetPath(state);
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

        // A state's button, which turns it on or off, with the dot that records into it while recording.
        private Button AddButton(VisualElement row, string text, PreviewState state)
        {
            var item = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var dot = new Button(() => UIStatePreview.SetTarget(state))
            {
                text = "●",
                tooltip = "Record into this state",
                style = { marginRight = 0, paddingLeft = 2, paddingRight = 2, borderRightWidth = 0 },
            };
            item.Add(dot);
            var button = new Button(() => UIStatePreview.Toggle(state)) { text = text };
            item.Add(button);
            row.Add(item);
            _buttons.Add((state, button, dot));
            return button;
        }

        // Record lit red while recording, what is on lit, and where edits go.
        private void Refresh()
        {
            if (_content == null) return;
            bool recording = UIStatePreview.Recording;
            var target = UIStatePreview.Target;
            _record.style.backgroundColor = recording ? new StyleColor(StatesDropdown.Recording) : new StyleColor(StyleKeyword.Null);
            _clear.SetEnabled(UIStatePreview.IsPreviewing);
            _hint.text = !recording ? ""
                : target is { } t ? "Edits in the inspector go to " + t.Name + "; click another lit state's dot to record into it."
                : "Turn a state on to record into it; until then edits change values as ever.";
            _hint.style.display = recording ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var (state, button, dot) in _buttons)
            {
                bool on = UIStatePreview.IsOn(state);
                button.style.backgroundColor = on ? new StyleColor(StatesDropdown.Lit) : new StyleColor(StyleKeyword.Null);
                dot.style.display = recording && on ? DisplayStyle.Flex : DisplayStyle.None;
                bool into = target is { } t2 && t2.Equals(state);
                dot.style.color = into ? new StyleColor(new Color(1f, 0.35f, 0.35f)) : new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                dot.tooltip = into ? "Edits are recorded into this state" : "Record into this state";
            }
        }
    }
}
