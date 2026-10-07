using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;

namespace TimboJimboTests.UINext
{
    public sealed class ListTests : UITestScene
    {
        private ListView List(float height, out ScrollView scroll)
        {
            var node = Root.Node.Child("List").Sized(Sizing.Fixed(300f), Sizing.Fixed(height));
            scroll = node.Scroll();
            var list = node.Add<ListView>();
            list.Overscan = 0.5f;
            return list;
        }

        [Test]
        public void ItMakesOnlyTheRowsNearViewAndPlacesThemWhereTheyGo()
        {
            var list = List(400f, out var scroll);
            list.EstimatedRowHeight = 40f;
            list.CreateCell = rows => rows.Child("Row").Sized(Sizing.Percent(1f), Sizing.Fixed(40f));
            int bound = 0;
            list.BindCell = (cell, index) => bound++;
            list.SetCount(10000);
            Frame();
            Frame();
            // 400 points in view and 200 past it: fifteen rows and the one the edge falls in.
            Assert.LessOrEqual(list.CellCount, 16);
            Assert.AreEqual(400000f, scroll.ContentSize.y, 1f, "as tall as all its rows");

            list.ScrollTo(5000, animated: false);
            Frame();
            Frame();
            Assert.IsTrue(list.TryGetCell(5000, out var cell));
            Assert.AreEqual(200000f, cell.LaidOutRect.y, 0.5f);
            Assert.LessOrEqual(list.CellCount, 21, "rows behind it put away");
            Assert.Less(bound, 60, "cells are reused, not made for every row passed");
        }

        [Test]
        public void RowsAreAsTallAsTheirCellsLayOut()
        {
            var list = List(600f, out _);
            list.EstimatedRowHeight = 50f;
            list.CreateCell = rows => rows.Child("Row").Sized(Sizing.Percent(1f), Sizing.Fixed(10f));
            // Even rows 30 tall, odd ones 70.
            list.BindCell = (cell, index) => cell.Height.Value = Sizing.Fixed(index % 2 == 0 ? 30f : 70f);
            list.Gap = 4f;
            list.SetCount(40);
            // Made at their estimated places, then placed by what they measured.
            Frame();
            Frame();
            Frame();
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(list.TryGetCell(i, out var cell), "row " + i);
                Assert.AreEqual(i / 2 * 108f + (i % 2 == 1 ? 34f : 0f), cell.LaidOutRect.y, 0.5f, "row " + i);
            }
        }
    }
}
