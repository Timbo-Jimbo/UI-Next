# Motion v2: decisions

One line per decision the brief left open, with the precedent followed.

## Velocity

- A value set directly lands where it is set, still, cutting short any spring moving it (it used to head there on its spring): the brief's "a value set directly by anything else lands still"; SwiftUI does not animate a change made without an animation.
- Speed is kept in the units a value springs in (its converter's: points, OkLab for colours), not as a value of its own type: SwiftUI keeps velocity in `animatableData`'s `VectorArithmetic`.
- A gesture's samples are fitted by least squares over the last tenth of a second, with the pointer's own tracker: UIKit's and Android's velocity trackers look back about that far.
- The last sample is taken as the gesture lets go, where the value is, so a finger held still before lifting hands on no speed: the brief's "no rules of time"; UIKit's pan velocity is zero after a pause.
- `Began` counts as a change callback as well as `Changed`: SwiftUI's `onChanged` runs for a drag's first change too.
- `Cancelled` hands the speed on as `Ended` does: UIKit's pan recognizer still reports its velocity when it is cancelled.
- A scroll view's offset carries the speed of the drag and the glide moving it, so a change made meanwhile sets off at that speed: the brief's "however it is moved", and springs keeping their speed when retargeted, as SwiftUI's do.
- A spring set off on a value nothing has drawn yet (a node made and animated in one go) starts at the speed the value had: the brief's "every value keeps the speed it is moving at".

## Handing a drag on

- The drag passes the moment the offset would cross the end, with no rubber band first, from where the finger was as it reached the end, so the taker does not jump: UIKit's sheet takes the drag from the scroll view inside it at its top this way.
- What is around it is asked nearest first, each scroll view that can still go that way and each draggable that takes the way the finger goes, stopping at a presentation: the web chains scrolling to the nearest scroller that can scroll.
- Only a draggable hands the drag back; an outer scroll view keeps the rest of it: the brief names only the draggable, and the web latches a scroll chain to the scroller that took it.
- A draggable hands back once the finger turns a way it does not take along the axis of the scroll view that passed it, so a sideways wobble does not: a sheet ignores sideways movement while it has the drag.
- Handing back ends the draggable's drag with `Ended`, at no translation and no speed, rather than `Cancelled`: it was not cut short, and the speed goes back with the drag.
- A drag that starts heading past the end a scroll view is at is taken by it and passed on at its first move, replacing the old start-of-drag chaining: one mechanism for both, as the brief's "the rest of the same drag".

## Presentations

- `Presenting` and `Dismissing` are C# events raised at the end of the presentation's own state changes, inside its change (shown and laid out, or taking no pointers, its content on its way): UIKit's `animate(alongsideTransition:)`, which runs code inside a presentation's animation.
- `ScrollTo` made inside a change scrolls on the change's animation, and is part of it, without flashing its indicators: SwiftUI's `scrollTo` inside `withAnimation` animates with that animation.
- A transition put where it comes in from inside a change is put there at once (`NodeTransition.Set` jumps), so the change it is set off in still completes: Core Animation's model value set with actions disabled inside a transaction.
