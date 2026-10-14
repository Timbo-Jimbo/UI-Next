## [Unreleased]

### Added

- `UIDebug.OffscreenNodes` (and the Rendering Debugger's Offscreen Nodes readout): how many nodes were scrolled out from under a clip over them, so not drawn, as the UI was last drawn; the Compare player logs it with its draw calls.
- `UIDebug.Occluders` (and the Rendering Debugger's Occluders switch): the occluders turned off, everything shaded, for measuring what they are worth on a GPU; `UIDebug.DrawCalls`, how many draw calls the UI was last drawn with. The Compare player takes `-repeat N` and `-noocc`, and logs each use case's draw calls.

### Changed

- The primitive shader is a pass a kind of primitive (shape, shadow, image, nine-slice, SDF glyph, bitmap glyph, group image, matched pair), each passing and computing only what its kind needs, in 32 registers or fewer where the one shader for every kind took 64 for every pixel; each kind has a plain pass and a clipped one, and the compositor draws each run of one kind with the plain pass when every clip over it is upright and square-cornered and its node upright (the vertex shader's cut is then exact, so no pixel tests a clip), the clipped pass otherwise. The overdraw view is a pass of its own. A node whose rect lies wholly outside an upright clip over it (scrolled out of a scroll view) is not drawn at all, so a long feed costs what is on screen. Each batch draws with a material of its own holding its textures and buffers, each run changing only where it starts. On a Pixel 8 the Compare screen's GPU time went from 2.89 to 1.35 ms a frame at 40 cards and from 3.28 to 1.38 at 100 (the empty screen 0.65; UGUI 1.00), drawn pixel for pixel the same, with the occluders worth 0.83 ms of that on its Mali.
- An occluder is laid down only for an opaque shape at least 48 pixels a side: a smaller one hides little for what its vertices and fill cost.
- Whether a node is scrolled out from under a clip over it is decided once, as its tree composites it, from what it paints: its rect and what its own drawing reaches past it (a shadow's blur, an outline, the focus ring), carried into its root's space and tested against each upright clip over it, a turned node by the box round its corners. Everything after reads that one bit. The compositor leaves such a node out of every draw, as it did, but from the node's real reach rather than 100 points of room round every node, so a wide shadow at a scroll view's edge is never cut and a plain node is not drawn 100 points before it appears. The node's drawing waits while it is away: a node changed offscreen keeps its drawing recorded (so the draw order holds while scrolling and nothing is built again) and is drawn again the frame it comes back, as a hidden node already was; its first drawing never waits, so a feed is drawn once as it is made. Hit testing skips offscreen nodes before any transform work. Layout and text measurement still run for them, since their siblings depend on their sizes; the list view keeps long lists short on its own.
- Every primitive is drawn in a layer such that nothing within a layer overlaps: a node's layer is its parent's plus one, or past any earlier sibling's subtree it overlaps; within a node a primitive goes past an earlier one it overlaps whose kind would otherwise draw after it, and a shape past any earlier shape it overlaps; overlay primitives sit above everything inside their node, the top layer and flights above their level. Layers draw in order, and within a layer kind by kind (shadow, shape, image, text), so where nothing overlaps the order is free: the main pass's draws fall from one a change of kind to one a kind a layer (on the Compare screen 36 to 12), and a shape's three flat bands between its corners draw in a pass of their own after its four corners, where a rounded rect's distance is a box's. Instances stay recorded in tree order, so patching, culling and the occluders are as they were; an order list, made only when the draw order is, is what batches, runs and the shaders go through. On a Pixel 8 the Compare screen's GPU time went from 1.36 to 1.32 ms a frame at 40 cards and from 1.44 to 1.30 at 100 (scrolling 1.47 to 1.36 and 1.48 to 1.29), the render thread from 0.82 to 0.65 ms, the main thread by 0.15 ms, drawn pixel for pixel the same, in 9 layers.
- `UIDebug.Layers`: how many layers the main pass was last drawn in; the Compare player logs it with its draws.
- A nine-slice is drawn as nine quads from its one record, each slice placed by the vertex shader with the part of the image it samples, so its pixel shader is the image pass's, sampling with the hardware's gradients, where it sliced the image pixel by pixel with explicit gradients in 56 registers: the same pixels at half the registers and the cost of a plain image. A pass declares how many quads it draws a primitive as, and the compositor draws a run with its count times that: nothing is recorded, patched or culled differently.
- A primitive's quad is drawn as four indexed vertices (an index buffer of its two triangles) rather than six: the same pixels from a third fewer vertex shader runs. A compute pass preparing every quad once a frame was tried and measured on the Pixel 8: at a few hundred primitives it saved nothing (the GPU's time is in the pixels), and recorded between URP's final blit and the UI's pass it cost 0.1 ms, so it was not kept.
- Project Settings > Timbo Jimbo > UI Next > Skip Overlay UI Pass: URP draws screen-space UGUI and UI Toolkit in a pass of its own after its final blit, which loads and stores the whole screen each frame even with nothing to draw (0.3 ms of GPU time a frame on a Pixel 8, a quarter of an empty frame). On, UI Next tells URP each frame not to record it, and the engine draws any such UI after URP as it used to. Off unless set, for projects that draw UGUI or UI Toolkit overlays, which are slower drawn that way. UI Next's own pass is unaffected: it draws inside URP's final pass either way.

## [0.5.0] - 14/10/2026

### Added

- In the Profiler, a UI Next module, which stacks where each frame's UI Next time goes (input, Model, layout, drawing, motion, the compositor, images), a UI Next Counts module (draw calls, primitives, and images loading, arriving and in use, the memory cache's size, atlas pages), and a UI Next Updates module (what each frame did again to bring the UI up to date: nodes whose layout values were read and nodes laid out, nodes composited and those whose values were read for it, draw orders made again, primitives patched, what went to the GPU). Their counters come from Unity's Profiling Core package, which UI Next now depends on, and they and UI Next's markers are in a category of their own, "UI Next".
- Profiler markers, all named `UINext.*`: the frame and its steps, each stretch of a flush spent in one phase (`UINext.Model`, `UINext.Layout`, `UINext.Draw`), layout solves, the compositor's build, upload and render, input, labels reading their text, each computation's runs (`UINext.NodeDraw`, an effect by what it runs, `UINext.Effect.Image.Load`, `UINext.Value.Resolve`, `UINext.Tree.LayOut`), each drawable's drawing by its type (`UINext.Draw.Label`, `UINext.Draw.Box`, `UINext.Draw.Image`, your own) and a cross-fade's, and images loading (starting, made into textures, written to disk, put on atlas pages, evicted); the disk cache's work shows as a thread of its own, "UI Next / Image disk cache".
- `ImageFit.Sliced`: a sprite nine-sliced, its borders (set in the Sprite Editor) keeping their size in points while the edges between them stretch along and the middle both ways. It is sliced as it is drawn, in one quad, so it follows its node's size as that springs. `Painter.SlicedImageNode` draws one.
- `node.Image(sprite, fit)` adds an image, as `node.Box` and `node.Label` add theirs.
- `Image.Url`: an image loaded from the web, downloaded once and shared by every image showing it, kept while anything shows it and after that while it fits the cache (64 MB of images nothing shows, the longest unseen going first). Downloads run six at a time, and one nothing wants any more is called off. It fades in as it arrives; until then its node is in `UIStates.Loading` (`UIStates.Failed` if it could not be loaded), states shipped as assets for variants to name. `node.Image(url)` adds one, and `Image.ClearCache()` forgets every image loaded, those on screen loading again.
- `ImageLoader` (**Create > Timbo Jimbo > UI Next > Image Loader**): how images ask for what they load, as Next.js's image loader does. Its templates for a copy and a preview name `{url}`, `{origin}` and `{path}`, and `{width}` and `{height}` in pixels; with `{height}` a copy is asked for in its node's shape, for the CDN to crop to (Bunny's `aspect_ratio={width}:{height}`, Cloudflare's `fit=cover`, imgix's `fit=crop`, Cloudinary's `c_fill`), and where a node's height is fitted to its image that part of the template is left out. An image with a loader (`Image.Loader`, else the project's default) asks, once laid out, for a copy as wide as it is drawn, rounded up to the loader's next width and never narrower than one it has asked for (a wider copy loaded already doing as well); until it arrives a narrower copy loaded already, or the preview, stands in, and it cross-fades in over that. An image whose node's width is fitted to it loads its URL as it is.
- Images loaded from URLs are kept on disk between runs too, in the OS's cache folder (`Application.temporaryCachePath`), and read from there off the main thread as a download is; past the project's limit the files used longest ago go first, and `Image.ClearCache()` empties it. Not on WebGL, whose browser keeps what it downloads.
- Small images loaded through a loader (copies up to 512 pixels a side, asked for at the size they are drawn, and previews) are copied onto shared atlas pages on the GPU, so that a screen of photos samples a few textures and draws in a few calls: 2048-pixel pages of 8-bit RGBA (an image in another format, as a decoded JPEG is, drawn into a render texture of theirs first), the images along shelves of their height with their edge pixels copied once more round them. An image let go of leaves its room for another, and a page with nothing left on it goes; past four pages, or where the GPU can't copy textures, an image keeps its own.
- Project Settings > Timbo Jimbo > UI Next (`UISettings`): the default image loader, how much of the images nothing shows is kept in memory (64 MB unless set) and how much is kept on disk (256 MB).
- The Showcase has an Images page: one sprite, sliced to three sizes, and three photos loaded from the web through a loader of the page's own (Bunny's optimizer: copies as wide as they are drawn, in their shape, and a blurred preview), which a tap opens full screen, where a wider copy loads with the page's standing in, and a button clearing the cache.
- `UIDebug.Repeat`: the UI drawn that many times over each frame, which keeps a phone's GPU at its full clock, so what one drawing costs shows in the GPU's time (a GPU with little to do slows down, which hides it).
- `UIDebug.VerifyLayout`: each layout pass that solves only what can have moved is checked against solving every node, and each node laid out anywhere else logged as an error: for testing layout.
- Two demo scenes for measuring: Compare (**Tools > UI Next > Demos > Build Compare**) builds one screen, a feed of cards under a header, in UI Next or in UGUI with TextMeshPro, and plays use cases over it measured as the benchmark measures them; Counters (**Build Counters**) changes labels every frame (counts, clocks and prices), for profiling what a change of text costs, with an Android player built for deep profiling.

