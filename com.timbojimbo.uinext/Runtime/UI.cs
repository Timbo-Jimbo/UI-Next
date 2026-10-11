using System;
using System.Globalization;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>The entry points: making changes that animate, laying out now, and which way the UI reads.</summary>
    public static class UI
    {
        private static ReadingDirection? s_readingDirection;

        /// <summary>
        /// Which way the UI reads, as UIKit's userInterfaceLayoutDirection and SwiftUI's layoutDirection: Tab goes along a
        /// row this way, and of equally near places focus could go, the first this way wins. Unless set, the way the
        /// current UI culture reads (<see cref="CultureInfo.CurrentUICulture"/>); set it as the app's language changes (to
        /// the way its culture's TextInfo reads), or to try a layout.
        /// </summary>
        public static ReadingDirection ReadingDirection
        {
            get => s_readingDirection
                   ?? (CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? ReadingDirection.RightToLeft : ReadingDirection.LeftToRight);
            set => s_readingDirection = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_readingDirection = null;

        /// <summary>
        /// Makes the change <paramref name="update"/> makes and moves what it changes on <paramref name="animation"/>,
        /// as SwiftUI's withAnimation and UIKit's animate with layoutIfNeeded: what changed before goes where it goes at
        /// once; then the update runs; then everything it changed, and every rect its layout moves, springs from where
        /// it is drawn at the speed it has. A node it shows with the <see cref="UINode.MatchName"/> and id of one it hides
        /// flies from where that one was drawn. Called inside another change's update, it is part of that change. Outside
        /// play mode the change lands at once.
        /// </summary>
        public static UITransaction Animate(UIAnimation animation, Action update)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            if (UITransaction.Current != null)
            {
                update();
                return UITransaction.Current;
            }

            var transaction = new UITransaction(animation);
            if (!UIFrame.Live)
            {
                update();
                UIScheduler.Flush(Phase.Layout);
                transaction.Finish();
                RequestRepaint();
                return transaction;
            }

            UIScheduler.Flush(Phase.Layout);
            MatchedGeometry.Begin();
            UITransaction.Current = transaction;
            try
            {
                update();
                UIScheduler.Flush(Phase.Layout);
                MatchedGeometry.End(transaction);
            }
            finally
            {
                UITransaction.Current = null;
            }
            if (transaction.Moving == 0)
                transaction.Finish();
            UITransaction.FlushFinished();
            return transaction;
        }

        /// <summary>Makes the change <paramref name="update"/> makes on the default spring.</summary>
        public static UITransaction Animate(Action update) => Animate(UIAnimation.Default, update);

        /// <summary>Resolves values and lays out every tree that changed, now, rather than when the frame draws.</summary>
        public static void LayoutNow() => UIScheduler.Flush(Phase.Layout);

        /// <summary>Outside play mode, asks the editor to draw again (it only runs the frame when something asks).</summary>
        public static void RequestRepaint()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
#endif
        }
    }
}
