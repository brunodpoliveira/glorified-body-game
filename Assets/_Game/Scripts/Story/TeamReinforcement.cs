using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// The door of sector C needs soldiers (Bruno, 2026-11-06: "if the player kills all four soldiers, then spawn in more
    /// soldiers"). When the flag goes up, Control tells the living soldiers of the team where she is, and they come. When all
    /// of them are dead, then or later, after a silence, it raises the flag of the group that comes after (a FlagGate). The figures appear when
    /// she is not in their room. They know where she is too. The flags are in the save, so it happens once.
    /// </summary>
    public sealed class TeamReinforcement : MonoBehaviour
    {
        [Tooltip("The story flag of the beat: sector C released.")]
        public string afterFlag = "";
        [Tooltip("The flag this raises when the team is dead: the group's FlagGate shows the new soldiers.")]
        public string reinforcementFlag = "";
        [Tooltip("The figures of the team, by name, under this object.")]
        public Transform teamParent;
        public string[] team = new string[0];
        [Tooltip("The group that comes after.")]
        public Transform group;
        [Tooltip("The room of the group: they appear when she is not in it.")]
        public GlorifiedBody.Rooms.Room room;

        public const float SilenceSeconds = 20f; // the writer: the kill has its silence before they come, PROPOSED
        private const float CheckGap = 0.5f;
        private float deadSince = -1f;
        private float nextCheck;
        private bool toldTeam;
        private bool toldGroup;

        /// <summary>The team has soldiers, and none of them lives.</summary>
        public bool TeamDead
        {
            get
            {
                int found = 0;
                foreach (Health h in Team())
                {
                    found++;
                    if (!h.IsDead) return false;
                }
                return found > 0;
            }
        }

        private IEnumerable<Health> Team()
        {
            if (teamParent == null) yield break;
            foreach (string figure in team)
            {
                Transform t = teamParent.Find(figure);
                if (t != null && t.TryGetComponent(out Health h)) yield return h;
            }
        }

        private void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + CheckGap;
            if (deadSince < 0f && TeamDead) deadSince = Time.time;
            if (string.IsNullOrEmpty(afterFlag) || !StoryFlags.Has(afterFlag)) return;
            if (!toldTeam)
            {
                toldTeam = true;
                foreach (Health h in Team())
                    if (!h.IsDead && h.TryGetComponent(out SoldierBrain brain)) Tell(brain);
            }
            if (!StoryFlags.Has(reinforcementFlag))
            {
                GameObject her = GameObject.FindWithTag("Player");
                if (deadSince >= 0f && Time.time - deadSince >= SilenceSeconds && (room == null || her == null || !room.Contains(her.transform.position)))
                {
                    StoryFlags.Raise(reinforcementFlag);
                    RunTelemetry.Event("reinforcements", reinforcementFlag);
                }
                return;
            }
            if (toldGroup || group == null) return;
            SoldierBrain[] brains = group.GetComponentsInChildren<SoldierBrain>();
            if (brains.Length == 0) return;
            toldGroup = true;
            foreach (SoldierBrain brain in brains) Tell(brain);
            if (brains[0].Current != SoldierBrain.Phase.Dead) BarkBook.Say(brains[0].gameObject, BarkSituation.TeamSecond, 2f);
        }

        private static void Tell(SoldierBrain brain)
        {
            brain.Alert("control", callOthers: false);
            GameObject her = GameObject.FindWithTag("Player");
            if (her != null) brain.Team?.Told(her.transform.position);
        }
    }
}
