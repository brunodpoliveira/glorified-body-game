using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Pooling;

namespace GlorifiedBody.Gore.ThatIndividual
{
    /// <summary>
    /// Drives the "Gore Systems by ThatIndividual" effects from HitEventBus, like SkinGoreDriver does for SkinGore.
    /// Hit sprays at the hit point, flat stains on the floor and the wall behind the victim, and bigger layered
    /// bursts on fatal and high-tier hits (L3 punch, finisher, divekick, throw, slam, blasts, bot Debug kills).
    /// Every effect comes from PoolService and lives in a fixed ring, so live counts are capped and hits do not allocate.
    ///
    /// Respects GoreManager's tier (Off / Low / Full) and the profile backend (SkinGore / ThatIndividual / Both).
    ///
    /// Setup: none in a scene that has PF_GoreManager. A bootstrap loads
    /// Resources/ThatIndividualGore/PF_ThatIndividualGore when GoreManager exists and no driver does.
    /// Mesh slicing (SkeletonMeshSlicer) is not used. See ThatIndividual/README.md for why.
    /// </summary>
    public sealed class ThatIndividualGoreDriver : MonoBehaviour
    {
        private const string ResourcePath = "ThatIndividualGore/PF_ThatIndividualGore";
        private static readonly string[] WeightNames = { "none", "light", "heavy", "fatal", "fatal_heavy" };

        [SerializeField] private ThatIndividualGoreProfile profile;
        [SerializeField] private PoolService poolService;
        [SerializeField] private bool logHits;

        private struct LiveSlot
        {
            public PooledObject Obj;
            public int Version;
            public float ExpireAt;
            public ThatIndividualGoreEffect Effect;
        }

        private readonly RaycastHit[] rayBuffer = new RaycastHit[8];
        private LiveSlot[] effectSlots;
        private LiveSlot[] stainSlots;
        private int effectHead;
        private int stainHead;
        private int effectCap;
        private int stainCap;
        private bool decalsSupported;
        private GoreTier appliedTier = (GoreTier)(-1);
        private GoreBackend appliedBackend = (GoreBackend)(-1);
        private bool skinGoreDisabledByUs;

        // Hit cost stats. The first hit of each weight counts as cold (first Play of its particle systems).
        private readonly bool[] weightSeen = new bool[5];
        private double warmHitMsTotal;

        public static ThatIndividualGoreDriver Instance { get; private set; }
        public ThatIndividualGoreProfile Profile => profile;
        /// <summary>
        /// The driver's own effect pool (on PF_ThatIndividualGore). It is not the game's main PoolService.
        /// Code that wants the main pool must not use FindFirstObjectByType&lt;PoolService&gt;, because it can return this one.
        /// </summary>
        public PoolService GorePool => poolService;
        public bool DecalsSupported => decalsSupported;
        public int LiveEffects => CountLive(effectSlots);
        public int LiveStains => CountLive(stainSlots);
        public int EffectCap => effectCap;
        public int StainCap => stainCap;
        public GoreBackend Backend => profile != null ? profile.backend : GoreBackend.Both;
        /// <summary>Weight the last hit played (None when the hit was skipped).</summary>
        public GoreHitWeight LastWeight { get; private set; }
        /// <summary>Hits that played effects since start, and the worst cost of one hit. Read by GoreTestShotRig.</summary>
        public int HitsPlayed { get; private set; }
        public float MaxHitMs { get; private set; }
        public long MaxHitAllocBytes { get; private set; }
        /// <summary>Cost of hits after the first hit of each weight: steady-state cost.</summary>
        public int WarmHits { get; private set; }
        public float MaxWarmHitMs { get; private set; }
        public float AvgWarmHitMs => WarmHits > 0 ? (float)(warmHitMsTotal / WarmHits) : 0f;
        /// <summary>Optional effects (fatal-heavy extras) skipped because the ring was full of live effects.</summary>
        public int OptionalSkipped { get; private set; }

        // ── Bootstrap ────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureDriver();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureDriver();

