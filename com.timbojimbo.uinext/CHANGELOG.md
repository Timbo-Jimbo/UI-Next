## [Unreleased]

### Added

- A UI stack of our own, owning layout, drawing, input and motion end to end (Design/UIStack.md, 18)
- Reactive core: `Observable<T>`, `Computation` and `Effect`, run in three phases (model, layout, draw), earliest first, reporting a computation that keeps invalidating itself
- `UIValue<T>` for every property: a literal, a token or a binding, with variants for interaction states (Selected, Hovered, Focused, Pressed, Disabled) and states of your own (`UIState`), and two faces: layout and game code read where a value is going, drawing reads where it is drawn
- `UI.Animate`: a change and everything its layout moves spring from where they are drawn, at the speed they have; `UITransaction` says when it has finished and whether it completed
- Tokens (`ColorToken`, `FloatToken`, `Vector2Token`, `InsetsToken`, `CornerRadiiToken`) with a value per mode
- `UIRoot`, `UINode` (Clay's layout: fit, grow, fixed and percent sizes, padding, gaps, wrapping, alignment, aspect ratio, floating against the parent, the root or another node, the safe area) and the drawn values Offset, Scale, Rotation, Opacity, ZIndex, Clips and TopLayer
- Drawables: `Box` (rounded rect with per-corner radii, gradient, border, shadow), `Label` (UI.Text's layout and Markdown, SDF and colour glyphs), `ImageView` (sprites and textures, fill, contain or cover, rounded), and `UIDrawable` and `Painter` for drawables of your own
- A renderer of one instanced shader drawing from structured buffers in a URP render graph pass after the camera stack, in the Game and Scene views, clipping by up to four rounded rects, with up to eight textures a draw call
- Input from the Input System: pointers (mouse, pen, every finger) hit tested against what was drawn, `IPointerHandler`, `IHoverHandler` and `IScrollHandler`, `Pressable`, `Draggable`, `ScrollView` (rubber band, iOS deceleration, bounce, pages, wheel, indicators), `UIFocus` (keyboard and gamepad navigation by drawn geometry) and `UIInput.Back`
- `Presentation`: sheets, alerts and pushed or full-screen pages in a stack, each laid out as a root of its own, with a dim backdrop, modality, back to dismiss, and culling below a cover in place
- `DisplayTransition`: a node shown or hidden inside `UI.Animate` fades, grows or slides in and out
- `UINode.GroupOpacity`: while not fully opaque, a node and everything inside it is drawn to an image of its own first and faded as one, so overlapping parts don't show through each other; nested groups and clips work, and fully opaque it costs nothing
- `UIBuild`, extension methods for building trees from code
- Editor: inspector drawers for `UIValue<T>`, `Sizing`, `Insets` and `CornerRadii`; `UISimulator` and `UICapture`, which play and capture frames outside play mode
- Demos: Circle (`Demos/Scenes/Circle.unity`) and the showcase (`Demos/Scenes/Showcase.unity`), built by `Tools/UI Next/Demos`
