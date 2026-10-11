using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// Store: a grid of items, each growing into a dialog and flying home. An item and the dialog's card share a match
    /// name, the card taking the item's id as it opens, and so do their icons, names and prices, which fly on their own;
    /// the item is hidden (keeping its place) as the dialog is presented, and shown as it is dismissed. Dragged away from
    /// anywhere in the dialog, the card flies home from where it is let go.
    /// </summary>
    public sealed class StoreDemo : MonoBehaviour
    {
        /// <summary>What the store sells.</summary>
        public static readonly (string Emoji, string Name, string Price, Color Colour)[] Items =
        {
            ("🎧", "Headphones", "$199", new Color32(0x5C, 0x8D, 0xFF, 0xFF)),
            ("⌚", "Watch", "$349", new Color32(0xFF, 0x7E, 0xB6, 0xFF)),
            ("📷", "Camera", "$599", new Color32(0xF2, 0xA9, 0x3B, 0xFF)),
            ("🎮", "Controller", "$69", new Color32(0xA7, 0x7B, 0xFF, 0xFF)),
            ("🛹", "Skateboard", "$89", new Color32(0x3F, 0xC1, 0xC9, 0xFF)),
            ("🪴", "Plant", "$24", new Color32(0x34, 0xC7, 0x59, 0xFF)),
        };

        [SerializeField] private List<Pressable> _items = new();
        [SerializeField] private Presentation _dialog;
        [SerializeField] private UINode _card;
        [SerializeField] private Box _icon;
        [SerializeField] private Label _emoji;
        [SerializeField] private Label _name;
        [SerializeField] private Label _price;
        [SerializeField] private List<Pressable> _buttons = new();

        private int _open = -1;

        public void Setup(List<Pressable> items, Presentation dialog, UINode card, Box icon, Label emoji, Label name, Label price,
            List<Pressable> buttons)
        {
            _items = items;
            _dialog = dialog;
            _card = card;
            _icon = icon;
            _emoji = emoji;
            _name = name;
            _price = price;
            _buttons = buttons;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                _items[i].Tapped += () => Open(index);
            }
            foreach (var button in _buttons)
                button.Tapped += () => _dialog.Dismiss();
            _dialog.Presenting += HideItem;
            _dialog.Dismissing += ShowItem;
        }

        private void OnDisable()
        {
            _dialog.Presenting -= HideItem;
            _dialog.Dismissing -= ShowItem;
        }

        private void Open(int index)
        {
            if (_dialog.IsPresented) return;
            var (emoji, name, price, colour) = Items[index];
            _icon.Fill.Value = colour;
            _emoji.Text.Value = emoji;
            _name.Text.Value = name;
            _price.Text.Value = price;
            _card.MatchId.Value = index.ToString();
            _open = index;
            _dialog.Present();
        }

        // Inside the dialog's changes: the item is hidden as the card grows out of it, and shown as the card flies home.
        private void HideItem()
        {
            if (_open >= 0)
                _items[_open].Node.Display.Value = DisplayMode.Hidden;
        }

        private void ShowItem()
        {
            if (_open >= 0)
                _items[_open].Node.Display.Value = DisplayMode.Visible;
        }
    }
}
