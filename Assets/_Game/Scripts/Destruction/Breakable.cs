using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Destruction
{
    /// <summary>
    /// A piece of the room that breaks (Bruno, 2026-10-24: "breakable cover, for more kinetic scenarios"): a table,
    /// a barricade, a wall's plaster, a lamp. All of it is collateral damage ("all collateral damage"): the auto-lock
    /// never picks it, the powers, the throws, the explosions and the enemy bullets wear it down
    /// (Breakables.Splash, Breakables.HitCollider). A chest-high cover piece stops her powers on their way to an
    /// enemy behind it ("cover only enemies, on purpose"); the enemies shoot over it.
    ///
    /// The pieces are cut in the editor (BoxFracture, BreakableAssets), so a break costs no CPU: the intact root
    /// turns off, the debris root turns on and its pieces fly away from the hit, then settle and freeze. The items
    /// that rest on it (Greeble) fall. What breaks stays broken for the visit and when she comes back
    /// ("broken room stays broken", RoomDamageLedger). Numbers PROPOSED.
    /// </summary>
    public class Breakable : MonoBehaviour
    {
        public enum Kind { Wood, Metal, Glass, Plaster, Plastic }

        [SerializeField] private string id;
        [SerializeField] private Kind kind = Kind.Wood;
        [SerializeField, Min(1f)] private float maxHealth = 60f;
        [Tooltip("Chest high: it stops her powers on their way to an enemy behind it.")]
        [SerializeField] private bool isCover;
        [SerializeField] private GameObject intactRoot;
        [SerializeField] private GameObject debrisRoot;
        [Tooltip("What stays after the break (a bare wall, the stumps). Optional.")]
        [SerializeField] private GameObject brokenRoot;
        [SerializeField] private Greeble[] resting = new Greeble[0];
        [SerializeField, Min(0f)] private float kick = 3f;

        private static readonly List<Breakable> all = new List<Breakable>();
        public static IReadOnlyList<Breakable> All => all;
        public static int TotalBroken { get; private set; }
        public static event Action<Breakable> OnBroken;

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public Kind Material => kind;
        public float MaxHealth => maxHealth;
        public float Health { get; private set; }
        public bool IsBroken { get; private set; }
        public bool IsCover => isCover && !IsBroken;
        public Bounds Bounds { get; private set; }
        public IReadOnlyList<DebrisPiece> Pieces => pieces;
        public IReadOnlyList<Greeble> Resting => resting;
        public GameObject IntactRoot => intactRoot;
        public GameObject BrokenRoot => brokenRoot;

        private DebrisPiece[] pieces = new DebrisPiece[0];
        private NavMeshObstacle obstacle;

        /// <summary>The builder sets the parts (editor code).</summary>
        public void Bind(string stableId, Kind material, float health, bool cover, GameObject intact, GameObject debris, GameObject broken, Greeble[] onTop, float kickSpeed)
        {
            id = stableId;
            kind = material;
            maxHealth = health;
            isCover = cover;
            intactRoot = intact;
            debrisRoot = debris;
            brokenRoot = broken;
            resting = onTop ?? new Greeble[0];
            kick = kickSpeed;
        }

        protected virtual void Awake()
        {
            Health = maxHealth;
            obstacle = GetComponent<NavMeshObstacle>();
            if (debrisRoot != null)
            {
                pieces = debrisRoot.GetComponentsInChildren<DebrisPiece>(true);
                for (int i = 0; i < pieces.Length; i++) pieces[i].Bind(this, i);
                debrisRoot.SetActive(false);
            }
            if (brokenRoot != null) brokenRoot.SetActive(false);
            Bounds = MeasureBounds();
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        protected virtual void Start()
        {
            RoomDamageLedger.Record record = RoomDamageLedger.Find(this);
            if (record == null) return;
            if (record.broken) RestoreBroken(record);
            else Health = Mathf.Clamp(record.health, 1f, maxHealth);
        }

        private Bounds MeasureBounds()
        {
            GameObject source = intactRoot != null ? intactRoot : gameObject;
            bool any = false;
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer r in source.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!any) bounds = new Bounds(transform.position, Vector3.one * 0.5f);
            return bounds;
        }

        /// <summary>Damage from a hit or a splash. True when this hit broke it.</summary>
        public bool Damage(float amount, DamageType type, Vector3 point, Vector3 direction)
        {
            if (IsBroken) return false;
            amount *= Scale(type);
            if (amount <= 0f) return false;
            Health = Mathf.Max(0f, Health - amount);
            RunTelemetry.Event("breakable_hit", $"{Id}:{type}:{amount:0}:{Health:0}/{maxHealth:0}");
            if (Health > 0f)
            {
                RoomDamageLedger.NoteHealth(this);
                DestructionFx.Chip(kind, point, direction);
                return false;
            }
            Break(point, direction);
            return true;
        }

        /// <summary>Explosions tear the room apart; the short does not break furniture (a lamp has its own rule).</summary>
        protected virtual float Scale(DamageType type)
        {
            switch (type)
            {
                case DamageType.Explosion:
                case DamageType.Grenade:
                case DamageType.EnemyRocket:
                    return 2f;
                case DamageType.PsychicTelekinesis:
                    return 1.5f;
                case DamageType.PsychicFire:
                    return kind == Kind.Wood || kind == Kind.Plastic ? 1f : 0.5f;
                case DamageType.ShortShock:
                case DamageType.EnemyGas:
                    return 0f;
                default:
                    return 1f;
            }
        }

        public void Break(Vector3 point, Vector3 direction)
        {
            if (IsBroken) return;
            IsBroken = true;
            Health = 0f;
            TotalBroken++;
            SetBroken();
            Vector3 centre = Bounds.center;
            foreach (DebrisPiece piece in pieces)
            {
                Vector3 away = piece.transform.position - point;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : UnityEngine.Random.onUnitSphere;
                Vector3 push = Vector3.Lerp(away, direction.sqrMagnitude > 0.0001f ? direction.normalized : away, 0.5f);
                piece.Launch(push * kick * UnityEngine.Random.Range(0.6f, 1.2f) + Vector3.up * kick * 0.4f,
                    UnityEngine.Random.insideUnitSphere * 8f);
            }
            foreach (Greeble item in resting)
                if (item != null) item.Disturb(point, kick * 0.5f);
            DestructionFx.Break(kind, centre, Bounds.size.magnitude);
            RoomDamageLedger.NoteBroken(this);
            RunTelemetry.Event("breakable_broken", $"{Id}:{kind}:{pieces.Length} pieces");
            OnBreak(point);
            OnBroken?.Invoke(this);
        }

        /// <summary>A subclass adds its own break (the lamp goes dark).</summary>
        protected virtual void OnBreak(Vector3 point) { }

        /// <summary>A subclass restores its own broken look without effects (the lamp is dark).</summary>
        protected virtual void OnRestoredBroken() { }

        private void SetBroken()
        {
            if (intactRoot != null) intactRoot.SetActive(false);
            if (debrisRoot != null) debrisRoot.SetActive(true);
            if (brokenRoot != null) brokenRoot.SetActive(true);
            if (obstacle != null) obstacle.enabled = false;
        }

        /// <summary>She came back: the room as she left it, with no effects. A piece that did not settle stays where it was cut.</summary>
        private void RestoreBroken(RoomDamageLedger.Record record)
        {
            IsBroken = true;
            Health = 0f;
            SetBroken();
            for (int i = 0; i < pieces.Length; i++)
            {
                if (record.pieces != null && i < record.pieces.Count && record.pieces[i].HasValue) pieces[i].Rest(record.pieces[i].Value);
                else pieces[i].Rest(new Pose(pieces[i].transform.position, pieces[i].transform.rotation));
            }
            OnRestoredBroken();
            RunTelemetry.Event("breakable_restored", Id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            all.Clear();
            TotalBroken = 0;
            OnBroken = null;
        }
    }
}
