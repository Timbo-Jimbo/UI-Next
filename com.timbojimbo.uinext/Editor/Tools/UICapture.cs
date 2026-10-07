using System.IO;
using TimboJimbo.UINext;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// Renders every root to a PNG as the game view would show it, through a camera of its own that draws nothing but
    /// the UI: for checking what the stack draws without a game view, from scripts and tests.
    /// </summary>
    public static class UICapture
    {
        /// <summary>
        /// Renders a frame at the main game view's size (or <paramref name="width"/> by <paramref name="height"/>) over
        /// <paramref name="background"/> and writes it to <paramref name="path"/>. Returns the size rendered.
        /// </summary>
        public static Vector2Int Capture(string path, Color background, int width = 0, int height = 0)
        {
            if (width <= 0 || height <= 0)
            {
                var size = Handles.GetMainGameViewSize();
                width = Mathf.Max(1, (int)size.x);
                height = Mathf.Max(1, (int)size.y);
            }

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "UINext Capture" };
            var go = new GameObject("UINext Capture") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.cullingMask = 0;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                camera.orthographic = true;
                camera.targetTexture = target;
                go.AddComponent<UniversalAdditionalCameraData>();
                UIFrame.ScreenSizeOverride = new Vector2(width, height);
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            finally
            {
                UIFrame.ScreenSizeOverride = null;
                UI.RequestRepaint();
                Object.DestroyImmediate(go);
                target.Release();
                Object.DestroyImmediate(target);
            }
            return new Vector2Int(width, height);
        }
    }
}
