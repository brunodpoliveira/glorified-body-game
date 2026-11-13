using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Rooms;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The soldiers of a fight as one team (Bruno, 2026-11-05: "they need to be (and look and feel like) an organized team ...
    /// the player shouid success by outsmarting them"). Invector has no team AI (devlog-notes/2026-11-05_study_squad_ai.md).
    /// Two elements take turns: one moves while the other holds and fires, then they swap (bounding overwatch). That fits
    /// "move or shoot, never both": a mover never fires, and that is her window. The calls tell the player that it is on purpose
    /// (F.E.A.R.). A team with one left has no turns. Numbers PROPOSED.
    /// The team knows only what it saw (2026-11-06, the rest of DEMO_PLAN §13.6). She leaves every line (HiddenAfter): the holders
    /// fire at the place where they last saw her, and the movers close in on that place, not on her. Nobody sees her for
    /// LostAfter: she is lost. The team goes through that place and the rooms next to it in pairs, one walks and one covers. A
    /// soldier who sees her, a hit on one of them, or a power near them ends the search at once. So she can break contact and move.
    /// </summary>
    public sealed class FireTeam
    {
        public const float JoinRadius = 25f;
        public const float MinBound = 2f;   // s: a bound lasts at least this
        public const float MaxBound = 7f;   // s: and at most this
        private const float CallGap = 6f;   // s between the calls of a team
        private const float ReplyDelay = 1f; // s from "moving" to "covering"
        public const float HiddenAfter = 1.5f; // s with no line: they fire at the place
        public const float LostAfter = 8f;     // s with no line: they search
        private const float SightGap = 0.2f;   // s between the sight checks of a team
        private const float PartnerOffset = 1.5f; // m: the second of a pair stops this far beside the first
        private const float MinPartnerGap = 1.1f; // m: never closer to the first than this (two agents of 0.5 m)
        private const float SearchCallDelay = 2.5f; // s from "clear" to the next leg's line
        private const int RouteLength = 5;     // the place, then the nearest rooms next to it

        private static readonly List<FireTeam> teams = new List<FireTeam>();

        private readonly List<SoldierBrain>[] elements = { new List<SoldierBrain>(), new List<SoldierBrain>() };
        private int moving;
        private float boundStart;
        private float nextCall;
        private float replyAt = -1f;
        private int aliveAtLastLook;
        private int lastFrame = -1;
        private int startedAs;
        private float lastSeenTime;
        private float nextSight;
        private bool searching;
        private readonly List<Vector3> route = new List<Vector3>();
        private readonly List<SearchPair> pairs = new List<SearchPair>();
        private readonly List<(float at, SoldierBrain who, string situation)> queued = new List<(float at, SoldierBrain who, string situation)>();

        private sealed class SearchPair
        {
            public SoldierBrain first, second;
            public int leg;
            public bool secondWalks;
            public Vector3 beside;      // the second's spot for this leg: chosen once (it swung with his own walk)
            public int besideLeg = -1;
        }

        /// <summary>The last place where a soldier of the team saw her.</summary>
        public Vector3 LastSeen { get; private set; }
        /// <summary>No soldier has a line to her: the holders fire at LastSeen, the movers close in on it.</summary>
        public bool HerHidden => Time.time - lastSeenTime >= HiddenAfter;
        /// <summary>Nobody has seen her for a long time: the pairs search.</summary>
        public bool Lost => Time.time - lastSeenTime >= LostAfter;
        public int Searches { get; private set; }
        public int LegsDone { get; private set; }
        public IReadOnlyList<Vector3> Route => route;

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
        public static FireTeam Join(SoldierBrain who, Vector3 her)
        {
            teams.RemoveAll(t => t.Empty);
            // A man on a post (the Dining Hall, SoldierBrain.LeashUntil) fights with the men of his post only: the team of the
            // concourse next door must not count him as one who bounds (2026-11-10, TheFireteam_Bounds: 100 % both moving).
            foreach (FireTeam team in teams)
                if (team.Near(who.transform.position) && team.Post == PostOf(who)) return team.Add(who);
            var fresh = new FireTeam { boundStart = Time.time, LastSeen = her, lastSeenTime = Time.time };
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

        private static GlorifiedBody.Rooms.Room PostOf(SoldierBrain b) => b != null && b.Leashed ? b.LeashRoom : null;

        /// <summary>The post of the team: the room its live men hold, or null for a team that hunts.</summary>
        private GlorifiedBody.Rooms.Room Post
        {
            get
            {
                foreach (List<SoldierBrain> e in elements)
                    foreach (SoldierBrain b in e)
                        if (Live(b)) return PostOf(b);
                return null;
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

        private static bool Live(SoldierBrain b) => b != null && b.Current != SoldierBrain.Phase.Dead && !b.Retreating && !b.IsDominated;

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
            if (Alive < aliveAtLastLook) CountLoss();
            aliveAtLastLook = Alive;
            if (elements[0].Count == 0 && elements[1].Count > 1) Rebalance(1, 0);
            else if (elements[1].Count == 0 && elements[0].Count > 1) Rebalance(0, 1);
            Look();
            if (Lost)
            {
                if (!searching) BeginSearch();
                SayQueued();
                return; // no bounds in a search: the pairs have their own turns
            }
            if (replyAt > 0f && Time.time >= replyAt)
            {
                replyAt = -1f;
                Say(elements[1 - moving], BarkSituation.TeamCovering);
            }
            if (Alive < 2) return;
            float bound = Time.time - boundStart;
            if (bound >= MaxBound || (bound >= MinBound && !AnyMoving(elements[moving]))) Swap();
        }

        /// <summary>The sight checks: any soldier with a line to her tells the team.</summary>
        private void Look()
        {
            if (Time.time < nextSight) return;
            nextSight = Time.time + SightGap;
            foreach (SoldierBrain b in Members)
            {
                if (!b.SpotsHer(Lost)) continue;
                if (HerHidden) RunTelemetry.Event("team_saw", $"{b.name} at {b.transform.position.x:0.0},{b.transform.position.z:0.0}: {b.HerPosition.x:0.0},{b.HerPosition.z:0.0}");
                Saw(b.HerPosition);
                return;
            }
        }

        /// <summary>A hit from her, or a power near a soldier: the team knows where she is now.</summary>
        public void Heard(Vector3 her) => Saw(her);

        /// <summary>Control tells them where she is (the fireteam at the door of sector C, the men who come after).</summary>
        public void Told(Vector3 her) => Saw(her);

        private void Saw(Vector3 her)
        {
            LastSeen = her;
            lastSeenTime = Time.time;
            if (!searching) return;
            searching = false;
            route.Clear();
            pairs.Clear();
            queued.Clear();
            boundStart = Time.time;
            RunTelemetry.Event("team_contact", $"{her.x:0.0},{her.z:0.0}");
            if (Say(elements[moving], BarkSituation.TeamContact) || Say(elements[1 - moving], BarkSituation.TeamContact)) nextCall = Time.time + CallGap;
        }

        /// <summary>One holder calls it, when the team starts to fire at the place.</summary>
        public void CallSuppression(SoldierBrain who)
        {
            RunTelemetry.Event("team_suppress", who.name);
            if (Time.time < nextCall) return;
            BarkBook.Say(who.gameObject, BarkSituation.TeamSuppressing, 2f);
            nextCall = Time.time + CallGap;
        }

        /// <summary>The route: where they last saw her, then the rooms next to it, the nearest door first. No rooms in the scene: points around the place.</summary>
        private void BeginSearch()
        {
            searching = true;
            Searches++;
            route.Clear();
            pairs.Clear();
            AddToRoute(LastSeen);
            Room room = Room.At(LastSeen);
            if (room != null)
            {
                var exits = new List<Room.Exit>(room.exits);
                exits.Sort((a, b) => (room.transform.TransformPoint(a.mouth) - LastSeen).sqrMagnitude.CompareTo((room.transform.TransformPoint(b.mouth) - LastSeen).sqrMagnitude));
                foreach (Room.Exit exit in exits)
                {
                    Room next = Room.Of(exit.to);
                    if (next != null && route.Count < RouteLength) AddToRoute(next.Centre);
                }
            }
            else
            {
                for (int i = 0; i < RouteLength - 1; i++) AddToRoute(LastSeen + Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward * 6f);
            }
            // Each soldier pairs with the nearest one still free: a pair stands together, so one can cover the other.
            var free = new List<SoldierBrain>(Members);
            while (free.Count > 0)
            {
                SoldierBrain first = free[0];
                free.RemoveAt(0);
                SoldierBrain second = null;
                foreach (SoldierBrain b in free)
                    if (second == null || (b.transform.position - first.transform.position).sqrMagnitude < (second.transform.position - first.transform.position).sqrMagnitude) second = b;
                free.Remove(second);
                pairs.Add(new SearchPair { first = first, second = second, leg = pairs.Count });
            }
            RunTelemetry.Event("team_lost_her", $"{LastSeen.x:0.0},{LastSeen.z:0.0}: {pairs.Count} pairs, {route.Count} points");
            if (Say(elements[moving], BarkSituation.TeamLostHer) || Say(elements[1 - moving], BarkSituation.TeamLostHer)) nextCall = Time.time + CallGap;
        }

        private void AddToRoute(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas)) route.Add(hit.position);
        }

        /// <summary>The partner of a soldier in the search, or null (the tests).</summary>
        public SoldierBrain PartnerOf(SoldierBrain who)
        {
            foreach (SearchPair p in pairs)
                if (p.first == who) return p.second;
                else if (p.second == who) return p.first;
            return null;
        }

        private SearchPair PairOf(SoldierBrain who)
        {
            foreach (SearchPair p in pairs)
                if (p.first == who || p.second == who) return p;
            var single = new SearchPair { first = who, leg = pairs.Count }; // he joined after the search began
            pairs.Add(single);
            return single;
        }

        /// <summary>
        /// The search order of a soldier: the point to walk to, or null when his partner walks and he covers him (watch: the
        /// place to look at). The first walks to the point, the second comes up beside him, then the next point.
        /// </summary>
        public Vector3? SearchOrder(SoldierBrain who, out Vector3 watch)
        {
            Tick();
            watch = LastSeen;
            if (route.Count == 0) return null;
            SearchPair pair = PairOf(who);
            bool firstLive = Live(pair.first), secondLive = Live(pair.second);
            Vector3 point = route[pair.leg % route.Count];
            watch = point;
            SoldierBrain walker = pair.secondWalks ? (secondLive ? pair.second : pair.first) : (firstLive ? pair.first : pair.second);
            if (who != walker) return null;
            if (who != pair.second) return point;
            if (pair.besideLeg != pair.leg)
            {
                pair.beside = Beside(point, pair.first, who);
                pair.besideLeg = pair.leg;
            }
            return pair.beside;
        }

        /// <summary>
        /// The second's spot by the first: to one side of the line he came along, else the other side, else behind, and never
        /// closer than MinPartnerGap to where the first stands (2026-11-10: a spot snapped to the NavMesh by a wall put them
        /// 0.6 m apart, and on the next leg the walker shoved his partner along for seconds).
        /// </summary>
        private static Vector3 Beside(Vector3 point, SoldierBrain first, SoldierBrain second)
        {
            Vector3 along = point - second.transform.position;
            along.y = 0f;
            along = along.sqrMagnitude > 0.01f ? along.normalized : second.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, along);
            Vector3 firstAt = first != null ? first.transform.position : point;
            foreach (Vector3 offset in new[] { side, -side, -along })
            {
                if (!NavMesh.SamplePosition(point + offset * PartnerOffset, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) continue;
                Vector3 gap = hit.position - firstAt;
                gap.y = 0f;
                if (gap.magnitude >= MinPartnerGap) return hit.position;
            }
            return NavMesh.SamplePosition(point - along * PartnerOffset * 1.5f, out NavMeshHit back, 1.5f, NavMesh.AllAreas) ? back.position : point;
        }

        /// <summary>He got to his point: his partner goes, or the pair takes the next point.</summary>
        public void LegDone(SoldierBrain who)
        {
            LegsDone++;
            SearchPair pair = PairOf(who);
            if (!pair.secondWalks && who == pair.first && Live(pair.second))
            {
                pair.secondWalks = true;
                return;
            }
            pair.secondWalks = false;
            pair.leg++;
            if (Time.time < nextCall || !Say(new List<SoldierBrain> { who }, BarkSituation.TeamSearchClear)) return;
            nextCall = Time.time + CallGap;
            // The next leg, as a dialogue between the two (F.E.A.R.: the player hears that it is on purpose).
            if (!Live(pair.first) || !Live(pair.second)) return;
            queued.Add((Time.time + SearchCallDelay, pair.first, BarkSituation.TeamSearchMoving));
            queued.Add((Time.time + SearchCallDelay + ReplyDelay, pair.second, BarkSituation.TeamSearchCovering));
        }

        private void SayQueued()
        {
            for (int i = queued.Count - 1; i >= 0; i--)
            {
                if (Time.time < queued[i].at) continue;
                if (searching && Live(queued[i].who)) Say(new List<SoldierBrain> { queued[i].who }, queued[i].situation);
                queued.RemoveAt(i);
            }
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
            if (Time.time >= nextCall && Say(elements[moving], BarkSituation.TeamMoving))
            {
                nextCall = Time.time + CallGap;
                replyAt = Time.time + ReplyDelay;
            }
        }

        private void CountLoss()
        {
            if (Alive == 0) return;
            string lines = Alive == 1 ? BarkSituation.TeamOneLeft : Alive == 2 ? BarkSituation.TeamTwoLeft : BarkSituation.TeamManDown;
            if (Alive == 2)
                foreach (SoldierBrain b in Members) b.BoundNow(); // the last two fall back at once
            Say(elements[elements[0].Count > 0 ? 0 : 1], lines);
            nextCall = Time.time + CallGap;
            replyAt = -1f;
            RunTelemetry.Event("team_lost", $"{Alive} left");
        }

        /// <summary>
        /// The first live man of the element with a line for the call says it (barks.csv, BarkBook). A power armour in the fight,
        /// in the room of that man, takes some of the calls he has rows for (contact, lost her, man down, one left; PROPOSED):
        /// he is not a member of the team, but he is in the fight.
        /// </summary>
        private static bool Say(List<SoldierBrain> element, string situation)
        {
            SoldierBrain speaker = null;
            foreach (SoldierBrain b in element)
                if (Live(b) && BarkBook.CanSay(b.gameObject, situation)) { speaker = b; break; }
            if (speaker == null) return false;
            SoldierBrain heavy = HeavyNear(speaker, situation);
            if (heavy != null && Random.value < HeavyCallChance) speaker = heavy;
            return BarkBook.Say(speaker.gameObject, situation, 2f);
        }

        private const float HeavyCallChance = 0.5f;

        private static SoldierBrain HeavyNear(SoldierBrain near, string situation)
        {
            foreach (EnemyMorale m in EnemyMorale.All)
            {
                if (m == null || m.RankOf != EnemyMorale.Rank.Heavy || m.Health == null || m.Health.IsDead) continue;
                if (!m.TryGetComponent(out SoldierBrain heavy) || !heavy.Alerted || !Live(heavy)) continue;
                if (!GlorifiedBody.Rooms.Room.Same(heavy.transform.position, near.transform.position)) continue;
                if (BarkBook.CanSay(heavy.gameObject, situation)) return heavy;
            }
            return null;
        }
    }
}
