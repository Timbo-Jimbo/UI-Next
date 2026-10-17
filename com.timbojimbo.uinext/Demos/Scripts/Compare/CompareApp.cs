#if UINEXT_UGUI
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TimboJimbo.UINext.Demos.Benchmark;
using TimboJimbo.UINext.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;

namespace TimboJimbo.UINext.Demos.Compare
{
    /// <summary>
    /// UI Next against UGUI: one screen, a header with a count in it over a feed of cards (an avatar, a name and a handle,
    /// a few lines of text, three chips), built as it starts in one or the other, chosen by its arguments (on Android, the
    /// "unity" extra): -system uinext, ugui, or none for the screen with nothing on it. It plays use cases over it as the
    /// benchmark does, in rounds, each warmed up and measured frame by frame, and logs what it measured a line a measure,
    /// "UINB|" first: idle, scroll (the feed dragged at a steady speed), ticker (the count going up every frame), recolor
    /// (every card's colour changing every frame), expand (the first three cards' text growing and shrinking twice a
    /// second) and resize (the feed's margins changing every frame, so every card wraps its text again). UGUI is built as
    /// it usually is: one Screen Space Overlay canvas scaled as UI Next's root is (390 by 844 points, matched by width),
    /// layout groups and a content size fitter, a scroll rect clipped by a RectMask2D, TextMeshPro text in the font UI
    /// Next draws with (the OS's default), and rounded corners from nine-sliced sprites. Neither has shadows or photos.
    /// With -ballast 2 it spins for 2 ms every frame, the same for both, which holds a phone's CPU clock up as one with
    /// more to do would be, so that the two are measured at the same clock rather than the lighter at a lower one.
    /// </summary>
    public sealed class CompareApp : MonoBehaviour
    {
        /// <summary>The use cases, in the order they are played.</summary>
        public static readonly string[] Scenarios = { "idle", "scroll", "ticker", "recolor", "expand", "resize" };

        private const float ScrollSpeed = 1500f;

        private static readonly Color Background = new Color32(0xF2, 0xF2, 0xF7, 0xFF);
        private static readonly Color CardColour = Color.white;
        private static readonly Color TextColour = new Color32(0x12, 0x12, 0x16, 0xFF);
        private static readonly Color Secondary = new Color32(0x6E, 0x6E, 0x78, 0xFF);
        private static readonly Color ChipColour = new Color32(0xEE, 0xEE, 0xF3, 0xFF);
        private static readonly Color Tint = new Color32(0xDD, 0xE8, 0xFF, 0xFF);
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

        private const string Short = "Short and sweet.";

        // The engine markers UGUI's work shows under, recorded beside UI Next's.
        private static readonly string[] UguiMarkers =
        {
            "PostLateUpdate.PlayerUpdateCanvases", "Canvas.SendWillRenderCanvases", "Canvas.BuildBatch", "Canvas.UpdateBatches",
            "PostLateUpdate.PlayerEmitCanvasGeometry", "Canvas.RenderOverlays", "UGUI.Rendering.RenderOverlays", "UIEvents.WillRenderCanvases",
            "Layout", "TextMeshPro.GenerateTextMesh", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "Update.ScriptRunBehaviourUpdate",
        };

        [SerializeField] private UIRoot _root;

        // What it was asked to do.
        private string _system = "uinext";
        private readonly List<string> _scenarios = new();
        private int _rounds = 3;
        private float _warmup = 1f;
        private float _measure = 4f;
        private int _cards = 40;
        private float _ballast;
        private bool _stay;
        private bool _running;
        private readonly StringBuilder _report = new();
        private readonly string[] _counts = new string[1000];

        // UI Next's screen, what the use cases move.
        private Label _nextTicker;
        private ScrollView _nextFeed;
        private UINode _nextFeedNode;
        private readonly List<Box> _nextCards = new();
        private readonly List<Label> _nextBodies = new();

        // UGUI's.
        private TMP_FontAsset _font;
        private Sprite _rounded16, _rounded14, _circle;
        private TextMeshProUGUI _uguiTicker;
        private RectTransform _uguiContent;
        private RectTransform _uguiViewport;
        private VerticalLayoutGroup _uguiContentLayout;
        private readonly List<UIImage> _uguiCards = new();
        private readonly List<TextMeshProUGUI> _uguiBodies = new();

