# Motion v2: decisions

One line per decision the brief left open, with the precedent followed.

## Matched geometry

- A match name and id are strings, `UIValue<string>` as every property is: SwiftUI's ids are any hashable value; a string is what an inspector can hold.
- Matching is done at the end of each animated change, against the named nodes shown as it began, by where they are going (in a tree, everything above them visible, in no presentation going away): the web's view transitions capture the old state before the update and the new one after.
- Display Hidden hides a node for matching as None does, so a list keeps the space of a row that flew away: SwiftUI's matched views are usually removed, and Hidden is this stack's way to keep their place.
- One shown node takes over from the first hidden namesake found, and failing that flies out of the first staying one: SwiftUI pairs one source with one destination per id.
- A pair's corners are each end's `ClipRadii`, whether or not it clips: a UIKit layer's `cornerRadius` is both its shape and its clip.
- The picture is the source's own primitives as last drawn, kept with their nodes' places, rather than a bitmap, so it stays sharp as it grows: the stack's drawing is already a display list, as Core Animation's snapshot views keep what they render.
- What is inside an end is fitted by scaling it evenly to the flight's width from its top, keeping its proportions: the web's default `::view-transition-old/new` (width 100%, height auto, at the top). Text is fitted to the flight's height instead, so its glyphs never swell with the width (Tim, 10 October, as the brief asks).
- An end's own background (what its root draws relative to its rect: its box, border and shadow, an image filling it) is laid out at the flight's size, unscaled, as the node would draw at that size, so its corners, borders and shadows keep their points, and the flight's shape, going from one end's corners to the other's, gives the outline: SwiftUI lays a matched view out at the frame in between (Tim, 10 October; scaled with what is inside it, a story ring's rounded box grew into a capsule).
- An end's own shadow is drawn under the pair, outside the flight's shape, laid out with its background and faded with its end, rather than cut away inside its image: SwiftUI draws a matched view's shadow outside its frame, faded with the view (Tim, 10 October; cut away, the Log out alert's shadow appeared only as it landed).
- The two ends are each drawn to an image of their own and summed in one quad (`PrimitiveType.Pair`), so nothing behind shows through: the web's `mix-blend-mode: plus-lighter` inside an isolated image pair.
- The live end leaves out the destination's own transition opacity (a presentation's fade), as the flight's cross-fade brings it in; its transition's movement and scale still carry the flight: the web's new image is the element's painting, and the brief's "a page sliding in carries the flight".
- A flight is drawn at the higher of the levels its two ends were drawn at, so one flying home from a presentation going away is drawn over it, out of every clip: the web draws view transitions in the top layer, above everything.
- A flight's destination is still hit where it is laid out while it flies, so it can be taken back mid-flight: UIKit's views take touches at their model frame while a transition animates.
- A flight's ends whose nodes both lie inside another flight's ends make it a named part of that one, drawn after it, cut to its shape, its place reckoned through that flight's fit: the web's nested view transition groups (`view-transition-group: nearest`).
- A flight taken back starts where it is drawn at the speed it moves, and fades on from where it had faded when it turns round between the same two nodes: SwiftUI springs retargeted mid-flight keep their position and speed.
- A source's speed is worked out from the speeds of everything moving it (its frames, offsets, scales, transitions and scrolls) looking a moment ahead, not from frames past: the brief's "velocity as fundamental as position".
- A source leaving by its own `DisplayTransition` is cut short when it is taken over, as its picture flies instead: the web's old element is gone once captured.
- What a flying node draws as a group (blur, a mask, group opacity) is drawn flat while it flies, its filters coming back as it lands: the brief's smallest design, as each would cost one more image a frame; a text or image cross-fade inside it fades within its end's image, the pair's blend left to the pair (the brief puts those through the blend out of scope).
- The unused `MatchFit` enum is gone: the brief keeps one fit and puts the others out of scope.

## Velocity

