using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using GlorifiedBody.Automation;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Level.Build;

namespace GlorifiedBody.Gore.ThatIndividual
{
    /// <summary>
    /// Dev-scene helper for GoreTest.unity. When a bot run is active (BotArgs.RunDir is set), it renders the
    /// kill-zone ShotPoint a few times after the first fatal gore burst, so critics can see the gore in Play mode.
    /// Output: runs/&lt;id&gt;/shots/gore_killzone_&lt;ms&gt;.png. Does nothing outside bot runs.
    ///
    /// After the shots it runs a driver check: every hit weight under every tier, a player-victim hit, a burst
    /// of kills in one frame, and each backend. It sends gore_matrix (pass/fail) and gore_stats (hit cost).
    /// A failed check logs an error, so triage marks the run DIRTY.
    /// Dev only: it lives in the runtime gore folder because the scene needs it, and it reads BotArgs.
    /// </summary>
    public sealed class GoreTestShotRig : MonoBehaviour
    {
        [SerializeField] private ShotPoint killZoneShot;
        [Tooltip("Real seconds after the first fatal burst.")]
        [SerializeField] private float[] delays = { 0.1f, 0.35f, 0.8f, 2f, 5f };

        private bool triggered;

        private void OnEnable() => RunTelemetry.OnEvent += OnTelemetry;
        private void OnDisable() => RunTelemetry.OnEvent -= OnTelemetry;

        private void OnTelemetry(string name, string detail)
        {
            if (triggered || name != "gore_fatal" || string.IsNullOrEmpty(BotArgs.RunDir) || killZoneShot == null)
            {
                return;
            }

            triggered = true;
            StartCoroutine(Capture());
        }

        private IEnumerator Capture()
        {
            var camGo = new GameObject("[GoreTestShotCam]");
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            cam.fieldOfView = killZoneShot.fieldOfView;
            camGo.transform.SetPositionAndRotation(killZoneShot.transform.position, killZoneShot.transform.rotation);
            if (Camera.main != null)
            {
                cam.clearFlags = Camera.main.clearFlags;
                cam.backgroundColor = Camera.main.backgroundColor;
            }

            float start = Time.unscaledTime;
            foreach (float delay in delays)
            {
                while (Time.unscaledTime - start < delay)
                {
                    yield return null;
                }

                // No WaitForEndOfFrame: it never resumes in -batchmode. ShotRenderer renders the camera itself.
                yield return null;
                string file = Path.Combine(BotArgs.RunDir, "shots", $"gore_killzone_{Mathf.RoundToInt(delay * 1000f)}ms.png");
                bool ok = ShotRenderer.Render(cam, file);
                ThatIndividualGoreDriver live = ThatIndividualGoreDriver.Instance;
                int effects = live != null ? live.LiveEffects : -1;
                int stains = live != null ? live.LiveStains : -1;
                Debug.Log($"[TIGore] kill-zone shot {(ok ? "wrote" : "failed")} {file} (live effects {effects}, stains {stains}).");
            }

            Destroy(camGo);

            ThatIndividualGoreDriver driver = ThatIndividualGoreDriver.Instance;
            if (driver == null)
            {
                yield break;
            }

            yield return CheckDriver(driver);

            string stats = string.Format(CultureInfo.InvariantCulture,
                "hits={0};max_hit_ms={1:0.00};warm_hits={2};max_warm_hit_ms={3:0.00};avg_warm_hit_ms={4:0.000};max_hit_alloc_bytes={5};optional_skipped={6}",
                driver.HitsPlayed, driver.MaxHitMs, driver.WarmHits, driver.MaxWarmHitMs, driver.AvgWarmHitMs,
                driver.MaxHitAllocBytes, driver.OptionalSkipped);
            Debug.Log($"[TIGore] stats {stats}");
            RunTelemetry.Event("gore_stats", stats);
        }

        // ── Driver check ─────────────────────────────────────────────

        private struct HitCase
        {
            public DamageType Type;
            public float Damage;
            public bool Fatal;
            public GoreHitWeight FullWeight;
        }

        private static readonly HitCase[] Cases =
        {
            new HitCase { Type = DamageType.PlayerBullet, Damage = 10f, Fatal = false, FullWeight = GoreHitWeight.Light },
            new HitCase { Type = DamageType.Throw, Damage = 10f, Fatal = false, FullWeight = GoreHitWeight.Heavy },
            new HitCase { Type = DamageType.PlayerBullet, Damage = 10f, Fatal = true, FullWeight = GoreHitWeight.Fatal },
            new HitCase { Type = DamageType.Debug, Damage = 100f, Fatal = true, FullWeight = GoreHitWeight.FatalHeavy },
        };

        private static readonly GoreTier[] Tiers = { GoreTier.Full, GoreTier.Low, GoreTier.Off };

        private int checks;
        private int failures;
        private readonly StringBuilder failLog = new();

