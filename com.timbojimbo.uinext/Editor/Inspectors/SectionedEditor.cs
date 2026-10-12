using System;
using System.Collections.Generic;
using TimboJimbo.UINext;
using TimboJimboEditor.Core;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// An inspector in sections, each the fields it names in the order given, under Core's foldout header so the
    /// packages' inspectors look alike; a section with no header is always shown. Each remembers whether it is open,
    /// and a closed one says how many of its fields are set: not as a new component has them (a literal changed, a
    /// token linked or a variant added). Fields no section names (added since) are drawn after, under Other. An editor
    /// can draw a field its own way (<see cref="DrawField"/>), and leave out one that does nothing as the others are set
    /// (<see cref="Shows"/>), which is then not counted either.
    /// </summary>
    internal abstract class SectionedEditor : Editor
    {
        /// <summary>A section: its header (null for none, always shown), whether it starts open, and its fields.</summary>
        protected readonly struct Section
        {
            public readonly string Header;
            public readonly bool Open;
            public readonly string[] Fields;

            public Section(string header, bool open, params string[] fields)
            {
                Header = header;
                Open = open;
                Fields = fields;
            }
        }

        private static readonly HashSet<string> s_drawn = new();
        private static readonly Dictionary<string, FoldoutGUI.SessionBool> s_states = new();
        private static readonly Dictionary<Type, SerializedObject> s_defaults = new();
        private static GameObject s_defaultsHost;

        protected abstract Section[] Sections { get; }

        // What it shows follows the states previewed, and recorded.
        protected virtual void OnEnable() => UIStatePreview.Changed += Repaint;

        protected virtual void OnDisable() => UIStatePreview.Changed -= Repaint;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            s_drawn.Clear();
            var sections = Sections;
            foreach (var section in sections)
            {
                bool any = false;
                foreach (var field in section.Fields)
                    any |= serializedObject.FindProperty(field) != null;
                if (!any) continue;
                if (section.Header == null)
                {
                    DrawFields(section.Fields);
                    continue;
                }

                var state = StateOf(section);
                bool open = state.Get(serializedObject);
                int set = open ? 0 : CountSet(section.Fields);
                bool now = FoldoutGUI.Draw(open, () => FoldoutGUI.Title(section.Header, set > 0 ? set + " set" : null),
                    onGroupToggle: all => SetAll(sections, all));
                if (now != open)
                    state.Set(serializedObject, now);
                if (now)
                {
                    FoldoutGUI.BeginContent();
                    DrawFields(section.Fields);
                    FoldoutGUI.EndContent();
                }
                else
                {
                    foreach (var field in section.Fields)
                        s_drawn.Add(field);
                }
            }
            DrawOther();
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Draws one field: its property drawer, unless an editor draws it some other way. While a state is recorded, a
        /// field that is not a value (which only values can be) is greyed out.
        /// </summary>
        protected virtual void DrawField(SerializedProperty property)
        {
            using (new EditorGUI.DisabledScope(UIStatePreview.RecordTarget != null && !IsValue(property)))
                EditorGUILayout.PropertyField(property, true);
        }

        /// <summary>Whether a field is a value (a <see cref="UIValue{T}"/>), which can be recorded into a state.</summary>
        protected static bool IsValue(SerializedProperty property) =>
            property.FindPropertyRelative("_value") != null && property.FindPropertyRelative("_variants") != null;

        /// <summary>Whether a field is shown: all of them, unless an editor leaves out one that does nothing as the others are set.</summary>
        protected virtual bool Shows(SerializedProperty property) => true;

        /// <summary>
        /// Whether a value can be other than its literal: linked to a token, with variants, or not the same on everything
        /// selected. What it decides is shown whatever its literal is.
        /// </summary>
        protected bool Varies(string field)
        {
            var value = serializedObject.FindProperty(field);
            if (value == null) return false;
            var token = value.FindPropertyRelative("_token");
            var variants = value.FindPropertyRelative("_variants");
            return value.hasMultipleDifferentValues || (token != null && token.objectReferenceValue != null)
                   || (variants != null && variants.arraySize > 0);
        }

        /// <summary>A value's literal.</summary>
        protected SerializedProperty Literal(string field) => serializedObject.FindProperty(field)?.FindPropertyRelative("_value");

        /// <summary>Whether a switch is on, or can be (<see cref="Varies"/>).</summary>
        protected bool On(string field) => Varies(field) || Literal(field) is { boolValue: true };

        private void DrawFields(string[] fields)
        {
            foreach (var field in fields)
            {
                var property = serializedObject.FindProperty(field);
                if (property == null) continue;
                s_drawn.Add(field);
                if (Shows(property))
                    DrawField(property);
            }
        }

        // Fields no section names, under Other; with no sections at all, just them.
        private void DrawOther()
        {
            var iterator = serializedObject.GetIterator();
            if (Sections.Length == 0)
            {
                for (bool enter = true; iterator.NextVisible(enter); enter = false)
                    if (iterator.name != "m_Script")
                        DrawField(iterator);
                return;
            }
            bool header = false;
            for (bool enter = true; iterator.NextVisible(enter); enter = false)
            {
                if (iterator.name == "m_Script" || s_drawn.Contains(iterator.name)) continue;
                if (!header)
                {
                    header = true;
                    var state = StateOf(new Section("Other", true));
                    bool open = state.Get(serializedObject);
                    bool now = FoldoutGUI.Draw(open, () => FoldoutGUI.Title("Other"));
                    if (now != open)
                        state.Set(serializedObject, now);
                    if (!now) return;
                    FoldoutGUI.BeginContent();
                }
                DrawField(iterator);
            }
            if (header)
                FoldoutGUI.EndContent();
        }

        // Alt-clicked, a header opens or closes them all.
        private void SetAll(Section[] sections, bool open)
        {
            foreach (var section in sections)
                if (section.Header != null)
                    StateOf(section).Set(serializedObject, open);
        }

        private FoldoutGUI.SessionBool StateOf(in Section section)
        {
            string key = "UINext." + GetType().Name + "." + section.Header;
            if (!s_states.TryGetValue(key, out var state))
                s_states[key] = state = new FoldoutGUI.SessionBool(key, section.Open);
            return state;
        }

        // How many of the fields shown are not as a new component has them.
        private int CountSet(string[] fields)
        {
            var defaults = Defaults();
            if (defaults == null) return 0;
            int set = 0;
            foreach (var field in fields)
            {
                var property = serializedObject.FindProperty(field);
                var standard = defaults.FindProperty(field);
                if (property != null && standard != null && Shows(property) && !SerializedProperty.DataEquals(property, standard))
                    set++;
            }
            return set;
        }

        // A new component of the kind inspected, on a hidden object that is never active or saved: what set is told from.
        private SerializedObject Defaults()
        {
            var type = target.GetType();
            if (s_defaults.TryGetValue(type, out var defaults) && defaults.targetObject != null) return defaults;
            if (s_defaultsHost == null)
            {
                s_defaultsHost = EditorUtility.CreateGameObjectWithHideFlags("UI Next defaults", HideFlags.HideAndDontSave, typeof(RectTransform));
                s_defaultsHost.SetActive(false);
                AssemblyReloadEvents.beforeAssemblyReload += DestroyDefaults;
            }
            var component = s_defaultsHost.GetComponent(type);
            if (component == null)
                component = s_defaultsHost.AddComponent(type);
            if (component == null) return null;
            defaults = new SerializedObject(component);
            s_defaults[type] = defaults;
            return defaults;
        }

        private static void DestroyDefaults()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= DestroyDefaults;
            s_defaults.Clear();
            if (s_defaultsHost != null)
                DestroyImmediate(s_defaultsHost);
        }
    }

    [CustomEditor(typeof(UINode)), CanEditMultipleObjects]
    internal sealed class UINodeEditor : SectionedEditor
    {
        private const float SizeGridWidth = 70f;
        private const float SizeGridHeight = 58f;
        private const float PointGridWidth = 44f;
        private const float PointGridHeight = 38f;
        private const float GridGap = 6f;

        // The size grid's columns (its width) and rows (its height): small to all of it.
        private static readonly SizingMode[] GridModes = { SizingMode.Fit, SizingMode.Fixed, SizingMode.Grow };
        private static readonly GUIContent AlignLabel = new("Align", "Where its children sit in it.");
        private static GUIContent[] s_sizeTips;

        private static readonly Section[] s_sections =
        {
            // Always shown: its size, beside the grid of the common ones; where it floats; and how it is moved, scaled
            // and turned as it is drawn.
            new(null, true, "_width", "_height", "_aspectRatio", "_floating", "_offset", "_scale", "_rotation"),
            new("Children", true, "_direction", "_alignX", "_alignY", "_gap", "_padding", "_wrap"),
            new("Look", false, "_display", "_opacity", "_zIndex", "_topLayer", "_clips", "_clipRadii", "_compositeGroup", "_blur", "_mask",
                "_saturation", "_brightness"),
            new("Safe area", false, "_ignoresSafeArea", "_avoidsKeyboard"),
            new("Input and focus", false, "_hitTestable", "_focusable", "_prefersFocus", "_focusRing", "_focusScope", "_focusEntry"),
            new("Matching", false, "_matchName", "_matchAnchor", "_matchId"),
        };

        protected override Section[] Sections => s_sections;

        public override void OnInspectorGUI()
        {
            RecordingStrip();
            base.OnInspectorGUI();
        }

        // While recording, a red strip at the top saying what edits go to, with Stop.
        private static void RecordingStrip()
        {
            if (!UIStatePreview.Recording) return;
            float line = EditorGUIUtility.singleLineHeight;
            var rect = EditorGUILayout.GetControlRect(false, line + 6f);
            EditorGUI.DrawRect(rect, StatesDropdown.Recording);
            string text = UIStatePreview.Target is { } target
                ? "● Recording " + target.Name + ": edits go to its variants"
                : "● Recording: turn a state on in UI States to record into it";
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            GUI.Label(new Rect(rect.x + 6f, rect.y + 3f, rect.width - 70f, line), text, style);
            if (GUI.Button(new Rect(rect.xMax - 58f, rect.y + 3f, 54f, line), "Stop", EditorStyles.miniButton))
                UIStatePreview.Recording = false;
            EditorGUILayout.Space(2f);
        }

        protected override bool Shows(SerializedProperty property) => property.name switch
        {
            // With no name it is not matched, so nothing lines up in a flight.
            "_matchAnchor" => Varies("_matchName") || !string.IsNullOrEmpty(Literal("_matchName").stringValue),
            // Where focus coming in lands is a section's.
            "_focusEntry" => Varies("_focusScope") || Literal("_focusScope").intValue == (int)FocusScope.Section,
            // Never focused, it has no ring to draw.
            "_focusRing" => Varies("_focusable") || Literal("_focusable").intValue != (int)Focusability.Never,
            _ => true,
        };

        protected override void DrawField(SerializedProperty property)
        {
            switch (property.name)
            {
                case "_width":
                    DrawSize(property);
                    break;
                // Drawn with the width, and with Align X.
                case "_height":
                case "_aspectRatio":
                case "_alignY":
                    break;
                case "_alignX":
                    DrawAlign(property);
                    break;
                case "_blur":
                case "_mask":
                    DrawEffect(property);
                    break;
                default:
                    base.DrawField(property);
                    break;
            }
        }

        // Width, height and aspect ratio beside a 3×3 grid of the common sizes, where RectTransform's anchor presets sit.
        private void DrawSize(SerializedProperty width)
        {
            var height = serializedObject.FindProperty("_height");
            var aspect = serializedObject.FindProperty("_aspectRatio");
            float space = EditorGUIUtility.standardVerticalSpacing;
            float rows = EditorGUI.GetPropertyHeight(width, true) + EditorGUI.GetPropertyHeight(height, true)
                         + EditorGUI.GetPropertyHeight(aspect, true) + 2f * space;
            var block = EditorGUILayout.GetControlRect(false, Mathf.Max(SizeGridHeight, rows));
            // The grid sets the node's own sizes, so not while a state is recorded or a variant gives either.
            var node = (UINode)target;
            using (new EditorGUI.DisabledScope(UIStatePreview.RecordTarget != null || Given(node.Width) || Given(node.Height)))
                SizeGrid(new Rect(block.x, block.y, SizeGridWidth, SizeGridHeight), width, height);

            // The rows' fields line up with every other field, their labels taking what the grid leaves.
            float labels = EditorGUIUtility.labelWidth;
            float shift = SizeGridWidth + GridGap;
            EditorGUIUtility.labelWidth = Mathf.Max(48f, labels - shift);
            var row = new Rect(block.x + shift, block.y, block.width - shift, 0f);
            foreach (var (property, name) in new[] { (width, "Width"), (height, "Height"), (aspect, "Aspect") })
            {
                row.height = EditorGUI.GetPropertyHeight(property, true);
                EditorGUI.PropertyField(row, property, new GUIContent(name, property.tooltip), true);
                row.y += row.height + space;
            }
            EditorGUIUtility.labelWidth = labels;
        }

        // A column for each way of sizing its width and a row for its height, the way it is sized lit (none with a
        // percentage, or a mixed selection): a click sets both.
        private void SizeGrid(Rect rect, SerializedProperty width, SerializedProperty height)
        {
            var across = width.FindPropertyRelative("_value");
            var down = height.FindPropertyRelative("_value");
            int column = GridIndex(across.FindPropertyRelative("Mode"));
            int row = GridIndex(down.FindPropertyRelative("Mode"));
            int chosen = column >= 0 && row >= 0 ? row * 3 + column : -1;
            int picked = LitButtons.Grid(rect, chosen, SizeTips(), DrawSizeCell);
            if (picked == chosen) return;
            var size = ((UINode)target).Size;
            SizingDrawer.SetMode(across, GridModes[picked % 3], size.x);
            SizingDrawer.SetMode(down, GridModes[picked / 3], size.y);
        }

        // Whether a variant gives a value now (its states on, or previewed).
        private static bool Given(IVariantSource value) => value.Winner >= 0;

        private static int GridIndex(SerializedProperty mode) =>
            mode.hasMultipleDifferentValues ? -1 : Array.IndexOf(GridModes, (SizingMode)mode.intValue);

        private static GUIContent[] SizeTips()
        {
            if (s_sizeTips != null) return s_sizeTips;
            string[] ways = { "fits its content", "is a fixed size", "fills its parent" };
            s_sizeTips = new GUIContent[9];
            for (int i = 0; i < 9; i++)
                s_sizeTips[i] = new GUIContent("", $"Width {ways[i % 3]}, height {ways[i / 3]}");
            return s_sizeTips;
        }

        // A box in its parent as the cell's sizes lay it out: small fitting its content, middling at a fixed size, all of
        // it growing.
        private static void DrawSizeCell(Rect cell, int index, bool on)
        {
            var parent = new Rect(cell.x + 3f, cell.y + 3f, cell.width - 6f, cell.height - 6f);
            var edge = LitButtons.Ink(on, 0.45f);
            EditorGUI.DrawRect(new Rect(parent.x, parent.y, parent.width, 1f), edge);
            EditorGUI.DrawRect(new Rect(parent.x, parent.yMax - 1f, parent.width, 1f), edge);
            EditorGUI.DrawRect(new Rect(parent.x, parent.y + 1f, 1f, parent.height - 2f), edge);
            EditorGUI.DrawRect(new Rect(parent.xMax - 1f, parent.y + 1f, 1f, parent.height - 2f), edge);
            var inside = new Rect(parent.x + 2f, parent.y + 2f, parent.width - 4f, parent.height - 4f);
            float width = Extent(inside.width, index % 3);
            float height = Extent(inside.height, index / 3);
            EditorGUI.DrawRect(new Rect(Mathf.Round(inside.center.x - width * 0.5f), Mathf.Round(inside.center.y - height * 0.5f), width, height),
                LitButtons.Ink(on, 0.9f));
        }

        private static float Extent(float all, int column) => Mathf.Max(2f, Mathf.Round(all * (column == 0 ? 0.3f : column == 1 ? 0.62f : 1f)));

        // Align X and Y as one 3×3 grid, as Figma's auto layout picks where children sit; as their two rows while either
        // can be other than its literal, so that shows, and while a state is recorded, which the rows record into.
        private void DrawAlign(SerializedProperty alignX)
        {
            var alignY = serializedObject.FindProperty("_alignY");
            if (alignY == null || Varies("_alignX") || Varies("_alignY") || UIStatePreview.RecordTarget != null)
            {
                base.DrawField(alignX);
                if (alignY != null)
                    base.DrawField(alignY);
                return;
            }
            var x = alignX.FindPropertyRelative("_value");
            var y = alignY.FindPropertyRelative("_value");
            var rect = EditorGUILayout.GetControlRect(false, PointGridHeight);
            var field = EditorGUI.PrefixLabel(rect, AlignLabel);
            VariantMenu(new Rect(rect.x, rect.y, field.x - rect.x, rect.height), alignX, alignY);
            int chosen = y.intValue * 3 + x.intValue;
            int picked = LitButtons.Grid(new Rect(field.x, field.y, PointGridWidth, PointGridHeight), chosen, LitButtons.PointTips, LitButtons.Dot);
            if (picked == chosen) return;
            x.intValue = picked % 3;
            y.intValue = picked / 3;
        }

        // Right-clicked, a label drawn for more than one value offers a variant of each, as a value's own label does.
        private static void VariantMenu(Rect label, params SerializedProperty[] values)
        {
            var e = Event.current;
            if (e.type != EventType.ContextClick || !label.Contains(e.mousePosition)) return;
            var menu = new GenericMenu();
            foreach (var value in values)
            {
                var copy = value.Copy();
                menu.AddItem(new GUIContent("Add Variant to " + value.displayName), false, () => UIValueDrawer.AddVariant(copy));
            }
            menu.ShowAsContext();
            e.Use();
        }

        // Blur and masks apply only to a composite group: greyed out without one, saying so when one is set anyway.
        private void DrawEffect(SerializedProperty property)
        {
            bool blur = property.name == "_blur";
            bool composite = true, set = false;
            foreach (var item in targets)
            {
                var node = (UINode)item;
                composite &= node.CompositeGroup.Peek;
                set |= blur ? node.Blur.Peek > 0.01f : node.Mask.Peek != null;
            }
            if (!composite && set)
                EditorGUILayout.HelpBox((blur ? "Blur" : "A mask") + " applies only to a composite group.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!composite))
                base.DrawField(property);
        }
    }

    [CustomEditor(typeof(Box)), CanEditMultipleObjects]
    internal sealed class BoxEditor : SectionedEditor
    {
        private static readonly Section[] s_sections =
        {
            new("Fill", true, "_fill", "_cornerRadii"),
            new("Gradient", true, "_gradient", "_gradientTo", "_gradientAngle"),
            new("Border", true, "_borderWidth", "_borderColor", "_borderGradient", "_borderColorTo", "_borderGradientAngle"),
            new("Shadow", true, "_shadowColor", "_shadowBlur", "_shadowOffset", "_shadowSpread", "_shadowGradient", "_shadowColorTo",
                "_shadowGradientAngle"),
        };

        protected override Section[] Sections => s_sections;

        // A gradient's far colour and angle, only while it is a gradient.
        protected override bool Shows(SerializedProperty property) => property.name switch
        {
            "_gradientTo" or "_gradientAngle" => On("_gradient"),
            "_borderColorTo" or "_borderGradientAngle" => On("_borderGradient"),
            "_shadowColorTo" or "_shadowGradientAngle" => On("_shadowGradient"),
            _ => true,
        };
    }

    [CustomEditor(typeof(Label)), CanEditMultipleObjects]
    internal sealed class LabelEditor : SectionedEditor
    {
        private static readonly Section[] s_sections =
        {
            new("Text", true, "_text", "_font", "_fontSize", "_color", "_bold", "_italic"),
            new("Lines", true, "_alignment", "_wrap", "_maxLines", "_overflow", "_characterSpacing"),
            new("Outline and shadow", true, "_outlineWidth", "_outlineColor", "_shadowColor", "_shadowOffset"),
            new("Markup", true, "_markdown", "_theme", "_onLinkClicked"),
        };

        protected override Section[] Sections => s_sections;
    }

    [CustomEditor(typeof(ImageView)), CanEditMultipleObjects]
    internal sealed class ImageViewEditor : SectionedEditor
    {
        private static readonly Section[] s_sections =
        {
            new("Image", true, "_sprite", "_texture", "_tint", "_fit", "_cornerRadii"),
        };

        protected override Section[] Sections => s_sections;
    }

    [CustomEditor(typeof(ScrollView)), CanEditMultipleObjects]
    internal sealed class ScrollViewEditor : SectionedEditor
    {
        private static readonly Section[] s_sections =
        {
            new("Scrolling", true, "_axis", "_bounces", "_atEnd", "_snap"),
            new("Indicators", true, "_showsIndicators", "_indicatorColor"),
            new("Clear of", true, "_clearOfTop", "_clearOfBottom", "_clearOfLeft", "_clearOfRight", "_clearMargin"),
        };

        protected override Section[] Sections => s_sections;
    }

    /// <summary>
    /// A UI Next component with no sections: its fields as Unity draws them, following the states previewed, and greyed
    /// out while a state is recorded where they are not values.
    /// </summary>
    internal abstract class PlainEditor : SectionedEditor
    {
        protected override Section[] Sections => Array.Empty<Section>();
    }

    [CustomEditor(typeof(Pressable)), CanEditMultipleObjects]
    internal sealed class PressableEditor : PlainEditor { }

    [CustomEditor(typeof(Draggable)), CanEditMultipleObjects]
    internal sealed class DraggableEditor : PlainEditor { }

    [CustomEditor(typeof(Pinchable)), CanEditMultipleObjects]
    internal sealed class PinchableEditor : PlainEditor { }

    [CustomEditor(typeof(DisplayTransition)), CanEditMultipleObjects]
    internal sealed class DisplayTransitionEditor : PlainEditor { }

    [CustomEditor(typeof(Presentation)), CanEditMultipleObjects]
    internal sealed class PresentationEditor : PlainEditor { }
}
