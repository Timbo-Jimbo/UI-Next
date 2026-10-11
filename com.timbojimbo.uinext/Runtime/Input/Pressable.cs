using System;
using UnityEngine;
using UnityEngine.Events;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Makes a node a button, as UIKit's UIControl and SwiftUI's Button: pressed while a pointer is down on it (and
    /// still over it, give or take a finger's width), tapped when it lets go there, hovered while the mouse is over it,
    /// disabled while it is not <see cref="Interactable"/>, and selected while it is on, when it <see cref="Toggles"/>.
    /// It provides those states to itself and everything inside it, so their values follow them (a fill
    /// <c>When(Pressed, ...)</c>). A scroll or a drag that takes the pointer cancels the press. The deepest button under
    /// the pointer takes the tap.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UINode))]
    [AddComponentMenu("Timbo Jimbo/UI Next/Pressable")]
    public sealed class Pressable : MonoBehaviour, IPointerHandler, IHoverHandler
    {
        [Tooltip("Whether it takes presses. Off, it is Disabled.")]
        [SerializeField] private UIValue<bool> _interactable = true;

        [Tooltip("A tap turns it on or off: Selected while on.")]
        [SerializeField] private bool _toggles;

        [Tooltip("Whether it is on, when it toggles.")]
        [SerializeField] private bool _selected;

        [SerializeField] private UnityEvent _onTap = new();

        [Tooltip("Raised when it is held this long without the pointer leaving it; letting go then does not tap it.")]
        [SerializeField] private UnityEvent _onLongPress = new();

        // How far a finger may wander off it and still count as on it, as UIKit's generous touch bounds.
        private const float TouchSlop = 24f;

        // How long a press is held before it is a long press, as UIKit's default.
        private const float LongPressTime = 0.5f;

        // The buttons being held that answer long presses, stepped with the frame.
        private static readonly System.Collections.Generic.List<Pressable> s_held = new();

        [NonSerialized] private UINode _node;
        [NonSerialized] private int _pointer = -1;
        [NonSerialized] private bool _inside;
        [NonSerialized] private Effect _disabled;
        [NonSerialized] private float _heldFor;
        [NonSerialized] private bool _longPressed;

        /// <summary>Raised when it is tapped (or submitted with focus on it).</summary>
        public event Action Tapped;

        /// <summary>Raised when it is turned on or off, with whether it is on now.</summary>
        public event Action<bool> Toggled;

        /// <summary>Raised when it is held for half a second without the pointer leaving it; letting go then does not tap it.</summary>
        public event Action LongPressed;

        /// <summary>Its node.</summary>
        public UINode Node => _node != null ? _node : _node = GetComponent<UINode>();

        /// <summary>Whether it takes presses. Off, it is Disabled, and so is everything inside it.</summary>
        public UIValue<bool> Interactable => _interactable;

        /// <summary>The UnityEvent raised when it is tapped, for wiring in the inspector.</summary>
        public UnityEvent OnTap => _onTap;

        /// <summary>The UnityEvent raised when it is long pressed.</summary>
        public UnityEvent OnLongPress => _onLongPress;

        /// <summary>Whether a tap turns it on and off.</summary>
        public bool Toggles
        {
            get => _toggles;
            set => _toggles = value;
        }

        /// <summary>Whether it is on: the Selected state.</summary>
        public bool IsSelected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                Node.SetInteraction(InteractionStates.Selected, value);
                Toggled?.Invoke(value);
            }
        }

        /// <summary>Whether a pointer is pressing it now.</summary>
        public bool IsPressed => _pointer >= 0 && _inside;

        /// <summary>Taps it, as a pointer letting go on it does: toggles it if it toggles, then raises its events.</summary>
        public void Tap()
        {
            if (!_interactable.Peek) return;
            if (_toggles) IsSelected = !_selected;
            Tapped?.Invoke();
            _onTap?.Invoke();
        }

        private void OnEnable()
        {
            var node = Node;
            ((IUIValue)_interactable).Attach(node);
            node.AddInteractionProvider();
            node.SetInteraction(InteractionStates.Selected, _selected);
            // Disabled follows Interactable, however it is given: a literal, a binding, a state's variant.
            _disabled = new Effect(() => Node.SetInteraction(InteractionStates.Disabled, !_interactable.Value));
            _disabled.Run();
        }

        private void OnDisable()
        {
            ((IUIValue)_interactable).Detach();
            _disabled?.Dispose();
            _disabled = null;
            Release();
            if (_node != null)
                _node.RemoveInteractionProvider();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            ((IUIValue)_interactable).Refresh();
            Node.SetInteraction(InteractionStates.Selected, _selected);
        }

        void IPointerHandler.OnPointerDown(in PointerEvent e)
        {
            if (_pointer >= 0 || !_interactable.Peek) return;
            if (!UIInput.TakeTap(this, e.PointerId)) return;
            _pointer = e.PointerId;
            _heldFor = 0f;
            _longPressed = false;
            SetInside(true);
            if (LongPressed != null || _onLongPress.GetPersistentEventCount() > 0)
                s_held.Add(this);
        }

        /// <summary>Moves every held button <paramref name="dt"/> seconds on: once a frame.</summary>
        internal static void StepAll(float dt)
        {
            for (int i = s_held.Count - 1; i >= 0; i--)
            {
                var pressable = s_held[i];
                if (pressable == null || pressable._pointer < 0 || pressable._longPressed)
                {
                    s_held.RemoveAt(i);
                    continue;
                }
                // Off it, the clock waits; held long enough on it, it is a long press.
                if (!pressable._inside) continue;
                pressable._heldFor += dt;
                if (pressable._heldFor < LongPressTime) continue;
                pressable._longPressed = true;
                s_held.RemoveAt(i);
                pressable.LongPressed?.Invoke();
                pressable._onLongPress?.Invoke();
            }
        }

        void IPointerHandler.OnPointerMove(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            SetInside(UIInput.IsOver(Node, e.Position, e.Kind == PointerKind.Touch ? TouchSlop : 0f));
        }

        void IPointerHandler.OnPointerUp(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            bool tapped = _inside && !_longPressed && UIInput.IsOver(Node, e.Position, e.Kind == PointerKind.Touch ? TouchSlop : 0f);
            Release();
            if (!tapped) return;
            // Focus comes here too, unseen, as a click focuses a button on the web.
            UIFocus.Tapped(Node);
            Tap();
        }

        void IPointerHandler.OnPointerCancel(in PointerEvent e)
        {
            if (e.PointerId == _pointer) Release();
        }

        void IHoverHandler.OnHoverChanged(bool hovered) => Node.SetInteraction(InteractionStates.Hovered, hovered);

        private void SetInside(bool inside)
        {
            _inside = inside;
            Node.SetInteraction(InteractionStates.Pressed, inside);
        }

        private void Release()
        {
            _pointer = -1;
            _inside = false;
            if (_node != null)
                _node.SetInteraction(InteractionStates.Pressed, false);
        }
    }
}
