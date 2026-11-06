using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The soldiers of a fight as one team (Bruno, 2026-11-05: "they need to be (and look and feel like) an organized team ...
    /// the player shouid success by outsmarting them"). Invector has no team AI (devlog-notes/2026-11-05_study_squad_ai.md).
    /// Two elements take turns: one moves while the other holds and fires, then they swap (bounding overwatch). That fits
    /// "move or shoot, never both": a mover never fires, and that is her window. The calls tell the player that it is on purpose
    /// (F.E.A.R.). A team with one left has no turns. Numbers PROPOSED.
    /// </summary>
    public sealed class FireTeam
    {
        public const float JoinRadius = 25f;
        public const float MinBound = 2f;   // s: a bound lasts at least this
        public const float MaxBound = 7f;   // s: and at most this
        private const float CallGap = 6f;   // s between the calls of a team
        private const float ReplyDelay = 1f; // s from "moving" to "covering"

        private static readonly List<FireTeam> teams = new List<FireTeam>();

        private readonly List<SoldierBrain>[] elements = { new List<SoldierBrain>(), new List<SoldierBrain>() };
        private int moving;
        private float boundStart;
        private float nextCall;
        private float replyAt = -1f;
        private int aliveAtLastLook;
        private int lastFrame = -1;
        private int startedAs;

        public int Swaps { get; private set; }
        public int Alive { get; private set; }
        public int MovingElement => moving;
        public int Size => elements[0].Count + elements[1].Count;
        /// <summary>Two or fewer left: they fall back to the last good cover (soldiers never run: an order, not a panic).</summary>
        public bool FallingBack => Alive <= 2 && lastFrame >= 0 && startedAs > 2;
        public IEnumerable<SoldierBrain> Members
        {
            get
            {
                foreach (List<SoldierBrain> e in elements)
                    foreach (SoldierBrain b in e) yield return b;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => teams.Clear();

        /// <summary>He joins the team near him, or starts one. The smaller element takes him.</summary>
        public static FireTeam Join(SoldierBrain who)
        {
            teams.RemoveAll(t => t.Empty);
            foreach (FireTeam team in teams)
                if (team.Near(who.transform.position)) return team.Add(who);
            var fresh = new FireTeam { boundStart = Time.time };
            teams.Add(fresh);
            return fresh.Add(who);
        }

        private bool Empty
        {
            get
            {
                foreach (List<SoldierBrain> e in elements)
                    foreach (SoldierBrain b in e)
                        if (Live(b)) return false;
                return true;
            }
        }

        private bool Near(Vector3 point)
        {
            foreach (List<SoldierBrain> e in elements)
                foreach (SoldierBrain b in e)
                    if (Live(b) && (b.transform.position - point).sqrMagnitude < JoinRadius * JoinRadius) return true;
            return false;
        }

        private FireTeam Add(SoldierBrain who)
        {
            List<SoldierBrain> small = elements[0].Count <= elements[1].Count ? elements[0] : elements[1];
            small.Add(who);
            startedAs = Mathf.Max(startedAs, elements[0].Count + elements[1].Count);
            RunTelemetry.Event("team_join", who.name);
            return this;
        }

        private static bool Live(SoldierBrain b) => b != null && b.Current != SoldierBrain.Phase.Dead && !b.Retreating;

        /// <summary>His element's turn to move, or a team too small for turns.</summary>
        public bool MayMove(SoldierBrain who)
        {
            Tick();
            return Alive < 2 || elements[moving].Contains(who);
        }

        public int ElementOf(SoldierBrain who) => elements[0].Contains(who) ? 0 : 1;

        /// <summary>Once a frame, from any member: the losses, the turn, the reply.</summary>
        public void Tick()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            foreach (List<SoldierBrain> e in elements) e.RemoveAll(b => !Live(b));
            Alive = elements[0].Count + elements[1].Count;
            if (Alive < aliveAtLastLook) Lost();
            aliveAtLastLook = Alive;
            if (elements[0].Count == 0 && elements[1].Count > 1) Rebalance(1, 0);
            else if (elements[1].Count == 0 && elements[0].Count > 1) Rebalance(0, 1);
            if (replyAt > 0f && Time.time >= replyAt)
            {
                replyAt = -1f;
                Say(elements[1 - moving], CoveringLines);
            }
            if (Alive < 2) return;
            float bound = Time.time - boundStart;
            if (bound >= MaxBound || (bound >= MinBound && !AnyMoving(elements[moving]))) Swap();
        }

        private void Rebalance(int from, int to)
        {
            SoldierBrain who = elements[from][elements[from].Count - 1];
            elements[from].Remove(who);
            elements[to].Add(who);
        }

        private static bool AnyMoving(List<SoldierBrain> element)
        {
            foreach (SoldierBrain b in element)
                if (b.Current == SoldierBrain.Phase.Move) return true;
            return false;
        }

        private void Swap()
        {
            moving = 1 - moving;
            boundStart = Time.time;
            Swaps++;
            foreach (SoldierBrain b in elements[moving]) b.BoundNow();
            RunTelemetry.Event("team_bound", $"element {moving}");
            if (Time.time >= nextCall && Say(elements[moving], MovingLines))
            {
                nextCall = Time.time + CallGap;
                replyAt = Time.time + ReplyDelay;
            }
        }

        private void Lost()
        {
            if (Alive == 0) return;
            string[] lines = Alive == 1 ? LastOneLines : Alive == 2 ? LastTwoLines : ManDownLines;
            if (Alive == 2)
                foreach (SoldierBrain b in Members) b.BoundNow(); // the last two fall back at once
            Say(elements[elements[0].Count > 0 ? 0 : 1], lines);
            nextCall = Time.time + CallGap;
            replyAt = -1f;
            RunTelemetry.Event("team_lost", $"{Alive} left");
        }

        private static bool Say(List<SoldierBrain> element, string[] lines)
        {
            foreach (SoldierBrain b in element)
            {
                if (!Live(b)) continue;
                Bark.Say(b.gameObject, lines[Random.Range(0, lines.Length)], 2f);
                return true;
            }
            return false;
        }

        // The writer's lines (barks.csv, team_*), PROPOSED.
        public static readonly string[] MovingLines = { "Moving up.", "Moving. Cover me.", "Next cover. Moving.", "I'm up. Moving.", "Changing position." };
        public static readonly string[] CoveringLines = { "Covering. Go.", "You're covered.", "Go. I've got you.", "Holding here. Go.", "Keeping her pinned." };
        public static readonly string[] ManDownLines = { "Man down. Keep firing.", "Man down. Oh, Jesus. Man down.", "Don't look. Stay on her.", "He's gone. Stay tight." };
        public static readonly string[] LastTwoLines = { "Two left. Fall back to cover.", "Falling back. Control, need support.", "Stay with me. Back to cover." };
        public static readonly string[] LastOneLines = { "Cover me. Somebody cover me.", "Control, come in. Control?", "It's just me. Hold. Hold." };
    }
}
