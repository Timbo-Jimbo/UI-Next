# Motion v4: matched geometry, the best of matching and v2

Branched from `overnight`, the base the matching branch and motion-v2 share, with motion-v2 merged in for velocity,
handing a drag on, presentations, the swipe component and the demos. Its matched geometry is rewritten to keep what
worked in each branch with as few rules as cover the demos, and no rules about what a node holds. This supersedes the
matched geometry sections of `MotionV2.md` and `MotionV2-Decisions.md`, and motion-v3's snapshots.

## A flight

- **Pairing, as both had it.** Inside one animated change, a node shown with the name and id of one the change hides
  takes over from it; one shown while a namesake stays flies out of it.
- **Two ends, as both had them.** The old end is a picture of the hidden node as it was last drawn, drawn again every
  frame so it stays sharp at any size. The new end is the shown node itself, drawn live, so whatever it animates on the
  way is seen and nothing pops as it lands.
- **Each end's own drawing is laid out at the flight's rect, as matching had it.** What a node draws relative to its
  own rect (its box, its border, its shadow, an image filling it) is laid out at the flight's rect, unscaled, as the
  node would draw at that size. A card's box grows with the flight keeping its corners, and its shadow falls past the
  flight's edges, uncut.
- **What is inside each end is scaled evenly along the axis the two ends are most alike in.** Their widths for a row
  growing into a page or a card into a story, their heights for a line of text growing into a longer one, so content is
  scaled as little as it can be and text never swells with a width it doesn't follow. Along the other axis the node's
  `MatchAnchor` lines it up, its centre by default, as SwiftUI's matchedGeometryEffect takes an anchor. A chat row and
  its conversation, and an App Store card and its story, hang from their tops.
- **What is inside is cut to the flight's window.** The window is the flight's rect with corners springing from one
  end's `ClipRadii` to the other's, as v2 had it, so a story growing out of a round avatar grows out of a circle. Each
  end's own drawing isn't cut.
- **The two ends are added together, as both had them.** Each end is drawn into an image of its own every frame, and
  the two are summed in one quad as one cross-fades into the other, as the web's view transitions blend an image pair
  (plus-lighter). Where both are opaque nothing behind shows through, and because each end's own drawing fills the
  flight's rect, a row's background grows into the page instead of the page fading on over what is behind it.
- **The flight is a difference, as both had it.** What springs is how far the flight is from where its destination is
  drawn, closing to nothing, so a scroll or a page sliding in carries it. It sets off at the speed the source was drawn
  moving, turns round from where it is drawn if taken back, and lands with the change it set off in.
- **Named parts fly on their own.** A named node inside an end with a flight of its own is left out of that end's
  images and drawn over it, on its own path.

## What came out

- v2's rule that text fits by height, which keyed on the node having a `Label`: the axis rule covers text without
  knowing what a node holds.
- v2's shadows drawn under the pair: a shadow is part of an end's own drawing, which isn't cut.
- v2's pair cut to the flight's shape, and its parts flying inside their whole's shape: the window cuts what is inside
  each end and nothing more.
- matching's rounding of an end's box to the flight's corners, and its guess at a node's corners from its first box:
  corners come from `ClipRadii` alone.
- motion-v3's images captured once, its masks, its spill frames and its coverage bands.

## Limitations

- **Corners.** An end's own box keeps its own corners while the window's corners spring, so where they differ, a sliver
  at each corner cross-fades rather than morphing.
- **Content past its node.** What is inside an end is cut to the window, so a badge hanging off a card is cut while it
  flies.
- **Parts.** A named part isn't clipped to its whole, so it can fly outside it while both are under way.
- **Groups drawn flat.** Blur, masks and group opacity inside an end are drawn flat while it flies.
- **One axis for both ends.** Both ends are fitted along the same axis, so an end whose content wants the other one is
  cut along it.
- **Not carried over from matching.** The editor's sectioned inspectors, the Focus page, scroll views keeping clear of
  what floats over them, and the CompositeGroup rename stay on the matching branch, as they did for v2.
