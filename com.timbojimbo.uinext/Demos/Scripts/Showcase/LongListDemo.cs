using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>Ten thousand rows in a <see cref="ListView"/>: only those near view are made, each filled as it comes into view.</summary>
    public sealed class LongListDemo : MonoBehaviour
    {
        [SerializeField] private ListView _list;
        [SerializeField] private Color _text = Color.black;
        [SerializeField] private Color[] _palette = { Color.gray };

        public void Setup(ListView list, Color text, Color[] palette)
        {
            _list = list;
            _text = text;
            _palette = palette;
        }

        private void OnEnable()
        {
            _list.CreateCell = rows =>
            {
                var row = rows.Child("Row").Row(12f).Pad(16f, 10f).Align(AlignX.Left, AlignY.Center);
                row.Child("Dot").Sized(Sizing.Fixed(32f), Sizing.Fixed(32f)).Box(Color.gray, 16f);
                row.Child("Title").Label("", 16f, _text);
                return row;
            };
            _list.BindCell = (cell, index) =>
            {
                cell.transform.GetChild(0).GetComponent<Box>().Fill.Value = _palette[index % _palette.Length];
                cell.transform.GetChild(1).GetComponent<Label>().Text.Value = "Row " + (index + 1).ToString("N0");
            };
            _list.SetCount(10000);
        }
    }
}
