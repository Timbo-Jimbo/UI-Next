## [Unreleased]

### Added

- A UI stack of our own, owning layout, drawing, input and motion end to end (Design/UIStack.md, 18)
- Reactive core: `Observable<T>`, `Computation` and `Effect`, run in three phases (model, layout, draw), earliest first, reporting a computation that keeps invalidating itself
- `UIValue<T>` for every property: a literal, a token or a binding, with variants for interaction states (Selected, Hovered, Focused, Pressed, Disabled) and states of your own (`UIState`), and two faces: layout and game code read where a value is going, drawing reads where it is drawn
- `UI.Animate`: a change and everything its layout moves spring from where they are drawn, at the speed they have, a node made and changed in one go springing from where it was put; `UITransaction` says when it has finished and whether it completed
- Tokens (`ColorToken`, `FloatToken`, `Vector2Token`, `InsetsToken`, `CornerRadiiToken`) with a value per mode
- `UIRoot`, `UINode` (Clay's layout: fit, grow, fixed and percent sizes, padding, gaps, wrapping, alignment, aspect ratio, floating against the parent, the root or another node, the safe area) and the drawn values Offset, Scale, Rotation, Opacity, ZIndex, Clips and TopLayer
- Drawables: `Box` (rounded rect with per-corner radii, gradient, border, shadow), `Label` (UI.Text's layout and Markdown, SDF and colour glyphs), `ImageView` (sprites and textures, fill, contain or cover, rounded), and `UIDrawable` and `Painter` (shapes, images, glyphs and laid-out text) for drawables of your own
- A renderer of one instanced shader drawing from structured buffers in a URP render graph pass after the camera stack, in the Game and Scene views, clipping by up to four rounded rects, with up to eight textures a draw call
- Input from the Input System: pointers (mouse, pen, every finger) hit tested against what was drawn, `IPointerHandler`, `IHoverHandler` and `IScrollHandler`, `Pressable` (with long press), `Draggable`, `Pinchable` (two-finger pinch and turn), `ScrollView` (rubber band, iOS deceleration, bounce, pages, wheel, indicators), `UIFocus` (keyboard and gamepad navigation by drawn geometry, and a first responder that has the keys while a field is edited) and `UIInput.Back`
- `TextField` (in `TimboJimbo.UINext.TextInput`, where UI Text Input is installed): text fields drawn by the stack and typed into with the platform's own keyboard through UI Text Input's `TextInputSystem`, with caret, selection, composition, secure fields, desktop shortcuts, mouse and touch selection, scrolling single lines and multi-line fields that grow
- `UINode.AvoidsKeyboard` and `UIRoot.KeyboardHeight`: content kept above the software keyboard, following it as it slides; the field being edited scrolls itself above the keyboard
- `ListView`: long lists that make only the rows near view, reusing cells as they scroll away, rows measuring themselves
- `Presentation`: sheets, alerts and pushed or full-screen pages in a stack, each laid out as a root of its own, with a dim backdrop, modality, back to dismiss, and culling below a cover in place
- `DisplayTransition`: a node shown or hidden inside `UI.Animate` fades, grows or slides in and out
- `UINode.GroupOpacity`: while not fully opaque, a node and everything inside it is drawn to an image of its own first and faded as one, so overlapping parts don't show through each other; nested groups and clips work, each image covers only what its group draws, and fully opaque it costs nothing
- `UINode.Saturation` and `Brightness`: colour filters that multiply down the tree and cost nothing (applied to every pixel drawn, no image)
- `UINode.Mask`: a texture's alpha cuts a node and everything inside it, stretched over its rect (a layer, then masked)
- `UINode.Blur`: a node and everything inside it blurred as one image (dual Kawase passes on its group image), animated
- Content cross-fades: inside `UI.Animate`, a change to a drawn value that cannot interpolate (a label's text, an image's sprite, a drawable's bool or enum) fades the node's old drawing out as the new one fades in
- A state's enter and exit animations move everything it changes when it is set outside `UI.Animate`, token values included
- `UIBuild`, extension methods for building trees from code
- Editor: the state bar, a Scene view overlay previewing interaction states, states and modes on the selected node (`UIStatePreview`); inspector drawers for `UIValue<T>`, `Sizing`, `Insets` and `CornerRadii`; `UISimulator` and `UICapture`, which play and capture frames outside play mode
- Demos: Circle (`Demos/Scenes/Circle.unity`) and the showcase (`Demos/Scenes/Showcase.unity`), built by `Tools/UI Next/Demos`