### Changed

- `ImageView` is `Image`, as `Box` and `Label` are; scenes keep their images.
- Compositing and the compositor do only what changed: each node is composited by its own computation, run when what it reads changes, so a scroll view scrolling composites what it holds rather than the whole tree; only the slots and clips that changed go to the GPU, a node drawn again is patched in place, and the draw order is made again only when what is drawn or its order changes. On a Pixel 8 the benchmark's scrolling went from 7.2 to 0.7 ms a frame.
- A node not seen (hidden, inside something hidden, or faded to nothing) is not drawn again until it is seen: a theme switch no longer draws again the pages that are not presented.
- Layout solves only what can have moved: a node whose values or content changed, the nodes up from it to its root and their children, each child holding no change taken whole, as it was last fitted.
- In the game view the UI's render pass has a depth buffer of its own (cleared, never stored, so a tiled GPU keeps it on the chip): what an opaque shape covers is not shaded, and nothing a clip cuts away is shaded either.
- Measuring (`IMeasurable`) is no longer tracked by layout, since a layout that does not reach a node would let go of what its measuring read: a drawable that measures says when what it measures changes (`UIDrawable.ContentChanged`), as `Label`, `Image` and `TextFieldText` do.
- A label measures its text by laying it out with the layout it draws (`TextLayout.MeasureAhead`), and draws that layout as it is, moved where its alignment puts it, in a box it fits (`TextLayout.Reflow`): a text that changes is laid out once, where it was measured unwrapped, measured again at its width, and laid out again to draw. A text that fits its width unwrapped is not measured again at that width. It reads its text with no allocations, its markup's rules kept from one reading to the next.
- Squeezed, a node fitted to content (a text, an image) gives way down to its padding, its text wrapping or cut short, as a label in a UIKit stack does, where a text stopped at its longest word. Layout no longer lays text out to find that word, which cost as much as measuring it.

