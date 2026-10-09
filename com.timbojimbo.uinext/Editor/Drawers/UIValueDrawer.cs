using System;
using System.Collections.Generic;
using TimboJimbo.UINext;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// Every <see cref="UIValue{T}"/> as a plain field would be: its literal, full width (a small enum as lit buttons),
    /// with a link at the end of its line for values that can take a token: click it to pick one, and the token's asset
    /// stands in for the literal with the link lit; click it again to unlink and have the literal back. Once a value
    /// has any variants, they are listed below. Right-click it to add a variant.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIValue<>), true)]
    internal sealed class UIValueDrawer : PropertyDrawer
    {
        private const float LinkWidth = 20f;
        private const float Gap = 2f;

        // The token type for each value type (the UIToken<T> there is for it), null for none: those values have no link.
        private static readonly Dictionary<Type, Type> s_tokenTypes = new();
        private static GUIContent s_linked;
        private static GUIContent s_unlinked;

        // The value a token is being picked for, and the picker opened for it.
        private static string s_pickingPath;
        private static UnityEngine.Object s_pickingTarget;
        private static int s_pickerId;

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
            var valueType = ValueType();
            var tokenType = TokenTypeFor(valueType);
            bool canLink = token != null && tokenType != null;
            bool linked = canLink && token.objectReferenceValue != null;
            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            float valueHeight = ValueHeight(property, label);
            var fieldRect = new Rect(position.x, position.y, position.width - (canLink ? LinkWidth + Gap : 0f), valueHeight);
            var firstLine = new Rect(fieldRect.x, fieldRect.y, fieldRect.width, line);

            if (linked)
            {
                // The token stands in for the literal; another can be dropped in its place.
                var field = EditorGUI.PrefixLabel(firstLine, label);
                var picked = EditorGUI.ObjectField(field, token.objectReferenceValue, tokenType, false);
                if (picked != token.objectReferenceValue)
                    token.objectReferenceValue = picked;
            }
            else if (value != null && LitButtons.Suits(valueType))
            {
                var field = EditorGUI.PrefixLabel(firstLine, label);
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                LitButtons.Choice(field, value, valueType);
                EditorGUI.indentLevel = indent;
            }
            else if (value != null && LitButtons.IsFlags(valueType))
            {
                var field = EditorGUI.PrefixLabel(firstLine, label);
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                LitButtons.Flags(field, value, valueType);
                EditorGUI.indentLevel = indent;
            }
            else if (value != null)
            {
                EditorGUI.PropertyField(fieldRect, value, label, true);
            }

            if (canLink)
            {
                s_linked ??= EditorGUIUtility.IconContent("Linked", "|Linked to a token: click to unlink");
                s_unlinked ??= EditorGUIUtility.IconContent("Unlinked", "|Link to a token");
                var linkRect = new Rect(position.xMax - LinkWidth, position.y + (line - 16f) * 0.5f, LinkWidth, 16f);
                if (GUI.Button(linkRect, linked ? s_linked : s_unlinked, EditorStyles.iconButton))
                {
                    if (linked)
                    {
                        token.objectReferenceValue = null;
                    }
                    else
                    {
                        s_pickerId = GUIUtility.GetControlID(FocusType.Passive) + 1000;
                        s_pickingPath = property.propertyPath;
                        s_pickingTarget = property.serializedObject.targetObject;
                        EditorGUIUtility.ShowObjectPicker<TokenAsset>(null, false, "t:" + tokenType.Name, s_pickerId);
                    }
                }
                // The picker's answer, for the value it was opened for.
                var e = Event.current;
                if (e.type == EventType.ExecuteCommand && s_pickingPath == property.propertyPath && s_pickingTarget == property.serializedObject.targetObject
                    && (e.commandName == "ObjectSelectorUpdated" || e.commandName == "ObjectSelectorClosed")
                    && EditorGUIUtility.GetObjectPickerControlID() == s_pickerId)
                {
                    var picked = EditorGUIUtility.GetObjectPickerObject();
                    if (picked == null || tokenType.IsInstanceOfType(picked))
                        token.objectReferenceValue = picked;
                    if (e.commandName == "ObjectSelectorClosed")
                        s_pickingPath = null;
                    e.Use();
                }
            }

            if (variants != null && variants.arraySize > 0)
            {
                var listRect = new Rect(position.x, position.y + valueHeight + EditorGUIUtility.standardVerticalSpacing, position.width,
                    EditorGUI.GetPropertyHeight(variants, true));
                EditorGUI.indentLevel++;
                EditorGUI.PropertyField(listRect, variants, new GUIContent($"Variants ({variants.arraySize})"), true);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private float ValueHeight(SerializedProperty property, GUIContent label)
        {
            var value = property.FindPropertyRelative("_value");
            var token = property.FindPropertyRelative("_token");
            var valueType = ValueType();
            if (value == null || (token != null && token.objectReferenceValue != null && TokenTypeFor(valueType) != null))
                return EditorGUIUtility.singleLineHeight;
            if (LitButtons.Suits(valueType) || LitButtons.IsFlags(valueType))
                return EditorGUIUtility.singleLineHeight;
            return EditorGUI.GetPropertyHeight(value, label, true);
        }

        // The T of the UIValue<T> drawn: the field's type, or the element type of a list of them.
        private Type ValueType()
        {
            var type = fieldInfo?.FieldType;
            if (type == null) return null;
            if (type.IsArray) type = type.GetElementType();
            else if (type.IsGenericType && typeof(List<>).IsAssignableFrom(type.GetGenericTypeDefinition())) type = type.GetGenericArguments()[0];
            for (; type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UIValue<>))
                    return type.GetGenericArguments()[0];
            }
            return null;
        }

        // The token asset type for a value type: the one UIToken<T> subclass there is for it, if any.
        private static Type TokenTypeFor(Type valueType)
        {
            if (valueType == null) return null;
            if (s_tokenTypes.TryGetValue(valueType, out var found)) return found;
            foreach (var type in TypeCache.GetTypesDerivedFrom<TokenAsset>())
            {
                if (type.IsAbstract) continue;
                for (var t = type.BaseType; t != null; t = t.BaseType)
                {
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(UIToken<>) && t.GetGenericArguments()[0] == valueType)
                    {
                        found = type;
                        break;
                    }
                }
                if (found != null) break;
            }
            s_tokenTypes[valueType] = found;
            return found;
        }
    }

    /// <summary>A size on one line: how it is sized, as lit buttons, and the number that goes with it (fixed or percent); min and max under the foldout.</summary>
    [CustomPropertyDrawer(typeof(Sizing))]
    internal sealed class SizingDrawer : PropertyDrawer
    {
        private static readonly GUIContent[] Modes =
        {
            new("Fit", "As big as what is inside"), new("Grow", "Fills what is left"), new("Fixed", "This many points"), new("%", "This share of its parent"),
        };

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
            float modeWidth = hasValue ? Mathf.Max(field.width * 0.6f, field.width - 70f) : field.width;
            int chosen = GUI.Toolbar(new Rect(field.x, field.y, modeWidth, line), Mathf.Clamp(mode.enumValueIndex, 0, Modes.Length - 1), Modes, EditorStyles.miniButton);
            if (chosen != mode.enumValueIndex)
                mode.enumValueIndex = chosen;
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

    /// <summary>Wrapping on one line: the mode as lit buttons, and the number that goes with it (a grid's count, adaptive's least size).</summary>
    [CustomPropertyDrawer(typeof(Wrap))]
    internal sealed class WrapDrawer : PropertyDrawer
    {
        private static readonly GUIContent[] Modes =
        {
            new("None", "One line"), new("Lines", "A child past the end starts the next line"), new("Grid", "Lines of this many equal cells"),
            new("Adaptive", "Lines of as many cells at least this long as fit"),
        };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var mode = property.FindPropertyRelative("Mode");
            var field = EditorGUI.PrefixLabel(position, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var wrap = (LayoutWrapMode)mode.enumValueIndex;
            var number = wrap == LayoutWrapMode.Grid ? property.FindPropertyRelative("Count")
                : wrap == LayoutWrapMode.Adaptive ? property.FindPropertyRelative("MinSize") : null;
            float modeWidth = number != null ? Mathf.Max(field.width * 0.6f, field.width - 70f) : field.width;
            int chosen = GUI.Toolbar(new Rect(field.x, field.y, modeWidth, field.height), Mathf.Clamp(mode.enumValueIndex, 0, Modes.Length - 1), Modes, EditorStyles.miniButton);
            if (chosen != mode.enumValueIndex)
                mode.enumValueIndex = chosen;
            if (number != null)
                EditorGUI.PropertyField(new Rect(field.x + modeWidth + 2f, field.y, field.width - modeWidth - 2f, field.height), number, GUIContent.none);
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
