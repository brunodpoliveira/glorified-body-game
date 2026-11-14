#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Interaction;
using GlorifiedBody.Rendering;
using GlorifiedBody.Story;
using GlorifiedBody.Subtitles;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The night of 30 December in the demo (2026-11-10): the staff's Christmas that the church does not keep (CANON round
    /// 59), the news of act 2 on a radio and a TV (rounds 106 to 112), the anime of the church in W14 (round 108) and the
    /// title card RAVEN (rounds 102 to 105). The words are the writer's (DEMO_PLAN §7 and §8, game/lines/N1_news.md,
    /// game/cutscenes/07c_the_party.md), PROPOSED unless the plan says CANON. First pass: greybox, text cards, subtitles
    /// with no voice (round 40).
    /// </summary>
    public static class DemoNight
    {
        public const string PartyId = "07c_the_party";
        public const string TreeScanned = "demo_c10_scanned";
        public const string NewsDone = "demo_r1_news_done";
        public const string W14TvDone = "demo_w14_tv";
        public const string BroadcastFolder = "Assets/_Game/Audio/Broadcast";

        // ── The title card (§14 item 14) ──────────────────────────────

        /// <summary>
        /// The card RAVEN, the first thing on screen, before cutscene 01 (rounds 104 and 105). It is the first shot of the wake
        /// (one cutscene: the skip key skips both, as one opening).
        /// </summary>
        public static CutsceneAsset WithTitle(CutsceneAsset wake)
        {
            Texture2D raven = DemoV2.Still("cs00_raven");
            var shots = new List<CutsceneAsset.Shot>(wake.shots ?? new CutsceneAsset.Shot[0]);
            if (shots.Count > 0 && shots[0].still == raven) return wake;
            shots.Insert(0, new CutsceneAsset.Shot { still = raven, seconds = 3.5f, panFrom = new Vector3(0f, 0f, 1f), panTo = new Vector3(0f, 0f, 1f), speaker = "", lineId = "", text = "" });
            wake.shots = shots.ToArray();
            EditorUtility.SetDirty(wake);
            AssetDatabase.SaveAssets();
            return wake;
        }

        // ── G10, the staff cafeteria (beat 19b) ───────────────────────

        /// <summary>
        /// G10 on the night: the desktop tree on table B with its blinking lights, the banner, the leftovers on the new fridge,
        /// the corkboard (L25), the printed email by the tree (L26), the handbook page by the door, the radio (N1a). The scan
        /// C10 on the tree plays cutscene 07c.
        /// </summary>
        public static void Cafeteria(DemoRoom r)
        {
            // The fridge (new in G10, §5.2) beside the side counter, the leftovers on top.
            r.Prop("Fridge", new Vector3(33.05f, 0f, -4.6f), new Vector3(0.8f, 1.8f, 0.7f), 0.6f);
            r.Prop("Pizza_Boxes", new Vector3(33.05f, 1.8f, -4.6f), new Vector3(0.45f, 0.12f, 0.45f), 0.55f);
            r.Prop("Foil_Tray", new Vector3(33.05f, 1.92f, -4.6f), new Vector3(0.4f, 0.06f, 0.3f), 0.8f);
            r.Sign("Sign_Leftovers", DemoPlan.Sign("G10, taped to the pizza boxes", "SINCE THE 25TH. EAT IT OR BIN IT.\nIt's Gene's birthday turkey. Have some respect."), new Vector3(33.05f, 1.55f, -4.24f), Vector3.forward, new Vector2(0.7f, 0.2f), 20);

            // The corkboard by the fridge: the winter schedule (L25).
            r.Box(r.Props, "Corkboard", new Vector3(34.3f, 1.5f, -4.83f), new Vector3(0.9f, 0.6f, 0.03f), DemoRoom.Colour("cork", new Color(0.55f, 0.4f, 0.26f)));
            r.Item("L25_Winter_Schedule", L25(), new Vector3(34.2f, 1.5f, -4.8f), new Vector3(0.21f, 0.28f, 0.02f));

            // The desktop tree at the end of table B, its lights, the banner over it, the email taped beside it (L26).
            Vector3 tree = new Vector3(34.85f, 0.75f, -2.5f);
            r.Box(r.Props, "Desktop_Tree", tree + Vector3.up * 0.16f, new Vector3(0.18f, 0.32f, 0.18f), DemoRoom.Colour("tree", new Color(0.12f, 0.35f, 0.16f)));
            GameObject string_ = MenuLoopSetup.EnsureChild(r.Props, "Tree_Lights");
            var lights = new List<Renderer>();
            Color[] colours = { new Color(1f, 0.85f, 0.4f), new Color(1f, 0.3f, 0.25f), new Color(0.4f, 0.75f, 1f), new Color(0.5f, 1f, 0.45f) };
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.75f, y = 0.06f + i * 0.033f, radius = 0.1f - i * 0.006f;
                Vector3 at = tree + new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
                GameObject bulb = r.Box(string_, "Bulb_" + i, at, Vector3.one * 0.025f, DemoRoom.Colour("bulb_" + (i % 4), colours[i % 4]));
                bulb.isStatic = false;
                Object.DestroyImmediate(bulb.GetComponent<Collider>());
                lights.Add(bulb.GetComponent<Renderer>());
            }
            Blink blink = MenuLoopSetup.EnsureComponent<Blink>(string_);
            blink.lights = lights.ToArray();
            EditorUtility.SetDirty(blink);
            r.Sign("Sign_Banner", DemoPlan.Sign("G10, over the tree", "HAPPY BIRTHDAY GENE!!!"), new Vector3(34.3f, 2.1f, -0.17f), Vector3.back, new Vector2(2.2f, 0.4f), 48);
            r.Item("L26_Birthday_Email", L26(), new Vector3(35.6f, 1.4f, -0.14f), new Vector3(0.21f, 0.28f, 0.02f));
            r.Sign("Sign_Handbook", DemoPlan.Sign("G10, by the door, a laminated page", "STAFF AREAS. PERSONAL CELEBRATIONS (A BIRTHDAY, A RETIREMENT, A NEW BABY) ARE WELCOME HERE. PLEASE CLEAN UP AFTER. RELIGIOUS OBSERVANCE HAS ITS PLACE IN THE CHAPEL."),
                new Vector3(31.6f, 1.5f, -0.17f), Vector3.back, new Vector2(0.9f, 0.5f), 16);

            // The scan C10 on the tree: the one scan that plays a cutscene (07c).
            Scannable scan = r.Scan("Scan_Desktop_Tree", tree + Vector3.up * 0.2f, "demo_c10_tree", "", TreeScanned);
            scan.cutscene = Party();
            EditorUtility.SetDirty(scan);

            // The radio on the hot counter, left on after the party (N1a).
            GameObject radio = r.Prop("Radio", new Vector3(37.3f, 1f, -0.9f), new Vector3(0.3f, 0.18f, 0.12f), 0.1f);
            Broadcast news = MenuLoopSetup.EnsureComponent<Broadcast>(radio);
            news.roomId = "G10";
            news.lines = new[]
            {
                Line("RADIO", "n1a_00", "...my door is open to the union, tonight and every night until this ends. Until then the Guard has the streets, and I ask the people of this city for their patience through the holidays."),
                Line("RADIO", "n1a_01", "The chief of police, earlier this evening. Sports now, and nobody saw this one coming. Dario Venn leaves Corwin Racing for Halberd Motorsport, with two years left on his contract."),
                Line("RADIO", "n1a_02", "Corwin calls it a breach. Venn calls it a fresh start. More after this."),
            };
            news.loop = Loop("station_loop");
            news.loopVolume = 0.25f;
            EditorUtility.SetDirty(news);
        }

        /// <summary>Cutscene 07c, first pass: text cards (the present on black, the memory warm), the lines as subtitles, the cork and the cheer.</summary>
        public static CutsceneAsset Party()
        {
            System.IO.Directory.CreateDirectory(ScriptTestSetup.CutscenesFolder);
            string path = ScriptTestSetup.CutscenesFolder + "/cutscene_" + PartyId + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<CutsceneAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CutsceneAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.cutsceneId = PartyId;
            asset.music = null;
            Texture2D S(int n) => DemoV2.Still("cs07c_s" + n);
            AudioClip cork = DemoV2.Sound("cs07c_cork"), cheer = DemoV2.Sound("cs07c_cheer");
            int id = 0;
            CutsceneAsset.Shot Shot(Texture2D still, float seconds, string speaker = "", string text = "", AudioClip sound = null) => new CutsceneAsset.Shot
            {
                still = still, seconds = seconds, sound = sound, panFrom = new Vector3(0f, 0f, 1f), panTo = new Vector3(0f, 0f, 1f),
                speaker = speaker, lineId = string.IsNullOrEmpty(text) ? "" : $"cs07c_{id++:00}", text = text,
            };
            asset.shots = new[]
            {
                Shot(S(1), 2.5f),                                                                       // S1: the present
                Shot(S(2), 3.2f, "THE ROOM", "On a cold winter's night that was so deep."),             // S2: the memory
                Shot(S(3), 3f, "THE ROOM", "Noel, Noel, Noel, Noel."),
                Shot(S(4), 3f, "THE ROOM", "Born is the King of Israel."),
                Shot(S(5), 2f, "SCIENTIST", "Okay, okay. Cups up."),
                Shot(S(5), 1.3f, "", "", cork),                                                          // the cork; the room cheers once
                Shot(S(5), 5f, "SCIENTIST", "Merry Christmas, everybody! I wish you all happiness, good health, and lots of money in the bank account!"),
                Shot(S(6), 2.2f, "THE ROOM", "Merry Christmas!", cheer),
                Shot(S(6), 2f, "A NURSE", "Happy birthday, Gene!"),
                Shot(S(6), 1.6f, "GENE", "It's in July."),
                Shot(S(7), 2.4f),                                                                       // S7: the cheer stops, the lights blink
            };
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // ── The logs of the season (§8, L24 to L29) ───────────────────

        public static ItemDefinition L24() => DemoItems.Log("l24_colton_thread", "A THREAD OF THREE EMAILS",
            "FROM: COLTON. 21 DEC. The tincture batch came in today, nine days late. I will not leave V3 for January. Day 0 is tomorrow, in one step, not the two-week ramp. Her mother agrees. I fly on the 23rd.\n\n" +
            "FROM: WARD 2, STATION. 24 DEC. V3 does not know her name this morning. Or ours. Please call the ward.\n\n" +
            "AUTO-REPLY: Dr. Colton is out of the office until 4 January, with his family. For urgent ward matters, contact the duty physician.");

        public static ItemDefinition L25() => DemoItems.Log("l25_winter_schedule", "A MEMO",
            DemoPlan.Log("L25", "STAFF OFFICE. THE WINTER SCHEDULE. The Memorial keeps its normal schedule on 25 December and 1 January. Our feast is Future Day, in February, and on that day all staff rest with full pay. On 31 December the chapel is open all night. If you keep a private holiday in December, you may request leave, subject to cover. We know this season matters to many of you. Thank you for the hard days.\n\n") +
            "By hand under it: Subject to cover. There is no cover. That's the point.");

        public static ItemDefinition L26() => DemoItems.Log("l26_gene_birthday", "A PRINTED EMAIL",
            DemoPlan.Log("L26", "FROM: ROSA (WARD 2). TO: ALL STAFF, FLOORS 1 AND 2. RE: GENE'S BIRTHDAY!!! Handbook, section 9: birthdays may be celebrated in staff areas. Gene's birthday is 25 December this year. Cafeteria, 8 pm, between the shifts. Bring a dish. All decorations are birthday decorations.\n\n") +
            "FROM: GENE. My birthday is in July.\n\nFROM: ROSA. Not this year, Gene.");

        public static ItemDefinition L27() => DemoItems.Log("l27_the_25th", "A REPLY-ALL THREAD",
            DemoPlan.Log("L27", "FROM: DENISE (WARD 2, NIGHTS). RE: RE: RE: the 25th. Third year in a row my kids open their presents on a screen, because of the boss's weird-ass religion or cult or whatever.\n\n") +
            "FROM: WARD SISTER. Denise. This is the work account.\n\nFROM: DENISE. I know what account it is.");

        public static ItemDefinition L28() => DemoItems.Log("l28_law_flyer", "A LAW OFFICE'S FLYER",
            DemoPlan.Log("L28", "Printed: MADE TO WORK ON YOUR RELIGIOUS HOLIDAY? RELIGIOUS DISCRIMINATION IS AGAINST THE LAW. FREE CONSULTATION.\n\n") +
            "By hand: Who do we even sue? The church? The foundation? Alterius?\n\nAlterius signs the checks. The church signs Alterius's.\n\nRead your contract. Page 40. Arbitration.");

        public static ItemDefinition L29() => DemoItems.Log("l29_holiday_roster", "A ROSTER",
            DemoPlan.Log("L29", "SECURITY. HOLIDAY ROSTER, 23 DEC TO 2 JAN. NIGHTS: HOLIDAY MINIMUM, 4 ON SITE. RESPONSE TEAM: ON CALL, OFF SITE. NIGHT SUPERVISOR, ALL NIGHTS: STEVENS (BY REQUEST).\n\n") +
            "By hand: Pat you're a saint.\n\nAnother hand under it: He just hates his in-laws.");

        /// <summary>L24 on the station monitor of W9: an email open on the screen.</summary>
        public static void W9Email(DemoRoom r) => r.Item("L24_Station_Email", L24(), new Vector3(11.3f, 1.25f, -6.12f), new Vector3(0.3f, 0.22f, 0.01f));

        /// <summary>L27 on a monitor of G13 that a clerk left when she ran.</summary>
        public static void G13Email(DemoRoom r) => r.Item("L27_Reply_All", L27(), new Vector3(10.6f, 0.92f, -3.86f), new Vector3(0.34f, 0.24f, 0.01f));

        /// <summary>L14, the union letter (DEMO_PLAN §8): on the fridge of G11, a copy a guard kept. The plan's words, PROPOSED.</summary>
        public static ItemDefinition L14() => DemoItems.Log("l14_union_letter", "A UNION LETTER",
            DemoPlan.Log("L14", "Commissioner: My members did not sign up to carry grenade launchers. You bought them after the tower fire, when the ladders stopped at the ninth floor and nobody could get up the stairs. Fine. It worked that night. Since then the papers call us the army, the police ask to borrow them, and a probationer put a canister through a window with a family behind it. No other department in the country does this. Take them back. The union."));

        /// <summary>
        /// L37, the union sign-up sheet (round 119, CANON: the union angle; DEMO_PLAN §8, the words PROPOSED): on the
        /// corkboard of G11 by the union letter L14, the executive notice pinned over it.
        /// </summary>
        public static ItemDefinition L37() => DemoItems.Log("l37_union_sheet", "A SIGN-UP SHEET",
            DemoPlan.Log("L37", "Pinned over it: A MESSAGE FROM THE EXECUTIVE OFFICE. WE VALUE DIRECT CONVERSATION WITH EVERY MEMBER OF OUR FAMILY. OUR DOOR IS OPEN. NO THIRD PARTY KNOWS YOUR WORK AS WE DO.\n\n") +
            "Under it: STAFF OF THE MEMORIAL. YOU HAVE THE RIGHT TO ASK FOR A VOTE. SIGN HERE. Thirty-one names. Four are crossed out.\n\n" +
            "By hand: Christmas was the last straw.\nA union won't get you Christmas. Talk to HR, they listened about the parking.\nHR works for them.");

        /// <summary>L14 on the fridge, L37 on the corkboard of the east wall (round 119).</summary>
        public static void G11Union(DemoRoom r)
        {
            r.Item("L14_Union_Letter", L14(), new Vector3(5.3f, 1.3f, -16.97f), new Vector3(0.21f, 0.28f, 0.01f));
            r.Box(r.Props, "Corkboard", new Vector3(5.84f, 1.4f, -17.75f), new Vector3(0.03f, 0.6f, 0.8f), DemoRoom.Colour("cork", new Color(0.6f, 0.45f, 0.3f)));
            r.Item("L37_Union_Sheet", L37(), new Vector3(5.81f, 1.4f, -17.75f), new Vector3(0.01f, 0.3f, 0.22f));
        }

        /// <summary>L28 taped to the lockers of G11.</summary>
        public static void G11Flyer(DemoRoom r) => r.Item("L28_Law_Flyer", L28(), new Vector3(-1.33f, 1.3f, -20.2f), new Vector3(0.01f, 0.28f, 0.21f));

        /// <summary>L29, the duty roster on the north wall of G9.</summary>
        public static void G9Roster(DemoRoom r) => r.Item("L29_Holiday_Roster", L29(), new Vector3(29.6f, 1.5f, -9.14f), new Vector3(0.21f, 0.3f, 0.01f));

        // ── W14, the anime of the church (N2) ─────────────────────────

        /// <summary>The last minute of the episode of Things to Come, then the end credits, as timed lines of the W14 TV.</summary>
        public static List<TapeLine> ThingsToCome()
        {
            var said = new (string speaker, string text)[]
            {
                ("CABAL", "There! There they go! That faint gleam of light."),
                ("PASSWORTHY", "I feel that what we've done is monstrous!"),
                ("CABAL", "What they've done is magnificent."),
                ("PASSWORTHY", "Will they come back?"),
                ("CABAL", "Yes, and go again and again, until the landing is made and the moon is conquered. This is only a beginning."),
                ("PASSWORTHY", "But if they don't come back, my son and your daughter, what of that, Cabal?"),
                ("CABAL", "Then presently others will go."),
                ("PASSWORTHY", "Oh God, is there ever to be any age of happiness? Is there never to be any rest?"),
                ("CABAL", "Rest enough for the individual man. Too much and too soon, and we call it death. But for Man, no rest and no ending. He must go on, conquest beyond conquest."),
                ("CABAL", "First this little planet and its winds and ways, and then all the laws of mind and matter that restrain him."),
                ("CABAL", "Then the planets about it, and at last out across immensity to the stars."),
                ("CABAL", "And when he has conquered all the deeps of space and all the mysteries of time, still he will be beginning."),
                ("PASSWORTHY", "But we're such little creatures. Our humanity is so fragile, so weak. Little animals."),
                ("CABAL", "Little animals. And if we're no more than animals, we must snatch each little scrap of happiness and live and suffer and pass, mattering no more than all the other animals do or have done."),
                ("CABAL", "It is this, or that. All the universe or nothingness. Which shall it be, Passworthy? Which shall it be?"),
                ("", "[The end credits roll: the names of an animation staff, fast and small, white on black.]"),
                ("", "© THE TECHNOPURITAN CHURCH. ALL RIGHTS RESERVED."),
            };
            var lines = new List<TapeLine>();
            float at = 0.5f;
            foreach ((string speaker, string text) in said)
            {
                lines.Add(new TapeLine { at = at, speaker = speaker, text = text });
                at += SubtitleService.ReadSeconds(text) + 0.4f;
            }
            return lines;
        }

        /// <summary>Lucy's examine text on the TV of W14 (CANON rounds 108 to 110; text only: not one of her spoken lines).</summary>
        public const string LucyOnTheTv = "Aw, man. I tuned in right at the end! Things to Come is the best cartoon I've ever seen. Like Starship Troopers, you can watch it for the pew-pew space lasers, or as a meditation on progress and technology. And this one is clearly on the side of the man in the arena, as Roosevelt said. Well, I can ask a nurse to put in the Blu-ray so I can catch up later.";

        /// <summary>
        /// Her text after the anime (N2 of game/lines/N1_news.md, CANON round 146): an audio log in her voice, like the art logs.
        /// One subtitle line a sentence, under LUCY, at reading pace. No line in the file: her copied text.
        /// </summary>
        public static ItemDefinition N2Log()
        {
            var said = DemoScripts.Lines("lines/N1_news.md", "N2", "*").FindAll(l => l.speaker == "LUCY");
            string text = said.Count > 0 ? string.Join(" ", said.ConvertAll(l => l.text)) : LucyOnTheTv;
            ItemDefinition item = ItemAssets.Item("log_n2_things_to_come", "THINGS TO COME", ItemKind.AudioLog, ItemAssets.TapeColour,
                "Her own voice, about the cartoon on the day room TV.", "", GlorifiedBody.Player.DrugType.Nalcon);
            item.audioClip = null;
            item.tapeLines = new List<TapeLine>();
            float at = 0.4f;
            foreach (string sentence in DemoArt.Sentences(text))
            {
                item.tapeLines.Add(new TapeLine { at = at, speaker = "LUCY", text = sentence });
                at += SubtitleService.ReadSeconds(sentence) + 0.2f;
            }
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>
        /// The W14 TV plays the end of the episode when she walks in (round 108: "with no input from the player"). Interact
        /// watches the rest full screen from where she stands, and her text shows at the end of the show (Bruno, 2026-11-11).
        /// </summary>
        public static void W14Tv(DemoRoom r, GameObject tv)
        {
            r.Beat("W14_TV", W14TvDone, new Vector3(-1.1f, 1f, 12.75f), new Vector3(1.2f, 2f, 1.8f), ScriptStep.Screen(tv));
            InWorldScreen screen = tv.GetComponent<InWorldScreen>();
            screen.thing = "the TV";
            screen.range = 3.2f;
            screen.watchable = true;
            screen.watchStill = DemoV2.Still("n2_speech");
            screen.creditsStill = DemoV2.Still("n2_credits");
            screen.endComment = LucyOnTheTv;
            screen.endLog = N2Log(); // round 146: an audio log in her voice
            EditorUtility.SetDirty(screen);
        }

        // ── R1, the TV behind the till (N1b) ──────────────────────────

        /// <summary>
        /// The lines of N1b in the writer's file: the report before "Low", the item of each band (low, middle, high), and the
        /// lines of "Every band" (the last is cut off). The picture is the last [bracket] of a line; her ID photo is a
        /// PLACEHOLDER until the picture exists (round 145). False when the file has no N1b.
        /// </summary>
        public static bool N1b(out List<Broadcast.Line> opening, out Broadcast.Line[] bands, out List<Broadcast.Line> every)
        {
            opening = new List<Broadcast.Line>();
            bands = new Broadcast.Line[3];
            every = new List<Broadcast.Line>();
            string path = System.IO.Path.Combine(DemoScripts.WriterGame, "lines/N1_news.md");
            if (!System.IO.File.Exists(path)) return false;
            bool inPart = false;
            int part = -1; // -1 the report, 0..2 the bands, 3 every band
            int n = 0;
            foreach (string raw in System.IO.File.ReadAllLines(path))
            {
                if (raw.StartsWith("## ")) { inPart = raw.StartsWith("## N1b"); continue; }
                if (!inPart) continue;
                if (raw.StartsWith("Low")) part = 0;
                else if (raw.StartsWith("Middle")) part = 1;
                else if (raw.StartsWith("High")) part = 2;
                else if (raw.StartsWith("Every band")) part = 3;
                if (!raw.StartsWith("TV: ")) continue;
                string text = raw.Substring(4);
                string picture = "";
                var bracket = System.Text.RegularExpressions.Regex.Match(text, @"\[([^\[\]]+)\]\s*$");
                if (bracket.Success) { picture = bracket.Groups[1].Value; text = text.Substring(0, bracket.Index); }
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*\([^()]*\)\s*$", "").Trim();
                if (picture.Contains("ID photo")) picture = "PLACEHOLDER: " + picture; // the first adult face of Lucy: production work
                Broadcast.Line line = Line("TV", $"n1b_{n++:00}", text, picture);
                if (part < 0) opening.Add(line);
                else if (part < 3) bands[part] = line;
                else every.Add(line);
            }
            return opening.Count > 0 && bands[0] != null && bands[1] != null && bands[2] != null && every.Count > 0;
        }

        /// <summary>
        /// The bulletin of the morning, low under the organ, mid-sentence when she steps in. The last line follows the body
        /// count, then it is cut off and the attendant switches to music. The poster beat waits for that (NewsDone).
        /// The Interact key on the TV shows the picture of the line (first pass: the picture as text).
        /// </summary>
        public static void R1Tv(DemoRoom r)
        {
            GameObject tv = r.Box(r.Props, "TV_Till", new Vector3(66.2f, 2.0f, 25.72f), new Vector3(0.6f, 0.4f, 0.3f), DemoRoom.Grey(0.12f));
            r.Box(r.Props, "TV_Till_Face", new Vector3(66.2f, 2.0f, 25.565f), new Vector3(0.5f, 0.32f, 0.01f), DemoRoom.Colour("tv_face", new Color(0.3f, 0.36f, 0.42f)));
            Examinable pictures = MenuLoopSetup.EnsureComponent<Examinable>(tv);
            pictures.thing = "the TV";
            pictures.range = 3.2f;
            Broadcast news = MenuLoopSetup.EnsureComponent<Broadcast>(tv);
            news.roomId = "R1";
            news.lines = new[]
            {
                Line("TV", "n1b_00", "...in uniform for the first time this morning, a week after the vote. Japan has an army again, in name as well as in fact: the JSSDF.", "a parade under grey sky, new flags"),
                Line("TV", "n1b_01", "In Washington, the Pentagon sends a third carrier group to the region, and American forces stay on high alert.", "a carrier at sea, aircraft on the deck"),
                Line("TV", "n1b_02", "It follows more reports that North Korea gave nuclear material to armed groups. Pyongyang denies it. None of the reports is confirmed.", "a map of the region, the border drawn red"),
                Line("TV", "n1b_03", "Here at home, the police strike enters its fifth day. The union walked out over pay and pensions, and the talks broke down again last night.", "a picket line in the snow, outside a precinct house"),
                Line("TV", "n1b_04", "The Guard holds the city in their place. Checkpoints now stand on the bridges and on the expressway. Expect delays into the city.", "a Guard checkpoint on a bridge, a barrier, a line of cars"),
                Line("TV", "n1b_05", "The mayor calls it a season of patience. The shops on Market Street call it something else.", "a shop window with its Christmas lights, an empty pavement"),
                Line("TV", "n1b_06", "The Guard says that the checkpoints stay through New Year's Eve, and the anniversary.", "a checkpoint at night, the city lights behind it"),
            };
            news.byBand = new[]
            {
                Line("TV", "n1b_low", "Also overnight: an incident at the Winwood Reade Memorial. The motive is unknown.", "the gates of the Memorial at dawn, a patrol car"),
                Line("TV", "n1b_middle", "Also overnight: a shooter attacked the Winwood Reade Memorial. [N] people are dead. The motive is unknown.", "the gates of the Memorial at dawn, tape across them"),
                Line("TV", "n1b_high", "Also overnight: [N] people are dead after a shooter attacked the Winwood Reade Memorial. The motive is unknown, and no group has claimed it.", "the gates of the Memorial at dawn, tape, a line of ambulances"),
            };
            news.closing = new[] { Line("TV", "n1b_cut", "Police are asking citizens to be on the lookout for -") };
            // The writer's N1b (game/lines/N1_news.md, CANON rounds 143 and 145): the report, one item by the band, then the
            // description and her ID photo (the photo is production work: a placeholder picture until it exists).
            if (N1b(out var opening, out var bands, out var every))
            {
                news.lines = opening.ToArray();
                news.byBand = bands;
                news.closing = every.ToArray();
            }
            news.doneFlag = NewsDone;
            news.loop = Loop("tv_music_loop");
            news.loopVolume = 0.2f;
            news.pictures = pictures;
            news.afterComment = "[A music channel. A Christmas song, low.]";
            pictures.comment = "[" + news.lines[0].picture + "]";
            EditorUtility.SetDirty(pictures);
            EditorUtility.SetDirty(news);
        }

        // ── W9, the window on the yard (beat 14) ──────────────────────

        public const float YardWindowWidth = 1.4f;
        public const string BackdropPath = "Assets/_Game/Props/yard_backdrop.png";
        private const float WindowX = 17f, WindowZ = -3f, Sill = 0.9f, YardY = -4f;

        /// <summary>
        /// The window of W9 on the yard (beat 14, CANON round 64 and 65: two grey armoured vans, an MRAP and a caveirão,
        /// weathered, the church's gear stencilled on them). Bruno, 2026-11-10: "add placeholder vans / background". The old
        /// window looked into the gym W7, so the window is in the east wall of W9 where no room touches it. A sill, a frame
        /// and a pane she cannot pass (a collider, no picture). Outside, 4 m down at the level of floor 1: the yard, a
        /// fence, two vans in boxes, two yard lamps, and a night backdrop. Nothing outside has a collider (the room scans
        /// and the NavMesh ignore it); it draws with W9.
        /// </summary>
        public static void Yard(DemoRoom r)
        {
            float height = DemoRoom.DoorHeight;
            GameObject window = MenuLoopSetup.EnsureChild(r.Props, "Yard_Window");
            r.Box(window, "Sill", new Vector3(WindowX, Sill * 0.5f, WindowZ), new Vector3(DemoRoom.Wall, Sill, YardWindowWidth), DemoRoom.Grey(0.3f));
            r.Box(window, "Mullion", new Vector3(WindowX, (Sill + height) * 0.5f, WindowZ), new Vector3(0.06f, height - Sill, 0.06f), DemoRoom.Grey(0.2f));
            GameObject pane = MenuLoopSetup.EnsureChild(window, "Pane");
            pane.transform.position = new Vector3(WindowX, (Sill + height) * 0.5f, WindowZ);
            BoxCollider glass = MenuLoopSetup.EnsureComponent<BoxCollider>(pane);
            glass.size = new Vector3(0.1f, height - Sill, YardWindowWidth);

            GameObject yard = MenuLoopSetup.EnsureChild(r.Props, "Yard");
            Bare(r.Box(yard, "Yard_Ground", new Vector3(29f, YardY - 0.05f, -1f), new Vector3(24f, 0.1f, 26f), DemoRoom.Colour("yard_ground", new Color(0.11f, 0.11f, 0.12f))));
            Bare(r.Box(yard, "Yard_Fence", new Vector3(41f, YardY + 1.5f, -1f), new Vector3(0.3f, 3f, 26f), DemoRoom.Colour("yard_fence", new Color(0.16f, 0.16f, 0.17f))));
            Bare(r.Box(yard, "Yard_Backdrop", new Vector3(48f, YardY + 7f, -1f), new Vector3(0.05f, 14f, 32f), Backdrop()));
            foreach ((string tag, float x, float z) in new[] { ("A", 24f, 7f), ("B", 37f, -10f) })
            {
                Bare(r.Box(yard, "Lamp_Post_" + tag, new Vector3(x, YardY + 2.5f, z), new Vector3(0.15f, 5f, 0.15f), DemoRoom.Grey(0.15f)));
                Bare(r.Box(yard, "Lamp_Head_" + tag, new Vector3(x, YardY + 5f, z), new Vector3(0.5f, 0.2f, 0.3f), DemoRoom.Colour("lamp_head", new Color(1f, 0.9f, 0.65f))));
            }
            // An MRAP, tall and square, and a caveirão, lower, its nose sloped: flat grey, weathered, the gear on the near side.
            Van(r, yard, "Van_MRAP", new Vector3(27f, YardY, -4.5f), new Vector3(2.5f, 2.6f, 6.4f), sloped: false);
            Van(r, yard, "Van_Caveirao", new Vector3(31.5f, YardY, 1.5f), new Vector3(2.4f, 2.2f, 5.6f), sloped: true);
        }

        private static void Van(DemoRoom r, GameObject yard, string name, Vector3 floor, Vector3 size, bool sloped)
        {
            GameObject van = MenuLoopSetup.EnsureChild(yard, name);
            Material grey = DemoRoom.Colour("van_grey", new Color(0.36f, 0.37f, 0.36f)), dark = DemoRoom.Colour("van_dark", new Color(0.06f, 0.06f, 0.07f));
            float clearance = 0.55f;
            Bare(r.Box(van, "Body", floor + new Vector3(0f, clearance + size.y * 0.5f, 0f), size, grey));
            Bare(r.Box(van, "Windscreen", floor + new Vector3(0f, clearance + size.y * 0.78f, size.z * 0.5f + 0.01f), new Vector3(size.x * 0.8f, size.y * 0.22f, 0.02f), dark));
            if (sloped)
            {
                GameObject nose = Bare(r.Box(van, "Nose", floor + new Vector3(0f, clearance + size.y * 0.35f, size.z * 0.5f + 0.45f), new Vector3(size.x * 0.96f, size.y * 0.7f, 1.1f), grey));
                nose.transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
            }
            foreach (float dz in new[] { -size.z * 0.32f, size.z * 0.32f })
                foreach (float dx in new[] { -size.x * 0.5f, size.x * 0.5f })
                    Bare(r.Box(van, $"Wheel_{(dx < 0 ? "W" : "E")}{(dz < 0 ? "S" : "N")}", floor + new Vector3(dx, 0.5f, dz), new Vector3(0.4f, 1f, 1f), dark));
            // The gear of the church, stencilled on the side she sees (the west side, toward the window).
            GameObject gear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gear.name = "Gear_Stencil";
            gear.transform.SetParent(van.transform, false);
            gear.transform.position = floor + new Vector3(-size.x * 0.5f - 0.02f, clearance + size.y * 0.55f, 0f);
            gear.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            gear.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
            gear.GetComponent<Renderer>().sharedMaterial = DemoRoom.Colour("van_stencil", new Color(0.75f, 0.75f, 0.72f));
            Bare(gear);
        }

        /// <summary>No collider and no shadow: the yard is a picture through the window.</summary>
        private static GameObject Bare(GameObject obj)
        {
            Collider c = obj.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return obj;
        }

        private static Material Backdrop()
        {
            string path = ItemAssets.MaterialsFolder + "/GB_Yard_Backdrop.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(ItemAssets.UnlitShaderName));
                AssetDatabase.CreateAsset(material, path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropPath);
            if (texture == null)
            {
                AssetDatabase.ImportAsset(BackdropPath);
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropPath);
            }
            if (texture == null) Debug.LogWarning("[DemoNight] no backdrop " + BackdropPath + " (run tools/christmas_placeholders.py)");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static Broadcast.Line Line(string speaker, string id, string text, string picture = "") =>
            new Broadcast.Line { speaker = speaker, id = id, text = text, picture = picture };

        private static AudioClip Loop(string name)
        {
            string path = BroadcastFolder + "/" + name + ".ogg";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                AssetDatabase.ImportAsset(path);
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            if (clip == null) Debug.LogWarning("[DemoNight] no loop " + path + " (run tools/christmas_placeholders.py)");
            return clip;
        }
    }
}
#endif
