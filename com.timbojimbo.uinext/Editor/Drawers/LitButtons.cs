using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// Small choices as a row of lit buttons rather than a popup: an enum of a few values (a direction, an alignment, a
    /// display mode), with Unity's own icons where it has fitting ones, and a flags enum as one toggle a flag.
    /// </summary>
    internal static class LitButtons
    {
        // Unity's icons for the values that have a picture: by "Type.Value".
        private static readonly Dictionary<string, string> s_icons = new()
        {
            { "LayoutDirection.LeftToRight", "HorizontalLayoutGroup Icon" },
            { "LayoutDirection.TopToBottom", "VerticalLayoutGroup Icon" },
            { "AlignX.Left", "align_horizontally_left" },
            { "AlignX.Center", "align_horizontally_center" },
            { "AlignX.Right", "align_horizontally_right" },
            { "AlignY.Top", "align_vertically_top" },
            { "AlignY.Center", "align_vertically_center" },
            { "AlignY.Bottom", "align_vertically_bottom" },
            { "DisplayMode.Visible", "scenevis_visible_hover" },
            { "DisplayMode.Hidden", "scenevis_hidden_hover" },
        };

        private static readonly Dictionary<Type, GUIContent[]> s_contents = new();
        private static readonly Dictionary<Type, (GUIContent Content, int Bit)[]> s_flags = new();

        /// <summary>Whether an enum is drawn as buttons: a handful of values, none of them flags.</summary>
        public static bool Suits(Type type) => type != null && type.IsEnum && !IsFlags(type) && System.Enum.GetValues(type).Length <= 4;

        public static bool IsFlags(Type type) => type != null && type.IsEnum && type.IsDefined(typeof(FlagsAttribute), false);

        /// <summary>An enum property as one lit button a value.</summary>
        public static void Choice(Rect rect, SerializedProperty property, Type type)
        {
            var contents = Contents(type);
            int index = Mathf.Clamp(property.enumValueIndex, 0, contents.Length - 1);
            int chosen = GUI.Toolbar(rect, index, contents, EditorStyles.miniButton);
            if (chosen != index)
                property.enumValueIndex = chosen;
        }

        /// <summary>A flags enum property as one toggle a flag (its single-bit values).</summary>
        public static void Flags(Rect rect, SerializedProperty property, Type type)
        {
            var flags = FlagContents(type);
            if (flags.Length == 0) return;
            float width = (rect.width - 2f * (flags.Length - 1)) / flags.Length;
            int value = property.intValue;
            for (int i = 0; i < flags.Length; i++)
            {
                var style = flags.Length == 1 ? EditorStyles.miniButton
                    : i == 0 ? EditorStyles.miniButtonLeft : i == flags.Length - 1 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
                var cell = new Rect(rect.x + i * (width + 2f), rect.y, width, rect.height);
                bool on = (value & flags[i].Bit) != 0;
                bool now = GUI.Toggle(cell, on, flags[i].Content, style);
                if (now != on)
                    value = now ? value | flags[i].Bit : value & ~flags[i].Bit;
            }
            if (value != property.intValue)
                property.intValue = value;
        }

        private static GUIContent[] Contents(Type type)
        {
            if (s_contents.TryGetValue(type, out var contents)) return contents;
            var names = System.Enum.GetNames(type);
            contents = new GUIContent[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string text = ObjectNames.NicifyVariableName(names[i]);
                var icon = s_icons.TryGetValue(type.Name + "." + names[i], out var name) ? Icon(name) : null;
                contents[i] = icon != null ? new GUIContent(icon, text) : new GUIContent(text, text);
            }
            s_contents[type] = contents;
            return contents;
        }

        private static (GUIContent Content, int Bit)[] FlagContents(Type type)
        {
            if (s_flags.TryGetValue(type, out var flags)) return flags;
            var list = new List<(GUIContent, int)>();
            var names = System.Enum.GetNames(type);
            var values = System.Enum.GetValues(type);
            for (int i = 0; i < names.Length; i++)
            {
                int bit = Convert.ToInt32(values.GetValue(i));
                if (bit == 0 || (bit & (bit - 1)) != 0) continue;
                string text = ObjectNames.NicifyVariableName(names[i]);
                list.Add((new GUIContent(text, text), bit));
            }
            flags = list.ToArray();
            s_flags[type] = flags;
            return flags;
        }

        // One of Unity's icons, in the skin's colour if it has one; null if it is not there.
        private static Texture2D Icon(string name)
        {
            if (EditorGUIUtility.isProSkin)
            {
                var dark = EditorGUIUtility.FindTexture("d_" + name);
                if (dark != null) return dark;
            }
            return EditorGUIUtility.FindTexture(name);
        }
    }
}
