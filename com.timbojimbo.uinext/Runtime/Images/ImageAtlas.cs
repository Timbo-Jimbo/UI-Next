using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Pages the small images loaded from URLs are copied onto, so that images drawn together sample one texture and draw
    /// in one call: a batch takes eight textures, and a feed of photos each its own would take a call for every few. An
    /// image up to 512 pixels a side is copied onto a page of its format, on the GPU, along a shelf of its height (in steps
    /// of 16 pixels), with its edge pixels copied once more round it so that sampling at its edge never reaches its
    /// neighbour. An image let go of leaves its room for another; free shelves at the top of a page give their height back,
    /// and a page with nothing on it goes. Pages are 2048 pixels square, at most four of a format; past that, or where the
    /// GPU can't copy textures, an image keeps its own texture.
    /// </summary>
    internal static class ImageAtlas
    {
        private const int PageSize = 2048;
        /// <summary>The widest and tallest image put on a page, in pixels.</summary>
        internal const int MaxSize = 512;
        private const int MaxPages = 4;
        private const int ShelfStep = 16;

        // Hidden and never saved, as the text engine's font assets are, but reclaimable once nothing holds them.
        private const HideFlags Transient = HideFlags.HideAndDontSave & ~HideFlags.DontUnloadUnusedAsset;

        /// <summary>Where an image is on a page.</summary>
        internal sealed class Slot
        {
            /// <summary>Its page's texture.</summary>
            public Texture2D Texture => Page.Texture;

            /// <summary>Where on the page it is, in UV.</summary>
            public Rect Uv;

            internal Page Page;
            internal Shelf Shelf;
            internal int X, Width;
        }

        internal sealed class Page
        {
            public Texture2D Texture;
            public GraphicsFormat Format;
            public readonly List<Shelf> Shelves = new();
            // The height its shelves take, from its bottom; and how many images are on it.
            public int Top, Used;
        }

        internal sealed class Shelf
        {
            public int Y, Height;
            // Its free spans, left to right: x where each starts, y how wide it is.
            public readonly List<Vector2Int> Free = new();
        }

        private static readonly List<Page> s_pages = new();

        /// <summary>Copies an image onto a page if it is small enough and there is room; where it went, or null.</summary>
        public static Slot Add(Texture2D image)
        {
            if (image == null || image.width > MaxSize || image.height > MaxSize) return null;
            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0) return null;
            int width = image.width + 2, height = image.height + 2;
            int shelfHeight = (height + ShelfStep - 1) / ShelfStep * ShelfStep;
            var format = image.graphicsFormat;
            Slot slot = null;
            int pages = 0;
            foreach (var page in s_pages)
            {
                if (page.Format != format) continue;
                pages++;
                slot = Place(page, width, shelfHeight);
                if (slot != null) break;
            }
            if (slot == null)
            {
                if (pages >= MaxPages) return null;
                slot = Place(NewPage(format), width, shelfHeight);
            }
            Copy(image, slot);
            return slot;
        }

        /// <summary>Gives an image's room back: a page with nothing left on it goes.</summary>
        public static void Remove(Slot slot)
        {
            var page = slot.Page;
            if (page.Texture == null) return;
            Release(slot.Shelf, slot.X, slot.Width);
            while (page.Shelves.Count > 0)
            {
                var top = page.Shelves[page.Shelves.Count - 1];
                if (top.Free.Count != 1 || top.Free[0].y != PageSize) break;
                page.Shelves.RemoveAt(page.Shelves.Count - 1);
                page.Top = top.Y;
            }
            if (--page.Used > 0) return;
            s_pages.Remove(page);
            Destroy(page.Texture);
            page.Texture = null;
        }

        /// <summary>Lets go of every page (what was on them is gone with them).</summary>
        public static void Clear()
        {
            foreach (var page in s_pages)
            {
                Destroy(page.Texture);
                page.Texture = null;
            }
            s_pages.Clear();
        }

        // On a shelf of its height with a span wide enough, else on a new shelf if the page has the height for it.
        private static Slot Place(Page page, int width, int shelfHeight)
        {
            foreach (var shelf in page.Shelves)
                if (shelf.Height == shelfHeight && Take(shelf, width, out int x))
                    return Fill(page, shelf, x, width);
            if (page.Top + shelfHeight > PageSize) return null;
            var added = new Shelf { Y = page.Top, Height = shelfHeight };
            added.Free.Add(new Vector2Int(0, PageSize));
            page.Shelves.Add(added);
            page.Top += shelfHeight;
            Take(added, width, out int at);
            return Fill(page, added, at, width);
        }

        // The start of the first free span of a shelf wide enough, taken.
        private static bool Take(Shelf shelf, int width, out int x)
        {
            for (int i = 0; i < shelf.Free.Count; i++)
            {
                var span = shelf.Free[i];
                if (span.y < width) continue;
                x = span.x;
                if (span.y == width)
                    shelf.Free.RemoveAt(i);
                else
                    shelf.Free[i] = new Vector2Int(span.x + width, span.y - width);
                return true;
            }
            x = 0;
            return false;
        }

        private static Slot Fill(Page page, Shelf shelf, int x, int width)
        {
            page.Used++;
            return new Slot { Page = page, Shelf = shelf, X = x, Width = width };
        }

        // A span given back to its shelf, joined to the free spans either side of it.
        private static void Release(Shelf shelf, int x, int width)
        {
            var free = shelf.Free;
            int i = 0;
            while (i < free.Count && free[i].x < x) i++;
            free.Insert(i, new Vector2Int(x, width));
            if (i + 1 < free.Count && free[i].x + free[i].y == free[i + 1].x)
            {
                free[i] = new Vector2Int(free[i].x, free[i].y + free[i + 1].y);
                free.RemoveAt(i + 1);
            }
            if (i > 0 && free[i - 1].x + free[i - 1].y == free[i].x)
            {
                free[i - 1] = new Vector2Int(free[i - 1].x, free[i - 1].y + free[i].y);
                free.RemoveAt(i);
            }
        }

        // A page on the GPU alone: its pixels aren't kept in memory, and where nothing was copied is never sampled.
        private static Page NewPage(GraphicsFormat format)
        {
            var texture = new Texture2D(PageSize, PageSize, format, TextureCreationFlags.DontInitializePixels)
            {
                name = "UI Next image atlas",
                hideFlags = Transient,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.Apply(false, true);
            var page = new Page { Texture = texture, Format = format };
            s_pages.Add(page);
            return page;
        }

        // Onto its slot on the GPU, its edge pixels copied once more round it (the image is a pixel in from the slot's
        // corner, its shelf's y counting up from the page's bottom, as textures do).
        private static void Copy(Texture2D image, Slot slot)
        {
            var page = slot.Page.Texture;
            int w = image.width, h = image.height, x = slot.X + 1, y = slot.Shelf.Y + 1;
            Graphics.CopyTexture(image, 0, 0, 0, 0, w, h, page, 0, 0, x, y);
            Graphics.CopyTexture(image, 0, 0, 0, 0, 1, h, page, 0, 0, x - 1, y);
            Graphics.CopyTexture(image, 0, 0, w - 1, 0, 1, h, page, 0, 0, x + w, y);
            Graphics.CopyTexture(image, 0, 0, 0, 0, w, 1, page, 0, 0, x, y - 1);
            Graphics.CopyTexture(image, 0, 0, 0, h - 1, w, 1, page, 0, 0, x, y + h);
            Graphics.CopyTexture(image, 0, 0, 0, 0, 1, 1, page, 0, 0, x - 1, y - 1);
            Graphics.CopyTexture(image, 0, 0, w - 1, 0, 1, 1, page, 0, 0, x + w, y - 1);
            Graphics.CopyTexture(image, 0, 0, 0, h - 1, 1, 1, page, 0, 0, x - 1, y + h);
            Graphics.CopyTexture(image, 0, 0, w - 1, h - 1, 1, 1, page, 0, 0, x + w, y + h);
            slot.Uv = new Rect((float)x / PageSize, (float)y / PageSize, (float)w / PageSize, (float)h / PageSize);
        }

        private static void Destroy(Object texture)
        {
            if (texture == null) return;
            if (Application.isPlaying)
                Object.Destroy(texture);
            else
                Object.DestroyImmediate(texture);
        }
    }
}
