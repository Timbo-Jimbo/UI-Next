# Timbo Jimbo - UI Next

A UI stack owned end to end, as the design in `Design/UIStack.md` lays it out: layout, values that animate on springs from where they are drawn, a compositor and a URP render-graph renderer drawing instanced SDF primitives, input straight from the Input System with UIKit-style gestures, presentations, text fields typed into with the platform's own keyboard, and composite groups, faded, blurred and masked as one image. Nodes are GameObjects with RectTransforms, so prefabs, the hierarchy and the scene view work as they always have; everything drawn comes from one shader in one pass after the camera stack.

Requires Unity 6000.5 or newer, URP 17.5 (render graph), the Input System, and the Core and [UI Next Text](https://github.com/Timbo-Jimbo/UI-Next.Text) packages. Text fields come with [UI Next Text Input](https://github.com/Timbo-Jimbo/UI-Next.TextInput); without it they are left out and the rest works.

## Installation

Core comes from [OpenUPM](https://openupm.com); UI Next Text, UI Next and UI Next Text Input come from GitHub.

1. Add the Scoped Registry: open **Edit > Project Settings > Package Manager** and add a new Scoped Registry (or the scope
   to one you have):
	- Name: `OpenUPM`
	- URL: `https://package.openupm.com/`
	- Scope(s): `com.timbojimbo`
2. Open **Window > Package Manager**, click Add, choose **Add package from git URL...** and paste, in turn:
	- `https://github.com/Timbo-Jimbo/UI-Next.Text.git?path=com.timbojimbo.uinext.text#0.1.1`
	- `https://github.com/Timbo-Jimbo/UI-Next.git?path=com.timbojimbo.uinext#0.3.1`
	- for text fields, `https://github.com/Timbo-Jimbo/UI-Next.TextInput.git?path=com.timbojimbo.uinext.textinput#0.1.0`

New to it? [Getting started](com.timbojimbo.uinext/Documentation~/GettingStarted.md) walks through a first screen: a root, layout, drawing, values and states, animation, input, presentations, focus and the tools.

## How it works

- **Reactive core.** `Observable<T>` and computations in three phases, model, layout and draw, run earliest first. Every value read while laying out or drawing is tracked, so a frame where nothing changed runs nothing.
- **Values.** Every property is a `UIValue<T>`: a literal, a token (a value per mode: Light, Dark, Sepia) or a binding (`Bind(() => ...)`), with variants for interaction states (`Pressed`, `Hovered`, `Focused`, `Selected`, `Disabled`, `FocusWithin`) and states of your own (`UIState`). Each value has two faces: layout and game code read where it is going, drawing reads where it is drawn.
- **Motion.** `UI.Animate(animation, change)` makes a change and lays out at once, as UIKit's `layoutIfNeeded` inside an animation block does: everything the change moves, directly or through layout, springs from where it is drawn at the speed it has. The springs are a closed-form store; `UITransaction` says when a change has landed and whether it completed. A value that cannot interpolate (a label's text, a sprite) cross-fades instead. A value set directly lands at once, still.
- **Velocity.** Every value that interpolates knows how fast it moves (`Velocity`), however it is moved, and a spring set off on it starts at that speed. Gestures hand theirs on, as SwiftUI's `tracksVelocity`: what a drag's or a pinch's change callbacks set directly is sampled, so the change made as it lets go carries on at the finger's speed, and motions add up. Code never handles velocity.
- **Layout.** Clay's solver over flat arrays: fit, grow, fixed and percent sizes with min and max, padding, gaps, direction, alignment, wrapping, aspect ratio, floating against the parent, the root or another node, scroll containers, the safe area.
- **Drawing.** Drawables (`Box`, `Label`, `ImageView`, or your own `UIDrawable` drawing through a `Painter`) add primitives to their node. The compositor sorts them by tree order, ZIndex, the top layer and presentation levels, batches them eight textures to a draw call, and the render pass draws them after the camera stack in the Game and Scene views, clipped by up to four rounded rects. A box's fill, border and shadow can each be a gradient, and a label can outline its text and cast a hard shadow, as game UI writes it. A label reads its text with its markup, the built-in Markdown unless it has another: its spans are drawn in their own styles, with underlines and strikethroughs, and what goes behind them (as behind code), its elements (a chip, an image) and blocks (a code block, a quote, a list item) are nodes made from prefabs, sized and placed as the text is laid out, and a tap on a link raises `LinkClicked`. A composite group (`CompositeGroup`, as SwiftUI's `compositingGroup`) is drawn to an image of its own first while it fades, is blurred (`Blur`) or is masked (`Mask`), so it fades as one; blur and masks apply to nothing else.
- **Input.** Pointers from the Input System are hit tested against what was drawn and heard by `IPointerHandler`s from the deepest node up; the first to claim a pointer cancels the rest. Keyboard and gamepad focus is part of the tree: it moves by drawn geometry and Tab goes in reading order, shaped by sections and groups on the nodes; a text field being edited is focus's first responder.

