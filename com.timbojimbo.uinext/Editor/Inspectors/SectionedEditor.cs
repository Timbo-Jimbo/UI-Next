using System.Collections.Generic;
using TimboJimbo.UINext;
using UnityEditor;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// An inspector in sections: each a header and the fields under it, in the order given, so a long component reads
    /// at a glance. Fields a section does not name (added since) are drawn after, under Other.
    /// </summary>
    internal abstract class SectionedEditor : Editor
    {
        private static readonly HashSet<string> s_drawn = new();

        protected abstract (string Header, string[] Fields)[] Sections { get; }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            s_drawn.Clear();
            bool first = true;
            foreach (var (header, fields) in Sections)
            {
                bool any = false;
                foreach (var field in fields)
                    any |= serializedObject.FindProperty(field) != null;
                if (!any) continue;
                Header(header, first);
                first = false;
                foreach (var field in fields)
                {
                    var property = serializedObject.FindProperty(field);
                    if (property == null) continue;
                    EditorGUILayout.PropertyField(property, true);
                    s_drawn.Add(field);
                }
            }
            var iterator = serializedObject.GetIterator();
            bool other = false;
            for (bool enter = true; iterator.NextVisible(enter); enter = false)
            {
                if (iterator.name == "m_Script" || s_drawn.Contains(iterator.name)) continue;
                if (!other)
                {
                    Header("Other", first);
                    other = true;
                }
                EditorGUILayout.PropertyField(iterator, true);
            }
            serializedObject.ApplyModifiedProperties();
        }

        private static void Header(string text, bool first)
        {
            if (!first) EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }
    }

    [CustomEditor(typeof(UINode)), CanEditMultipleObjects]
    internal sealed class UINodeEditor : SectionedEditor
    {
        protected override (string Header, string[] Fields)[] Sections => new[]
        {
            ("Layout", new[] { "_width", "_height", "_padding", "_gap", "_direction", "_wrap", "_alignX", "_alignY", "_aspectRatio", "_floating", "_display", "_ignoresSafeArea" }),
            ("Drawn", new[] { "_opacity", "_offset", "_scale", "_rotation", "_zIndex" }),
            ("Effects", new[] { "_groupOpacity", "_blur", "_saturation", "_brightness", "_mask" }),
            ("Clipping", new[] { "_clips", "_clipRadii", "_topLayer" }),
            ("Input", new[] { "_hitTestable", "_avoidsKeyboard" }),
            ("Focus", new[] { "_focusable", "_focusScope", "_focusEntry", "_prefersFocus", "_focusRing" }),
            ("Matching", new[] { "_matchName", "_matchId", "_matchAnchor" }),
        };
    }

    [CustomEditor(typeof(Box)), CanEditMultipleObjects]
    internal sealed class BoxEditor : SectionedEditor
    {
        protected override (string Header, string[] Fields)[] Sections => new[]
        {
            ("Fill", new[] { "_fill", "_cornerRadii" }),
            ("Gradient", new[] { "_gradient", "_gradientTo", "_gradientAngle" }),
            ("Border", new[] { "_borderWidth", "_borderColor", "_borderGradient", "_borderColorTo" }),
            ("Shadow", new[] { "_shadowColor", "_shadowBlur", "_shadowOffset", "_shadowSpread", "_shadowGradient", "_shadowColorTo" }),
        };
    }

    [CustomEditor(typeof(Label)), CanEditMultipleObjects]
    internal sealed class LabelEditor : SectionedEditor
    {
        protected override (string Header, string[] Fields)[] Sections => new[]
        {
            ("Text", new[] { "_text", "_font", "_fontSize", "_color", "_bold", "_italic" }),
            ("Lines", new[] { "_alignment", "_wrap", "_maxLines", "_overflow", "_characterSpacing" }),
            ("Outline and shadow", new[] { "_outlineWidth", "_outlineColor", "_shadowColor", "_shadowOffset" }),
            ("Markup", new[] { "_markdown" }),
        };
    }

    [CustomEditor(typeof(ImageView)), CanEditMultipleObjects]
    internal sealed class ImageViewEditor : SectionedEditor
    {
        protected override (string Header, string[] Fields)[] Sections => new[]
        {
            ("Image", new[] { "_sprite", "_texture", "_tint", "_fit", "_cornerRadii" }),
        };
    }

    [CustomEditor(typeof(ScrollView)), CanEditMultipleObjects]
    internal sealed class ScrollViewEditor : SectionedEditor
    {
        protected override (string Header, string[] Fields)[] Sections => new[]
        {
            ("Scrolling", new[] { "_axis", "_bounces", "_atEnd", "_snap" }),
            ("Indicators", new[] { "_showsIndicators", "_indicatorColor" }),
            ("Clear of", new[] { "_clearOfTop", "_clearOfBottom", "_clearOfLeft", "_clearOfRight", "_clearMargin" }),
            ("Into view", new[] { "_scrollPadding" }),
        };
    }
}
