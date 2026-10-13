using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// The files of the images loaded from URLs, kept between runs in the OS's cache folder (Application.temporaryCachePath,
    /// which iOS and Android may empty to make room), each named by its URL's hash and taken as never changing. Past the
    /// project's limit the files used longest ago go first; when a file was last used is its write time, touched as it is
    /// read, so no index is kept. What touches the folder runs on a background thread, one thing at a time, and a file is
    /// written under another name and renamed once whole, so it is never read half written. Not on WebGL, whose browser
    /// keeps what it downloads itself.
    /// </summary>
    internal static class ImageDiskCache
    {
        private static readonly SHA256 s_hash = SHA256.Create();
        private static string s_folder;
        private static Task s_io = Task.CompletedTask;
        // On the background thread: each file's size and when it was last used, read from the folder the first time.
        private static Dictionary<string, (long Size, DateTime Used)> s_files;
        private static long s_bytes;

        /// <summary>Where the files are kept, or null where they are not.</summary>
        private static string Folder
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return null;
#else
                return s_folder ??= Path.Combine(Application.temporaryCachePath, "UI Next Images");
#endif
            }
        }

        // How many bytes of files are kept: the project's setting, 256 MB without one.
        private static long Limit
        {
            get
            {
                var settings = UISettings.Instance;
                return (settings != null ? settings.ImageDiskCacheMegabytes : UISettings.DefaultImageDiskCacheMegabytes) * 1024L * 1024L;
            }
        }

        /// <summary>The file a URL's image is kept in, as a URL to read it from, or null if it has none; marked as used.</summary>
        public static string Find(string url)
        {
            string folder = Folder;
            if (folder == null || Limit <= 0) return null;
            string name = NameOf(url), path = Path.Combine(folder, name);
            if (!File.Exists(path)) return null;
            Run(() =>
            {
                Scan(folder);
                var now = DateTime.UtcNow;
                try
                {
                    File.SetLastWriteTimeUtc(path, now);
                }
                catch (IOException)
                {
                    // Gone since (trimmed): nothing to mark.
                    return;
                }
                if (s_files.TryGetValue(name, out var file))
                    s_files[name] = (file.Size, now);
            });
            return new Uri(path).AbsoluteUri;
        }

        /// <summary>Keeps the bytes downloaded for a URL's image, letting go of the files used longest ago to make room.</summary>
        public static void Keep(string url, byte[] bytes)
        {
            string folder = Folder;
            long limit = Limit;
            if (folder == null || limit <= 0 || bytes == null || bytes.Length == 0) return;
            string name = NameOf(url);
            Run(() =>
            {
                Scan(folder);
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, name), whole = path + ".tmp";
                File.WriteAllBytes(whole, bytes);
                if (File.Exists(path)) File.Delete(path);
                File.Move(whole, path);
                Record(name, bytes.Length, DateTime.UtcNow);
                Trim(folder, limit);
            });
        }

        /// <summary>Lets go of a URL's file: one that would not read.</summary>
        public static void Forget(string url)
        {
            string folder = Folder;
            if (folder == null) return;
            string name = NameOf(url);
            Run(() =>
            {
                Scan(folder);
                Delete(folder, name);
            });
        }

        /// <summary>Lets go of every file.</summary>
        public static void Clear()
        {
            string folder = Folder;
            if (folder == null) return;
            Run(() =>
            {
                s_files = new Dictionary<string, (long, DateTime)>();
                s_bytes = 0;
                if (!Directory.Exists(folder)) return;
                foreach (string path in Directory.GetFiles(folder))
                    TryDelete(path);
            });
        }

        // The rest runs on the background thread.

        // Reads the folder the first time: the size of each file and when it was last used; what a run cut short left half
        // written goes.
        private static void Scan(string folder)
        {
            if (s_files != null) return;
            s_files = new Dictionary<string, (long, DateTime)>();
            s_bytes = 0;
            if (!Directory.Exists(folder)) return;
            foreach (string path in Directory.GetFiles(folder))
            {
                if (path.EndsWith(".tmp", StringComparison.Ordinal))
                {
                    TryDelete(path);
                    continue;
                }
                var info = new FileInfo(path);
                Record(info.Name, info.Length, info.LastWriteTimeUtc);
            }
        }

        private static void Record(string name, long size, DateTime used)
        {
            if (s_files.TryGetValue(name, out var file))
                s_bytes -= file.Size;
            s_files[name] = (size, used);
            s_bytes += size;
        }

        // Lets go of the files used longest ago until those kept fit.
        private static void Trim(string folder, long limit)
        {
            if (s_bytes <= limit) return;
            var byUse = new List<KeyValuePair<string, (long Size, DateTime Used)>>(s_files);
            byUse.Sort((a, b) => a.Value.Used.CompareTo(b.Value.Used));
            foreach (var file in byUse)
            {
                if (s_bytes <= limit) break;
                Delete(folder, file.Key);
            }
        }

        private static void Delete(string folder, string name)
        {
            if (s_files.TryGetValue(name, out var file))
            {
                s_files.Remove(name);
                s_bytes -= file.Size;
            }
            TryDelete(Path.Combine(folder, name));
        }

        // A file being read can't be deleted on Windows: it goes the next time instead.
        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void Run(Action work) => s_io = s_io.ContinueWith(_ =>
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                Debug.LogWarning("UI Next's image cache on disk: " + e.Message);
            }
        }, TaskScheduler.Default);

        // A URL's file name: the first half of its SHA-256, in hex.
        private static string NameOf(string url)
        {
            var hash = s_hash.ComputeHash(Encoding.UTF8.GetBytes(url));
            var name = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
                name.Append(hash[i].ToString("x2"));
            return name.ToString();
        }
    }
}
