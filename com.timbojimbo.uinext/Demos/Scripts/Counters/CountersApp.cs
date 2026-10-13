using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Counters
{
    /// <summary>
    /// Labels whose text changes every frame, for profiling what a change of text costs: a column of rows, each a name and
    /// a value that changes every frame (a count, a clock and a price, in turn). Every value is made as it starts, so
    /// changing them allocates nothing of its own: what the Profiler shows allocating is the text's. Where UI Next Text has
    /// its replay of plain one-line text (the experimental AtgReplay), Replay turns it on and off.
    /// </summary>
    public sealed class CountersApp : MonoBehaviour
    {
        private const int Values = 1000;
        private static readonly string[] Kinds = { "Count", "Clock", "Price" };
        private static readonly Color Ink = new(0.07f, 0.07f, 0.09f, 1f);
        private static readonly Color Secondary = new(0.43f, 0.43f, 0.47f, 1f);

        [Tooltip("How many labels change every frame. Set before entering Play mode.")]
        [SerializeField, Range(1, 60)] private int _rows = 12;

        [Tooltip("The changing labels' size in points. Set before entering Play mode.")]
        [SerializeField] private float _fontSize = 17f;

        [Tooltip("Bold changing labels. Set before entering Play mode.")]
        [SerializeField] private bool _bold;

        [Tooltip("Whether the values change; off, nothing changes, a frame to compare with.")]
        [SerializeField] private bool _changing = true;

        [Tooltip("UI Next Text's replay of plain one-line text (AtgReplay), where the package has it.")]
        [SerializeField] private bool _replay = true;

        // UI Next Text's replay switch, when the package has one.
        private static readonly FieldInfo s_replay =
            Type.GetType("TimboJimbo.UINext.Text.Bridge.AtgReplay, Unity.TextCore.FontEngine.Tools")?.GetField("Enabled", BindingFlags.Public | BindingFlags.Static);

        private static string[][] s_values;
        private readonly List<Label> _labels = new();
        private bool? _replaySet;
        private int _frame;

        private void Start()
        {
            Application.targetFrameRate = 120;
            MakeValues();
            var app = GetComponent<UIRoot>().Node.Column(10f).Pad(new Insets(20f, 20f, 16f, 16f));
            app.Box(Color.white);
            app.Child("Title").Label("Counters", 22f, Ink, bold: true);
            for (int i = 0; i < _rows; i++)
            {
                var row = app.Child("Row").Row(12f).Align(AlignX.Left, AlignY.Center);
                row.Width.Value = Sizing.Grow();
                row.Child("Name").Label(Kinds[i % Kinds.Length], 15f, Secondary).Node.Width.Value = Sizing.Grow();
                _labels.Add(row.Child("Value").Label(ValueOf(i, 0), _fontSize, Ink, _bold));
            }
        }

        private void Update()
        {
            if (s_replay != null && _replaySet != _replay)
            {
                _replaySet = _replay;
                s_replay.SetValue(null, _replay);
            }
            if (!_changing) return;
            _frame++;
            for (int i = 0; i < _labels.Count; i++)
                _labels[i].Text.Value = ValueOf(i, _frame);
        }

        // The replay is left on, as it was, for the editor's own text.
        private void OnDisable()
        {
            if (s_replay != null && _replaySet == false)
                s_replay.SetValue(null, true);
            _replaySet = null;
        }

        // Each row's value at a frame: rows step through their values at different rates, so each changes every frame.
        private static string ValueOf(int row, int frame) => s_values[row % Kinds.Length][(frame * (row + 1) + row * 37) % Values];

        private static void MakeValues()
        {
            if (s_values != null) return;
            var culture = CultureInfo.InvariantCulture;
            var counts = new string[Values];
            var clocks = new string[Values];
            var prices = new string[Values];
            for (int i = 0; i < Values; i++)
            {
                counts[i] = (i * 7).ToString(culture);
                clocks[i] = string.Format(culture, "{0:00}:{1:00}.{2:00}", i / 60, i % 60, i * 17 % 100);
                prices[i] = string.Format(culture, "${0:0.00}", i * 1.37f);
            }
            s_values = new[] { counts, clocks, prices };
        }
    }
}
