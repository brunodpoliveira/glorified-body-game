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

    /// <summary>
    /// mk_edgar (Bruno, 2026-11-06: it works "either in puzzles (so it controls enemies at full health) or when the enemy has at least
    /// damage"; "civvies can be controlled on full health, guards and above need damage to be dominated. bosses cannot be dominated";
    /// "the player needs to be in range like in the other powers"; "spend huge amounts of its mk bar"; only a full circle). One mind at a
    /// time, PROPOSED: a new one lets the old one go, and that one is an enemy again.
    /// </summary>
    public static class Domination
    {
        public static Dominated Current { get; private set; }

        /// <summary>Why she cannot take this target now (a result word for the HUD), or null.</summary>
        public static string Refusal(LockOnTargetable target, Vector3 her, float range, bool hasMk)
        {
            if (target == null || !target.IsAlive || target.IsProp) return "dom_none";
            IDominable mind = target.GetComponentInParent<IDominable>();
            if (mind == null || mind.IsDominated) return "dom_none";
            if (target.IsShockImmune) return "dom_boss";
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
            return health == null || health.CurrentHealth >= health.MaxHealth - 0.01f;
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
        private static readonly string[] TakenLines = { "...Yes.", "I hear you.", "Tell me where." };

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
            if (lockable != null) lockable.SetSelectable(false);
            if (health != null)
            {
                health.OnDeath ??= new UnityEngine.Events.UnityEvent();
                health.OnDeath.AddListener(OnDied);
            }
            mind.TakeMind(this);
            Mark();
            Bark.Say(gameObject, TakenLines[Random.Range(0, TakenLines.Length)], 2f);
        }

        /// <summary>A small violet light over the head: her enemies wear no mark, and she must tell this one from them. PLACEHOLDER.</summary>
        private void Mark()
        {
            mark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mark.name = "DominatedMark";
            Object.Destroy(mark.GetComponent<Collider>());
            mark.transform.SetParent(transform, false);
            mark.transform.localPosition = Vector3.up * (Body.ChestHeight + 1f);
            mark.transform.localScale = Vector3.one * 0.14f;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader != null) mark.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.72f, 0.42f, 1f) };
        }

        private void OnDied()
        {
            Domination.Forget(this);
            if (mark != null) Destroy(mark);
        }

        /// <summary>She takes another one: this one is an enemy again.</summary>
        public void LetGo()
        {
            Domination.Forget(this);
            if (lockable != null) lockable.SetSelectable(true);
            if (health != null) health.OnDeath?.RemoveListener(OnDied);
            if (mark != null) Destroy(mark);
            mind.ReleaseMind();
            RunTelemetry.Event("dom_let_go", name);
            Destroy(this);
        }

        /// <summary>The defend press: the nearest switch within her reach. False when there is none.</summary>
        public bool SendToSwitch(Vector3 from, float reach)
        {
            SentSwitch best = null;
            float bestDistance = reach * SwitchSearchShare;
            foreach (SentSwitch candidate in SentSwitch.All)
            {
                if (candidate == null || candidate.Used) continue;
                float distance = Vector3.Distance(from, candidate.transform.position);
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            if (best == null) return false;
            sentTo = best;
            body.MoveTo(best.Point, MoveSpeed.Run);
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
