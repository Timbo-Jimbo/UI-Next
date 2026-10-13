using System;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Builds trees from code, one line a node: <c>card.Child("Title").Label("Hello", 20, bold: true)</c>. Each call
    /// sets a node's values (literals, as the inspector would) and returns the node, so they chain. For editor scripts
    /// that build scenes and for UI made at run time alike.
    /// </summary>
    public static class UIBuild
    {
        /// <summary>A new node under <paramref name="parent"/> (or at the top of the active scene).</summary>
        public static UINode Node(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
            }
            return go.AddComponent<UINode>();
        }

        /// <summary>A new root: a node with a <see cref="UIRoot"/>, covering the screen.</summary>
        public static UIRoot Root(string name, Transform parent = null)
        {
            var node = Node(name, parent);
            return node.gameObject.AddComponent<UIRoot>();
        }

        /// <summary>A new node under this one.</summary>
        public static UINode Child(this UINode parent, string name) => Node(name, parent.transform);

        /// <summary>Adds a component to the node's object.</summary>
        public static T Add<T>(this UINode node) where T : Component => node.gameObject.AddComponent<T>();

        public static UINode Sized(this UINode node, Sizing width, Sizing height)
        {
            node.Width.Value = width;
            node.Height.Value = height;
            return node;
        }

        /// <summary>Lays its children out top to bottom, <paramref name="gap"/> apart.</summary>
        public static UINode Column(this UINode node, float gap = 0f)
        {
            node.Direction.Value = LayoutDirection.TopToBottom;
            node.Gap.Value = gap;
            return node;
        }

        /// <summary>Lays its children out left to right, <paramref name="gap"/> apart.</summary>
        public static UINode Row(this UINode node, float gap = 0f)
        {
            node.Direction.Value = LayoutDirection.LeftToRight;
            node.Gap.Value = gap;
            return node;
        }

        public static UINode Pad(this UINode node, float all)
        {
            node.Padding.Value = Insets.All(all);
            return node;
        }

        public static UINode Pad(this UINode node, float horizontal, float vertical)
        {
            node.Padding.Value = Insets.Symmetric(horizontal, vertical);
            return node;
        }

        public static UINode Pad(this UINode node, Insets padding)
        {
            node.Padding.Value = padding;
            return node;
        }

        public static UINode Align(this UINode node, AlignX x, AlignY y)
        {
            node.AlignX.Value = x;
            node.AlignY.Value = y;
            return node;
        }

        /// <summary>Puts its children in its middle both ways.</summary>
        public static UINode Center(this UINode node) => node.Align(AlignX.Center, AlignY.Center);

        public static UINode Clip(this UINode node, CornerRadii radii = default)
        {
            node.Clips.Value = true;
            node.ClipRadii.Value = radii;
            return node;
        }

        /// <summary>Floats it against its parent, its <paramref name="point"/> on the parent's <paramref name="target"/>, moved by <paramref name="offset"/>.</summary>
        public static UINode Float(this UINode node, AttachPoint point, AttachPoint target, Vector2 offset = default)
        {
            node.Floating.Value = new Floating { AttachTo = FloatingAttach.Parent, Point = point, TargetPoint = target, Offset = offset };
            return node;
        }

        /// <summary>Floats it against another node in its tree (a highlight behind the selected tab).</summary>
        public static UINode FloatOn(this UINode node, UINode element, AttachPoint point = AttachPoint.CenterCenter, AttachPoint target = AttachPoint.CenterCenter)
        {
            node.Floating.Value = new Floating { AttachTo = FloatingAttach.Element, Element = element, Point = point, TargetPoint = target };
            return node;
        }

        /// <summary>A box filling the node.</summary>
        public static Box Box(this UINode node, Color fill, CornerRadii radii = default)
        {
            var box = node.gameObject.AddComponent<Box>();
            box.Fill.Value = fill;
            box.CornerRadii.Value = radii;
            return box;
        }

        /// <summary>A label: the node fits it unless sized otherwise.</summary>
        public static Label Label(this UINode node, string text, float size = 17f, Color? color = null, bool bold = false)
        {
            var label = node.gameObject.AddComponent<Label>();
            label.Text.Value = text;
            label.FontSize.Value = size;
            if (color.HasValue) label.Color.Value = color.Value;
            label.Bold.Value = bold;
            return label;
        }

        /// <summary>An image of a sprite: the node fits it unless sized otherwise.</summary>
        public static Image Image(this UINode node, Sprite sprite, ImageFit fit = ImageFit.Cover)
        {
            var image = node.gameObject.AddComponent<Image>();
            image.Sprite.Value = sprite;
            image.Fit.Value = fit;
            return image;
        }

        /// <summary>
        /// An image loaded from <paramref name="url"/>, asked for through <paramref name="loader"/> (else the project's
        /// default): the node fits it once it arrives unless sized otherwise.
        /// </summary>
        public static Image Image(this UINode node, string url, ImageFit fit = ImageFit.Cover, ImageLoader loader = null)
        {
            var image = node.gameObject.AddComponent<Image>();
            image.Url.Value = url;
            image.Fit.Value = fit;
            if (loader != null)
                image.Loader.Value = loader;
            return image;
        }

        /// <summary>Makes the node a button, raising <paramref name="tapped"/> when tapped.</summary>
        public static Pressable Pressable(this UINode node, Action tapped = null)
        {
            var pressable = node.gameObject.AddComponent<Pressable>();
            if (tapped != null) pressable.Tapped += tapped;
            return pressable;
        }

        /// <summary>Makes the node scroll its children the way it lays them out.</summary>
        public static ScrollView Scroll(this UINode node, ScrollAxis axis = ScrollAxis.Vertical)
        {
            var scroll = node.gameObject.AddComponent<ScrollView>();
            scroll.Axis.Value = axis;
            return scroll;
        }
    }
}
