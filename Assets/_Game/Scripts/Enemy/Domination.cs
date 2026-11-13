using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;

namespace GlorifiedBody.Enemy
{
    /// <summary>A figure that mk_edgar can take: SoldierBrain, CivilianBrain.</summary>
    public interface IDominable
    {
        bool IsDominated { get; }
        bool IsDeadFigure { get; }
        void BeginKneel();
        void EndKneel();
        void TakeMind(Dominated mind);
        void ReleaseMind();
    }

    /// <summary>A puzzle figure: mk_edgar takes it at full HP (EnemySpawn.dominableUnhurt).</summary>
    public sealed class DominableUnhurt : MonoBehaviour { }

    /// <summary>A figure that came out of mk_edgar by itself: it cannot be taken again, or each capsule buys more time from the same soldier.</summary>
    public sealed class MindSnapped : MonoBehaviour { }

    /// <summary>
    /// mk_edgar (Bruno, 2026-11-06: it works "either in puzzles (so it controls enemies at full health) or when the enemy has at least
    /// damage"; "civvies can be controlled on full health, guards and above need damage to be dominated. bosses cannot be dominated";
    /// "the player needs to be in range like in the other powers"; "spend huge amounts of its mk bar"; only a full circle). One mind at a
    /// time, PROPOSED: a new one lets the old one go, and that one is an enemy again.
    /// </summary>
    public static class Domination
    {
        public static Dominated Current { get; private set; }
        public const float TakeHpShare = 0.5f; // a soldier or the armour at this share of HP or less, PROPOSED

        /// <summary>Why she cannot take this target now (a result word for the HUD), or null.</summary>
        public static string Refusal(LockOnTargetable target, Vector3 her, float range, bool hasMk)
        {
            if (target == null || !target.IsAlive || target.IsProp) return "dom_none";
            IDominable mind = target.GetComponentInParent<IDominable>();
            if (mind == null || mind.IsDominated) return "dom_none";
            if (target.IsShockImmune || target.GetComponentInParent<MindSnapped>() != null) return "dom_boss";
            Vector3 to = target.transform.position - her;
            to.y = 0f;
            if (to.magnitude > range) return "dom_far";
            if (!hasMk) return "no_nalcon";
            return NeedsDamage(target) ? "dom_not_hurt" : null;
        }

        private static bool NeedsDamage(LockOnTargetable target)
        {
            if (target.GetComponentInParent<DominableUnhurt>() != null) return false;
            EnemyMorale morale = target.GetComponentInParent<EnemyMorale>();
            if (morale != null && (morale.RankOf == EnemyMorale.Rank.UnarmedCivilian || morale.RankOf == EnemyMorale.Rank.ArmedCivilian)) return false;
            Health health = target.GetComponentInParent<Health>();
            if (health == null) return true;
            // A guard needs any damage. A soldier or the armour must be half dead (Bruno, 2026-11-07), so he is a weaker ally too.
            bool tough = morale != null && (morale.RankOf == EnemyMorale.Rank.Soldier || morale.RankOf == EnemyMorale.Rank.Heavy);
            return tough ? health.CurrentHealth > health.MaxHealth * TakeHpShare : health.CurrentHealth >= health.MaxHealth - 0.01f;
        }

        public static void Take(IDominable mind, GameObject her)
        {
            if (Current != null) Current.LetGo(); // Unity's null: a figure of the last room is gone
            var figure = (MonoBehaviour)mind;
            Dominated dominated = figure.gameObject.AddComponent<Dominated>();
            dominated.Bind(mind, her);
            Current = dominated;
            RunTelemetry.Event("dom_taken", figure.name);
        }

        public static void Forget(Dominated mind)
        {
            if (Current == mind) Current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Current = null;
    }

    /// <summary>
    /// The figure she holds with mk_edgar. It follows her, it fights her enemies when it has a gun (SoldierBrain runs the fight, with a
    /// colleague as its target), and it goes to a switch when she sends it (SentSwitch). A load brings it back as an enemy: only the
    /// dead are saved. Numbers PROPOSED, lines PLACEHOLDER (the writer owns the barks).
    /// </summary>
    public sealed class Dominated : MonoBehaviour
    {
        private const float PickGap = 0.3f;
        private const float FollowGap = 0.5f;
        private const float FollowStop = 2.5f;   // m from her
        private const float FightReach = 12f;    // m: it goes for an enemy this near
        private const float SprintOver = 8f;     // m
        private const float SwitchReach = 1.2f;  // m
        public const float MarkOverHead = 2.25f, MarkOverArmour = 3f; // m over his feet
        // The writer's lines (barks.csv, BarkBook): the groups of the enemy column, "fighter", "civilian" and "any", PROPOSED.
        private const float KneelLineSeconds = 2.5f; // the writer: the kneel lines have the whole circle