### Removed

- `IMeasurable.MinWidth` and `IContentHost.Narrowest`, which layout no longer asks for.

### Fixed

- A script reload, or anything else laying a root out again in the editor, no longer leaves its scene with unsaved changes: the RectTransforms layout drives are no longer recorded for undo.
- A frame no longer allocates 40 bytes going through the roots (a foreach through the read-only list of trees boxed its enumerator).

## [0.4.0] - 13/10/2026

### Added

- Text handlers, for a label's markup to show what their triggers find: a `SpanHandler` keeps it text, in a style of its own, with a node made from a prefab behind each line of it and perhaps tappable; an `ElementHandler` makes it a node from a prefab sitting in its line, and a `BlockHandler` a node across the width, each as big as its own layout makes it. A `Markup` asset holds handlers in order, then the parts of the built-in Markdown it keeps (`MarkdownFeatures`). A label reads its text with its own markup, else the project's default (**Project Settings > Timbo Jimbo > UI Next Text**), else the built-in Markdown.
- `ITextContent` tells a handler's prefab what a node made from it stands for; `TextSlot` shows a match's text (or the match as written, its argument or its list marker) in a label, in the font, size, colour and markup of the label it is in, following them as they change.
- The built-in Markdown has fenced code blocks, in a monospaced font the platform has, and quotes and list items, as blocks; code has a node behind it on each of its lines.
- An element handler with no prefab makes chips: a pill with the match as written in it (@name). `ImageHandler` shows images in the text by name (:name:), from a table of sprites, each as big as the text around it times its size, and leaves names it has none for as text; `TextImage` shows one in a prefab of your own. Both are handlers to add to a markup, not part of the built-in Markdown.
- Tappable elements: a tap on one raises `Label.MatchTapped` (and `OnMatchTapped`), as a tap on a tappable span other than a link does, and the nodes standing for a match held down (a chip, the backgrounds behind a span) are Pressed meanwhile. `Label.Document`, `Label.EffectiveMarkup`, and `Label.FontSizeAround`, the size of the text around a match, which elements sized in em follow.
- Content can host its node's floating children (`IContentHost`): a label measures, sizes and places what its text makes nodes of in the layout pass that lays the text out, so nothing is placed a frame late, and text whose width animates re-wraps with what is in it.
- `Markup.CodeFont`, the fonts the built-in Markdown shows code in: prewarm it on a loading screen (`FontStack.Prewarm`), so the first code shown does not hold up its frame, or show code of your own in it. The Showcase prewarms it as it starts.

