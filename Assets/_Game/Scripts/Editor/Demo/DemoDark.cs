#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Interaction;
using GlorifiedBody.Rendering;
using GlorifiedBody.Rooms;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The dark room in the demo (DEMO_PLAN §10.1, Bruno's puzzle, CANON round 52; built in DARK TEST on 2026-11-08 and in
    /// the demo on 2026-11-09). She enters G9: the lights of G9 and the closet G9b go out, the monitor wall goes black,
    /// the three units in G9b beep. The release of sector C waits ("WAITING FOR NET."). NET, REC, ACCESS: the monitors
    /// come back one by one, then the lights. A wrong unit: the fault, 5 s, the countdown on the units. The sources of
    /// the order: the hub's units under the counter of W9 (their up-times), the IT note on the clerk's monitor in G8.
    /// </summary>
    public static class DemoDark
    {
        public const string DarkFlag = "demo_g9_dark", UnitsUp = "demo_g9_units_up";
        public static readonly string[] Order = { "NET", "REC", "ACCESS" };

        /// <summary>After every room is built (DemoWorld.BuildAll): the office and the closet go dark together.</summary>
        public static void Wire(Dictionary<string, DemoRoom> rooms)
        {
            if (!rooms.TryGetValue("G9", out DemoRoom office) || !rooms.TryGetValue("G9b", out DemoRoom closet)) return;
            DarkRoom dark = MenuLoopSetup.EnsureComponent<DarkRoom>(office.Root);
            dark.darkFlag = DarkFlag;
            dark.litFlag = UnitsUp; // the units up: the lights come back for good
            dark.alsoDark = new[] { closet.Root.GetComponent<Room>() };
            EditorUtility.SetDirty(dark);
            GameObject holder = MenuLoopSetup.EnsureChild(office.Story, "UPS_Bank");
            UpsBank bank = MenuLoopSetup.EnsureComponent<UpsBank>(holder);
            bank.darkRoom = dark;
            bank.doneFlag = UnitsUp;
            bank.order = Order;
            bank.units = Order.Select(l => closet.Props.transform.Find("UPS_" + l).GetComponent<UpsUnit>()).ToArray();
            foreach (UpsUnit u in bank.units) { u.bank = bank; EditorUtility.SetDirty(u); }
            Transform wall = office.Props.transform.Find("Screen_Monitors");
            bank.monitors = wall.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("CrtFace_")).ToArray();
            bank.monitorLights = Enumerable.Range(0, 5).Select(i => office.Props.transform.Find("Monitor_Light_" + i).GetComponent<Light>()).ToArray();
            bank.monitorGap = 0.3f;
            bank.restoreOwnLook = true; // her face on the wall again
            // The recorder plays the last thing it played: the intake tape of cutscene 08 (its lines are under "## The tape (S4)", round 138).
            var tape = DemoScripts.Lines("cutscenes/08_the_security_office.md", "The tape", "*");
            bank.recLine = tape.Count > 0 ? string.Join("  ", tape.GetRange(0, Mathf.Min(2, tape.Count)).ConvertAll(l => l.speaker + ": " + l.text))
                : "PLACEHOLDER: the recorder plays the last thing it played, the intake tape of cutscene 08.";
            EditorUtility.SetDirty(bank);
        }

        /// <summary>The hub's units under the counter of W9: up, green, each with its up-time. The one up longest came first.</summary>
        public static void HubUnits(DemoRoom r)
        {
            var uptimes = new[] { ("NET", "11:52:07"), ("REC", "11:51:40"), ("ACCESS", "11:51:02") };
            Material green = DemoRoom.Colour("led_green", new Color(0.2f, 0.9f, 0.3f));
            for (int i = 0; i < uptimes.Length; i++)
            {
                (string label, string up) = uptimes[i];
                Vector3 at = new Vector3(10.2f + i * 0.75f, 0f, -7.05f); // against the south face of the counter, the fronts to the room
                r.Box(r.Props, "Hub_UPS_" + label, at + Vector3.up * 0.22f, new Vector3(0.4f, 0.44f, 0.32f), DemoRoom.Grey(0.2f));
                GameObject led = r.Box(r.Props, "Hub_LED_" + label, at + new Vector3(-0.12f, 0.34f, -0.17f), new Vector3(0.04f, 0.04f, 0.02f), green);
                led.AddComponent<Emissive>();
                r.Sign("Hub_Display_" + label, label + " UP " + up, at + new Vector3(0.04f, 0.2f, -0.17f), Vector3.back, new Vector2(0.34f, 0.1f), 18);
            }
        }
    }
}
#endif
