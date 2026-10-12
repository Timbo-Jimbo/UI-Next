# Getting started with UI Next

This walks through a first screen: a root, layout, drawing, values that change with state, animation, input,
presentations and focus, then the tools that help along the way. Every snippet builds UI from code with `UIBuild`;
everything it does can also be done in the inspector, since every node and drawable is a component.

## 1. Set up

- Unity 6000.5 or newer, with URP 17.5 as the render pipeline. There is no renderer feature to add: UI Next adds its
  render pass to every URP camera itself, and draws after the camera stack, in the Game and Scene views.
- The Input System, set as the active input handling (or Both).
- The package and its dependencies in `Packages/manifest.json`: `com.timbojimbo.uinext`, `com.timbojimbo.core` and
  `com.timbojimbo.ui.text`. Add `com.timbojimbo.ui.textinput` too for text fields; without it they are left out and
  the rest works.

## 2. A root

A root covers the screen and lays a tree of nodes out in points. Add one with **Add Component → Timbo Jimbo → UI
Next → Root** on an empty GameObject, or from code:

```csharp
using TimboJimbo.UINext;

var root = UIBuild.Root("Screen");
```

By default a root is laid out for a 390 × 844 point screen (`ReferenceSize`), scaled to fit the real one, and keeps its
content clear of the notch, rounded corners and home bar (its safe area edges). `ConstantScale` lays out at a fixed
number of pixels per point instead.

## 3. Nodes and layout

Every GameObject under the root with a **Node** (`UINode`) is laid out by a Clay-style solver: fit, grow, fixed and
percent sizes, padding, gaps, direction, alignment and wrapping.

```csharp
var page = root.Node.Column(12f).Pad(20f);              // children top to bottom, 12 points apart
var header = page.Child("Header").Row(8f).Align(AlignX.Left, AlignY.Center);
header.Width.Value = Sizing.Grow();                      // as wide as the page allows
var card = page.Child("Card").Sized(Sizing.Grow(), Sizing.Fixed(120f));
var badge = card.Child("Badge").Float(AttachPoint.RightTop, AttachPoint.RightTop, new Vector2(-8f, 8f));
```

`Float` takes a node out of the flow and pins one of its points to its parent's (or to another node, `FloatOn`).

## 4. Drawing

Drawables draw in their node: `Box` (a rounded rect with a fill, border and shadow, each a colour or a gradient),
`Label` (Markdown text, wrapped, with an optional outline and hard shadow) and `ImageView` (a sprite or texture).

```csharp
var box = card.Box(Color.white, 16f);                    // white, corners 16 points round
box.ShadowColor.Value = new Color(0f, 0f, 0f, 0.1f);
box.ShadowBlur.Value = 16f;
header.Child("Title").Label("**Inbox**  3 new", 20f);
```

For drawing of your own, subclass `UIDrawable` and add primitives through the `Painter` it is given.

## 5. Values and state

Every property is a `UIValue<T>`: a literal, a token or a binding, with variants for states.

```csharp
// Interaction states come from the nearest button: Pressed, Hovered, Focused, Selected, Disabled, FocusWithin.
box.Fill.When(InteractionStates.Pressed, new Color(0.9f, 0.9f, 0.95f));

// A binding follows what it reads.
var unread = new Observable<int>(3);
var count = header.Child("Count").Label("");
count.Text.Bind(() => unread.Value + " new");
unread.Value = 4;                                         // the label follows
```

Game states of your own are `UIState` assets (**Create → Timbo Jimbo → UI Next → State**), turned on for a node and
everything in it with `node.SetState(state, true)`. A state of the Mode kind (Dark, a size class) is what tokens switch
on: a **Color Token** asset (**Create → Timbo Jimbo → UI Next → Tokens → Color**) holds a colour, and one per mode, and
`box.Fill.Token = token` makes the box follow it. Turn the mode on at the root, and everything drawn from tokens follows.

## 6. Animation

Changes made inside `UI.Animate` spring from where things are drawn, at the speed they are moving, and so does
everything the change moves through layout:

```csharp
UI.Animate(UIAnimation.Snappy, () =>
{
    card.Height.Value = Sizing.Fixed(200f);               // the card grows, and what is below it moves down with it
    badge.Opacity.Value = 0f;
});
```

`UIAnimation.Default`, `Smooth`, `Snappy` and `Bouncy` are ready-made springs, or give a duration and a bounce. The
change returns a `UITransaction`, whose `Finished` event says when it has landed. A node shown or hidden inside a change
(`Display`) with a `DisplayTransition` fades, grows or slides in and out; two nodes with the same `MatchName` fly from
one to the other as one is hidden and the other shown.