### Changed

- `Label.Markup` (a `Markup`) replaces `Label.Theme`, and `Label.RichText` replaces `Label.Markdown`; scenes keep whether a label reads Markdown.
- A label draws its glyphs over its children, which its inline content is; its inline content is where its text puts it at once inside a change, as its glyphs are, rather than springing there.
- Text a span colours (a link) keeps its colour in a label of any colour, at the label's opacity; the two were multiplied, so links in dark text were near black.
- The Showcase has a Rich text page: Markdown, blocks, and handlers of its own (@mentions as chips, #tags, emoji images and ==highlights==), saying what was tapped. The Text page's Markdown case moved to it.

### Fixed

- A label drew its underlines and strikethroughs twice.

## [0.3.1] - 12/10/2026

### Added

- `Label.Theme`: the text theme its Markdown is read with (heading sizes, link and code colours, and inline handlers, such as an `InlineBackground` behind text of your own); none for the project's default theme, which UI Next Text 0.1.1 now hands texts that have none. The Showcase's Markdown case highlights ==text== with a theme of its own.

## [0.3.0] - 12/10/2026

### Changed

- Text comes from UI Next Text (`com.timbojimbo.uinext.text`), UI Text's engine moved over to UI Next, rather than UI Text, which brought UGUI and the old UI package with it. Its namespace is `TimboJimbo.UINext.Text`, and `TextBlockDirection`, `TextBlockOverflow` and `TextBlockSettings` are `TextDirection`, `TextOverflow` and `TextSettings`; a project sets its default font under **Project Settings > Timbo Jimbo > UI Next Text**.
- Text fields moved out to UI Next Text Input (`com.timbojimbo.uinext.textinput`), with the platform layer they type through; the assembly keeps its name, `TimboJimbo.UINext.TextInput`, and scenes keep their fields. The demos' text fields come on with it.

### Added

- `Label` draws what UI Text's TextBlock did:
  - underlines and strikethroughs, under the glyphs;
  - backgrounds behind code spans and blocks, and behind whatever else a theme's `InlineBackground` handlers match;
  - links: a tap on one raises `LinkClicked` (and `OnLinkClicked`) with its href, taking the tap from a button the label is in;
  - inline content: a theme's handlers' prefabs (UI Next nodes: an emote, a mention chip) placed over their tokens as nodes inside the label's, told what they stand for through `IInlineContent`, and used again as the text changes.
- Labels lay their text out again when a font stack, a theme or the text settings change.
- The Showcase's Markdown case shows underline, strike and links, which open in the browser.

## [0.2.0] - 12/10/2026

### Changed

- A scroll view's `ClearMargin` is the one room it keeps at its edges, as UIKit's content inset: what it holds starts that far in and scrolls that far past its end, and whatever brings something into view (`ScrollIntoView`, focus) lands that far in. `ScrollPadding` is gone; set the margin instead. Focus still keeps room for its ring where the margin is less.
- Clear insets (the nodes listed for an edge, and the margin) count only on the edges a scroll view scrolls along: top and bottom for a vertical one, left and right for a horizontal one. Across, what it holds is laid out to its size, and a margin there pushed it out past its edge.
- `UINode.GroupOpacity` is `CompositeGroup`, as SwiftUI's `compositingGroup`: a node and what is inside it drawn as one image first, so its opacity, blur and mask apply to that image as a whole. Scenes and prefabs keep the old field's value.
  - Blur and masks apply only to a composite group, rather than silently making a node one: without it they do nothing, are greyed out in the inspector (with a note when set anyway), and are warned of once at run time.
  - The inspector's Effects section is two: Compositing (composite group, blur, mask) and Colour (saturation, brightness).
- A floating node grows to fill, or takes a percentage of, the rect it is placed against: its parent's whole rect, padding and all (the root's safe rect, for one floating against the root), as Clay's floating elements and CSS's absolutely positioned boxes are. It was sized to the parent's content box, inside its padding, while placed against the whole rect, so a background that grew both ways came up short, and off-centre with uneven padding. To keep one inside the padding, put it in an inner node, or pad it as its parent is for what it holds.
- Editor: the node inspector is shorter, and laid out by what is reached for most:
  - always shown first: its size, beside a 3×3 grid of the common ones (fit, fixed or grow on each axis, where RectTransform's anchor presets sit; a click sets both, a fixed size starting at the size it is laid out at), where it floats, and its drawn offset, scale and rotation;
  - the rest in sections under Core's foldout headers, open or closed as last left (alt-click for all of them), a closed one saying how many of its fields are set: Children (direction, alignment as one 3×3 grid, gap, padding, wrap), Look, Safe area (with Avoids Keyboard), Input and focus, Matching;
  - Floating is one row of what it is attached to; while it floats, its point and the point it goes on are two 3×3 grids under it, with the offset;
  - fields that do nothing as the others are set are left out: a match anchor with no match name, the focus entry of anything but a section, the focus ring of a node never focused, and a box's far gradient colour and angle while it is no gradient. One that can differ by state (a token, variants) keeps what it decides shown;
  - Box, Label, Image View and Scroll View have the same headers, and a size offers its min and max only when it is not fixed, which they do not bound.
- Editor: UI States previews everywhere: a state turned on is on for every node in every tree, over its own (a state previewed turns the others of its group off), rather than on the selected node, so a page of controls can be seen pressed or focused at once.
- Editor: a value's field shows what is drawn. While a variant gives it (its states on, or previewed), the field shows the variant's value, read-only, tinted blue as Unity tints what an animation drives, its states in the tooltip. A value with variants has a mark at the end of its line naming them, which lists them when clicked, rather than the list always open; right-click a value to remove one.
- Editor: the state bar is the *UI States* toolbar among the Scene view's overlays, one dropdown lit with how many states are previewed, opening a popup that labels its groups Interaction, Modes and States, tells two of a name apart by their folder, and has Clear preview. Its icon is Unity's own unless States.png is in the package's Editor/Icons folder.

### Removed

- The Arena demo (a mobile game's menus). What it showed off stays in the engine (`Label`'s outline and shadow, `Box`'s gradient borders and shadows), and `PresentFrom`, which it shared with Circle, stays with the demos' shared scripts.

### Fixed

- `ListView` works out what is in view where its rows really are:
  - past its scroll view's padding, and anything above the rows;
  - drawn from under its top clear inset, so rows under a header floating over it are made;
  - `ScrollTo` puts a row under what covers the top, and the clear margin.
- Tab skips what is out of sight in a scroll view (another page of a pager, the hidden end of a row), as the arrows do, but goes on down the list focus is in, scrolling it.
- A text field being edited keeps itself clear of what covers its scroll view's edges (its clear insets), as well as above the keyboard.

### Added

- Editor: recording states, from the inspector rather than Unity's recording. Record, in the UI States popup, makes an edit to a value the value's variant for a state, on everything selected:
  - the state turned on last, or another that is on whose dot is clicked; with none on, edits change values as ever;
  - it stays on as states are turned on and off to compare and objects are selected one after another;
  - a value recorded for the state is tinted red; one a stronger state's variant gives is locked until that state is off; what is not a value cannot vary by state and is greyed out;
  - the toolbar turns red, naming the state, and the node's inspector has a red strip with Stop.
- Editor: a click in the Scene view selects the node drawn topmost under it, where it is drawn (moved, scrolled, presented), inside its clips, whether it takes the pointer or not; clicked again in the same place, the one under it. It is a custom picking pass (`HandleUtility.pickGameObjectCustomPasses`).
- Debug views: `UIDebug` in code (with `Changed`), and toggles in the Rendering Debugger's UI Next panel, in the editor and in development builds (kept on for the session in the editor):
  - Overdraw: what is behind the UI is darkened, and every pixel shaded adds heat, a quad's clear parts and clipped parts included; what is drawn into a layer shows through its quad.
  - Layer Bounds: an outline where each layer's image lies: cyan fading as one, magenta blurred, yellow masked, green a flight's end.
  - Focus Path: an outline round what has focus, and round every section and group it is in, out to its presentation.
- The Rendering Debugger's UI Next panel (SRP Core's DebugManager, as URP adds its own): the debug views, and what drawing the UI took in the last frame: draw calls (main, and layers'), primitives, layers and their images' memory, nodes, and springs moving.

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
