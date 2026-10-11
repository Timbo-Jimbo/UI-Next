using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos.Circle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TimboJimboEditor.UINext.Demos
{
    /// <summary>What the demo builders share: scenes, cameras, roots, token assets and labels drawn from tokens.</summary>
    internal static class DemoKit
    {
        public const string DemosFolder = "Packages/com.timbojimbo.uinext/Demos";

        /// <summary>
        /// Builds a scene with <paramref name="build"/> and saves it to <paramref name="path"/>. Built in a scene of its
        /// own opened beside whatever is open (left untouched), then closed, unless <paramref name="open"/> opens it.
        /// </summary>
        public static void BuildScene(string path, System.Action<Scene> build, bool open)
        {
            if (open && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = open
                ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                build(scene);
            }
            finally
            {
                if (previous.IsValid() && previous != scene && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
            EditorSceneManager.SaveScene(scene, path);
            if (!open)
                EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>A camera that draws nothing but a clear colour: the UI is drawn after it.</summary>
        public static Camera Camera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.cullingMask = 0;
            camera.orthographic = true;
            go.AddComponent<UniversalAdditionalCameraData>();
            go.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        /// <summary>A root covering the screen, sized for a phone held upright.</summary>
        public static UIRoot Root(string name)
        {
            var root = UIBuild.Root(name);
            root.transform.position = new Vector3(-195f, 422f, 0f);
            // A rounded focus ring, round whatever has focus.
            root.FocusRingRadius.Value = 14f;
            return root;
        }

        /// <summary>A colour token at <paramref name="path"/>, made or updated: <paramref name="light"/> by default, <paramref name="dark"/> in <paramref name="mode"/>.</summary>
        public static ColorToken Token(string path, Color light, UIState mode, Color dark)
        {
            var token = AssetDatabase.LoadAssetAtPath<ColorToken>(path);
            if (token == null)
            {
                token = ScriptableObject.CreateInstance<ColorToken>();
                AssetDatabase.CreateAsset(token, path);
            }
            token.Default = light;
            if (mode != null) token.SetMode(mode, dark);
            EditorUtility.SetDirty(token);
            return token;
        }

        /// <summary>A state asset at <paramref name="path"/>, made if it is not there.</summary>
        public static UIState State(string path, string name, StateKind kind)
        {
            var state = AssetDatabase.LoadAssetAtPath<UIState>(path);
            if (state != null) return state;
            state = UIState.Create(name, kind);
            state.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(state, path);
            return state;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        /// <summary>A label whose colour is a token.</summary>
        public static Label Text(UINode parent, string name, string text, float size, ColorToken color, bool bold = false)
        {
            var label = parent.Child(name).Label(text, size, bold: bold);
            label.Color.Token = color;
            return label;
        }

        /// <summary>A box whose fill is a token.</summary>
        public static Box Fill(UINode node, ColorToken fill, CornerRadii radii = default)
        {
            var box = node.Box(Color.white, radii);
            box.Fill.Token = fill;
            return box;
        }

        /// <summary>A round avatar of a colour with initials on it (round in a matched flight too).</summary>
        public static UINode Avatar(UINode parent, float size, Color colour, string initials)
        {
            var avatar = parent.Child("Avatar").Sized(Sizing.Fixed(size), Sizing.Fixed(size)).Center();
            avatar.Box(colour, size * 0.5f);
            avatar.ClipRadii.Value = size * 0.5f;
            avatar.Child("Initials").Label(initials, size * 0.36f, Color.white, bold: true);
            return avatar;
        }

        /// <summary>
        /// A slider: a track the knob rides along, its fill as long as its value. The track takes focus itself, and left and
        /// right, its focus ring shaking no at its ends.
        /// </summary>
        public static Slider Slider(UINode parent, float value, Color fill)
        {
            var track = parent.Child("Slider").Row().Pad(13f, 0f).Align(AlignX.Left, AlignY.Center);
            track.Sized(Sizing.Grow(), Sizing.Fixed(32f));
            track.Focusable.Value = Focusability.Always;
            // Centred, so it lies under the fill: a floating child is as wide as its parent inside its padding.
            var rail = track.Child("Rail").Float(AttachPoint.CenterCenter, AttachPoint.CenterCenter);
            rail.Sized(Sizing.Percent(1f), Sizing.Fixed(4f));
            rail.Box(new Color(0.47f, 0.47f, 0.5f, 0.25f), 2f);
            var bar = track.Child("Fill").Sized(Sizing.Percent(value), Sizing.Fixed(4f));
            bar.Box(fill, 2f);
            var knob = track.Child("Knob").Sized(Sizing.Fixed(26f), Sizing.Fixed(26f)).FloatOn(bar, AttachPoint.CenterCenter, AttachPoint.RightCenter);
            Shadow(knob.Box(Color.white, 13f), 0.25f, 4f, 2f);
            knob.HitTestable.Value = false;
            track.Add<Draggable>();
            var slider = track.Add<Slider>();
            slider.Setup(bar, value);
            return slider;
        }

        /// <summary>A soft shadow under a box.</summary>
        public static void Shadow(Box box, float alpha = 0.08f, float blur = 16f, float y = 4f)
        {
            box.ShadowColor.Value = new Color(0f, 0f, 0f, alpha);
            box.ShadowBlur.Value = blur;
            box.ShadowOffset.Value = new Vector2(0f, y);
        }
    }
}
