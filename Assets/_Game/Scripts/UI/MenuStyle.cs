using UnityEngine;
using UnityEngine.UI;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The look of the menu screens after the main menu (T46, round 173): OPTIONS, DIFFICULTY, LOAD and SAVE, CREDITS,
    /// HOW TO PLAY. One palette (orange, green, electric blue, silver, off-white), the two game fonts, dark see-through
    /// panels over the Memorial (or the paused game), and an orange wash on the selected row.
    /// The heads use Roboto Condensed 700 (squeezed to 64 % on titles, round 129). The body uses Arimo 600. The body is
    /// not squeezed: in a layout group a squeezed legacy Text keeps its full cell and leaves a blank margin.
    /// </summary>
    public static class MenuStyle
    {
        // The palette (MenuSymbol holds the five colours of the main menu).
        public static Color Orange => MenuSymbol.Orange;
        public static Color Green => MenuSymbol.Green;
        public static Color Blue => MenuSymbol.Blue;
        public static Color Silver => MenuSymbol.Silver;
        public static Color OffWhite => MenuSymbol.OffWhite;

        /// <summary>Small print and hints: silver, a little see-through.</summary>
        public static readonly Color Dim = new Color(0.72f, 0.75f, 0.78f, 0.78f);
        /// <summary>The full-screen shade behind a menu screen. The Memorial or the game shows through it.</summary>
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.62f);
        /// <summary>The panel of a menu screen.</summary>
        public static readonly Color Panel = new Color(0.03f, 0.035f, 0.05f, 0.88f);
        /// <summary>A darker band in a panel (a title bar, a list).</summary>
        public static readonly Color Band = new Color(0f, 0f, 0f, 0.35f);
        /// <summary>A row, a button or a field at rest.</summary>
        public static readonly Color Well = new Color(0.72f, 0.75f, 0.78f, 0.08f);
        /// <summary>A selected or pointed row: an orange wash. Off-white text stays easy to read on it.</summary>
        public static readonly Color WellOn = new Color(1f, 0.54f, 0.12f, 0.32f);
        /// <summary>A row while it is pressed.</summary>
        public static readonly Color WellDown = new Color(1f, 0.54f, 0.12f, 0.55f);
        /// <summary>A thin line between parts of a panel.</summary>
        public static readonly Color Rule = new Color(0.72f, 0.75f, 0.78f, 0.30f);
        /// <summary>A strong fill for the "harder" side and for an active preset: deep orange.</summary>
        public static readonly Color OrangeDeep = new Color(0.78f, 0.38f, 0.04f, 0.92f);
        /// <summary>A strong fill for the "easier" side: deep green.</summary>
        public static readonly Color GreenDeep = new Color(0.08f, 0.46f, 0.26f, 0.92f);
        /// <summary>The empty part of a slider or a check box.</summary>
        public static readonly Color Track = new Color(0.72f, 0.75f, 0.78f, 0.16f);

        public const float TitleSqueeze = 0.64f;

        public static Font Head => MainMenuUI.HeadFont;
        public static Font Body => MainMenuUI.ProfileFont;

        /// <summary>The colours of a button or a row: the wash at rest, orange when selected or pointed.</summary>
        public static ColorBlock Colors(ColorBlock c)
        {
            c.normalColor = Well;
            c.highlightedColor = WellOn;
            c.selectedColor = WellOn;
            c.pressedColor = WellDown;
            c.disabledColor = new Color(0.72f, 0.75f, 0.78f, 0.04f);
            c.colorMultiplier = 1f;
            return c;
        }

        /// <summary>
        /// The colours of a button whose image colour shows a state (a tab, a mode): no tint at rest, a stronger wash
        /// when it is pointed or selected.
        /// </summary>
        public static ColorBlock StateColors(ColorBlock c)
        {
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 2.5f);
            c.selectedColor = new Color(1.3f, 1.3f, 1.3f, 2.5f);
            c.pressedColor = new Color(1f, 1f, 1f, 4f);
            c.colorMultiplier = 1f;
            return c;
        }

        /// <summary>Gives a button its target graphic in white, so the ColorBlock alone sets its colour.</summary>
        public static void Style(Button b)
        {
            if (b == null) return;
            if (b.targetGraphic != null) b.targetGraphic.color = Color.white;
            if (b.targetGraphic is Image img && img.sprite == null)
            {
                img.sprite = RoundFill; // the AC3 rounded corners, small on a button
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 2.5f;
            }
            b.colors = Colors(b.colors);
        }

        /// <summary>A screen title: Roboto Condensed 700, off-white, squeezed to 64 %.</summary>
        public static void Title(Text t)
        {
            if (t == null) return;
            t.font = Head;
            t.fontStyle = FontStyle.Normal;
            t.color = OffWhite;
            Squeeze(t, TitleSqueeze);
        }

        /// <summary>A head (a button label, a section name): Roboto Condensed 700, no squeeze.</summary>
        public static void HeadText(Text t, Color colour)
        {
            if (t == null) return;
            t.font = Head;
            t.fontStyle = FontStyle.Normal;
            t.color = colour;
        }

        // ── The AC3 frame (T46, 2026-11-17) ─────────────────────────────────────────────────────────────────
        // Bruno: "follow the main menu art direction and have it ace combat 3 inspired". Every menu screen sits in one
        // frame: a big rotated side label, an orange header plate with a heavy title and a big figure, a strip of
        // orange and black hazard stripes, a pictogram badge, a framed black panel for the content, and the status
        // strip of the main menu at the bottom. The shapes are drawn in code, so no art file is needed.

        /// <summary>The parts of a frame that a screen fills or changes.</summary>
        public sealed class MenuFrame
        {
            public RectTransform Root;
            /// <summary>The screen puts its panel here (stretched, 18 px inside the framed edge).</summary>
            public RectTransform Content;
            public Text Title;
            public Text Subtitle;
            /// <summary>The big figure at the right end of the header plate (empty by default).</summary>
            public Text Figure;
            /// <summary>The green status line at the bottom left (key hints or the screen path).</summary>
            public Text Status;
            /// <summary>The name under the badge symbol (it starts as the title).</summary>
            public Text Caption;
        }

        private static Sprite roundFill, roundEdge;
        private static Texture2D stripes;

        /// <summary>A white rounded rectangle for a sliced Image (radius 24 px of the 64 px source).</summary>
        public static Sprite RoundFill => roundFill != null ? roundFill : roundFill = MakeRound(false);
        /// <summary>A white rounded ring, 6 px thick, for a sliced Image laid over a fill.</summary>
        public static Sprite RoundEdge => roundEdge != null ? roundEdge : roundEdge = MakeRound(true);

        private static Sprite MakeRound(bool ring)
        {
            const int size = 64, radius = 24, thick = 6;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundDistance(x + 0.5f, y + 0.5f, size, radius); // < 0 inside
                float outer = Mathf.Clamp01(0.5f - d);
                float a = outer;
                if (ring) a = Mathf.Min(outer, Mathf.Clamp01(d + thick + 0.5f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 4, radius + 4, radius + 4, radius + 4));
        }

        /// <summary>The signed distance from a point to the edge of a rounded square (negative inside).</summary>
        private static float RoundDistance(float x, float y, int size, int radius)
        {
            float cx = Mathf.Clamp(x, radius, size - radius);
            float cy = Mathf.Clamp(y, radius, size - radius);
            return Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - radius;
        }

        /// <summary>Orange and black stripes at 45 degrees, one 32 px tile that repeats.</summary>
        public static Texture2D Stripes
        {
            get
            {
                if (stripes != null) return stripes;
                const int size = 32;
                stripes = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                Color32 o = Orange, k = new Color32(8, 8, 10, 255);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = ((x + y) % size) < size / 2 ? o : k;
                stripes.SetPixels32(px);
                stripes.Apply(false, true);
                return stripes;
            }
        }

        /// <summary>A rounded panel: a fill, and a ring over it when edge is not clear.</summary>
        public static Image Plate(Transform parent, string name, Color fill, Color edge, float corner = 1f)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            img.sprite = RoundFill;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = corner;
            img.color = fill;
            img.raycastTarget = false;
            if (edge.a > 0f)
            {
                var ringObj = new GameObject("Edge", typeof(RectTransform));
                ringObj.transform.SetParent(obj.transform, false);
                Fill((RectTransform)ringObj.transform, 0f);
                var ring = ringObj.AddComponent<Image>();
                ring.sprite = RoundEdge;
                ring.type = Image.Type.Sliced;
                ring.pixelsPerUnitMultiplier = corner;
                ring.color = edge;
                ring.raycastTarget = false;
            }
            return img;
        }

        /// <summary>Stretches a rect over its parent, inset by the margin.</summary>
        public static void Fill(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Sets the anchor (both corners), the pivot, the position and the size of a rect.</summary>
        public static RectTransform Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = position;
            r.sizeDelta = size;
            return r;
        }

        /// <summary>A Text that ignores the pointer and overflows its box (the parts of a frame).</summary>
        public static Text Words(Transform parent, string name, string words, Font font, int size, Color colour, TextAnchor anchor)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var t = obj.AddComponent<Text>();
            t.text = words;
            t.font = font;
            t.fontSize = size;
            t.color = colour;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>
        /// Builds the frame of a menu screen over the whole parent (the safe area of the screen's canvas).
        /// title: the header plate. subtitle: the small line under it. side: the rotated label on the left.
        /// icon: the menu symbol of the badge (UI/Menu/UI_menu_&lt;icon&gt;). status: the green line at the bottom left.
        /// </summary>
        public static MenuFrame Frame(Transform parent, string title, string subtitle, string side, string icon, string status)
        {
            var f = new MenuFrame();
            var rootObj = new GameObject("MenuFrame", typeof(RectTransform));
            rootObj.transform.SetParent(parent, false);
            f.Root = (RectTransform)rootObj.transform;
            Fill(f.Root, 0f);
            Transform root = f.Root;

            // The top rule, as in the AC3 data screens.
            var top = new GameObject("TopRule", typeof(RectTransform), typeof(Image));
            top.transform.SetParent(root, false);
            var topRect = (RectTransform)top.transform;
            topRect.anchorMin = new Vector2(0f, 1f); topRect.anchorMax = new Vector2(1f, 1f);
            topRect.offsetMin = new Vector2(60f, -46f); topRect.offsetMax = new Vector2(-60f, -40f);
            top.GetComponent<Image>().color = new Color(0.72f, 0.75f, 0.78f, 0.45f);
            top.GetComponent<Image>().raycastTarget = false;

            // The rotated side label: big, off-white, read from the bottom up.
            Text sideText = Words(root, "SideLabel", side, Head, 150, new Color(0.95f, 0.94f, 0.9f, 0.92f), TextAnchor.MiddleCenter);
            Place(sideText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(104f, -70f), new Vector2(760f, 150f));
            sideText.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
            sideText.rectTransform.localScale = new Vector3(TitleSqueeze, 1f, 1f);

            // The header plate: orange, a darker orange edge, the title in black, a black rule, the subtitle.
            Image plate = Plate(root, "HeaderPlate", Orange, OrangeDeep);
            Place(plate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -64f), new Vector2(1000f, 158f));
            f.Title = Words(plate.transform, "Title", title, Head, 108, new Color(0.03f, 0.03f, 0.04f, 1f), TextAnchor.MiddleLeft);
            var titleRect = f.Title.rectTransform;
            titleRect.pivot = new Vector2(0f, 0.5f); // the pivot first: a pivot set after the offsets moves the rect
            titleRect.anchorMin = new Vector2(0f, 0.34f); titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(34f, 0f); titleRect.offsetMax = new Vector2(-34f, -8f);
            titleRect.localScale = new Vector3(TitleSqueeze, 1f, 1f);
            var rule = new GameObject("PlateRule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(plate.transform, false);
            var ruleRect = (RectTransform)rule.transform;
            ruleRect.anchorMin = new Vector2(0f, 0.34f); ruleRect.anchorMax = new Vector2(1f, 0.34f);
            ruleRect.offsetMin = new Vector2(34f, -2f); ruleRect.offsetMax = new Vector2(-34f, 2f);
            rule.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.04f, 0.9f);
            rule.GetComponent<Image>().raycastTarget = false;
            f.Subtitle = Words(plate.transform, "Subtitle", subtitle, Head, 26, new Color(0.03f, 0.03f, 0.04f, 0.9f), TextAnchor.MiddleLeft);
            var subRect = f.Subtitle.rectTransform;
            subRect.anchorMin = new Vector2(0f, 0f); subRect.anchorMax = new Vector2(1f, 0.34f);
            subRect.offsetMin = new Vector2(36f, 6f); subRect.offsetMax = new Vector2(-36f, -4f);
            f.Figure = Words(plate.transform, "Figure", "", Head, 120, new Color(0.03f, 0.03f, 0.04f, 1f), TextAnchor.MiddleRight);
            var figRect = f.Figure.rectTransform;
            figRect.pivot = new Vector2(1f, 0.5f);
            figRect.anchorMin = new Vector2(0.5f, 0.34f); figRect.anchorMax = new Vector2(1f, 1f);
            figRect.offsetMin = new Vector2(0f, 0f); figRect.offsetMax = new Vector2(-34f, -8f);
            figRect.localScale = new Vector3(TitleSqueeze, 1f, 1f);

            // The hazard stripes under the plate, and a white bar after them.
            var stripeObj = new GameObject("Stripes", typeof(RectTransform));
            stripeObj.transform.SetParent(root, false);
            var stripeRect = Place((RectTransform)stripeObj.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -236f), new Vector2(520f, 28f));
            var stripeImg = stripeObj.AddComponent<RawImage>();
            stripeImg.texture = Stripes;
            stripeImg.uvRect = new Rect(0f, 0f, 520f / 28f, 1f);
            stripeImg.raycastTarget = false;
            stripeObj.AddComponent<Outline>().effectColor = new Color(0.95f, 0.94f, 0.9f, 0.9f);
            var bar = new GameObject("WhiteBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(root, false);
            Place((RectTransform)bar.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(730f, -246f), new Vector2(460f, 8f));
            bar.GetComponent<Image>().color = OffWhite;
            bar.GetComponent<Image>().raycastTarget = false;

            // The badge: a silver plate, a lighter inner plate, the menu symbol in black, its name under it.
            Image badge = Plate(root, "Badge", new Color(0.55f, 0.58f, 0.62f, 0.95f), new Color(0.95f, 0.94f, 0.9f, 0.9f));
            Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -58f), new Vector2(206f, 206f));
            Image inner = Plate(badge.transform, "Inner", new Color(0.78f, 0.8f, 0.83f, 1f), new Color(0f, 0f, 0f, 0f), 1.4f);
            Fill(inner.rectTransform, 16f);
            var iconObj = new GameObject("Icon", typeof(RectTransform));
            iconObj.transform.SetParent(inner.transform, false);
            Place((RectTransform)iconObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(112f, 112f));
            var iconImg = iconObj.AddComponent<RawImage>();
            iconImg.texture = Resources.Load<Texture2D>(MainMenuUI.ArtFolder + "UI_menu_" + icon);
            iconImg.color = new Color(0.04f, 0.04f, 0.05f, 1f);
            iconImg.raycastTarget = false;
            Text caption = Words(inner.transform, "Caption", title, Head, 30, new Color(0.04f, 0.04f, 0.05f, 1f), TextAnchor.MiddleCenter);
            Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(240f, 40f));
            caption.rectTransform.localScale = new Vector3(0.8f, 1f, 1f);
            f.Caption = caption;

            // The content: a black panel with a thick off-white edge.
            Image body = Plate(root, "Body", new Color(0.02f, 0.025f, 0.035f, 0.9f), new Color(0.95f, 0.94f, 0.9f, 0.85f));
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero; bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(190f, 118f); bodyRect.offsetMax = new Vector2(-70f, -282f);
            var contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(body.transform, false);
            f.Content = (RectTransform)contentObj.transform;
            Fill(f.Content, 18f);

            // The status strip of the main menu: a thin rule, green on the left, silver on the right.
            var strip = new GameObject("StatusRule", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(root, false);
            var stripRect = (RectTransform)strip.transform;
            stripRect.anchorMin = new Vector2(0f, 0f); stripRect.anchorMax = new Vector2(1f, 0f);
            stripRect.offsetMin = new Vector2(120f, 79f); stripRect.offsetMax = new Vector2(-120f, 81f);
            strip.GetComponent<Image>().color = new Color(0.72f, 0.75f, 0.78f, 0.47f);
            strip.GetComponent<Image>().raycastTarget = false;
            // The same place and size as the strip of the main menu (MainMenuUI.CreateSmallPrint).
            f.Status = Words(root, "Status", status, Body, 26, Green, TextAnchor.UpperLeft);
            Place(f.Status.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(120f, 72f), new Vector2(1600f, 36f));
            f.Status.rectTransform.localScale = new Vector3(0.77f, 1f, 1f);
            Text name = Words(root, "Version", "GLORIFIED  DEMO " + Application.version, Body, 26, Silver, TextAnchor.UpperRight);
            Place(name.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-120f, 72f), new Vector2(900f, 36f));
            name.rectTransform.localScale = new Vector3(0.77f, 1f, 1f);
            return f;
        }

        /// <summary>
        /// A compact AC3 dialog for a question over another screen (the START prompt of the main menu): the orange
        /// header plate, the stripes and the white bar under it, and a framed black body. The parent places Root.
        /// </summary>
        public static MenuFrame Dialog(Transform parent, string title, string subtitle, Vector2 size)
        {
            var f = new MenuFrame();
            var rootObj = new GameObject("Dialog", typeof(RectTransform));
            rootObj.transform.SetParent(parent, false);
            f.Root = (RectTransform)rootObj.transform;
            f.Root.sizeDelta = size;
            Transform root = f.Root;

            Image plate = Plate(root, "HeaderPlate", Orange, OrangeDeep);
            var plateRect = plate.rectTransform;
            plateRect.anchorMin = new Vector2(0f, 1f); plateRect.anchorMax = new Vector2(1f, 1f);
            plateRect.pivot = new Vector2(0.5f, 1f);
            plateRect.anchoredPosition = Vector2.zero;
            plateRect.sizeDelta = new Vector2(0f, 118f);
            f.Title = Words(plate.transform, "Title", title, Head, 80, new Color(0.03f, 0.03f, 0.04f, 1f), TextAnchor.MiddleLeft);
            var titleRect = f.Title.rectTransform;
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchorMin = new Vector2(0f, 0.36f); titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(30f, 0f); titleRect.offsetMax = new Vector2(-30f, -6f);
            titleRect.localScale = new Vector3(TitleSqueeze, 1f, 1f);
            var rule = new GameObject("PlateRule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(plate.transform, false);
            var ruleRect = (RectTransform)rule.transform;
            ruleRect.anchorMin = new Vector2(0f, 0.36f); ruleRect.anchorMax = new Vector2(1f, 0.36f);
            ruleRect.offsetMin = new Vector2(30f, -2f); ruleRect.offsetMax = new Vector2(-30f, 2f);
            rule.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.04f, 0.9f);
            rule.GetComponent<Image>().raycastTarget = false;
            f.Subtitle = Words(plate.transform, "Subtitle", subtitle, Head, 22, new Color(0.03f, 0.03f, 0.04f, 0.9f), TextAnchor.MiddleLeft);
            var subRect = f.Subtitle.rectTransform;
            subRect.anchorMin = Vector2.zero; subRect.anchorMax = new Vector2(1f, 0.36f);
            subRect.offsetMin = new Vector2(32f, 4f); subRect.offsetMax = new Vector2(-32f, -2f);

            var stripeObj = new GameObject("Stripes", typeof(RectTransform));
            stripeObj.transform.SetParent(root, false);
            Place((RectTransform)stripeObj.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -128f), new Vector2(size.x * 0.45f, 22f));
            var stripeImg = stripeObj.AddComponent<RawImage>();
            stripeImg.texture = Stripes;
            stripeImg.uvRect = new Rect(0f, 0f, size.x * 0.45f / 22f, 1f);
            stripeImg.raycastTarget = false;
            stripeObj.AddComponent<Outline>().effectColor = new Color(0.95f, 0.94f, 0.9f, 0.9f);
            var bar = new GameObject("WhiteBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(root, false);
            Place((RectTransform)bar.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(size.x * 0.45f + 20f, -135f), new Vector2(size.x * 0.4f, 8f));
            bar.GetComponent<Image>().color = OffWhite;
            bar.GetComponent<Image>().raycastTarget = false;

            Image body = Plate(root, "Body", new Color(0.02f, 0.025f, 0.035f, 0.92f), new Color(0.95f, 0.94f, 0.9f, 0.85f));
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero; bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = Vector2.zero; bodyRect.offsetMax = new Vector2(0f, -164f);
            var contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(body.transform, false);
            f.Content = (RectTransform)contentObj.transform;
            Fill(f.Content, 22f);
            return f;
        }

        /// <summary>A key name in green, for a key line in rich text ("E  TAKE"). The keys of the death screen are green too.</summary>
        public static string KeyTag(string key) => "<color=#" + ColorUtility.ToHtmlStringRGB(Green) + ">" + key + "</color>";

        /// <summary>A strip of the hazard stripes with an off-white outline. The caller places it.</summary>
        public static RawImage StripeStrip(Transform parent, string name, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            ((RectTransform)obj.transform).sizeDelta = size;
            var img = obj.AddComponent<RawImage>();
            img.texture = Stripes;
            img.uvRect = new Rect(0f, 0f, size.x / size.y, 1f);
            img.raycastTarget = false;
            obj.AddComponent<Outline>().effectColor = new Color(0.95f, 0.94f, 0.9f, 0.9f);
            return img;
        }

        /// <summary>Squeezes a Text across, about its aligned edge, so it stays where its alignment puts it.</summary>
        public static void Squeeze(Text t, float amount)
        {
            RectTransform r = t.rectTransform;
            float px = 0.5f;
            switch (t.alignment)
            {
                case TextAnchor.UpperLeft: case TextAnchor.MiddleLeft: case TextAnchor.LowerLeft: px = 0f; break;
                case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight: px = 1f; break;
            }
            Vector2 size = r.rect.size;
            Vector2 oldPivot = r.pivot;
            r.pivot = new Vector2(px, oldPivot.y);
            // Keep the rect in place when it is not in a layout group (a layout group sets the place itself).
            r.anchoredPosition += new Vector2((px - oldPivot.x) * size.x, 0f);
            r.localScale = new Vector3(amount, 1f, 1f);
        }
    }
}
