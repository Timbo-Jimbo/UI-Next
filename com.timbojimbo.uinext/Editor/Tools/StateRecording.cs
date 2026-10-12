using System;
using System.Collections.Generic;
using TimboJimbo.UINext;
using UnityEditor;
using Object = UnityEngine.Object;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// A value's variants read and written through its serialized fields, on every object an inspector draws it for, so
    /// each takes an edit whatever its own list holds: recording into a state, and removing a variant.
    /// </summary>
    internal static class StateRecording
    {
        // Interaction states in the order they are named in: weakest first.
        private static readonly InteractionStates[] s_order =
        {
            InteractionStates.FocusWithin, InteractionStates.Selected, InteractionStates.Hovered, InteractionStates.Focused,
            InteractionStates.Pressed, InteractionStates.Disabled,
        };

        /// <summary>The index of the variant for exactly <paramref name="when"/> in a value's list, or -1.</summary>
        public static int Find(SerializedProperty variants, in StateCondition when)
        {
            for (int i = 0; i < variants.arraySize; i++)
                if (Same(variants.GetArrayElementAtIndex(i).FindPropertyRelative("When"), when))
                    return i;
            return -1;
        }

        /// <summary>
        /// Sets the variant for <paramref name="when"/> of the value at <paramref name="property"/>'s path, on every object
        /// it is drawn for, making it where there is none: its value when <paramref name="setsValue"/>, its token when
        /// <paramref name="setsToken"/>. One made starts at <paramref name="value"/> either way.
        /// </summary>
        public static void Write(SerializedProperty property, StateCondition when, object value, bool setsValue, Object token, bool setsToken)
        {
            foreach (var target in property.serializedObject.targetObjects)
            {
                var serialized = new SerializedObject(target);
                var variants = serialized.FindProperty(property.propertyPath)?.FindPropertyRelative("_variants");
                if (variants == null) continue;
                int index = Find(variants, when);
                if (index < 0)
                {
                    index = variants.arraySize;
                    variants.arraySize++;
                    var made = variants.GetArrayElementAtIndex(index);
                    SetWhen(made.FindPropertyRelative("When"), when);
                    made.FindPropertyRelative("Value").boxedValue = value;
                    made.FindPropertyRelative("Token").objectReferenceValue = null;
                }
                var variant = variants.GetArrayElementAtIndex(index);
                if (setsValue)
                    variant.FindPropertyRelative("Value").boxedValue = value;
                if (setsToken)
                    variant.FindPropertyRelative("Token").objectReferenceValue = token;
                serialized.ApplyModifiedProperties();
            }
        }

        /// <summary>Removes the variant for <paramref name="when"/> of the value at <paramref name="property"/>'s path, from every object it is drawn for.</summary>
        public static void Remove(SerializedProperty property, StateCondition when)
        {
            foreach (var target in property.serializedObject.targetObjects)
            {
                var serialized = new SerializedObject(target);
                var variants = serialized.FindProperty(property.propertyPath)?.FindPropertyRelative("_variants");
                if (variants == null) continue;
                int index = Find(variants, when);
                if (index < 0) continue;
                variants.DeleteArrayElementAtIndex(index);
                serialized.ApplyModifiedProperties();
            }
        }

        /// <summary>The condition a variant's When holds.</summary>
        public static StateCondition Read(SerializedProperty when)
        {
            var states = when.FindPropertyRelative("States");
            var list = new UIState[states.arraySize];
            for (int i = 0; i < list.Length; i++)
                list[i] = states.GetArrayElementAtIndex(i).objectReferenceValue as UIState;
            return new StateCondition((InteractionStates)when.FindPropertyRelative("Interaction").intValue, list);
        }

        /// <summary>A condition's name: its interaction states, weakest first, then its states, joined.</summary>
        public static string Name(in StateCondition when)
        {
            var names = new List<string>();
            foreach (var state in s_order)
                if ((when.Interaction & state) != 0)
                    names.Add(ObjectNames.NicifyVariableName(state.ToString()));
            if (when.States != null)
                foreach (var state in when.States)
                    names.Add(state != null ? state.name : "(missing)");
            return names.Count > 0 ? string.Join(" + ", names) : "(no states)";
        }

        private static bool Same(SerializedProperty when, in StateCondition condition)
        {
            if (when.FindPropertyRelative("Interaction").intValue != (int)condition.Interaction) return false;
            var states = when.FindPropertyRelative("States");
            int count = condition.States?.Length ?? 0;
            if (states.arraySize != count) return false;
            for (int i = 0; i < count; i++)
                if (Array.IndexOf(condition.States, states.GetArrayElementAtIndex(i).objectReferenceValue as UIState) < 0)
                    return false;
            return true;
        }

        private static void SetWhen(SerializedProperty when, in StateCondition condition)
        {
            when.FindPropertyRelative("Interaction").intValue = (int)condition.Interaction;
            var states = when.FindPropertyRelative("States");
            int count = condition.States?.Length ?? 0;
            states.arraySize = count;
            for (int i = 0; i < count; i++)
                states.GetArrayElementAtIndex(i).objectReferenceValue = condition.States[i];
        }
    }
}
