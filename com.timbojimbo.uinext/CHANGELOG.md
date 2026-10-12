## [Unreleased]

### Removed

- The Arena demo (a mobile game's menus). What it showed off stays in the engine (`Label`'s outline and shadow, `Box`'s gradient borders and shadows), and `PresentFrom`, which it shared with Circle, stays with the demos' shared scripts.

### Fixed

- `ListView` works out what is in view where its rows really are:
  - past its scroll view's padding, and anything above the rows;
  - drawn from under its top clear inset, so rows under a header floating over it are made;
  - `ScrollTo` puts a row under what covers the top, with the scroll padding above it.
- Tab skips what is out of sight in a scroll view (another page of a pager, the hidden end of a row), as the arrows do, but goes on down the list focus is in, scrolling it.
- A text field being edited keeps itself clear of what covers its scroll view's edges (its clear insets) and keeps its scroll padding, as well as above the keyboard.

### Added

- Editor: a click in the Scene view selects the node drawn topmost under it, where it is drawn (moved, scrolled, presented), inside its clips, whether it takes the pointer or not; clicked again in the same place, the one under it. It is a custom picking pass (`HandleUtility.pickGameObjectCustomPasses`).
- Debug views: `UIDebug` in code, and Tools/UI Next/Debug in the editor (kept on for the session):
  - Overdraw: what is behind the UI is darkened, and every pixel shaded adds heat, a quad's clear parts and clipped parts included; what is drawn into a layer shows through its quad.
  - Layer Bounds: an outline where each layer's image lies: cyan fading as one, magenta blurred, yellow masked, green a flight's end.
  - Focus Path: an outline round what has focus, and round every section and group it is in, out to its presentation.

## [0.1.0] - 12/10/2026

### Added

- A UI stack of our own, owning layout, drawing, input and motion end to end (Design/UIStack.md, 18).
- Reactive core: `Observable<T>`, `Computation` and `Effect`, run in three phases (model, layout, draw), earliest first, reporting a computation that keeps invalidating itself.
- `UIValue<T>` for every property:
  - a literal, a token or a binding;
  - variants for interaction states (Selected, Hovered, Focused, Pressed, Disabled, and FocusWithin, the weakest) and states of your own (`UIState`);
  - two faces: layout and game code read where a value is going, drawing reads where it is drawn.
- `UI.Animate`: a change and everything its layout moves spring from where they are drawn, at the speed they have; a node made and changed in one go springs from where it was put. `UITransaction` says when a change has finished and whether it completed.
- Velocity:
  - every value that interpolates (numbers, vectors, colours, rects, insets, corner radii) knows the speed it moves at (`AnimatedValue.Velocity`): on a spring, set by a gesture, or moved by a scroll dragged or gliding;
  - a spring set off on a moving value starts at its speed;
  - a value already moving that is set directly heads there keeping its speed, and one a gesture's callbacks set lands at once, under the finger.
- Gestures hand their speed on, as SwiftUI's tracksVelocity:
  - what the change callbacks of a `Draggable` or `Pinchable` set directly is sampled, and the change made as it lets go starts at that speed;
  - once it is over, what it moved that nothing took over lands still;
  - `TracksVelocity` off for precise controls, such as a stepped slider whose snap must not overshoot.
- Tokens (`ColorToken`, `FloatToken`, `Vector2Token`, `InsetsToken`, `CornerRadiiToken`), with a value per mode.
- `UIRoot` and `UINode`: Clay's layout (fit, grow, fixed and percent sizes, padding, gaps, wrapping, alignment, aspect ratio, floating against the parent, the root or another node, the safe area), and the drawn values Offset, Scale, Rotation, Opacity, ZIndex, Clips and TopLayer.
- Drawables:
  - `Box`: a rounded rect with per-corner radii, a fill, a border and a shadow, each a colour or a gradient at an angle of its own (`Gradient`, `BorderGradient` for a bevel, `ShadowGradient` for a glow);
  - `Label`: UI.Text's layout and Markdown, SDF and colour glyphs; `OutlineWidth` and `OutlineColor` outline every glyph past its edge from its distance field (as CSS's text-stroke and TextMeshPro's outline), and `ShadowColor` and `ShadowOffset` draw a hard shadow under it, outline and all;
  - `ImageView`: sprites and textures, fill, contain or cover, rounded;
  - `UIDrawable` and `Painter` (shapes, images, glyphs and laid-out text) for drawables of your own.
- A renderer of one instanced shader drawing from structured buffers in a URP render graph pass after the camera stack, in the Game and Scene views, clipping by up to four rounded rects, with up to eight textures a draw call.
- Input from the Input System:
  - pointers (mouse, pen, every finger) hit tested against what was drawn; `IPointerHandler`, `IHoverHandler` and `IScrollHandler`;
  - `Pressable` (with long press), `Draggable` (saying which drags it takes, `Takes`), `Pinchable` (two-finger pinch and turn);
  - `UIInput.Back`.
- `ScrollView`:
  - rubber band, iOS deceleration, bounce, pages, the wheel, indicators;
  - a new drag heading past an end it is at, or past one it is caught bouncing back from, goes to a scroll view it is in that can still scroll that way;
  - past an end a drag stretches it, or with `AtEnd` set to PassOn the rest of the drag goes to a scroll view it is in that can go on, or a `Draggable` that takes it, which hands it back as the finger turns, as a sheet and the list in it share a drag on iOS;
  - the wheel at an end scrolls the scroll view it is in;
  - `ScrollTo` inside a change scrolls on the change's animation, as part of it.
- `ScrollView.ClearOfTop`, `ClearOfBottom`, `ClearOfLeft`, `ClearOfRight` and `ClearMargin`: nodes floating over a scroll view's edges (a header, a tab bar), listed for the edge they lie along and kept clear of by reference, as drawn now, with room on top, as SwiftUI's safeAreaInset insets a scroll view:
  - its range grows by how far they reach in, so what it holds scrolls out from under them;
  - what it holds is drawn from under the top and left ones;
  - `ScrollIntoView` (and so focus) lands clear of them, and pages and the indicators keep to the part they leave;
  - `ClearInsets` says how far each edge is covered.
- `ScrollView.ScrollPadding`: room a scroll view keeps round anything it brings into view, whatever brings it (focus moving to it, `ScrollIntoView` from code), as CSS's scroll-padding, so it lands the same either way; it moves nothing at rest.
- `UIFocus`, keyboard and gamepad focus on nodes, as part of the tree:
  - the arrows, the d-pad and the stick go to the next row that way as drawn (the next column, going left or right) and in it to the nearest across, of equally near ones the one focus last left;
  - what the scroll view focus is in holds comes first, out of sight or under a bar (focus leaves it only at its end), and what is clipped out of sight in another scroll view is out of reach;
  - Tab and Shift Tab go in reading order, along rows the way `UI.ReadingDirection` reads (the current UI culture's unless set);
  - a node's `FocusScope` makes it a section, which a move towards it enters as one (at the nearest inside or, with `FocusEntry` Remembered, where focus last was) and which Tab goes through before going on, as SwiftUI's focusSection; or a group, one stop until Submit enters it and Back leaves it, as Xbox's focus engagement;
  - `PrefersFocus` says where focus starts;
  - a node's `Focusable` says whether it takes focus (a button does unless Never; anything else when Always);
  - an `IFocusMoveHandler` on what has focus takes the arrows it wants (a slider stepping), as SwiftUI's focusable and onMoveCommand, saying whether it moved, has no move left that way (`FocusMoveResult.Blocked`: a slider at its end) or leaves the move to focus;
  - a key that goes nowhere, blocked or with nothing that way to move to, shakes the focus ring no, a quick swing that way and back, on a fresh press only;
  - a presentation coming up takes focus, at what prefers it, and gives it back to what opened it as it goes;
  - focus lost (hidden, disabled, gone) goes to the nearest where it was; a tap focuses a button, unseen;
  - it shows only after a key, `Focused` on what has it and `FocusWithin` on everything it is in, and the first key after a pointer only shows it, where it is;
  - meanwhile the focus ring is drawn round what has it, as iPadOS's focus halo, out past its edge, in its root's look (`FocusRingColor`, `FocusRingWidth`, `FocusRingOffset`, and `FocusRingRadius` for its corners, whatever it goes round); `UINode.FocusRing` is off on a node that shows focus its own way;
  - what takes focus in a scroll view is scrolled into view with the scroll view's `ScrollPadding` round it, or room for the ring where that is more;
  - a first responder has the keys while a field is edited.
- `TextField` (in the `TimboJimbo.UINext.TextInput` assembly, where UI Text Input is installed): text fields drawn by the stack and typed into with the platform's own keyboard through UI Text Input's `TextInputSystem`, with caret, selection, composition, secure fields, desktop shortcuts, mouse and touch selection, scrolling single lines and multi-line fields that grow.
- `UINode.AvoidsKeyboard` and `UIRoot.KeyboardHeight`: content kept above the software keyboard, following it as it slides; the field being edited scrolls itself above the keyboard.
- `ListView`: long lists that make only the rows near view, reusing cells as they scroll away, rows measuring themselves, what is in view kept put as rows above it are measured.
- `Presentation`:
  - sheets, alerts and pushed or full-screen pages in a stack, each laid out as a root of its own, with a dim backdrop, modality and back to dismiss;
  - culling below a cover while it is drawn in place (what is below is drawn again as it is swiped aside);
  - presenting and dismissing each make all their state changes inside one change of their own, and raise `Presenting` and `Dismissing` inside it, so code joins the change, as UIKit's animate(alongsideTransition:).
- `DisplayTransition`: a node shown or hidden inside `UI.Animate` fades, grows or slides in and out.
- Matched geometry, as SwiftUI's matchedGeometryEffect drawn as the web's view transitions draw it:
  - `UINode.MatchName`, `MatchId` (which what is inside a node inherits) and `MatchAnchor`;
  - inside one change, a node shown with the name and id of one the change hides takes over from it, flying from where that one was drawn to its own place; shown while a namesake stays, it flies out of it;
  - one end is a picture of the hidden node as drawn, the other the shown node, live, cross-faded as one image by summing them, so nothing behind shows through where both are opaque;
  - each end's own drawing (its box, border and shadow, an image filling it) is laid out at the flight's rect; what is inside it is scaled evenly into the flight along the axis the two ends are most alike in, lined up along the other by its node's `MatchAnchor`, and cut to the flight's window, whose corners go from one end's `ClipRadii` to the other's;
  - named parts inside an end fly on their own, drawn over it;
  - what springs is the flight's difference from where its destination is drawn, so a scroll or a page sliding in carries it;
  - drawn above everything at its level, out of clips; taken back mid-flight, it turns round from where it is drawn; it sets off at the speed its source was drawn moving.
- `UINode.GroupOpacity`: while not fully opaque, a node and everything inside it is drawn to an image of its own first and faded as one, so overlapping parts don't show through each other; nested groups and clips work, each image covers only what its group draws, and fully opaque it costs nothing.
- `UINode.Saturation` and `Brightness`: colour filters that multiply down the tree and cost nothing (applied to every pixel drawn, no image).
- `UINode.Mask`: a texture's alpha cuts a node and everything inside it, stretched over its rect (a layer, then masked).
- `UINode.Blur`: a node and everything inside it blurred as one image (dual Kawase passes on its group image), animated.
- Content cross-fades: inside `UI.Animate`, a change to a drawn value that cannot interpolate (a label's text, an image's sprite, a drawable's bool or enum) fades the node's old drawing out as the new one fades in; changed again while fading, what is drawn then fades out.
- A state's enter and exit animations move everything it changes when it is set outside `UI.Animate`, token values included.
- `UIBuild`, extension methods for building trees from code.
- Editor:
  - the state bar, a Scene view overlay previewing interaction states, states and modes on the selected node (`UIStatePreview`), with a readout of the frame's draw calls, primitives and layers;
  - inspectors in sections (Layout, Drawn, Effects, Clipping, Input, Matching on a node; Fill, Gradient, Border, Shadow on a box; and so on);
  - small choices as lit buttons with Unity's icons (direction, alignment, display, sizing and wrap modes, safe-area edges);
  - a link at the end of each value that can take a token (click to pick one; linked, the token stands in for the literal; click again to unlink), and no link on values that cannot;
  - `UISimulator` and `UICapture`, which play and capture frames outside play mode.
- Demos, built by `Tools/UI Next/Demos`, and one demo component, `SwipeToDismiss`, behind every swipe to dismiss in them, moving its own node or a panel inside it, as far as the finger up to where letting go closes it and with a scroll view's rubber band past that:
  - Circle (`Demos/Scenes/Circle.unity`), with the screens the UI package's mobile demo has:
    - the pages scroll clear of the floating tab bar;
    - the photo viewer flies out of the feed and home (thrown away any way from the photo or its backdrop, or pinched), the feed bringing the photo into view clear of the bar as it lands;
    - a chat row grows into its conversation (its avatar and name becoming the header, the newest two chats unread) and collapses back;
    - stories grow out of their rings and shrink back, stepped on and back by taps on their sides or left and right, their rings greyed once seen, with a like;
    - a post's author rises into their profile (pushed, with Follow and their photos);
    - the compose button grows its card out of itself, and Post flies the draft into the feed;
    - a post's + chip grows the reaction picker, whose emoji fly into the chips they join, and its comments open a sheet;
    - Share profile grows a card with a code to scan;
    - the settings row's title rises into the settings page (a text field, switches, a segmented control for the theme, a stepper and a slider that take left and right, swatches, a list with a check mark, quiet hours showing a row, a storage bar Clear cache empties), and Log out there grows its dialog out of itself, which a drag anywhere throws away.
  - Arena (`Demos/Scenes/Arena.unity`), a mobile game's menus after the big card-battle games:
    - a top bar with level, gold and gems counting as they change, and five pages swiped between over a tab bar whose selected tab widens and lifts its icon;
    - the Battle page's arena, Battle button and chest slots (unlocking, opening into a popup the chest grows into, its rewards revealed a tap at a time);
    - the Cards page's deck and collection (a card growing into its details, upgraded with its copies and gold);
    - the Shop's offers, the Clan's chat, and Events.
  - The showcase (`Demos/Scenes/Showcase.unity`), one idea a case:
    - a Store (a grid of items growing into a dialog, their icons, names and prices flying inside, which a drag anywhere throws away);
    - an App Store (Today cards opening into stories, shrunk in place by a pull down past their top and closed back into the list, which scrolls the card into view);
    - a stepped slider with momentum and one without;
    - a Focus page: a grid, sections side by side, a group, buttons that go, a row wider than the page, a dialog and a field to try keyboard and gamepad focus on, a switch that has rows read right to left, and a readout of where focus is.
