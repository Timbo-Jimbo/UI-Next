using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Where focus goes, worked out on rects in a root's points (x right, y down): the one a direction leads to, as the
    /// W3C's spatial navigation and tvOS's focus engine pick it, and reading order, for Tab.
    /// </summary>
    internal static class FocusNavigation
    {
        // How much a step across counts against a candidate, next to a step the way it goes.
        private const float AcrossWeight = 2f;

        // How close two candidates' scores are to count as equally near: closer than anyone could see.
        private const float TieTolerance = 1f;

        private static readonly List<int> s_tied = new();

        /// <summary>
        /// The index in <paramref name="rects"/> of the one to move to from <paramref name="from"/> going
        /// <paramref name="direction"/> (a step along one axis, y down), or -1 for none. A candidate lies that way: its
        /// centre past this one's, and its far edge past this one's far edge. Those overlapping this one across the way it
        /// goes (in its beam) come first; among them, or among all when none do, the nearest wins, by the gap the way it
        /// goes plus twice the gap across. Several equally near (to within a point: a row of tabs above a wide row) are told
        /// apart by <paramref name="rank"/>, lowest first, then by reading order, the first of them winning.
        /// </summary>
        public static int Pick(Rect from, Vector2 direction, IReadOnlyList<Rect> rects, Func<int, int> rank, bool rightToLeft)
        {
            // How near the nearest is, those in the beam first.
            bool found = false, inBeam = false;
            float best = float.PositiveInfinity;
            for (int i = 0; i < rects.Count; i++)
            {
                if (!Score(from, rects[i], direction, out bool beam, out float score)) continue;
                if (beam && !inBeam)
                {
                    inBeam = true;
                    best = score;
                }
                else if (beam == inBeam)
                {
                    best = Mathf.Min(best, score);
                }
                found = true;
            }
            if (!found) return -1;

            // Every one as near as that.
            s_tied.Clear();
            for (int i = 0; i < rects.Count; i++)
            {
                if (!Score(from, rects[i], direction, out bool beam, out float score)) continue;
                if (beam == inBeam && score <= best + TieTolerance)
                    s_tied.Add(i);
            }
            if (s_tied.Count > 1 && rank != null)
            {
                int top = int.MaxValue;
                foreach (int i in s_tied)
                    top = Mathf.Min(top, rank(i));
                int kept = 0;
                for (int k = 0; k < s_tied.Count; k++)
                {
                    if (rank(s_tied[k]) == top)
                        s_tied[kept++] = s_tied[k];
                }
                s_tied.RemoveRange(kept, s_tied.Count - kept);
            }
            return s_tied.Count == 1 ? s_tied[0] : FirstInReadingOrder(rects, s_tied, rightToLeft);
        }

        /// <summary>The index in <paramref name="rects"/> of the one whose centre is nearest <paramref name="from"/>'s, or -1 for none.</summary>
        public static int Nearest(Rect from, IReadOnlyList<Rect> rects)
        {
            int nearest = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < rects.Count; i++)
            {
                float distance = (rects[i].center - from.center).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = i;
            }
            return nearest;
        }

        // The first of `among` in reading order (ReadingOrder's first), without sorting: in the row of the one reaching
        // highest, the one furthest along the way rows read.
        private static int FirstInReadingOrder(IReadOnlyList<Rect> rects, List<int> among, bool rightToLeft)
        {
            int head = among[0];
            foreach (int i in among)
            {
                if (rects[i].yMin < rects[head].yMin)
                    head = i;
            }
            int first = head;
            foreach (int i in among)
            {
                float middle = rects[i].center.y;
                if (middle < rects[head].yMin || middle > rects[head].yMax) continue;
                if (rightToLeft ? rects[i].xMax > rects[first].xMax : rects[i].xMin < rects[first].xMin)
                    first = i;
            }
            return first;
        }

        // Whether `to` lies `direction` of `from`, and if so whether it is in the beam (overlapping `from` across the way
        // it goes) and how near it is: the gap the way it goes plus AcrossWeight times the gap across.
        private static bool Score(Rect from, Rect to, Vector2 direction, out bool inBeam, out float score)
        {
            inBeam = false;
            score = 0f;
            if (!Ahead(from, to, direction, out float gap, out float across)) return false;
            inBeam = across <= 0f;
            score = gap + AcrossWeight * across;
            return true;
        }

        // Whether `to` lies `direction` of `from`, and if so the gap between them that way, and the gap across it (0 when
        // they overlap across it).
        private static bool Ahead(Rect from, Rect to, Vector2 direction, out float gap, out float across)
        {
            gap = across = 0f;
            if (direction.x > 0f)
            {
                if (to.center.x <= from.center.x || to.xMax <= from.xMax) return false;
                gap = Mathf.Max(0f, to.xMin - from.xMax);
                across = Mathf.Max(0f, Mathf.Max(to.yMin - from.yMax, from.yMin - to.yMax));
                return true;
            }
            if (direction.x < 0f)
            {
                if (to.center.x >= from.center.x || to.xMin >= from.xMin) return false;
                gap = Mathf.Max(0f, from.xMin - to.xMax);
                across = Mathf.Max(0f, Mathf.Max(to.yMin - from.yMax, from.yMin - to.yMax));
                return true;
            }
            if (direction.y > 0f)
            {
                if (to.center.y <= from.center.y || to.yMax <= from.yMax) return false;
                gap = Mathf.Max(0f, to.yMin - from.yMax);
                across = Mathf.Max(0f, Mathf.Max(to.xMin - from.xMax, from.xMin - to.xMax));
                return true;
            }
            if (direction.y < 0f)
            {
                if (to.center.y >= from.center.y || to.yMin >= from.yMin) return false;
                gap = Mathf.Max(0f, from.yMin - to.yMax);
                across = Mathf.Max(0f, Mathf.Max(to.xMin - from.xMax, from.xMin - to.xMax));
                return true;
            }
            return false;
        }

        // ReadingOrder's working lists and the rects it is sorting, kept so that it makes nothing.
        private static readonly List<int> s_byTop = new();
        private static readonly List<int> s_row = new();
        private static readonly List<int> s_order = new();
        private static IReadOnlyList<Rect> s_sorting;
        private static readonly IComparer<int> s_higherFirst = new EdgeOrder(EdgeOrder.Top);
        private static readonly IComparer<int> s_leftFirst = new EdgeOrder(EdgeOrder.Left);
        private static readonly IComparer<int> s_rightFirst = new EdgeOrder(EdgeOrder.Right);

        /// <summary>
        /// The indices of <paramref name="rects"/> in reading order, as Tab goes: in rows from the top down, a rect joining
        /// the row above it when its middle lies within that row's first rect's height, and along each row the way rows
        /// read, left to right or right to left. The list is shared, and good until the next call.
        /// </summary>
        public static List<int> ReadingOrder(IReadOnlyList<Rect> rects, bool rightToLeft)
        {
            s_sorting = rects;
            s_byTop.Clear();
            for (int i = 0; i < rects.Count; i++) s_byTop.Add(i);
            s_byTop.Sort(s_higherFirst);

            s_order.Clear();
            s_row.Clear();
            Rect head = default;
            foreach (int i in s_byTop)
            {
                float middle = rects[i].center.y;
                if (s_row.Count > 0 && (middle < head.yMin || middle > head.yMax))
                    Flush(rightToLeft);
                if (s_row.Count == 0) head = rects[i];
                s_row.Add(i);
            }
            Flush(rightToLeft);
            s_sorting = null;
            return s_order;
        }

        private static void Flush(bool rightToLeft)
        {
            s_row.Sort(rightToLeft ? s_rightFirst : s_leftFirst);
            s_order.AddRange(s_row);
            s_row.Clear();
        }

        // Indices by their rect in s_sorting: the one reaching higher first, the one further left, or the one further right;
        // the earlier index first where they are level, so the order is the same however the sort goes.
        private sealed class EdgeOrder : IComparer<int>
        {
            public const int Top = 0, Left = 1, Right = 2;

            private readonly int _edge;

            public EdgeOrder(int edge) => _edge = edge;

            public int Compare(int a, int b)
            {
                Rect ra = s_sorting[a], rb = s_sorting[b];
                int order = _edge switch
                {
                    Top => ra.yMin.CompareTo(rb.yMin),
                    Left => ra.xMin.CompareTo(rb.xMin),
                    _ => rb.xMax.CompareTo(ra.xMax),
                };
                return order != 0 ? order : a.CompareTo(b);
            }
        }
    }
}
