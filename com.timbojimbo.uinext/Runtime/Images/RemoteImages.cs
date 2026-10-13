using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// The images <see cref="Image"/>s load from URLs: each downloaded once and shared by every image showing it, kept
    /// while anything shows it and, after that, while the ones nothing shows fit in the cache, the longest unseen going
    /// first. What is downloaded is kept on disk too (<see cref="ImageDiskCache"/>), and read from there the next time. A
    /// small copy asked for at the size it is drawn goes onto a shared atlas page (<see cref="ImageAtlas"/>). Downloads run a
    /// few at a time, in the order they were asked for, and one nothing wants any more is called off.
    /// </summary>
    internal static class RemoteImages
    {
        // How much of the images nothing shows is kept for the next time one is asked for, in bytes as drawn: the project's
        // setting (Project Settings > Timbo Jimbo > UI Next), 64 MB without one.
        private static long CacheBytes
        {
            get
            {
                var settings = UISettings.Instance;
                return (settings != null ? settings.ImageMemoryCacheMegabytes : UISettings.DefaultImageMemoryCacheMegabytes) * 1024L * 1024L;
            }
        }

        // How many downloads run at once.
        private const int MaxDownloads = 6;

        // Hidden and never saved, as the text engine's font assets are, but reclaimable once nothing holds them.
        private const HideFlags Transient = HideFlags.HideAndDontSave & ~HideFlags.DontUnloadUnusedAsset;

        /// <summary>One URL's image: loading, loaded, or failed to load.</summary>
        internal sealed class Entry
        {
            public readonly string Url;

            /// <summary>The texture the image is on, once loaded: its own, or an atlas page; null while it loads and if it failed to.</summary>
            public Texture2D Texture;

            /// <summary>Where on its texture the image is: all of it, or its slot on an atlas page.</summary>
            public Rect Uv = new Rect(0f, 0f, 1f, 1f);

            /// <summary>Its size in pixels.</summary>
            public int Width, Height;

            /// <summary>Whether it is still loading.</summary>
            public bool Loading = true;

            internal int Users;
            internal long Bytes;
            // Whether it may go onto an atlas page (a copy asked for at the size it is drawn), and its slot there.
            internal bool Atlas;
            internal ImageAtlas.Slot Slot;
            internal UnityWebRequest Request;
            internal LinkedListNode<Entry> Unused;

            /// <summary>Raised once, as it loads or fails to.</summary>
            public event Action<Entry> Done;

            /// <summary>Raised if it is forgotten while something shows it (the cache cleared), for that to load it again.</summary>
            public event Action<Entry> Dropped;

            public Entry(string url) => Url = url;

            internal void Drop() => Dropped?.Invoke(this);

            internal void Finish()
            {
                Loading = false;
                var done = Done;
                Done = null;
                done?.Invoke(this);
            }
        }

        private static readonly Dictionary<string, Entry> s_entries = new();
        // The loaded images nothing shows, the longest unseen first.
        private static readonly LinkedList<Entry> s_unused = new();
        private static readonly Queue<Entry> s_waiting = new();
        private static long s_unusedBytes;
        private static int s_downloads;

        /// <summary>
        /// The image at a URL, loading it unless it is loaded or loading already; let go of it with <see cref="Release"/>.
        /// <paramref name="atlas"/>: a copy asked for at the size it is drawn, which needs no mipmaps and, if small, goes onto
        /// an atlas page.
        /// </summary>
        public static Entry Acquire(string url, bool atlas = false)
        {
            if (!s_entries.TryGetValue(url, out var entry))
            {
                entry = new Entry(url) { Atlas = atlas };
                s_entries.Add(url, entry);
                s_waiting.Enqueue(entry);
            }
            else if (entry.Unused != null)
            {
                s_unused.Remove(entry.Unused);
                entry.Unused = null;
                s_unusedBytes -= entry.Bytes;
            }
            entry.Users++;
            Pump();
            return entry;
        }

        /// <summary>Whether the image at a URL is loaded, shown or in the cache.</summary>
        public static bool IsLoaded(string url) => s_entries.TryGetValue(url, out var entry) && entry.Texture != null;

        /// <summary>
        /// Lets go of an image: one nothing else shows is kept while it fits in the cache, and one still loading that
        /// nothing else wants is called off (as one that failed, or was forgotten, goes), so that asking for it again
        /// starts over.
        /// </summary>
        public static void Release(Entry entry)
        {
            if (--entry.Users > 0) return;
            if (entry.Texture == null || !Known(entry))
            {
                Forget(entry);
                entry.Request?.Abort();
                Unload(entry);
                return;
            }
            entry.Unused = s_unused.AddLast(entry);
            s_unusedBytes += entry.Bytes;
            Trim();
        }

        // Drops the longest unseen images until those nothing shows fit in the cache.
        private static void Trim()
        {
            while (s_unusedBytes > CacheBytes && s_unused.First != null)
            {
                var entry = s_unused.First.Value;
                s_unused.RemoveFirst();
                entry.Unused = null;
                s_unusedBytes -= entry.Bytes;
                Forget(entry);
                Unload(entry);
            }
        }

        // Lets go of what an image takes: its slot on an atlas page, or its own texture.
        private static void Unload(Entry entry)
        {
            if (entry.Slot != null)
                ImageAtlas.Remove(entry.Slot);
            else
                Destroy(entry.Texture);
            entry.Slot = null;
            entry.Texture = null;
        }

        /// <summary>
        /// Forgets every image, on disk too: those nothing shows go now, and those shown go as what shows them lets go of
        /// them to load them again (<see cref="Entry.Dropped"/>).
        /// </summary>
        public static void Clear()
        {
            ImageDiskCache.Clear();
            var shown = new List<Entry>();
            foreach (var entry in s_entries.Values)
            {
                if (entry.Users > 0)
                {
                    shown.Add(entry);
                    continue;
                }
                entry.Unused = null;
                Unload(entry);
            }
            s_entries.Clear();
            s_unused.Clear();
            s_unusedBytes = 0;
            foreach (var entry in shown)
                entry.Drop();
        }

        private static bool Known(Entry entry) => s_entries.TryGetValue(entry.Url, out var known) && known == entry;

        private static void Forget(Entry entry)
        {
            if (Known(entry))
                s_entries.Remove(entry.Url);
        }

        // Starts what waits, as far as downloads may run at once; what nothing wants any more by its turn is skipped.
        private static void Pump()
        {
            while (s_downloads < MaxDownloads && s_waiting.Count > 0)
            {
                var entry = s_waiting.Dequeue();
                if (entry.Users > 0)
                    Download(entry);
            }
        }

        // From its file on disk if it has one, else from the web.
        private static void Download(Entry entry)
        {
            string file = ImageDiskCache.Find(entry.Url);
            Send(entry, file ?? entry.Url, fromDisk: file != null);
        }

        private static void Send(Entry entry, string source, bool fromDisk)
        {
            // Decoded off the main thread by the download handler and kept on the GPU alone; with mipmaps, as an image is
            // likely drawn smaller than it is, but for a copy asked for at the size it is drawn.
            var parameters = DownloadedTextureParams.Default;
            parameters.readable = false;
            parameters.mipmapChain = !entry.Atlas;
            var request = UnityWebRequestTexture.GetTexture(source, parameters);
            entry.Request = request;
            s_downloads++;
            request.SendWebRequest().completed += _ => Downloaded(entry, request, fromDisk);
        }

        private static void Downloaded(Entry entry, UnityWebRequest request, bool fromDisk)
        {
            s_downloads--;
            entry.Request = null;
            // An entry nothing wants any more was called off.
            if (entry.Users > 0)
            {
                var texture = request.result == UnityWebRequest.Result.Success ? DownloadHandlerTexture.GetContent(request) : null;
                if (texture == null && fromDisk)
                {
                    // A file that would not read goes, and the image is downloaded.
                    ImageDiskCache.Forget(entry.Url);
                    request.Dispose();
                    Send(entry, entry.Url, fromDisk: false);
                    return;
                }
                if (texture != null)
                {
                    if (!fromDisk)
                        ImageDiskCache.Keep(entry.Url, request.downloadHandler.data);
                    entry.Width = texture.width;
                    entry.Height = texture.height;
                    var slot = entry.Atlas ? ImageAtlas.Add(texture) : null;
                    if (slot != null)
                    {
                        // On a page now, its own texture goes.
                        Destroy(texture);
                        entry.Slot = slot;
                        entry.Texture = slot.Texture;
                        entry.Uv = slot.Uv;
                        entry.Bytes = (long)entry.Width * entry.Height * 4;
                    }
                    else
                    {
                        texture.name = entry.Url;
                        texture.hideFlags = Transient;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        entry.Texture = texture;
                        // Four bytes a pixel, and a third more for its mipmaps.
                        entry.Bytes = (long)texture.width * texture.height * 4 * (texture.mipmapCount > 1 ? 4 : 3) / 3;
                    }
                }
                else
                {
                    Debug.LogWarning($"UI Next could not load the image at {entry.Url}: {request.error}");
                }
                entry.Finish();
            }
            request.Dispose();
            Pump();
        }

        private static void Destroy(Object texture)
        {
            if (texture == null) return;
            if (Application.isPlaying)
                Object.Destroy(texture);
            else
                Object.DestroyImmediate(texture);
        }

#if UNITY_EDITOR
        // What it loaded outlives a script reload, which forgets it: it goes before.
        [UnityEditor.InitializeOnLoadMethod]
        private static void ClearOnReload() => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            foreach (var entry in s_entries.Values)
            {
                entry.Request?.Dispose();
                if (entry.Slot == null)
                    Destroy(entry.Texture);
            }
            ImageAtlas.Clear();
            s_entries.Clear();
            s_unused.Clear();
            s_waiting.Clear();
            s_unusedBytes = 0;
        };
#endif
    }
}
