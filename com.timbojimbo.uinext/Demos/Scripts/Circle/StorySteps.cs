using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// The story viewer's middle, one focus stop of its own (Focusable Always): left and right step back and on through the
    /// stories, as taps on its sides do (back from the first, the focus ring shakes no), while up and down move focus on.
    /// </summary>
    public sealed class StorySteps : MonoBehaviour, IFocusMoveHandler
    {
        [SerializeField] private StoryViewer _viewer;

        public void Setup(StoryViewer viewer) => _viewer = viewer;

        public FocusMoveResult OnFocusMove(Vector2 direction)
        {
            if (direction.x == 0f) return FocusMoveResult.Ignored;
            return _viewer.Step(direction.x > 0f ? 1 : -1) ? FocusMoveResult.Handled : FocusMoveResult.Blocked;
        }
    }
}