- A value already moving that is set directly heads there keeping its speed, spring and change, as it always did, so a frame laid out again mid-flight glides on to its new place; set by a gesture's callbacks, it lands at once, under the finger, whether or not the gesture hands its speed on, so a drag grabbing a springing value stops it there (Tim, 10 October, in place of landing every direct set, SwiftUI's way, which snapped layout mid-flight).
- Speed is kept in the units a value springs in (its converter's: points, OkLab for colours), not as a value of its own type: SwiftUI keeps velocity in `animatableData`'s `VectorArithmetic`.
- A gesture's samples are fitted by least squares over the last tenth of a second, with the pointer's own tracker: UIKit's and Android's velocity trackers look back about that far.
- The last sample is taken as the gesture lets go, where the value is, so a finger held still before lifting hands on no speed: the brief's "no rules of time"; UIKit's pan velocity is zero after a pause.
- `Began` counts as a change callback as well as `Changed`: SwiftUI's `onChanged` runs for a drag's first change too.
- `Cancelled` hands the speed on as `Ended` does: UIKit's pan recognizer still reports its velocity when it is cancelled.
- A scroll view's offset carries the speed of the drag and the glide moving it, so a change made meanwhile sets off at that speed: the brief's "however it is moved", and springs keeping their speed when retargeted, as SwiftUI's do.
- A spring set off on a value nothing has drawn yet (a node made and animated in one go) starts at the speed the value had: the brief's "every value keeps the speed it is moving at".

## Handing a drag on

- Passing a drag on is each scroll view's choice, `AtEnd` (Bounce, the default, or PassOn), and only a drag that set off within its range is passed on as it crosses an end, so one that catches it bouncing past an end and pushes it back keeps it (Tim, 10 October).
- The drag passes the moment the offset would cross the end, with no rubber band first, from where the finger was as it reached the end, so the taker does not jump: UIKit's sheet takes the drag from the scroll view inside it at its top this way.
- What is around it is asked nearest first, each scroll view that can still go that way and each draggable that takes the way the finger goes, stopping at a presentation: the web chains scrolling to the nearest scroller that can scroll.
- Only a draggable hands the drag back; an outer scroll view keeps the rest of it: the brief names only the draggable, and the web latches a scroll chain to the scroller that took it.
- A draggable hands back once the finger turns a way it does not take along the axis of the scroll view that passed it, so a sideways wobble does not: a sheet ignores sideways movement while it has the drag.
- Handing back ends the draggable's drag with `Ended`, at no translation and no speed, rather than `Cancelled`: it was not cut short, and the speed goes back with the drag.
- A new drag heading past the end a scroll view is already at still goes to a scroll view it is in that can scroll that way, from the start, as the engine already chained nested scroll views: passing on at an end is for the rest of a drag already under way (Tim, 10 October).
- A drag that catches a scroll view bouncing back from past an end and heads further past it goes where a new drag from that end would, to a scroll view it is in that can scroll that way or, passing drags on, to what takes it, and the scroll view springs back to its end: the web latches a new gesture to the nearest scroller that can still scroll its way, and room past an end is not room to scroll (Tim, 10 October).

## Demos

- Matched nodes are hidden with Display Hidden, so a grid, a list or a row of stories keeps the place of what flew away: SwiftUI hides a hero's source with `opacity(0)` or `isSource` for the same reason.
- A story grows out of the avatar inside its ring, not the whole ring, which stays in the feed round the avatar's place (Tim, 10 October).
- Presentations a flight carries in and out have no transition of their own, Log out's alert included, its button hiding as it comes and showing as it goes so the alert flies back into it: a matched flight is the transition, as an iOS hero transition replaces the push (Tim, 10 October).
- The swipe component dismisses when let go a third of the way or flicked away faster than 600 points a second, and springs back otherwise: UIKit's interactive dismissals finish on progress or velocity past a threshold.
- Its shrink goes to three quarters at most, and its progress is how far along the ways it allows the panel has gone: the App Store's pulled-down story shrinks to about that.
- It moves the panel as far as the finger up to where letting go takes it away, and past that with the scroll view's rubber band, sized by its node along the way the finger went (Tim, 10 October).
- The photo viewer, the store's dialog and Log out's alert are thrown away by a drag anywhere in their presentation, on the panel or the backdrop around it: the swipe component moves a panel inside its node when given one, as the first version's throw did (Tim, 10 October).
- The store's price flies inside the pair with its icon and name (Tim, 10 October).
- Every showcase page and the showcase's sheet use it too (right to go back, down to dismiss), so "a page thrown away carries on at the speed of the throw as its presentation takes it out" can be seen: UINavigationController's swipe back and a sheet's swipe down.
- Momentum and precision are shown with two stepped sliders that differ only in `TracksVelocity`, snapping on the same critically damped spring: the brief's own example of a precise control.
- A chat row has an opaque background (the page's) instead of a clear one, so its picture is opaque as it grows into the conversation and nothing behind it shows through.

## Presentations

- `Presenting` and `Dismissing` are C# events raised at the end of the presentation's own state changes, inside its change (shown and laid out, or taking no pointers, its content on its way): UIKit's `animate(alongsideTransition:)`, which runs code inside a presentation's animation.
- `ScrollTo` made inside a change scrolls on the change's animation, and is part of it, without flashing its indicators: SwiftUI's `scrollTo` inside `withAnimation` animates with that animation.
- A transition put where it comes in from inside a change is put there at once (`NodeTransition.Set` jumps), so the change it is set off in still completes: Core Animation's model value set with actions disabled inside a transaction.