        // The core's voice through her (DEMO_PLAN, annotated by Bruno 2026-11-07, round 49: "Lucy speaks after she takes a
        // mind"): from her mouth and not her voice, and it does not count against P7. The label is VOICE (round 50). The
        // writer's lines (barks.csv, kind voice): the first three takes are Bruno's (CANON), the rest PROPOSED. No clip yet:
        // the subtitle alone (Voice.SayAs logs voice_missing).
        public const string CoreSpeaker = "VOICE";
        public static (string id, string text)[] CoreTakeLines => BarkBook.Set("voice", BarkSituation.EdgarVoiceTake, "edgar_voice_take_");
        public static (string id, string text)[] CoreSentLines => BarkBook.Set("voice", BarkSituation.EdgarVoiceSent, "edgar_voice_sent_");
        public static (string id, string text)[] CoreSnapLines => BarkBook.Set("voice", BarkSituation.EdgarVoiceSnap, "edgar_voice_snap_");
        /// <summary>The core's lines said so far (the tests).</summary>
        public static int CoreLines { get; private set; }
        public static string LastCoreLine { get; private set; } = "";

        /// <summary>The core speaks through her: one line of the set, as a subtitle under VOICE (and its clip, when it exists).</summary>
        public static void SayCore((string id, string text)[] set)
        {
            if (set.Length == 0) return;
            (string id, string text) line = set[Random.Range(0, set.Length)];
            CoreLines++;
            LastCoreLine = line.text;
            GlorifiedBody.Audio.Voice.SayAs(CoreSpeaker, line.id, line.text, GlorifiedBody.Audio.VoiceMode.Radio, 2f);
        }
        private const float SeenGap = 8f; // s between the lines of the colleagues

        /// <summary>A figure goes down on its knees: its line (the armed say the fighter lines).</summary>
        public static void SayKneel(GameObject figure, bool fighter)
        {
            if (fighter) BarkBook.SayAs(figure, "fighter", BarkSituation.EdgarKneelFighter, KneelLineSeconds);
            else BarkBook.SayAs(figure, "civilian", BarkSituation.EdgarKneelCivilian, KneelLineSeconds);
        }

        private static float nextSeen;

        /// <summary>A colleague turns his gun on this one: the line, not too often.</summary>
        public static void SaySeen(GameObject colleague)
        {
            if (Time.time < nextSeen) return;
            nextSeen = Time.time + SeenGap;
            BarkBook.SayAs(colleague, "fighter", BarkSituation.EdgarSeen, 2.5f);
        }


        private IDominable mind;
        private IEnemyBody body;
        private Transform her;
        private LockOnTargetable lockable;
        private Health health;
        private GameObject mark;
        private SentSwitch sentTo;
        private Transform enemy;
        private float nextPick;
        private float nextFollow;

        public Transform Enemy => enemy;
        public SentSwitch SentTo => sentTo;

        public void Bind(IDominable taken, GameObject lucy)
        {
            mind = taken;
            her = lucy.transform;
            body = GetComponent<IEnemyBody>();
            health = GetComponent<Health>();
            lockable = GetComponent<LockOnTargetable>();
            if (lockable != null) lockable.SetAlly(true);
            EnemyMorale morale = GetComponent<EnemyMorale>();
            TimeLeft = morale != null ? LimitFor(morale.RankOf, GetComponent<DominableUnhurt>() != null) : float.PositiveInfinity;
            if (health != null)
            {
                health.OnDeath ??= new UnityEngine.Events.UnityEvent();
                health.OnDeath.AddListener(OnDied);
            }
            mind.TakeMind(this);
            Mark();
            SayCore(CoreTakeLines); // the core first, through her mouth
            BarkBook.SayAs(gameObject, "any", BarkSituation.EdgarTaken, 2f);
        }

