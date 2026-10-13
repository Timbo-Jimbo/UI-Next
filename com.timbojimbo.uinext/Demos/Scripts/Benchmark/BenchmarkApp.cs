using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Benchmark
{
    /// <summary>
    /// UI Next's benchmark. As it starts it builds a screen as an app's might be: a feed of cards scrolling under a header,
    /// a tab bar, and pages and a sheet waiting to be presented over it, its colours tokens that a dark mode switches. Then
    /// it plays <see cref="Scenarios"/> over it in rounds, each warmed up and then measured frame by frame: what each of UI
    /// Next's markers and counters came to, the frame's CPU and GPU times, and what it allocated. Each scenario's measures
    /// are logged a line each, "UINB|" first, and kept in benchmark.txt in the app's data, for a script to read off a
    /// device. Its arguments come from the command line (on Android, the "unity" extra): -scenarios scroll,ticker,
    /// -rounds 3, -warmup 1 and -measure 4 (seconds), -cards 40, -pages 6, and -stay to stay open once done.
    /// </summary>
    public sealed class BenchmarkApp : MonoBehaviour
    {
        /// <summary>The scenarios, in the order they are played.</summary>
        public static readonly string[] Scenarios = { "idle", "scroll", "springs", "pulse", "ticker", "expand", "sheet", "page", "theme" };

        // How fast the feed is dragged, in points a second.
        private const float ScrollSpeed = 1500f;
        private const int DotCount = 40;
        private const int CardsPerPage = 8;

        private static readonly Color Accent = new Color32(0x34, 0x78, 0xF6, 0xFF);
        private static readonly Color PulseColour = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
        private static readonly Color Pressed = new(0.5f, 0.5f, 0.55f, 0.25f);
        private static readonly Color[] Palette =
        {
            new Color32(0xFF, 0x7E, 0xB6, 0xFF), new Color32(0x5C, 0x8D, 0xFF, 0xFF), new Color32(0xF2, 0xA9, 0x3B, 0xFF),
            new Color32(0x3F, 0xC1, 0xC9, 0xFF), new Color32(0xA7, 0x7B, 0xFF, 0xFF), new Color32(0xFF, 0x6B, 0x6B, 0xFF),
        };

        private static readonly string[] Names = { "Ada Park", "Ben Okafor", "Chloé Martin", "Dev Patel", "Eun-ji Kim", "Felix Braun", "Gia Rossi", "Hugo Silva" };

        private static readonly string[] Bodies =
        {
            "Spent the morning sketching the new onboarding flow. Three screens instead of seven, and every one of them springs into the next as you swipe.",
            "Shipping the dark theme today. Every colour comes from a token, so the whole app turns over in one change, and nothing flashes on the way.",
            "Tried laying the gallery out as a wrapped row. Photos keep their shape, captions fit under them, and the grid reflows as the window turns.",
            "Note to self: a sheet that follows your finger down feels twice as good as one that only closes when you tap the dim behind it.",
        };

        // What it was asked to do.
        private readonly List<string> _scenarios = new();
        private int _rounds = 3;
        private float _warmup = 1f;
        private float _measure = 4f;
        private int _cards = 40;
        private int _pages = 6;
        private bool _stay;

        // What it built, and what the scenarios move.
        private UIRoot _root;
        private UIState _dark;
        private ColorToken _background, _card, _text, _secondary, _chip;
        private Texture2D[] _photos;
        private ScrollView _feed;
        private Label _ticker;
        private Box _pulse;
        private readonly List<UINode> _dots = new();
        private readonly List<Label> _bodies = new();
        private readonly List<Presentation> _pageList = new();
        private Presentation _sheet;
        private readonly string[] _counts = new string[1000];

        // Whether the scenario playing now carries on.
        private bool _running;

        private readonly StringBuilder _report = new();

        private IEnumerator Start()
        {
            Application.targetFrameRate = 120;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            // Upright throughout: a turn mid-run would lay everything out again, at another size.
            Screen.orientation = ScreenOrientation.Portrait;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            ReadArguments(Environment.GetCommandLineArgs());
            _root = GetComponent<UIRoot>();
            Build();

            // Fonts, glyphs, shaders and first layouts settle before anything is measured.
            yield return Wait(2f);
            int nodes = _root.GetComponentsInChildren<UINode>(true).Length;
            Log(string.Format(CultureInfo.InvariantCulture,
                "UINB|start|device={0}|cpu={1}|gpu={2}|api={3}|screen={4}x{5}@{6:F0}|nodes={7}|rounds={8}|warmup={9}|measure={10}|unity={11}",
                SystemInfo.deviceModel, SystemInfo.processorType, SystemInfo.graphicsDeviceName, SystemInfo.graphicsDeviceType,
                Screen.width, Screen.height, Screen.currentResolution.refreshRateRatio.value, nodes, _rounds, _warmup, _measure,
                Application.unityVersion));

            using var recorder = new BenchmarkRecorder(Mathf.CeilToInt(_measure * 200f) + 64);
            for (int round = 1; round <= _rounds; round++)
            {
                foreach (var scenario in _scenarios)
                {
                    _running = true;
                    var playing = StartCoroutine(Play(scenario));
                    yield return Wait(_warmup);
                    recorder.Begin();
                    float end = Time.realtimeSinceStartup + _measure;
                    while (Time.realtimeSinceStartup < end)
                    {
                        recorder.Frame();
                        yield return null;
                    }
                    recorder.End();
                    _running = false;
                    // It puts back what it changed before the next begins.
                    yield return playing;
                    Log($"UINB|r{round}|{scenario}|screen|{Screen.width}x{Screen.height}");
                    recorder.Report($"UINB|r{round}|{scenario}|", Log);
                    Save();
                    yield return Wait(0.5f);
                }
            }
            Log("UINB|done");
            Save();
            if (!_stay && !Application.isEditor)
                Application.Quit();
        }

        private void ReadArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-scenarios" when next != null:
                        foreach (var name in next.Split(','))
                            if (Array.IndexOf(Scenarios, name.Trim()) >= 0)
                                _scenarios.Add(name.Trim());
                        break;
                    case "-rounds" when next != null: int.TryParse(next, out _rounds); break;
                    case "-warmup" when next != null: float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out _warmup); break;
                    case "-measure" when next != null: float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out _measure); break;
                    case "-cards" when next != null: int.TryParse(next, out _cards); break;
                    case "-pages" when next != null: int.TryParse(next, out _pages); break;
                    case "-stay": _stay = true; break;
                }
            }
            if (_scenarios.Count == 0)
                _scenarios.AddRange(Scenarios);
            _rounds = Mathf.Max(1, _rounds);
            _pages = Mathf.Max(1, _pages);
        }

        private void Log(string line)
        {
            Debug.Log(line);
            _report.AppendLine(line);
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "benchmark.txt"), _report.ToString());
            }
            catch (IOException e)
            {
                Debug.LogWarning("UINB|could not save the report: " + e.Message);
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
                yield return null;
        }

        // ── Scenarios ────────────────────────────────────────────────────────────

        private IEnumerator Play(string scenario) => scenario switch
        {
            "scroll" => Scroll(),
            "springs" => Springs(),
            "pulse" => Pulse(),
            "ticker" => Ticker(),
            "expand" => Expand(),
            "sheet" => Presenting(_sheet, 0.75f),
            "page" => Presenting(_pageList[0], 0.9f),
            "theme" => Theme(),
            _ => Idle(),
        };

        // Nothing changes: what a frame costs with nothing to do.
        private IEnumerator Idle()
        {
            while (_running)
                yield return null;
        }

        // The feed dragged at a steady speed down to its end and back, as a finger moves it.
        private IEnumerator Scroll()
        {
            float y = 0f, speed = ScrollSpeed;
            while (_running)
            {
                float range = _feed.Range.y;
                y += speed * Time.unscaledDeltaTime;
                if (y >= range)
                {
                    y = range;
                    speed = -ScrollSpeed;
                }
                else if (y <= 0f)
                {
                    y = 0f;
                    speed = ScrollSpeed;
                }
                _feed.ContentOffset = new Vector2(0f, y);
                yield return null;
            }
            _feed.ContentOffset = Vector2.zero;
        }

        // Dots bouncing on springs, more set off every so often, so dozens move at once.
        private IEnumerator Springs()
        {
            int next = 0;
            while (_running)
            {
                UI.Animate(UIAnimation.Bouncy, () =>
                {
                    for (int i = 0; i < 10; i++, next++)
                    {
                        var dot = _dots[next % _dots.Count];
                        dot.Offset.Value = new Vector2(0f, dot.Offset.Peek.y > 0f ? -8f : 8f);
                    }
                });
                yield return Wait(0.15f);
            }
            foreach (var dot in _dots)
            {
                dot.Offset.Value = Vector2.zero;
                dot.Offset.Settle();
            }
        }

        // One colour springing back and forth: a single node drawn again each frame, nothing laid out.
        private IEnumerator Pulse()
        {
            bool on = false;
            while (_running)
            {
                on = !on;
                UI.Animate(new UIAnimation(0.5f), () => _pulse.Fill.Value = on ? PulseColour : Accent);
                yield return Wait(0.5f);
            }
        }

        // A count in the header going up every frame: its label measured, and the tree laid out, each frame.
        private IEnumerator Ticker()
        {
            int count = 0;
            while (_running)
            {
                _ticker.Text.Value = _counts[++count % _counts.Length];
                yield return null;
            }
        }

        // The first cards' text opening out to all of it and back in a change: laid out once each way, with everything
        // below springing to its new place.
        private IEnumerator Expand()
        {
            bool open = false;
            while (_running)
            {
                open = !open;
                UI.Animate(UIAnimation.Smooth, () =>
                {
                    for (int i = 0; i < 3 && i < _bodies.Count; i++)
                        _bodies[i].MaxLines.Value = open ? 0 : 2;
                });
                yield return Wait(0.6f);
            }
            for (int i = 0; i < 3 && i < _bodies.Count; i++)
                _bodies[i].MaxLines.Value = 2;
        }

        // A presentation coming up and going away again, over and over.
        private IEnumerator Presenting(Presentation presentation, float every)
        {
            while (_running)
            {
                presentation.Present();
                yield return Wait(every);
                presentation.Dismiss();
                yield return Wait(every);
            }
        }

        // Dark mode on and off: every colour from a token resolves again and springs to its new value.
        private IEnumerator Theme()
        {
            bool dark = false;
            while (_running)
            {
                dark = !dark;
                _root.Node.SetState(_dark, dark);
                yield return Wait(0.8f);
            }
            if (dark)
            {
                _root.Node.SetState(_dark, false);
                yield return Wait(0.6f);
            }
        }

        // ── The screen ───────────────────────────────────────────────────────────

        private void Build()
        {
            _dark = UIState.Create("Dark", StateKind.Mode, enter: new UIAnimation(0.4f), exit: new UIAnimation(0.4f));
            _background = Token(new Color32(0xF2, 0xF2, 0xF7, 0xFF), new Color32(0x0C, 0x0C, 0x10, 0xFF));
            _card = Token(Color.white, new Color32(0x1C, 0x1C, 0x22, 0xFF));
            _text = Token(new Color32(0x12, 0x12, 0x16, 0xFF), new Color32(0xF2, 0xF2, 0xF7, 0xFF));
            _secondary = Token(new Color32(0x6E, 0x6E, 0x78, 0xFF), new Color32(0x9A, 0x9A, 0xA4, 0xFF));
            _chip = Token(new Color32(0xEE, 0xEE, 0xF3, 0xFF), new Color32(0x2A, 0x2A, 0x32, 0xFF));
            _photos = new[] { Photo(Palette[0], Palette[1]), Photo(Palette[2], Palette[5]), Photo(Palette[3], Palette[4]), Photo(Palette[1], Palette[3]) };
            for (int i = 0; i < _counts.Length; i++)
                _counts[i] = i.ToString(CultureInfo.InvariantCulture);

            var app = _root.Node.Column();
            Fill(app, _background);

            var header = app.Child("Header").Row(10f).Pad(new Insets(16f, 16f, 12f, 10f)).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            Fill(header, _card);
            Text(header, "Title", "Benchmark", 22f, _text, bold: true).Node.Width.Value = Sizing.Grow();
            _ticker = Text(header, "Ticker", "0", 15f, _secondary);
            _pulse = header.Child("Pulse").Sized(Sizing.Fixed(14f), Sizing.Fixed(14f)).Box(Accent, 7f);

            var springs = app.Child("Springs").Row(6f).Pad(16f, 10f).Align(AlignX.Center, AlignY.Center);
            springs.Width.Value = Sizing.Grow();
            springs.Wrap.Value = Wrap.Lines;
            for (int i = 0; i < DotCount; i++)
            {
                var dot = springs.Child("Dot").Sized(Sizing.Fixed(10f), Sizing.Fixed(10f));
                dot.Box(Palette[i % Palette.Length], 5f);
                _dots.Add(dot);
            }

            var feed = app.Child("Feed").Sized(Sizing.Grow(), Sizing.Grow()).Column(12f).Pad(new Insets(12f, 12f, 12f, 24f));
            _feed = feed.Scroll();
            for (int i = 0; i < _cards; i++)
                _bodies.Add(Card(feed, i));

            var tabs = app.Child("Tabs").Row().Pad(new Insets(8f, 8f, 6f, 6f));
            tabs.Width.Value = Sizing.Grow();
            Fill(tabs, _card);
            foreach (var name in new[] { "Home", "Search", "Inbox", "Profile" })
            {
                var tab = tabs.Child(name).Column(3f).Align(AlignX.Center, AlignY.Center);
                tab.Width.Value = Sizing.Grow();
                tab.Child("Icon").Sized(Sizing.Fixed(22f), Sizing.Fixed(22f)).Box(name == "Home" ? Accent : Palette[1], 6f);
                Text(tab, "Title", name, 11f, _secondary);
                tab.Pressable();
            }

            for (int p = 0; p < _pages; p++)
                _pageList.Add(Page(app, p));
            _sheet = Sheet(app);
        }

        // A post: who and when, its text, a photo every other one, and its actions.
        private Label Card(UINode parent, int index)
        {
            var card = parent.Child("Card").Column(10f).Pad(14f);
            card.Width.Value = Sizing.Grow();
            var box = Fill(card, _card, 16f);
            box.ShadowColor.Value = new Color(0f, 0f, 0f, 0.08f);
            box.ShadowBlur.Value = 12f;
            box.ShadowOffset.Value = new Vector2(0f, 3f);

            var header = card.Child("Header").Row(10f).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            string name = Names[index % Names.Length];
            var avatar = header.Child("Avatar").Sized(Sizing.Fixed(40f), Sizing.Fixed(40f)).Center();
            avatar.Box(Palette[index % Palette.Length], 20f);
            avatar.Child("Initials").Label(name.Substring(0, 1) + name.Substring(name.IndexOf(' ') + 1, 1), 15f, Color.white, bold: true);
            var names = header.Child("Names").Column(2f);
            names.Width.Value = Sizing.Grow();
            Text(names, "Name", name, 16f, _text, bold: true);
            Text(names, "Handle", "@" + name.Split(' ')[0].ToLowerInvariant() + index, 13f, _secondary);
            Text(header, "Time", (index % 59 + 1) + "m", 13f, _secondary);

            var body = Text(card, "Body", Bodies[index % Bodies.Length], 15f, _text);
            body.MaxLines.Value = 2;
            body.Node.Width.Value = Sizing.Grow();

            if (index % 2 == 0)
            {
                var photo = card.Child("Photo").Sized(Sizing.Grow(), Sizing.Fixed(180f));
                var image = photo.Add<Image>();
                image.Texture.Value = _photos[index / 2 % _photos.Length];
                image.Fit.Value = ImageFit.Cover;
                image.CornerRadii.Value = 12f;
            }

            var actions = card.Child("Actions").Row(8f);
            foreach (var action in new[] { "Like " + (index * 7 % 90 + 3), "Reply " + (index * 3 % 20 + 1), "Share" })
            {
                var button = actions.Child(action).Pad(12f, 6f).Center();
                Fill(button, _chip, 14f).Fill.When(InteractionStates.Pressed, Pressed);
                Text(button, "Title", action, 13f, _text);
                button.Pressable();
            }
            return body;
        }

        // A page pushed over everything: a header, and cards scrolling under it. Out of layout until it is presented.
        private Presentation Page(UINode app, int index)
        {
            var host = app.Child("Page " + (index + 1)).Column();
            var page = host.Add<Presentation>();
            page.Style = PresentationStyle.Cover;
            page.Transition = PresentationTransition.Push;
            page.Dim.Value = new Color(0f, 0f, 0f, 0.15f);
            page.Animation = new UIAnimation(0.4f);
            var content = host.Child("Content").Sized(Sizing.Grow(), Sizing.Grow()).Column();
            content.IgnoresSafeArea.Value = Edges.All;
            Fill(content, _background);
            var header = content.Child("Header").Row(8f).Pad(new Insets(16f, 16f, 10f, 10f)).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            Fill(header, _card);
            Text(header, "Back", "‹ Back", 17f, _text).Node.Pressable();
            Text(header, "Title", "Page " + (index + 1), 17f, _text, bold: true);
            var body = content.Child("Body").Sized(Sizing.Grow(), Sizing.Grow()).Column(12f).Pad(12f);
            body.Scroll();
            for (int i = 0; i < CardsPerPage; i++)
                Card(body, 100 + index * CardsPerPage + i);
            return page;
        }

        // A sheet sliding up over the feed: a title, and people to share with.
        private Presentation Sheet(UINode app)
        {
            var host = app.Child("Sheet").Column().Align(AlignX.Left, AlignY.Bottom);
            var sheet = host.Add<Presentation>();
            var content = host.Child("Card").Column(12f).Pad(new Insets(20f, 20f, 12f, 34f));
            content.Width.Value = Sizing.Grow();
            content.IgnoresSafeArea.Value = Edges.Bottom;
            Fill(content, _card, new CornerRadii(24f, 24f, 0f, 0f));
            content.Child("Handle").Sized(Sizing.Fixed(36f), Sizing.Fixed(5f)).Box(new Color(0.47f, 0.47f, 0.5f, 0.4f), 2.5f);
            Text(content, "Title", "Share with", 20f, _text, bold: true);
            for (int i = 0; i < 6; i++)
            {
                var row = content.Child("Person").Row(12f).Align(AlignX.Left, AlignY.Center);
                row.Width.Value = Sizing.Grow();
                row.Child("Avatar").Sized(Sizing.Fixed(36f), Sizing.Fixed(36f)).Box(Palette[i % Palette.Length], 18f);
                Text(row, "Name", Names[i], 16f, _text);
                row.Pressable();
            }
            return sheet;
        }

        private ColorToken Token(Color light, Color dark)
        {
            var token = ScriptableObject.CreateInstance<ColorToken>();
            token.hideFlags = HideFlags.DontSave;
            token.Default = light;
            token.SetMode(_dark, dark);
            return token;
        }

        private static Box Fill(UINode node, ColorToken fill, CornerRadii radii = default)
        {
            var box = node.Box(Color.white, radii);
            box.Fill.Token = fill;
            return box;
        }

        private static Label Text(UINode parent, string name, string text, float size, ColorToken colour, bool bold = false)
        {
            var label = parent.Child(name).Label(text, size, bold: bold);
            label.Color.Token = colour;
            return label;
        }

        // A stand-in photo: two colours blended corner to corner, lit from its top left.
        private static Texture2D Photo(Color from, Color to)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Benchmark Photo",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float light = 1f - 0.4f * Mathf.Clamp01(new Vector2(x - 30f, y - 30f).magnitude / size);
                    var colour = Color.Lerp(from, to, (x + y) / (2f * (size - 1))) * light;
                    colour.a = 1f;
                    pixels[y * size + x] = colour;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