## Usage

```csharp
var root = UIBuild.Root("Screen");
var page = root.Node.Column(12f).Pad(20f);
page.Box(new Color(0.95f, 0.95f, 0.97f));

var title = page.Child("Title").Label("Hello", 28f, bold: true);
var taps = new Observable<int>(0);
page.Child("Count").Label("").Text.Bind(() => $"Tapped {taps.Value} times");

var button = page.Child("Button").Pad(18f, 10f).Center();
var fill = button.Box(new Color(0.2f, 0.47f, 0.96f), 12f);
fill.Fill.When(InteractionStates.Pressed, new Color(0.15f, 0.35f, 0.75f));
button.Child("Text").Label("Grow", 16f, Color.white, bold: true);
button.Pressable(() =>
{
    taps.Value++;
    // The title grows on a spring, and everything below it moves with it.
    UI.Animate(UIAnimation.Bouncy, () => title.FontSize.Value += 4f);
});
```

- **Nodes:** `UINode` is layout and drawn transform (`Offset`, `Scale`, `Rotation`, `Opacity`, `ZIndex`, `Clips`, `TopLayer`), compositing (`CompositeGroup`, which `Blur` and `Mask` apply to) and colour filters (`Saturation`, `Brightness`); `UIRoot` sizes a tree to the screen. `UIBuild` builds trees from code; the inspector builds them by hand.
- **Coming and going:** a node shown or hidden inside `UI.Animate` stays drawn while it leaves; `DisplayTransition` fades, grows or slides it.
- **Matched geometry:** give two nodes the same `MatchName` (and, for many of a kind, a `MatchId` on what holds them): shown inside a change that hides the other, a node flies from where that one was drawn to its own place, a picture of the one cross-fading into the other, as SwiftUI's `matchedGeometryEffect` drawn the way the web's view transitions are. Each end's own box, border and shadow is laid out at the flight's rect; what is inside it is scaled evenly into the flight along the axis the two ends are most alike in, lined up by `MatchAnchor`, and cut to a window whose corners go from one end's `ClipRadii` to the other's. Named parts inside fly on their own; a presentation's `Presenting` and `Dismissing` are where to show and hide them.
- **Presentations:** `Presentation` presents a node over everything as a sheet, an alert or a pushed or full-screen page, with a dim backdrop, back to dismiss, and the screens under a cover culled. It presents and dismisses in one change of its own, raising `Presenting` and `Dismissing` inside it, so what code changes there moves with it.
- **Focus:** buttons take keyboard and gamepad focus. The arrows and the d-pad go to the next row that way, as drawn (the next column, for left and right), and the nearest in it, staying in the scroll view focus is in until its end; Tab and Shift Tab go in reading order, along rows the way `UI.ReadingDirection` reads (the UI culture's, unless set); Enter or a gamepad's A presses. A node's `FocusScope` makes it a section (focus moves in and out freely, a move towards it entering it as one, at the nearest inside or, with `FocusEntry` set to Remembered, where focus last was) or a group (one stop until Enter goes in; Escape comes out); `PrefersFocus` says where focus starts; `Focusable` makes anything a stop (Always) or a button not one (Never), and an `IFocusMoveHandler` takes the arrows of its own (a slider), saying when it has no move left that way. A key that goes nowhere shakes the focus ring no. A presentation takes focus as it comes up and gives it back to what opened it as it goes; focus lost goes to the nearest. It shows only after a key, as `Focused` on what has it and `FocusWithin` on everything it is in, and as a ring round what has it, out past its edge, in its root's `FocusRingColor`, `FocusRingWidth`, `FocusRingOffset` and `FocusRingRadius` (`FocusRing` off on a node that shows focus its own way); the first key after a pointer only shows it.
- **Gestures:** `Pressable` (tap, long press, toggles, Interactable), `Draggable` (pan; `Takes` says which drags it takes, `TracksVelocity` whether it hands its speed on), `Pinchable` (two-finger pinch and turn), `ScrollView` (iOS deceleration, rubber band, paging, wheel; a new drag past an end it is at goes to an outer scroll view; with `AtEnd` set to PassOn, the rest of a drag past an end goes on to an outer scroll view or a draggable that takes it, which hands it back as the finger turns; `ClearOfTop` and the other three edges keep what it holds clear of nodes floating over them, a tab bar or a header, with `ClearMargin` of room on top, along the edges it scrolls along, and whatever brings something into view, `ScrollIntoView` or focus, lands clear of them), `ListView` (long lists that make only the rows near view).
- **Rich text:** a label's text is read with its `Markup` (**Create > Timbo Jimbo > UI Next > Text > Markup**): its own handlers in order, then the parts of the built-in Markdown it keeps. A `SpanHandler` styles what its trigger finds and can put a prefab behind each line of it; an `ElementHandler` makes it a node in its line (a mention chip) and a `BlockHandler` a node across the width (a callout), each as big as its own layout makes it, in the same layout pass as the text. Their prefabs' `ITextContent` components are told the match; a `TextSlot` on a label in one shows the match's text, Markdown and all, in the font, size and colour of the label it is in. An element handler with no prefab makes chips, and an `ImageHandler` shows images by name (`:heart:`). A tap on a tappable span or element raises `MatchTapped`, what stands for it Pressed while it is held.
- **Text fields** (UI Next Text Input): `node.TextField("Message", 16f, multiline: true, maxLines: 5)`; `Submitted`, `TextChanged`, `Config` (content type, return key, capitalisation, autocorrection), `MaxLength`. A node with `AvoidsKeyboard` keeps what is inside it above the software keyboard.
- **Tools:** inspectors in sections under foldout headers, a closed one saying how many of its fields are set, a node's size always shown first beside a 3×3 grid of the common sizes, with small choices as lit buttons and a link at the end of each value that can take a token (click to pick one, click again to unlink); the *UI States* toolbar among the Scene view's overlays previews interaction states, states and modes everywhere in the Scene and Game views, and records edits in the inspector into the state turned on last, each value showing what is drawn (from a variant, recorded, or locked while a stronger state wins); the Rendering Debugger's *UI Next* panel, in the editor and in development builds, turns the debug views on (overdraw, layer bounds, the focus path) and says what drawing the UI took in the last frame; a click in the Scene view selects the node drawn under it; `UISimulator` and `UICapture` run and capture frames outside play mode, for tests and screenshots.

## Demos

`Demos/Scenes/Circle.unity` is a small social app with the screens the UI package's mobile demo has: a feed with stories, posts, reactions and a reaction picker, comments, profiles and a compose card; chats with a composer; and Me, with a share card and a settings page holding a control of each kind (a text field, switches, a segmented control, a stepper, swatches, a slider, a list with a check mark, a storage bar). Everything opens out of what was tapped and goes back into it: photos fly out of the feed and home again, thrown away in any direction or pinched; a chat row grows into its conversation; stories grow out of their rings and step on with a tap or a key; a post's author rises into their profile; cards grow out of their buttons, and Log out grows its dialog out of its button. Keyboards and gamepads move focus through all of it. `Demos/Scenes/Showcase.unity` shows the engine's features one idea a case, with a Store (items growing into a dialog), an App Store (Today cards opening into stories that a pull down shrinks and closes) and a Focus page to try keyboard and gamepad focus on, with a readout of where it is. Every swipe to dismiss in them is one demo component, `SwipeToDismiss`, and every popup that grows out of its button another, `PresentFrom`. `Tools/UI Next/Demos/Build Circle` and `Build Showcase` rebuild them from code.

## Known limits

- Not tried on devices yet; the native keyboards, the IME and pinch with real fingers are untested.
- No backdrop blur (materials): the UI draws straight into the back buffer, which the render graph cannot read back.
- Masks are layers (flattened, then masked); coverage masks in tile memory are not built. No caching layers, no accessibility yet.
- A matched flight draws what flies flat: a composite group inside it fades, blurs and masks as one again once it lands. A flight is cut to its rounded shape, so a shadow outside it shows again as it lands.