        private IEnumerator Start()
        {
            Application.targetFrameRate = 120;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            ReadArguments(Environment.GetCommandLineArgs());
            for (int i = 0; i < _counts.Length; i++)
                _counts[i] = i.ToString(CultureInfo.InvariantCulture);

            if (_root != null)
                _root.gameObject.SetActive(_system == "uinext");
            if (_system == "uinext")
                BuildUINext();
            else if (_system == "ugui")
                BuildUgui();

            // Fonts, glyphs, shaders and first layouts settle before anything is measured.
            yield return Wait(2f);
            Log(string.Format(CultureInfo.InvariantCulture,
                "UINB|start|system={0}|device={1}|cpu={2}|gpu={3}|api={4}|screen={5}x{6}@{7:F0}|cards={8}|rounds={9}|warmup={10}|measure={11}|unity={12}|font={13}|ballast={14}|development={15}",
                _system, SystemInfo.deviceModel, SystemInfo.processorType, SystemInfo.graphicsDeviceName, SystemInfo.graphicsDeviceType,
                Screen.width, Screen.height, Screen.currentResolution.refreshRateRatio.value, _cards, _rounds, _warmup, _measure,
                Application.unityVersion, _font != null ? _font.faceInfo.familyName : "UI Next default", _ballast, Debug.isDebugBuild));

            using var recorder = new BenchmarkRecorder(Mathf.CeilToInt(_measure * 200f) + 64, name => Array.IndexOf(UguiMarkers, name) >= 0);
            for (int round = 1; round <= _rounds; round++)
            {
                foreach (var scenario in _scenarios)
                {
                    _running = true;
                    var playing = StartCoroutine(Play(scenario));
                    yield return Wait(_warmup);
                    if (_system == "uinext")
                        Log("UINB|draws|" + scenario + "|" + UIDebug.DrawCalls + "|offscreen=" + UIDebug.OffscreenNodes + "|layers=" + UIDebug.Layers);
                    recorder.Begin();
                    float end = Time.realtimeSinceStartup + _measure;
                    while (Time.realtimeSinceStartup < end)
                    {
                        recorder.Frame();
                        yield return null;
                    }
                    recorder.End();
                    _running = false;
                    yield return playing;
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
                    case "-system" when next != null: _system = next.Trim().ToLowerInvariant(); break;
                    case "-scenarios" when next != null:
                        foreach (var name in next.Split(','))
                            if (Array.IndexOf(Scenarios, name.Trim()) >= 0)
                                _scenarios.Add(name.Trim());
                        break;
                    case "-rounds" when next != null: int.TryParse(next, out _rounds); break;
                    case "-warmup" when next != null: float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out _warmup); break;
                    case "-measure" when next != null: float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out _measure); break;
                    case "-cards" when next != null: int.TryParse(next, out _cards); break;
                    case "-ballast" when next != null: float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out _ballast); break;
                    case "-stay": _stay = true; break;
                    // For measuring the GPU: drawn that many times over. (UI Next itself takes -uinext-noocc and
                    // -uinext-noshare, for measuring without the occluders or shared drawing.)
                    case "-repeat" when next != null && int.TryParse(next, out int repeat): UIDebug.Repeat = repeat; break;
                }
            }
            if (_scenarios.Count == 0)
                _scenarios.AddRange(Scenarios);
            _rounds = Mathf.Max(1, _rounds);
        }

        // The same work every frame whatever is measured, keeping the CPU's clock where work keeps it.
        private void Update()
        {
            if (_ballast <= 0f) return;
            double end = UnityEngine.Time.realtimeSinceStartupAsDouble + _ballast / 1000.0;
            while (UnityEngine.Time.realtimeSinceStartupAsDouble < end) { }
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
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "compare.txt"), _report.ToString());
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

        // ── Use cases ────────────────────────────────────────────────────────────

        private IEnumerator Play(string scenario) => scenario switch
        {
            "scroll" => Scroll(),
            "ticker" => Ticker(),
            "recolor" => Recolor(),
            "expand" => Expand(),
            "resize" => Resize(),
            _ => Idle(),
        };

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
                float range = ScrollRange();
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
                ScrollTo(y);
                yield return null;
            }
            ScrollTo(0f);
        }

        private float ScrollRange()
        {
            if (_nextFeed != null) return _nextFeed.Range.y;
            if (_uguiContent != null) return Mathf.Max(0f, _uguiContent.rect.height - _uguiViewport.rect.height);
            return 0f;
        }

        private void ScrollTo(float y)
        {
            if (_nextFeed != null) _nextFeed.ContentOffset = new Vector2(0f, y);
            if (_uguiContent != null) _uguiContent.anchoredPosition = new Vector2(0f, y);
        }

        // The count in the header going up every frame.
        private IEnumerator Ticker()
        {
            int count = 0;
            while (_running)
            {
                string text = _counts[++count % _counts.Length];
                if (_nextTicker != null) _nextTicker.Text.Value = text;
                if (_uguiTicker != null) _uguiTicker.text = text;
                yield return null;
            }
        }

        // Every card's colour changing every frame, as a highlight sweeping over them would.
        private IEnumerator Recolor()
        {
            while (_running)
            {
                float t = Time.realtimeSinceStartup * 4f;
                for (int i = 0; i < _nextCards.Count; i++)
                    _nextCards[i].Fill.Value = Color.Lerp(CardColour, Tint, 0.5f + 0.5f * Mathf.Sin(t + i * 0.3f));
                for (int i = 0; i < _uguiCards.Count; i++)
                    _uguiCards[i].color = Color.Lerp(CardColour, Tint, 0.5f + 0.5f * Mathf.Sin(t + i * 0.3f));
                yield return null;
            }
            foreach (var card in _nextCards)
                card.Fill.Value = CardColour;
            foreach (var card in _uguiCards)
                card.color = CardColour;
        }

        // The first three cards' text cut to a line and back, twice a second: laid out again, everything below them moved.
        private IEnumerator Expand()
        {
            bool cut = false;
            while (_running)
            {
                cut = !cut;
                for (int i = 0; i < 3; i++)
                {
                    string text = cut ? Short : Bodies[i % Bodies.Length];
                    if (i < _nextBodies.Count) _nextBodies[i].Text.Value = text;
                    if (i < _uguiBodies.Count) _uguiBodies[i].text = text;
                }
                yield return Wait(0.5f);
            }
            for (int i = 0; i < 3; i++)
            {
                if (i < _nextBodies.Count) _nextBodies[i].Text.Value = Bodies[i % Bodies.Length];
                if (i < _uguiBodies.Count) _uguiBodies[i].text = Bodies[i % Bodies.Length];
            }
        }

        // The feed's margins swinging between 12 and 48 points every frame: every card as wide as is left, its text wrapped again.
        private IEnumerator Resize()
        {
            while (_running)
            {
                float margin = 30f + 18f * Mathf.Sin(Time.realtimeSinceStartup * 5f);
                SetMargin(margin);
                yield return null;
            }
            SetMargin(12f);
        }

        private void SetMargin(float margin)
        {
            if (_nextFeedNode != null)
                _nextFeedNode.Padding.Value = new Insets(margin, margin, 12f, 24f);
            if (_uguiContentLayout != null)
                _uguiContentLayout.padding = new RectOffset(Mathf.RoundToInt(margin), Mathf.RoundToInt(margin), 12, 24);
        }

        // ── UI Next's screen ────────────────────────────────────────────────────

        private void BuildUINext()
        {
            var app = _root.Node.Column();
            app.Box(Background);

            var header = app.Child("Header").Row(10f).Pad(new Insets(16f, 16f, 12f, 10f)).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            header.Box(CardColour);
            header.Child("Title").Label("Compare", 22f, TextColour, bold: true).Node.Width.Value = Sizing.Grow();
            _nextTicker = header.Child("Ticker").Label("0", 15f, Secondary);

            _nextFeedNode = app.Child("Feed").Sized(Sizing.Grow(), Sizing.Grow()).Column(12f).Pad(new Insets(12f, 12f, 12f, 24f));
            _nextFeed = _nextFeedNode.Scroll();
            for (int i = 0; i < _cards; i++)
                NextCard(_nextFeedNode, i);
        }

        private void NextCard(UINode feed, int index)
        {
            var card = feed.Child("Card").Column(10f).Pad(14f);
            card.Width.Value = Sizing.Grow();
            _nextCards.Add(card.Box(CardColour, 16f));

            var header = card.Child("Header").Row(10f).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            string name = Names[index % Names.Length];
            var avatar = header.Child("Avatar").Sized(Sizing.Fixed(40f), Sizing.Fixed(40f)).Center();
            avatar.Box(Palette[index % Palette.Length], 20f);
            avatar.Child("Initials").Label(Initials(name), 15f, Color.white, bold: true);
            var names = header.Child("Names").Column(2f);
            names.Width.Value = Sizing.Grow();
            names.Child("Name").Label(name, 16f, TextColour, bold: true);
            names.Child("Handle").Label(Handle(name, index), 13f, Secondary);
            header.Child("Time").Label(Ago(index), 13f, Secondary);

            var body = card.Child("Body").Label(Bodies[index % Bodies.Length], 15f, TextColour);
            body.Node.Width.Value = Sizing.Grow();
            _nextBodies.Add(body);

            var actions = card.Child("Actions").Row(8f);
            foreach (var action in Actions(index))
            {
                var chip = actions.Child(action).Pad(12f, 6f).Center();
                chip.Box(ChipColour, 14f);
                chip.Child("Title").Label(action, 13f, TextColour);
            }
        }

        // ── UGUI's screen ───────────────────────────────────────────────────────

        private void BuildUgui()
        {
            // The font UI Next draws with by default, the OS's, as TextMeshPro's.
            var face = FontStack.Default.Primary;
            if (face != null)
                _font = TMP_FontAsset.CreateFontAsset(face.faceInfo.familyName, face.faceInfo.styleName);
            if (_font == null)
                _font = TMP_Settings.defaultFontAsset;
            // At three pixels to a point, as the screen is about that many.
            _rounded16 = RoundedSprite(16, 3);
            _rounded14 = RoundedSprite(14, 3);
            _circle = RoundedSprite(20, 3, sliced: false);

            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var background = Make("Background", canvasObject.transform);
            Stretch(background);
            Image(background, null, Background);

            // Clear of the notch and the home bar, as UI Next's root keeps its content.
            var safe = Make("Safe", canvasObject.transform);
            Stretch(safe);
            float scale = Screen.width / 390f;
            var area = Screen.safeArea;
            safe.offsetMin = new Vector2(area.xMin, area.yMin) / scale;
            safe.offsetMax = -new Vector2(Screen.width - area.xMax, Screen.height - area.yMax) / scale;
            var column = safe.gameObject.AddComponent<VerticalLayoutGroup>();
            Controls(column, expandWidth: true);

            var header = Make("Header", safe);
            Image(header, null, CardColour);
            var headerRow = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            Controls(headerRow);
            headerRow.padding = new RectOffset(16, 16, 12, 10);
            headerRow.spacing = 10f;
            headerRow.childAlignment = TextAnchor.MiddleLeft;
            var title = Text(header, "Compare", 22f, TextColour, bold: true);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _uguiTicker = Text(header, "0", 15f, Secondary);

            var feed = Make("Feed", safe);
            feed.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var scroll = feed.gameObject.AddComponent<ScrollRect>();
            _uguiViewport = Make("Viewport", feed);
            Stretch(_uguiViewport);
            _uguiViewport.gameObject.AddComponent<RectMask2D>();
            _uguiContent = Make("Content", _uguiViewport);
            _uguiContent.anchorMin = new Vector2(0f, 1f);
            _uguiContent.anchorMax = new Vector2(1f, 1f);
            _uguiContent.pivot = new Vector2(0.5f, 1f);
            _uguiContent.sizeDelta = Vector2.zero;
            _uguiContentLayout = _uguiContent.gameObject.AddComponent<VerticalLayoutGroup>();
            Controls(_uguiContentLayout, expandWidth: true);
            _uguiContentLayout.padding = new RectOffset(12, 12, 12, 24);
            _uguiContentLayout.spacing = 12f;
            _uguiContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _uguiContent;
            scroll.viewport = _uguiViewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;

            for (int i = 0; i < _cards; i++)
                UguiCard(_uguiContent, i);
        }

        private void UguiCard(Transform feed, int index)
        {
            var card = Make("Card", feed);
            _uguiCards.Add(Image(card, _rounded16, CardColour));
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            Controls(layout, expandWidth: true);
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 10f;

            var header = Make("Header", card);
            var row = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            Controls(row);
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleLeft;
            string name = Names[index % Names.Length];
            var avatar = Make("Avatar", header);
            Image(avatar, _circle, Palette[index % Palette.Length]);
            var size = avatar.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 40f;
            size.minHeight = size.preferredHeight = 40f;
            var initials = Text(avatar, Initials(name), 15f, Color.white, bold: true);
            initials.alignment = TextAlignmentOptions.Center;
            Stretch(initials.rectTransform);
            var names = Make("Names", header);
            var namesColumn = names.gameObject.AddComponent<VerticalLayoutGroup>();
            Controls(namesColumn);
            namesColumn.spacing = 2f;
            names.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Text(names, name, 16f, TextColour, bold: true);
            Text(names, Handle(name, index), 13f, Secondary);
            Text(header, Ago(index), 13f, Secondary);

            _uguiBodies.Add(Text(card, Bodies[index % Bodies.Length], 15f, TextColour));

            var actions = Make("Actions", card);
            var actionsRow = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            Controls(actionsRow);
            actionsRow.spacing = 8f;
            foreach (var action in Actions(index))
            {
                var chip = Make(action, actions);
                Image(chip, _rounded14, ChipColour);
                var chipRow = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
                Controls(chipRow);
                chipRow.padding = new RectOffset(12, 12, 6, 6);
                Text(chip, action, 13f, TextColour);
            }
        }

        private static RectTransform Make(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // A layout group sizing its children to what they ask for, and across, with expandWidth, to its own width.
        private static void Controls(HorizontalOrVerticalLayoutGroup group, bool expandWidth = false)
        {
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = expandWidth;
            group.childForceExpandHeight = false;
        }

        private static UIImage Image(RectTransform rect, Sprite sprite, Color colour)
        {
            var image = rect.gameObject.AddComponent<UIImage>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            if (sprite != null)
                image.type = sprite.border != Vector4.zero ? UIImage.Type.Sliced : UIImage.Type.Simple;
            return image;
        }

        private TextMeshProUGUI Text(Transform parent, string text, float size, Color colour, bool bold = false)
        {
            var rect = Make("Text", parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.fontSize = size;
            label.color = colour;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        // A white rounded rectangle (or circle), nine-sliced round its corners, drawn `scale` pixels to a point as a sprite
        // imported at that scale is (100 pixels a unit for each).
        private static Sprite RoundedSprite(int radius, int scale, bool sliced = true)
        {
            int r = radius * scale;
            int size = sliced ? 2 * r + 2 : 2 * r;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Compare rounded " + radius,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance outside the rounded rectangle, a pixel's centre at a time, anti-aliased over a pixel.
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, r, size - r), cy = Mathf.Clamp(py, r, size - r);
                    float d = new Vector2(px - cx, py - cy).magnitude - r;
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(0.5f - d));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var border = sliced ? new Vector4(r, r, r, r) : Vector4.zero;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * scale, 0, SpriteMeshType.FullRect, border);
        }

        // ── What the cards say ───────────────────────────────────────────────────

        private static string Initials(string name) => name.Substring(0, 1) + name.Substring(name.IndexOf(' ') + 1, 1);

        private static string Handle(string name, int index) => "@" + name.Split(' ')[0].ToLowerInvariant() + index;

        private static string Ago(int index) => (index % 59 + 1) + "m";

        private static string[] Actions(int index) => new[] { "Like " + (index * 7 % 90 + 3), "Reply " + (index * 3 % 20 + 1), "Share" };
    }
}
#endif
