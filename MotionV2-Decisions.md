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