        // Calls the driver directly (PlayForCheck), not HitEventBus, so score, hit text and legacy gore
        // do not see these test hits.
        private IEnumerator CheckDriver(ThatIndividualGoreDriver driver)
        {
            GoreManager manager = GoreManager.Instance;
            GoreTier startTier = manager != null ? manager.CurrentTier : GoreTier.Full;
            GoreBackend startBackend = driver.Backend;
            var victim = new GameObject("[GoreCheckVictim]") { tag = "Enemy" };
            Vector3 at = killZoneShot != null ? killZoneShot.transform.position + killZoneShot.transform.forward * 6f : Vector3.zero;
            if (Physics.Raycast(at + Vector3.up * 5f, Vector3.down, out RaycastHit floor, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                at = floor.point;
            }

            victim.transform.position = at;
            var hit = new HitInfo { Victim = victim, HitPoint = at + Vector3.up, HitNormal = Vector3.forward };

            // 1. Every weight under every tier, four hits each (one per frame).
            if (manager != null)
            {
                foreach (GoreTier tier in Tiers)
                {
                    manager.SetTier(tier);
                    yield return null;
                    int expectedCap = ThatIndividualGoreRules.CapFor(driver.Profile.maxLiveEffects, tier, driver.Profile.lowTierBudget);
                    Expect(driver.EffectCap == expectedCap, $"tier {tier}: effect cap {driver.EffectCap}, want {expectedCap}");
                    foreach (HitCase c in Cases)
                    {
                        GoreHitWeight want = ThatIndividualGoreRules.ApplyTier(c.FullWeight, tier);
                        for (int i = 0; i < 4; i++)
                        {
                            SetHit(hit, victim, c.Type, c.Damage, c.Fatal);
                            GoreHitWeight got = driver.PlayForCheck(hit);
                            Expect(got == want, $"tier {tier} {c.Type} fatal={c.Fatal}: played {got}, want {want}");
                            ExpectCaps(driver, $"tier {tier} {c.Type}");
                            yield return null;
                        }
                    }
                }

                manager.SetTier(GoreTier.Full);
                yield return null;
            }

            // 2. Twelve fatal heavy kills in one frame: the rings never grow past their caps.
            for (int i = 0; i < 12; i++)
            {
                SetHit(hit, victim, DamageType.Debug, 100f, true);
                driver.PlayForCheck(hit);
            }

            ExpectCaps(driver, "12 kills in one frame");
            yield return null;

            // 3. Player victim: one small spray, no stains.
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                int stainsBefore = driver.LiveStains;
                SetHit(hit, player, DamageType.EnemyBullet, 10f, false);
                GoreHitWeight got = driver.PlayForCheck(hit);
                Expect(got == GoreHitWeight.Light, $"player victim: played {got}, want Light");
                Expect(driver.LiveStains <= stainsBefore, "player victim: made a stain");
            }

            // 4. Backends.
            SkinGoreDriver[] skins = FindObjectsByType<SkinGoreDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            driver.SetBackend(GoreBackend.ThatIndividual);
            yield return null;
            Expect(!AnySkinEnabled(skins), "backend ThatIndividual: SkinGoreDriver still on");
            SetHit(hit, victim, DamageType.Debug, 100f, true);
            Expect(driver.PlayForCheck(hit) == GoreHitWeight.FatalHeavy, "backend ThatIndividual: no gore");

            driver.SetBackend(GoreBackend.SkinGore);
            yield return null;
            Expect(skins.Length == 0 || AnySkinEnabled(skins), "backend SkinGore: SkinGoreDriver not restored");
            SetHit(hit, victim, DamageType.Debug, 100f, true);
            Expect(driver.PlayForCheck(hit) == GoreHitWeight.None, "backend SkinGore: ThatIndividual still plays");

            driver.SetBackend(startBackend);
            yield return null;
            if (manager != null)
            {
                manager.SetTier(startTier);
            }

            driver.ClearAll();
            Destroy(victim);

            string detail = $"checks={checks};fail={failures};skin_drivers={skins.Length}";
            RunTelemetry.Event("gore_matrix", detail);
            if (failures > 0)
            {
                Debug.LogError($"[TIGore] driver check failed ({detail}):{failLog}");
            }
            else
            {
                Debug.Log($"[TIGore] driver check passed ({detail}).");
            }
        }

        private static void SetHit(HitInfo hit, GameObject victim, DamageType type, float damage, bool fatal)
        {
            hit.Victim = victim;
            hit.HitPoint = victim.transform.position + Vector3.up;
            hit.DamageType = type;
            hit.DamageAmount = damage;
            hit.WasFatal = fatal;
        }

        private void ExpectCaps(ThatIndividualGoreDriver driver, string label)
        {
            Expect(driver.LiveEffects <= driver.EffectCap, $"{label}: {driver.LiveEffects} live effects, cap {driver.EffectCap}");
            Expect(driver.LiveStains <= driver.StainCap, $"{label}: {driver.LiveStains} live stains, cap {driver.StainCap}");
        }

        private void Expect(bool ok, string message)
        {
            checks++;
            if (!ok)
            {
                failures++;
                failLog.Append("\n  ").Append(message);
            }
        }

        private static bool AnySkinEnabled(SkinGoreDriver[] skins)
        {
            foreach (SkinGoreDriver skin in skins)
            {
                if (skin != null && skin.enabled)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