        private static void EnsureDriver()
        {
            if (Instance != null || GoreManager.Instance == null)
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[TIGore] Resources/{ResourcePath} missing. Run GlorifiedBody.Editor.Gore.ThatIndividualGoreBuilder.Build.");
                return;
            }

            GameObject driver = Instantiate(prefab);
            driver.name = prefab.name;
            Debug.Log($"[TIGore] Bootstrap added {prefab.name} to scene {SceneManager.GetActiveScene().name}.");
        }

        // ── Lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[TIGore] Duplicate driver on {name}; destroying it.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (poolService == null)
            {
                poolService = GetComponent<PoolService>();
            }

            if (profile == null || poolService == null)
            {
                Debug.LogWarning("[TIGore] Profile or PoolService missing; driver disabled.", this);
                enabled = false;
                return;
            }

            // ScriptableObjects with runtime state are copied before any write (SetBackend writes).
            profile = Instantiate(profile);
            effectSlots = new LiveSlot[Mathf.Max(1, profile.maxLiveEffects)];
            stainSlots = new LiveSlot[Mathf.Max(1, profile.maxLiveStains)];
            effectCap = effectSlots.Length;
            stainCap = stainSlots.Length;
            decalsSupported = DetectDecalFeature();
            Prewarm();
        }

        private void OnEnable()
        {
            HitEventBus.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            HitEventBus.OnHit -= HandleHit;
            SetSkinGoreEnabled(true);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            ApplyState(true);
            WarmUp();
            Debug.Log($"[TIGore] Ready. Backend={profile.backend}, Tier={CurrentTier}, URP decals={(decalsSupported ? "on" : "off (flat stains only)")}, caps={effectCap}/{stainCap}.");
        }

        private void Update()
        {
            ApplyState(false);
            float now = Time.time;
            ExpireSlots(effectSlots, now);
            ExpireSlots(stainSlots, now);
        }

        // ── Public controls ──────────────────────────────────────────

        public void SetBackend(GoreBackend backend)
        {
            profile.backend = backend;
            ApplyState(true);
        }

        /// <summary>
        /// Plays one hit without HitEventBus, so a check can run every weight, tier and backend without side
        /// effects in other subscribers (score, hit text, legacy gore). Returns the weight it played.
        /// </summary>
        public GoreHitWeight PlayForCheck(HitInfo hit)
        {
            HandleHit(hit);
            return LastWeight;
        }

        public void ClearAll()
        {
            ReleaseAll(effectSlots);
            ReleaseAll(stainSlots);
        }

        private static GoreTier CurrentTier => GoreManager.Instance != null ? GoreManager.Instance.CurrentTier : GoreTier.Full;

        // ── State: tier, backend, legacy mute ────────────────────────

        private void ApplyState(bool force)
        {
            GoreTier tier = CurrentTier;
            if (!force && tier == appliedTier && profile.backend == appliedBackend)
            {
                return;
            }

            bool tierChanged = tier != appliedTier;
            appliedTier = tier;
            appliedBackend = profile.backend;

            effectCap = ThatIndividualGoreRules.CapFor(effectSlots.Length, tier, profile.lowTierBudget);
            stainCap = ThatIndividualGoreRules.CapFor(stainSlots.Length, tier, profile.lowTierBudget);
            if (tierChanged || tier == GoreTier.Off)
            {
                ClearAll();
                effectHead = 0;
                stainHead = 0;
            }

            bool runsTI = ThatIndividualGoreRules.RunsThatIndividual(profile.backend);
            SetSkinGoreEnabled(ThatIndividualGoreRules.RunsSkinGore(profile.backend));
            MuteLegacyBlood(runsTI && profile.muteLegacyBlood);
        }

        // SkinGoreDriver is one scene manager on PF_GoreManager (SetupGoreIntegration), not a per-enemy part,
        // so a lookup when the state changes finds it.
        private void SetSkinGoreEnabled(bool on)
        {
            if (!on)
            {
                foreach (SkinGoreDriver skin in FindObjectsByType<SkinGoreDriver>(FindObjectsSortMode.None))
                {
                    if (skin.enabled)
                    {
                        skin.enabled = false;
                        skinGoreDisabledByUs = true;
                    }
                }
                return;
            }

            if (!skinGoreDisabledByUs)
            {
                return;
            }

            skinGoreDisabledByUs = false;
            foreach (SkinGoreDriver skin in FindObjectsByType<SkinGoreDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                skin.enabled = true;
            }
        }

        // GoreManager re-applies the legacy caps on every tier change, so this runs again after each one.
        private static void MuteLegacyBlood(bool mute)
        {
            GoreManager manager = GoreManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (!mute)
            {
                manager.GetComponentInChildren<BloodFeedbackController>(true)?.Configure(manager.Settings);
                return;
            }

            manager.GetComponentInChildren<BloodParticleSpawner>(true)?.Configure(0);
            manager.GetComponentInChildren<BloodDecalSpawner>(true)?.Configure(0);
            manager.GetComponentInChildren<WoundMarkerSpawner>(true)?.Configure(0);
        }

        // ── Hit handling ─────────────────────────────────────────────

        private void HandleHit(HitInfo hit)
        {
            // Cost and allocation of the gore work itself, for the budget check (no allocation to measure).
            long allocBefore = System.GC.GetAllocatedBytesForCurrentThread();
            long ticksBefore = System.Diagnostics.Stopwatch.GetTimestamp();
            GoreHitWeight weight = PlayHit(hit);
            LastWeight = weight;
            if (weight == GoreHitWeight.None)
            {
                return;
            }

            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - ticksBefore) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            long alloc = System.GC.GetAllocatedBytesForCurrentThread() - allocBefore;
            HitsPlayed++;
            MaxHitMs = Mathf.Max(MaxHitMs, (float)ms);
            MaxHitAllocBytes = System.Math.Max(MaxHitAllocBytes, alloc);
            if (weightSeen[(int)weight])
            {
                WarmHits++;
                warmHitMsTotal += ms;
                MaxWarmHitMs = Mathf.Max(MaxWarmHitMs, (float)ms);
            }

            weightSeen[(int)weight] = true;

            if (weight == GoreHitWeight.Fatal || weight == GoreHitWeight.FatalHeavy)
            {
                RunTelemetry.Event("gore_fatal", WeightNames[(int)weight]);
            }
        }

        // Plays the effects for one hit. Returns the weight it played, or None.
        private GoreHitWeight PlayHit(HitInfo hit)
        {
            if (hit == null || hit.Victim == null || !ThatIndividualGoreRules.RunsThatIndividual(profile.backend))
            {
                return GoreHitWeight.None;
            }

            GoreTier tier = CurrentTier;
            if (tier == GoreTier.Off || !HitClassifier.IsActorHit(hit))
            {
                return GoreHitWeight.None;
            }

            GoreHitWeight weight = ThatIndividualGoreRules.ApplyTier(
                ThatIndividualGoreRules.Classify(hit.DamageType, hit.DamageAmount, hit.WasFatal, profile.heavyDamageThreshold), tier);
            if (weight == GoreHitWeight.None)
            {
                return GoreHitWeight.None;
            }

            bool playerVictim = hit.Victim.CompareTag("Player");
            Vector3 point = ResolveHitPoint(hit);
            Vector3 sprayDir = ResolveSprayDirection(hit, point);
            Quaternion sprayRot = Quaternion.LookRotation(sprayDir, Vector3.up);
            float scale = profile.sprayScale * (playerVictim ? profile.playerVictimScale : 1f);

            if (logHits)
            {
                Debug.Log($"[TIGore] {WeightNames[(int)weight]} {hit.DamageType} on {hit.Victim.name} at {point}.");
            }

            // The player's own blood stays small: one spray, no stains.
            if (playerVictim)
            {
                SpawnEffect(profile.lightSprays, point, sprayRot, scale, profile.lightLifetime);
                return GoreHitWeight.Light;
            }

            Transform victimRoot = hit.Victim.transform;
            switch (weight)
            {
                case GoreHitWeight.Light:
                    SpawnEffect(profile.lightSprays, point, sprayRot, scale, profile.lightLifetime);
                    TryWallStain(point, sprayDir, victimRoot, profile.lightWallStainChance);
                    break;

                case GoreHitWeight.Heavy:
                    SpawnEffect(profile.heavySprays, point, sprayRot, scale, profile.heavyLifetime);
                    TryFloorStain(point, victimRoot, 1f);
                    TryWallStain(point, sprayDir, victimRoot, profile.heavyWallStainChance);
                    break;

                case GoreHitWeight.Fatal:
                    SpawnEffect(profile.fatalBursts, point, sprayRot, scale, profile.fatalLifetime);
                    TryFloorStain(point, victimRoot, 1.25f);
                    TryWallStain(point, sprayDir, victimRoot, profile.heavyWallStainChance);
                    break;

                case GoreHitWeight.FatalHeavy:
                    SpawnEffect(profile.fatalBursts, point, sprayRot, scale * 1.15f, profile.fatalLifetime);
                    SpawnEffect(profile.heavySprays, point, sprayRot, scale, profile.heavyLifetime);
                    // Extras are optional: when the ring is full they do not cut off another kill's live burst.
                    SpawnEffect(profile.fatalHeavyExtras, point, Quaternion.identity, scale, profile.fatalLifetime, true);
                    TryFloorStain(point, victimRoot, 1.5f);
                    TryWallStain(point, sprayDir, victimRoot, 1f);
                    break;
            }

            return weight;
        }

        private static Vector3 ResolveHitPoint(HitInfo hit)
        {
            if (hit.HitPoint.sqrMagnitude > 0.0001f)
            {
                return hit.HitPoint;
            }

            return hit.Victim.transform.position + Vector3.up;
        }

        // Blood flies away from the attacker, tilted up a little so it arcs.
        private Vector3 ResolveSprayDirection(HitInfo hit, Vector3 point)
        {
            Vector3 dir = Vector3.zero;
            if (hit.Attacker != null)
            {
                dir = point - hit.Attacker.transform.position;
            }

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = -hit.HitNormal;
                dir.y = 0f;
            }

            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = -hit.Victim.transform.forward;
            }

            dir.Normalize();
            dir.y = profile.sprayLift;
            return dir.normalized;
        }

        // ── Surfaces ─────────────────────────────────────────────────

        private void TryFloorStain(Vector3 point, Transform victimRoot, float sizeBoost)
        {
            if (profile.floorStains == null || profile.floorStains.Length == 0)
            {
                return;
            }

            Vector3 origin = point + Vector3.up * profile.floorRayHeight;
            if (!RaySurface(origin, Vector3.down, profile.floorRayHeight + profile.floorRayDistance, victimRoot, true, out RaycastHit surface))
            {
                return;
            }

            float scale = Random.Range(profile.floorStainScale.x, profile.floorStainScale.y) * sizeBoost;
            Quaternion rot = ThatIndividualGoreRules.StainRotation(surface.normal, Random.Range(0f, 360f));
            SpawnStain(profile.floorStains, surface.point + surface.normal * profile.surfaceOffset, rot, scale);
        }

        private void TryWallStain(Vector3 point, Vector3 sprayDir, Transform victimRoot, float chance)
        {
            if (chance <= 0f || Random.value > chance)
            {
                return;
            }

            Vector3 flat = new Vector3(sprayDir.x, 0f, sprayDir.z);
            if (flat.sqrMagnitude < 0.0001f || !RaySurface(point, flat.normalized, profile.wallRayDistance, victimRoot, false, out RaycastHit surface))
            {
                return;
            }

            Vector3 at = surface.point + surface.normal * profile.surfaceOffset;
            float scale = Random.Range(profile.wallStainScale.x, profile.wallStainScale.y);

            if (decalsSupported && profile.wallDecals != null && profile.wallDecals.Length > 0)
            {
                // A projector looks along its +Z, into the wall. Up stays up, so the bleed runs down.
                Quaternion decalRot = Quaternion.LookRotation(-surface.normal, Vector3.up);
                SpawnDecal(profile.wallDecals, at, decalRot, scale * 1.4f);
                return;
            }

            if (profile.wallStains == null || profile.wallStains.Length == 0)
            {
                return;
            }

            Quaternion rot = ThatIndividualGoreRules.StainRotation(surface.normal, Random.Range(0f, 360f));
            SpawnStain(profile.wallStains, at, rot, scale);
        }

        // Nearest static surface along the ray. Skips the victim, triggers and moving bodies (ragdolls, props).
        private bool RaySurface(Vector3 origin, Vector3 dir, float distance, Transform victimRoot, bool wantFloor, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(origin, dir, rayBuffer, distance, profile.surfaceMask, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = rayBuffer[i];
                Collider col = candidate.collider;
                if (col == null || candidate.distance >= bestDistance)
                {
                    continue;
                }

                if (victimRoot != null && col.transform.IsChildOf(victimRoot))
                {
                    continue;
                }

                Rigidbody body = col.attachedRigidbody;
                if (body != null && !body.isKinematic)
                {
                    continue;
                }

                bool floorLike = candidate.normal.y >= 0.45f;
                bool wallLike = Mathf.Abs(candidate.normal.y) < 0.5f;
                if (wantFloor ? !floorLike : !wallLike)
                {
                    continue;
                }

                best = candidate;
                bestDistance = candidate.distance;
            }

            return bestDistance < float.MaxValue;
        }

        // ── Spawning ─────────────────────────────────────────────────

        private void SpawnEffect(GameObject[] set, Vector3 position, Quaternion rotation, float scale, float lifetime, bool optional = false)
        {
            GameObject prefab = Pick(set);
            if (prefab == null)
            {
                return;
            }

            if (optional && IsLive(effectSlots[ThatIndividualGoreRules.PeekSlot(effectHead, effectCap)]))
            {
                OptionalSkipped++;
                return;
            }

            PooledObject pooled = Acquire(effectSlots, ref effectHead, effectCap, prefab, lifetime, out ThatIndividualGoreEffect effect);
            if (pooled == null)
            {
                return;
            }

            pooled.transform.SetPositionAndRotation(position, rotation);
            if (effect != null)
            {
                effect.Play(scale);
            }
        }

        private void SpawnStain(GameObject[] set, Vector3 position, Quaternion rotation, float scale)
        {
            GameObject prefab = Pick(set);
            if (prefab == null)
            {
                return;
            }

            PooledObject pooled = Acquire(stainSlots, ref stainHead, stainCap, prefab, profile.stainLifetime, out ThatIndividualGoreEffect effect);
            if (pooled == null)
            {
                return;
            }

            pooled.transform.SetPositionAndRotation(position, rotation);
            if (effect != null)
            {
                effect.Play(scale);
            }
        }

        private void SpawnDecal(GameObject[] set, Vector3 position, Quaternion rotation, float size)
        {
            GameObject prefab = Pick(set);
            if (prefab == null)
            {
                return;
            }

            PooledObject pooled = Acquire(stainSlots, ref stainHead, stainCap, prefab, profile.stainLifetime, out _);
            if (pooled == null)
            {
                return;
            }

            pooled.transform.SetPositionAndRotation(position, rotation);
            if (pooled.TryGetComponent(out ThatIndividualGoreDecal decal))
            {
                decal.Play(size, profile.stainLifetime);
            }
        }

        // Takes the next ring slot. A full ring returns its oldest effect first, so the live count never grows.
        private PooledObject Acquire(LiveSlot[] slots, ref int head, int cap, GameObject prefab, float lifetime, out ThatIndividualGoreEffect effect)
        {
            effect = null;
            int index = ThatIndividualGoreRules.TakeSlot(ref head, cap);
            Release(ref slots[index]);

            PooledObject pooled = poolService.Get(prefab, slots.Length, transform);
            if (pooled == null)
            {
                return null;
            }

            pooled.TryGetComponent(out effect);
            slots[index] = new LiveSlot
            {
                Obj = pooled,
                Version = pooled.SpawnVersion,
                ExpireAt = Time.time + Mathf.Max(0.1f, lifetime),
                Effect = effect
            };
            return pooled;
        }

        private void Release(ref LiveSlot slot)
        {
            if (slot.Obj != null && slot.Obj.SpawnVersion == slot.Version)
            {
                if (slot.Effect != null)
                {
                    slot.Effect.StopAndClear();
                }

                poolService.ReturnIfVersion(slot.Obj, slot.Version);
            }

            slot = default;
        }

        private void ExpireSlots(LiveSlot[] slots, float now)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Obj != null && now >= slots[i].ExpireAt)
                {
                    Release(ref slots[i]);
                }
            }
        }

        private void ReleaseAll(LiveSlot[] slots)
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                Release(ref slots[i]);
            }
        }

        private static int CountLive(LiveSlot[] slots)
        {
            if (slots == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (IsLive(slots[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsLive(LiveSlot slot) => slot.Obj != null && slot.Obj.SpawnVersion == slot.Version;

        private static GameObject Pick(GameObject[] set)
        {
            if (set == null || set.Length == 0)
            {
                return null;
            }

            return set[Random.Range(0, set.Length)];
        }

        private void Prewarm()
        {
            int n = profile.prewarmPerPrefab;
            PrewarmSet(profile.lightSprays, n);
            PrewarmSet(profile.heavySprays, n);
            PrewarmSet(profile.fatalBursts, n);
            PrewarmSet(profile.fatalHeavyExtras, n);
            PrewarmSet(profile.floorStains, n);
            PrewarmSet(profile.wallStains, n);
            if (decalsSupported)
            {
                PrewarmSet(profile.wallDecals, n);
            }
        }

        private void PrewarmSet(GameObject[] set, int count)
        {
            if (set == null)
            {
                return;
            }

            foreach (GameObject prefab in set)
            {
                poolService.Prewarm(prefab, count, transform);
            }
        }

        // Plays and stops every effect prefab once, far below the arena. The first Play of a particle system
        // costs more than later ones, so this moves that cost from the first real hit to startup.
        private void WarmUp()
        {
            int warmed = WarmSet(profile.lightSprays) + WarmSet(profile.heavySprays) + WarmSet(profile.fatalBursts) +
                         WarmSet(profile.fatalHeavyExtras) + WarmSet(profile.floorStains) + WarmSet(profile.wallStains);
            Debug.Log($"[TIGore] Warmed {warmed} effect prefabs.");
        }

        // Every prewarmed instance plays once: each particle system pays its own first-Play setup.
        private int WarmSet(GameObject[] set)
        {
            if (set == null)
            {
                return 0;
            }

            int count = 0;
            int perPrefab = Mathf.Max(1, profile.prewarmPerPrefab);
            var taken = new System.Collections.Generic.List<PooledObject>(perPrefab);
            foreach (GameObject prefab in set)
            {
                if (prefab == null)
                {
                    continue;
                }

                taken.Clear();
                for (int i = 0; i < perPrefab; i++)
                {
                    PooledObject pooled = poolService.Get(prefab, perPrefab, transform);
                    if (pooled == null)
                    {
                        break;
                    }

                    pooled.transform.SetPositionAndRotation(new Vector3(0f, -500f, 0f), Quaternion.identity);
                    if (pooled.TryGetComponent(out ThatIndividualGoreEffect effect))
                    {
                        effect.Play(1f);
                        effect.StopAndClear();
                    }

                    taken.Add(pooled);
                }

                for (int i = 0; i < taken.Count; i++)
                {
                    poolService.Return(taken[i]);
                }

                count++;
            }

            return count;
        }

        // URP decals only draw when the active renderer has a DecalRendererFeature. Read once at startup.
        private static bool DetectDecalFeature()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp))
            {
                return false;
            }

            foreach (ScriptableRendererData data in urp.rendererDataList)
            {
                if (data == null)
                {
                    continue;
                }

                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature is DecalRendererFeature && feature.isActive)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
