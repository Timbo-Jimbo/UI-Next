using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimboJimboTests.UINext
{
    /// <summary>
    /// A fixture whose tests each get a scene of their own, opened beside whatever is open and closed after (so nothing
    /// open is touched), a root on a phone-sized screen, and the frame simulated: changes animate, and frames run only
    /// when a test advances them.
    /// </summary>
    public abstract class UITestScene
    {
        protected UIRoot Root;
        private Scene _scene;
        private Scene _previous;

        [SetUp]
        public void OpenScene()
        {
            _previous = SceneManager.GetActiveScene();
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(_scene);
            UIFrame.Simulating = true;
            UIFrame.ScreenSizeOverride = new Vector2(390f, 844f);
            Root = UIBuild.Root("Root");
            Frame();
        }

        [TearDown]
        public void CloseScene()
        {
            UIInput.CancelAll();
            UIFrame.Simulating = false;
            UIFrame.ScreenSizeOverride = null;
            if (_previous.IsValid() && _previous.isLoaded)
                SceneManager.SetActiveScene(_previous);
            EditorSceneManager.CloseScene(_scene, true);
            UIFrame.Advance(0f);
        }

        /// <summary>Runs a frame <paramref name="dt"/> seconds on.</summary>
        protected static void Frame(float dt = 0f) => UIFrame.Advance(dt);

        /// <summary>Runs frames for <paramref name="seconds"/>, a sixtieth of a second apart.</summary>
        protected static void Run(float seconds)
        {
            for (float t = 0f; t < seconds; t += 1f / 60f)
                UIFrame.Advance(1f / 60f);
        }

        /// <summary>A screen point (pixels, y up) over a point in the root (points, y down).</summary>
        protected Vector2 Screen(Vector2 point) => new(point.x * Root.PixelsPerPoint, Root.PixelSize.y - point.y * Root.PixelsPerPoint);

        protected Vector2 Center(UINode node) => node.DrawnRect.center;
    }
}
