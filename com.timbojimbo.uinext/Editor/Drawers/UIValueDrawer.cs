using TimboJimbo.UINext;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// Every <see cref="UIValue{T}"/> as a plain field would be: its literal (or, with a token, the token in its place)
    /// with a compact token field at the end of its first line, and, once it has any, its variants as a list below.
    /// Right-click it to add a variant.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIValue<>), true)]
    internal sealed class UIValueDrawer : PropertyDrawer
    {
        private const float TokenWidth = 64f;
        private const float Gap = 2f;

        [InitializeOnLoadMethod]
        private static void AddContextMenu() => EditorApplication.contextualPropertyMenu += (menu, property) =>
        {
            var variants = property.FindPropertyRelative("_variants");
            if (variants == null || property.FindPropertyRelative("_value") == null) return;
            var copy = property.Copy();
            menu.AddItem(new GUIContent("Add Variant"), false, () =>
            {
                var list = copy.FindPropertyRelative("_variants");
                list.arraySize++;
                list.isExpanded = true;
                copy.serializedObject.ApplyModifiedProperties();
            });
        };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var variants = property.FindPropertyRelative("_variants");
            float height = ValueHeight(property, label);
            if (variants != null && variants.arraySize > 0)
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(variants, true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var value = property.FindPropertyRelative("_value");
            var token = property.FindPropertyRelative("_token");
            var variants = property.FindPropertyRelative("_variants");
            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            float valueHeight = ValueHeight(property, label);
            var fieldRect = new Rect(position.x, position.y, position.width - (token != null ? TokenWidth + Gap : 0f), valueHeight);

            if (token != null && token.objectReferenceValue != null)
            {
                // A token stands in for the literal; clear it from its own field to go back.
                var field = EditorGUI.PrefixLabel(new Rect(fieldRect.x, fieldRect.y, fieldRect.width, line), label);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.ObjectField(field, token.objectReferenceValue, typeof(TokenAsset), false);
            }
            else if (value != null)
            {
                EditorGUI.PropertyField(fieldRect, value, label, true);
            }

            if (token != null)
            {
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                EditorGUI.PropertyField(new Rect(position.xMax - TokenWidth, position.y, TokenWidth, line), token, GUIContent.none);
                EditorGUI.indentLevel = indent;
            }

            if (variants != null && variants.arraySize > 0)
            {
                var listRect = new Rect(position.x, position.y + valueHeight + EditorGUIUtility.standardVerticalSpacing, position.width,
                    EditorGUI.GetPropertyHeight(variants, true));
                EditorGUI.indentLevel++;
                EditorGUI.PropertyField(listRect, variants, new GUIContent($"{label.text} Variants ({variants.arraySize})"), true);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static float ValueHeight(SerializedProperty property, GUIContent label)
        {
            var value = property.FindPropertyRelative("_value");
            var token = property.FindPropertyRelative("_token");
            if (value == null || (token != null && token.objectReferenceValue != null))
                return EditorGUIUtility.singleLineHeight;
            return EditorGUI.GetPropertyHeight(value, label, true);
        }
    }

    /// <summary>A size on one line: how it is sized, and the number that goes with it (fixed or percent).</summary>
    [CustomPropertyDrawer(typeof(Sizing))]
    internal sealed class SizingDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight * (property.isExpanded ? 2f : 1f) + (property.isExpanded ? EditorGUIUtility.standardVerticalSpacing : 0f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            var mode = property.FindPropertyRelative("Mode");
            var value = property.FindPropertyRelative("Value");
            var min = property.FindPropertyRelative("Min");
            var max = property.FindPropertyRelative("Max");
            var first = new Rect(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(new Rect(first.x, first.y, EditorGUIUtility.labelWidth, line), property.isExpanded, GUIContent.none, true);
            var field = EditorGUI.PrefixLabel(first, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var sizing = (SizingMode)mode.enumValueIndex;
            bool hasValue = sizing is SizingMode.Fixed or SizingMode.Percent;
            float modeWidth = hasValue ? field.width * 0.55f : field.width;
            EditorGUI.PropertyField(new Rect(field.x, field.y, modeWidth, line), mode, GUIContent.none);
            if (hasValue)
                EditorGUI.PropertyField(new Rect(field.x + modeWidth + 2f, field.y, field.width - modeWidth - 2f, line), value, GUIContent.none);
            if (property.isExpanded)
            {
                var second = new Rect(field.x, field.y + line + EditorGUIUtility.standardVerticalSpacing, field.width, line);
                float half = (second.width - 4f) * 0.5f;
                float labels = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 30f;
                EditorGUI.PropertyField(new Rect(second.x, second.y, half, line), min, new GUIContent("Min"));
                EditorGUI.PropertyField(new Rect(second.x + half + 4f, second.y, half, line), max, new GUIContent("Max"));
                EditorGUIUtility.labelWidth = labels;
            }
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>Four numbers on one line, each labelled by its initial.</summary>
    internal abstract class FourFloatDrawer : PropertyDrawer
    {
        protected abstract (string Field, string Label)[] Fields { get; }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var field = EditorGUI.PrefixLabel(position, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float labels = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 14f;
            var fields = Fields;
            float width = (field.width - 2f * (fields.Length - 1)) / fields.Length;
            for (int i = 0; i < fields.Length; i++)
            {
                var rect = new Rect(field.x + i * (width + 2f), field.y, width, field.height);
                EditorGUI.PropertyField(rect, property.FindPropertyRelative(fields[i].Field), new GUIContent(fields[i].Label, fields[i].Field));
            }
            EditorGUIUtility.labelWidth = labels;
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(Insets))]
    internal sealed class InsetsDrawer : FourFloatDrawer
    {
        protected override (string Field, string Label)[] Fields => new[] { ("Left", "L"), ("Right", "R"), ("Top", "T"), ("Bottom", "B") };
    }

    [CustomPropertyDrawer(typeof(CornerRadii))]
    internal sealed class CornerRadiiDrawer : FourFloatDrawer
    {
        protected override (string Field, string Label)[] Fields => new[] { ("TopLeft", "↖"), ("TopRight", "↗"), ("BottomRight", "↘"), ("BottomLeft", "↙") };
    }
}
