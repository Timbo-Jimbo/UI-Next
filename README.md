# Timbo Jimbo - UI Next

A UI stack owned end to end, as the design in `Design/UIStack.md` lays it out: layout, values that animate on springs from where they are drawn, a compositor and a URP render-graph renderer drawing instanced SDF primitives, input straight from the Input System with UIKit-style gestures, presentations, text fields typed into with the platform's own keyboard, and group opacity and blur. Nodes are GameObjects with RectTransforms, so prefabs, the hierarchy and the scene view work as they always have; everything drawn comes from one shader in one pass after the camera stack.

Requires Unity 6000.5 or newer, URP 17.5 (render graph), the Input System, and the Core and UI Text packages. Text fields also need UI Text Input; without it they are left out and the rest works.

## How it works

- **Reactive core.** `Observable<T>` and computations in three phases, model, layout and draw, run earliest first. Every value read while laying out or drawing is tracked, so a frame where nothing changed runs nothing.
- **Values.** Every property is a `UIValue<T>`: a literal, a token (a value per mode: Light, Dark, Sepia) or a binding (`Bind(() => ...)`), with variants for interaction states (`Pressed`, `Hovered`, `Focused`, `Selected`, `Disabled`) and states of your own (`UIState`). Each value has two faces: layout and game code read where it is going, drawing reads where it is drawn.
- **Motion.** `UI.Animate(animation, change)` makes a change and lays out at once, as UIKit's `layoutIfNeeded` inside an animation block does: everything the change moves, directly or through layout, springs from where it is drawn at the speed it has. The springs are a closed-form store; `UITransaction` says when a change has landed and whether it completed. A value that cannot interpolate (a label's text, a sprite) cross-fades instead. A value set directly lands at once, still.
- **Velocity.** Every value that interpolates knows how fast it moves (`Velocity`), however it is moved, and a spring set off on it starts at that speed. Gestures hand theirs on, as SwiftUI's `tracksVelocity`: what a drag's or a pinch's change callbacks set directly is sampled, so the change made as it lets go carries on at the finger's speed, and motions add up. Code never handles velocity.
- **Layout.** Clay's solver over flat arrays: fit, grow, fixed and percent sizes with min and max, padding, gaps, direction, alignment, wrapping, aspect ratio, floating against the parent, the root or another node, scroll containers, the safe area.
- **Drawing.** Drawables (`Box`, `Label`, `ImageView`, or your own `UIDrawable` drawing through a `Painter`) add primitives to their node. The compositor sorts them by tree order, ZIndex, the top layer and presentation levels, batches them eight textures to a draw call, and the render pass draws them after the camera stack in the Game and Scene views, clipped by up to four rounded rects. A node fading as one image (`GroupOpacity`) or blurred (`Blur`) is drawn to an image of its own first.
- **Input.** Pointers from the Input System are hit tested against what was drawn and heard by `IPointerHandler`s from the deepest node up; the first to claim a pointer cancels the rest. Keyboard and gamepad focus moves by drawn geometry; a text field being edited is focus's first responder.

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

- **Nodes:** `UINode` is layout and drawn transform (`Offset`, `Scale`, `Rotation`, `Opacity`, `ZIndex`, `Clips`, `TopLayer`) and filters (`GroupOpacity`, `Blur`, `Saturation`, `Brightness`, `Mask`); `UIRoot` sizes a tree to the screen. `UIBuild` builds trees from code; the inspector builds them by hand.
- **Coming and going:** a node shown or hidden inside `UI.Animate` stays drawn while it leaves; `DisplayTransition` fades, grows or slides it.
- **Matched geometry:** give two nodes the same `MatchName` (and, for many of a kind, a `MatchId` on what holds them): shown inside a change that hides the other, a node flies from where that one was drawn to its own place, a picture of the one cross-fading into the other in one rounded shape, as SwiftUI's `matchedGeometryEffect` drawn the way the web's view transitions are. Named parts inside fly on their own inside it; a presentation's `Presenting` and `Dismissing` are where to show and hide them.
- **Presentations:** `Presentation` presents a node over everything as a sheet, an alert or a pushed or full-screen page, with a dim backdrop, back to dismiss, and the screens under a cover culled. It presents and dismisses in one change of its own, raising `Presenting` and `Dismissing` inside it, so what code changes there moves with it.
- **Gestures:** `Pressable` (tap, long press, toggles, Interactable), `Draggable` (pan; `Takes` says which drags it takes, `TracksVelocity` whether it hands its speed on), `Pinchable` (two-finger pinch and turn), `ScrollView` (iOS deceleration, rubber band, paging, wheel; at an end, the rest of a drag goes on to an outer scroll view or a draggable that takes it, which hands it back as the finger turns), `ListView` (long lists that make only the rows near view).
- **Text fields:** `node.TextField("Message", 16f, multiline: true, maxLines: 5)`; `Submitted`, `TextChanged`, `Config` (content type, return key, capitalisation, autocorrection), `MaxLength`. A node with `AvoidsKeyboard` keeps what is inside it above the software keyboard.
- **Tools:** the state bar (*UI Next States* in the Scene view's overlays) previews interaction states, states and modes on the selected node in the Scene and Game views; `UISimulator` and `UICapture` run and capture frames outside play mode, for tests and screenshots.

## Demos

`Demos/Scenes/Circle.unity` is a small social app (feed, stories, a photo viewer, chats with a composer, settings with a dark mode): photos fly out of the feed and home again, thrown away in any direction or pinched; a chat row grows into its conversation and collapses back; stories grow out of their rings; Log out grows its dialog out of its button. `Demos/Scenes/Showcase.unity` shows the engine's features one idea a case, with a Store (items growing into a dialog) and an App Store (Today cards opening into stories that a pull down shrinks and closes). Every swipe to dismiss in them is one demo component, `SwipeToDismiss`. `Tools/UI Next/Demos/Build Circle` and `Build Showcase` rebuild them from code.

## Known limits

- Not tried on devices yet; the native keyboards, the IME and pinch with real fingers are untested.
- No backdrop blur (materials): the UI draws straight into the back buffer, which the render graph cannot read back.
- Masks are layers (flattened, then masked); coverage masks in tile memory are not built. No caching layers, no accessibility yet.
- A matched flight draws what flies flat: blur, masks and group opacity inside it apply again once it lands. A flight is cut to its rounded shape, so a shadow outside it shows again as it lands.
