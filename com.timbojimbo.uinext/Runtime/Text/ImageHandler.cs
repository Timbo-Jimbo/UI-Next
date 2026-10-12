using System;
using System.Collections.Generic;
using TimboJimbo.UINext.Text;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>An image an <see cref="ImageHandler"/> shows, by the name the text gives it.</summary>
    [Serializable]
    public struct NamedImage
    {
        public string Name;
        public Sprite Sprite;

        public NamedImage(string name, Sprite sprite)
        {
            Name = name;
            Sprite = sprite;
        }
    }

    /// <summary>
    /// Shows images in the text by name, as :name: (its trigger) says them: emoji of your own, an icon beside a number.
    /// Each is an element as big as the text around it times <see cref="SizeEm"/>; a name it has no image for stays text.
    /// With no prefab, each is the built-in image node; a prefab of your own shows the image with a
    /// <see cref="TextImage"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Timbo Jimbo/UI Next/Text/Image Handler", fileName = "Image Handler")]
    public class ImageHandler : ElementHandler
    {
        [Tooltip("The images it shows, by the name the text gives them; names match whatever their case.")]
        [SerializeField] private List<NamedImage> _images = new();

        [Tooltip("How big an image is, in em of the text around it.")]
        [SerializeField, Min(0.1f)] private float _sizeEm = 1.25f;

        /// <summary>The images it shows; tell texts of a change with <see cref="TextSettings.NotifyChanged"/>.</summary>
        public List<NamedImage> Images => _images;

        /// <summary>How big an image is, in em of the text around it.</summary>
        public float SizeEm => _sizeEm;

        /// <summary>The image it has for a name, or null.</summary>
        public Sprite Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var image in _images)
                if (image.Sprite != null && string.Equals(image.Name, name, StringComparison.OrdinalIgnoreCase))
                    return image.Sprite;
            return null;
        }

        /// <summary>Only names it has an image for: :30: in a time stays text.</summary>
        public override bool Accepts(in TextMatch match) => Find(match.Text) != null;

        public override GameObject PrefabFor(in TextMatch match) => Prefab != null ? Prefab : BuiltInText.Image;

        /// <summary>An image handler made in code.</summary>
        public static ImageHandler Create(TextTrigger trigger, IEnumerable<NamedImage> images, float sizeEm = 1.25f,
            ElementAlignment alignment = ElementAlignment.Center, bool tappable = false)
        {
            var handler = CreateInstance<ImageHandler>();
            handler.Configure(trigger, null, alignment, tappable);
            if (images != null)
                handler._images.AddRange(images);
            handler._sizeEm = sizeEm;
            return handler;
        }

        // A new handler takes :name:.
        protected override void Reset() => Configure(TextTrigger.Wrapped(":"));
    }
}
