using System.Collections;
using System.Linq;
using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// The power armour of the Dining Hall (rounds 121 and 122, CANON: "once it is reduced to three the power armor comes
    /// in"; PROPOSED: it steps out of the lodge door when the squad calls for it). It watches the six of the hall: when
    /// three or fewer stand (dead or cowering do not stand), a living soldier calls for it (armour_call), the flag shows
    /// its group (it spawns at the lodge door), and its operator speaks as it steps out (armour_enter); then it hunts her.
    /// The flag is in the saves: a load does not bring it twice.
    /// </summary>
    public sealed class HeavyArrival : MonoBehaviour
    {
        public Transform squad;
        public GameObject armourGroup;
        public string flag = "demo_g18_armour";
        [Min(0)] public int standingAtMost = 3;
        public string callLine = "";
        public string enterLine = "";

        public bool Arrived { get; private set; }
        public int StandingAtCall { get; private set; } = -1;

        private void Update()
        {
            if (Arrived || squad == null) return;
            if (StoryFlags.Has(flag)) { Arrived = true; return; }
            SoldierBrain[] men = squad.GetComponentsInChildren<SoldierBrain>().Where(m => armourGroup == null || !m.transform.IsChildOf(armourGroup.transform)).ToArray();
            if (men.Length == 0) return; // not spawned yet
            int standing = men.Count(m => m.Current != SoldierBrain.Phase.Dead && !m.Retreating && !m.GetComponent<Health>().IsDead);
            if (squad.GetComponentsInChildren<EnemySpawn>(true).Any(e => armourGroup == null || !e.transform.IsChildOf(armourGroup.transform))) return; // the markers still wait
            if (standing > standingAtMost) return;
            Arrived = true;
            StandingAtCall = standing;
            StartCoroutine(Arrive(men.FirstOrDefault(m => m.Current != SoldierBrain.Phase.Dead && !m.GetComponent<Health>().IsDead)));
        }

        private IEnumerator Arrive(SoldierBrain caller)
        {
            if (caller != null && !string.IsNullOrEmpty(callLine)) Bark.Say(caller.gameObject, callLine, 2.6f);
            RunTelemetry.Event("armour_call", $"{name}:{StandingAtCall}");
            yield return new WaitForSeconds(1.5f);
            StoryFlags.Raise(flag);
            yield return null;
            yield return null;
            SoldierBrain armour = armourGroup != null ? armourGroup.GetComponentInChildren<SoldierBrain>() : null;
            if (armour != null)
            {
                // His own personality's "armour_enter" row (barks.csv, BarkBook), else the line the room gave.
                if (!GlorifiedBody.Enemy.BarkBook.Say(armour.gameObject, GlorifiedBody.Enemy.BarkSituation.ArmourEnter, 2.6f) && !string.IsNullOrEmpty(enterLine))
                    Bark.Say(armour.gameObject, enterLine, 2.6f);
                armour.Alert("script", callOthers: false);
            }
            RunTelemetry.Event("armour_enter", armour != null ? armour.name : "none");
        }
    }
}
