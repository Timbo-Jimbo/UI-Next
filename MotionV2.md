# Motion v2: the brief

This branch adds a motion layer to UI Next, the package in `com.timbojimbo.uinext`. The engine as it stands has a
reactive core, values with a model face and a presentation face, closed-form springs, `UI.Animate` changes, layout,
a compositor, input, presentations, and two demo scenes built from code. Learn it from the code, the README and the
CHANGELOG before changing it.

**SwiftUI is the guide.** Where this brief names a behaviour, build that. Where it is silent, do what SwiftUI does;
where SwiftUI has nothing to say, do what UIKit or the web does. The brief describes the end result, not how to build
it: the design inside is yours.

## Principles

- **Follow the platform.** SwiftUI first; the web's view transitions for how matched geometry is drawn; UIKit for how
  a drag is handed between scroll views.
- **Explicit over inferred.** Authors say which edge, which direction, which nodes. The engine never guesses them from
  geometry.
- **A general engine, specific demos.** Engine features are common patterns with an equivalent on a platform.
  Behaviour that only a demo needs lives in demo code, and the engine's public API never grows for a demo's
  convenience.
- **Changes happen inside the change.** Whatever should animate together is changed inside one `UI.Animate`, and the
  engine raises the events that let code join a change, such as a presentation coming or going, inside it.
- **The smallest design that reaches the goal.** No options, abstractions or modes beyond what the goal needs.

## The goal

### Matched geometry

As SwiftUI's `matchedGeometryEffect`, drawn the way the web's view transitions draw.

- A node can carry a match name, and an id that its descendants inherit, so that many of a kind, such as a list's
  rows, stay distinct.
- Inside one animated change, a node shown with the name and id of one the change hides takes over from it: it flies
  from where the hidden one was drawn to its own place. A node shown while a namesake stays flies out of it instead.
- One end is a picture of the source as it was drawn; the other is the destination itself, live. The two cross-fade
  as one isolated image, so nothing behind shows through and a background never dips mid-flight.
- Content is fitted to the flight's width from the top, as the web does, so a row growing into a page keeps its size
  rather than swelling. Text keeps its height, and a node's own background is laid out at the flight's size, as the node
  would draw at that size, its shadow falling outside the flight's shape.
- Named parts inside a pair fly to their own places, inside the whole's shape. A pair is one rounded shape whose
  corners morph from one end's to the other's.
- What springs is the difference between where the flight started and where the destination belongs, closing to
  nothing. Whatever moves the destination meanwhile, a scroll or a page sliding in, carries the flight with it, so it
  never chases a moving target.
- A flight is drawn above everything at its level, and can be taken back mid-flight, turning round from where it is
  drawn.
- Presentations make all their state changes inside their own animated change, and say that they are presenting or
  dismissing inside it, so code can hide or show matched nodes in the same change.

### Velocity

As fundamental as position.

- Every value that interpolates keeps the speed it is moving at, however it is moved: numbers, vectors, colours,
  rects, insets and corner radii alike. Values that do not interpolate have no speed.
- Springs keep their speed when a new change retargets them.
- Gestures hand theirs on, as SwiftUI's `tracksVelocity` does. While a gesture's change callbacks run, the values they
  set directly are sampled, and the animated change made as the gesture lets go starts at that speed. Once the
  gesture has ended, whatever it moved that nothing took over lands still. No rules of time: the gesture's own lifetime
  decides.
- A value already moving that is set directly heads there keeping its speed, so a frame laid out again mid-flight
  glides on; set by a gesture's callbacks, it lands at once, under the finger.
- A matched flight starts at the speed its source was drawn moving.
- Motions add up. A page thrown away, whose offset springs home inside its dismissal, carries on at the speed of the
  throw as its presentation takes it out. Code never handles velocity.
- Precision or momentum: tracking is on by default, and a gesture has a switch, named after SwiftUI's
  `tracksVelocity`, to stop it handing its speed on, for precise controls such as a stepped slider whose snap must not
  overshoot the step the user chose.

### Handing a drag on

- A scroll view at an end either bounces, or, if it is set to, passes the rest of the same drag to what is around it:
  an outer scroll view that can scroll that way, or a draggable that takes it. The draggable hands the drag back as
  soon as it would not take the way the finger moves. A draggable says which drags it takes, at a drag's start as well
  as when one is passed on.
- A new drag heading past the end a scroll view is already at goes to a scroll view it is in that can still scroll
  that way, from the start, as the engine already chains nested scroll views. So does a drag that catches it bouncing
  back from past that end and heads further past it, and the scroll view springs back.

### Demos

Rebuildable from code through the existing `Tools/UI Next/Demos` builders.

- **Showcase, Store:** a grid of items, each growing into a dialog and flying home.
- **Showcase, App Store:** Today cards opening into full-screen stories. A pull down past a story's top shrinks it in
  place, and letting go closes it back into the list, which scrolls the card into view.
- **Circle:** the photo viewer flies out of the feed and home again, thrown away in any direction or pinched. A chat
  row grows open into its full-screen conversation, its avatar and name becoming the header and its preview fading,
  and collapses back into the row. Stories grow out of their rings and shrink back into them. Log out grows its dialog
  out of its button.
- **One demo component behind every swipe to dismiss:** the directions it allows, chosen within its draggable's axis;
  following the finger or shrinking in place; a progress event for whatever fades or rounds with the throw. Reopened
  mid-throw, the panel slides back from where it is.

### Out of scope

- A node hidden while its namesake stays, shrinking back into it.
- Fits other than the one above.
- Text and image cross-fades going through a pair's isolated blend.
- A public way for code that is not a gesture to hand on its speed.
- A projection helper for momentum controls that snap to targets.

## Working

- Work on this branch, `motion-v2`, in this repository only. This brief is the spec: do not look at other branches or
  their history, or at documents outside this repository.
- Commit in logical steps with clear messages, each ending with the attribution line you are given. Never push.
- Add tests alongside the existing ones in `Tests`, in their style.
- Do not drive the Unity editor: the user compiles and runs the tests. Read the APIs you call, so the code compiles.
- Do not commit the scene files in `Demos/Scenes`; the user rebuilds them from the builders.
- Keep the CHANGELOG and README current.
- Keep `MotionV2-Decisions.md`, next to this brief: one line per decision this brief left open, with the precedent you
  followed.