## 7. Input

```csharp
var button = page.Child("Button").Pad(18f, 10f).Center();
button.Box(new Color(0.2f, 0.47f, 0.96f), 12f);
button.Child("Title").Label("Mark all read", 16f, Color.white, bold: true);
button.Pressable(() => unread.Value = 0);                 // tapped
```

`Pressable` also long presses and toggles; `Draggable` pans, `Pinchable` pinches and turns. `node.Scroll()` makes a
node scroll what it holds with iOS's physics, and `ListView` makes only the rows of a long list that are near view. A
scroll view keeps clear of what floats over its edges, with a margin on top, as UIKit's content inset: what it holds
starts clear of them and scrolls out from under them, and whatever it brings into view (focus moving to it, or
`ScrollIntoView`) lands clear of them. Only the edges it scrolls along count:

```csharp
var feed = page.Child("Feed").Sized(Sizing.Grow(), Sizing.Grow()).Column(12f);
var tabBar = root.Node.Child("Tab Bar").Row(8f).Pad(12f).Float(AttachPoint.CenterBottom, AttachPoint.CenterBottom, new Vector2(0f, -20f));
var scroll = feed.Scroll();
scroll.ClearOfBottom.Add(tabBar);                         // the tab bar floats over its bottom
scroll.ClearMargin.Value = Insets.All(12f);               // and 12 points more at its top and bottom
```

With UI Text Input installed, `node.TextField("Message", 16f)` (from the `TimboJimbo.UINext.TextInput` assembly) is a
text field typed into with the platform's own keyboard.

## 8. Presentations

A `Presentation` puts its node over everything: a sheet, an alert, a pushed page or a full-screen cover, with a dim
backdrop, back to dismiss, and a transition of its own. It is hidden until it is presented.

```csharp
var host = root.Node.Child("Saved").Center();
var dialog = host.Add<Presentation>();
dialog.Transition = PresentationTransition.Pop;
var panel = host.Child("Panel").Column(12f).Pad(20f);
panel.Box(Color.white, 16f);
panel.Child("Text").Label("Saved", 18f, bold: true);
panel.Child("OK").Pad(16f, 8f).Pressable(() => dialog.Dismiss());

var save = page.Child("Save").Pad(18f, 10f).Center();
save.Box(new Color(0.2f, 0.47f, 0.96f), 12f);
save.Child("Title").Label("Save", 16f, Color.white, bold: true);
save.Pressable(() => dialog.Present());                    // a node takes one Pressable: add handlers to its Tapped
```

Presenting and dismissing each happen in one animated change of their own, and raise `Presenting` and `Dismissing`
inside it, so code that joins them moves with the presentation.

## 9. Focus

Keyboards and gamepads move focus with nothing to set up: every button takes it, the arrows go to the next row (or
column) that way and the nearest in it, Tab goes in reading order, and Enter or a gamepad's A presses. To shape it:

- `node.FocusScope.Value = FocusScope.Section` makes a part of the screen focus moves into as one (a sidebar, a tab
  bar); `FocusEntry.Remembered` brings focus back to where it left.
- `FocusScope.Group` makes a part one stop until Submit enters it (a grid of cards); Back leaves it.
- `node.Focusable.Value = Focusability.Always` gives focus to what is not a button (a slider), and an
  `IFocusMoveHandler` on it takes the arrows it wants.
- `node.PrefersFocus.Value = true` says where focus starts.

The focus ring's look is on the root: `FocusRingColor`, `FocusRingWidth`, `FocusRingOffset`, `FocusRingRadius`.

## 10. Tools

- **Inspectors** in sections, small choices as buttons, and a link at the end of any value that can take a token.
- **The UI Next toolbar** (among the Scene view's overlays):
  - **States** previews interaction states, modes and states of your own on the selected node, in the Scene and Game
    views; nothing is saved, and Clear preview takes it all back.
  - **Overdraw**, **Layers** and **Focus path** are the debug views (also `UIDebug` in code).
  - At its end, what drawing the UI took in the last frame: draw calls, primitives and layers.
- **Scene view picking:** a click in the Scene view selects the node drawn under it; again, the one under that.
- **Demos:** **Tools → UI Next → Demos → Build Circle** (a small social app) and **Build Showcase** (one idea a case)
  build the demo scenes in `Demos/Scenes`.