        /// <summary>A small violet light over the head: her enemies wear no mark, and she must tell this one from them. PLACEHOLDER.</summary>
        private void Mark()
        {
            mark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mark.name = "DominatedMark";
            Object.Destroy(mark.GetComponent<Collider>());
            mark.transform.SetParent(transform, false);
            // In metres over his feet: the root carries the model's scale, and a local offset put the mark far over his head (2026-11-07).
            float scale = Mathf.Max(0.01f, transform.lossyScale.y);
            bool tall = TryGetComponent(out EnemyMorale morale) && morale.RankOf == EnemyMorale.Rank.Heavy;
            mark.transform.localPosition = Vector3.up * ((tall ? MarkOverArmour : MarkOverHead) / scale);
            mark.transform.localScale = Vector3.one * (0.14f / scale);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader != null) mark.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.72f, 0.42f, 1f) };
        }

        private void OnDied()
        {
            Domination.Forget(this);
            if (mark != null) Destroy(mark);
        }

        // How long a fighter stays taken (Bruno, 2026-11-07: "for guards and soldiers there needs to be a limit where they snap out of it after a
        // while, but civvies are different: they can't fight, and i'm planning on doing puzzles"). Seconds, PROPOSED.
        public const float WarnSeconds = 5f;      // the mark flickers and he shakes
        public const float SnapKneelSeconds = 2f; // on his knees, then he is an enemy

        /// <summary>The time a rank stays taken. An unarmed civilian and a puzzle figure have no limit.</summary>
        public static float LimitFor(EnemyMorale.Rank rank, bool puzzle)
        {
            if (puzzle) return float.PositiveInfinity;
            switch (rank)
            {
                case EnemyMorale.Rank.UnarmedCivilian: return float.PositiveInfinity;
                case EnemyMorale.Rank.Guard: return 30f;
                case EnemyMorale.Rank.Heavy: return 12f;
                default: return 20f; // a soldier, an armed lab worker
            }
        }

        /// <summary>The seconds until he snaps out of it. It stops while he walks to a switch.</summary>
        public float TimeLeft { get; private set; } = float.PositiveInfinity;
        public bool Snapping => snapEnds > 0f;
        private float snapEnds;
        private Tremble trembling;
        private float trembleBefore;

        /// <summary>Tests and the bot: the time left now.</summary>
        public void SetTimeLeft(float seconds)
        {
            if (!float.IsInfinity(TimeLeft)) TimeLeft = seconds;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f || (health != null && health.IsDead)) return;
            if (Snapping)
            {
                if (Time.time >= snapEnds) FinishSnap();
                return;
            }
            if (float.IsInfinity(TimeLeft) || sentTo != null) return;
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f) BeginSnap();
            else if (TimeLeft <= WarnSeconds) Warn();
        }

        private void Warn()
        {
            if (mark != null) mark.SetActive(Mathf.Repeat(Time.time * 6f, 1f) > 0.4f);
            if (trembling != null) return;
            trembling = GetComponent<Tremble>();
            if (trembling == null) trembling = gameObject.AddComponent<Tremble>();
            trembleBefore = trembling.Amount;
            trembling.Amount = 1f;
        }

        /// <summary>The time ran out: he drops to his knees and says what she did. He cannot be taken again.</summary>
        private void BeginSnap()
        {
            snapEnds = Time.time + SnapKneelSeconds;
            Domination.Forget(this);
            if (lockable != null) lockable.SetAlly(false);
            if (mark != null) Destroy(mark);
            if (trembling != null) trembling.Amount = trembleBefore;
            gameObject.AddComponent<MindSnapped>();
            mind.BeginKneel();
            SayCore(CoreSnapLines);
            BarkBook.SayAs(gameObject, "any", BarkSituation.EdgarLetGo, SnapKneelSeconds);
            RunTelemetry.Event("dom_snap", name);
        }

        private void FinishSnap()
        {
            if (health != null) health.OnDeath?.RemoveListener(OnDied);
            mind.EndKneel();
            mind.ReleaseMind(); // he knows where she is
            Destroy(this);
        }

        /// <summary>She takes another one: this one is an enemy again.</summary>
        public void LetGo()
        {
            Domination.Forget(this);
            if (lockable != null) lockable.SetAlly(false);
            if (trembling != null) trembling.Amount = trembleBefore;
            if (health != null) health.OnDeath?.RemoveListener(OnDied);
            if (mark != null) Destroy(mark);
            mind.ReleaseMind();
            BarkBook.SayAs(gameObject, "any", BarkSituation.EdgarLetGo, 2.5f);
            RunTelemetry.Event("dom_let_go", name);
            Destroy(this);
        }

        /// <summary>The nearest unused switch within her reach that he can walk to. Null when there is none.</summary>
        public SentSwitch SwitchInReach(Vector3 from, float reach)
        {
            SentSwitch best = null;
            float bestDistance = reach * SwitchSearchShare;
            foreach (SentSwitch candidate in SentSwitch.All)
            {
                if (candidate == null || candidate.Used) continue;
                float distance = Vector3.Distance(from, candidate.transform.position);
                if (distance >= bestDistance || !HasRouteTo(candidate.Point)) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        // Without this he took the order, could never arrive, and his timer stood still for ever (it stops while he walks to a switch).
        private bool HasRouteTo(Vector3 point)
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start, 1.5f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(point, out NavMeshHit end, SwitchReach, NavMesh.AllAreas)) return false;
            route ??= new NavMeshPath();
            return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, route) && route.status == NavMeshPathStatus.PathComplete;
        }

        private NavMeshPath route;

        /// <summary>The defend press: the nearest switch he can reach, within her reach. False when there is none.</summary>
        public bool SendToSwitch(Vector3 from, float reach)
        {
            SentSwitch best = SwitchInReach(from, reach);
            if (best == null) return false;
            sentTo = best;
            body.MoveTo(best.Point, MoveSpeed.Run);
            SayCore(CoreSentLines);
            BarkBook.SayAs(gameObject, "any", BarkSituation.EdgarSent, 2f);
            nextFollow = Time.time + 0.5f;
            RunTelemetry.Event("dom_sent", best.name);
            return true;
        }

        private const float SwitchSearchShare = 2.5f; // she sends it farther than she reaches

        /// <summary>True when the brain runs its fight this frame (it has an enemy). A soldier's fight state calls it.</summary>
        public bool TickFight(SoldierBrain brain)
        {
            if (sentTo != null)
            {
                brain.StandDown();
                TickSent();
                return false;
            }
            if (Time.time >= nextPick)
            {
                nextPick = Time.time + PickGap;
                enemy = NearestEnemy();
            }
            if (enemy != null)
            {
                brain.Hunt(enemy);
                return true;
            }
            brain.StandDown();
            Follow();
            return false;
        }

        /// <summary>A civilian's whole tick: no fight, it follows and it goes where she sends it.</summary>
        public void TickWithoutAGun(IEnemyBody unused)
        {
            if (sentTo != null) TickSent();
            else Follow();
        }

        private void TickSent()
        {
            Vector3 to = sentTo.Point - transform.position;
            to.y = 0f;
            if (to.magnitude <= SwitchReach)
            {
                body.Stop();
                sentTo.Use(name);
                sentTo = null;
                return;
            }
            if (Time.time < nextFollow) return;
            nextFollow = Time.time + FollowGap;
            body.MoveTo(sentTo.Point, MoveSpeed.Run);
        }

        private void Follow()
        {
            if (her == null || Time.time < nextFollow) return;
            nextFollow = Time.time + FollowGap;
            Vector3 flat = her.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= FollowStop)
            {
                body.Stop();
                return;
            }
            Vector3 point = her.position - her.forward * 1.5f + her.right * 1f;
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)) point = hit.position;
            body.MoveTo(point, flat.magnitude > SprintOver ? MoveSpeed.Sprint : MoveSpeed.Run);
        }

        private Transform NearestEnemy()
        {
            Transform best = null;
            float bestDistance = FightReach;
            foreach (LockOnTargetable candidate in TargetRegistry.All)
            {
                if (candidate == null || candidate == lockable || candidate.IsProp || candidate.IsCivilian || !candidate.IsAlive) continue;
                SoldierBrain other = candidate.GetComponent<SoldierBrain>();
                if (other == null || other.IsDominated || other.Retreating) continue;
                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance >= bestDistance || !Room.Hear(transform.position, candidate.transform.position)) continue;
                best = candidate.transform;
                bestDistance = distance;
            }
            return best;
        }
    }
}
