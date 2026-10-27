using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The AI bot: loads the AI test room from the menu, lets the FSM soldiers fight a Lucy who takes no damage,
    /// and shoots the moments: the soldiers walking, a telegraph (the laser and the HUD circle), a soldier in the
    /// lift, "TOO HEAVY" on the power armour, a corpse. It checks the rule "move or shoot, never both".
    /// The weapons (Bruno, 2026-10-24): a grenade and its floor ring, the attack icons on the HUD (also on the edge
    /// arrows of attackers out of the frame), "BLOCKED" on the shield, a melee strike when she stands close.
    /// Active only when BotArgs.Bot == "ai". Writes playthrough.json and shots/ai_&lt;n&gt;_&lt;moment&gt;.png.
    /// Batch: tools/unity.sh play AiTest Main_Menu ai 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AiPlaythrough : MonoBehaviour
    {
        public const string SceneName = "AiTest";
        public const int ShotWidth = 1920, ShotHeight = 1080;

        [Serializable]
        private class Step
        {
            public string name;
            public bool ok;
            public string detail;
            public float t;
        }

        private readonly List<Step> steps = new List<Step>();
        private float startTime;
        private bool finished;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "ai", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<AiPlaythrough>() != null) return;
            var go = new GameObject("[AiPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<AiPlaythrough>();
        }

        private void Start()
        {
            startTime = Time.unscaledTime;
            StartCoroutine(Run());
            StartCoroutine(Watchdog());
        }

        private IEnumerator Watchdog()
        {
            float limit = BotArgs.TimeoutSeconds;
            while (!finished && Time.unscaledTime - startTime < limit) yield return null;
            if (!finished)
            {
                Record("timeout", false, $"bot did not finish in {limit:0}s");
                Finish();
            }
        }

        private void Record(string name, bool ok, string detail = "")
        {
            steps.Add(new Step { name = name, ok = ok, detail = detail, t = Time.unscaledTime - startTime });
            string line = $"[AiBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(SceneName); else SceneManager.LoadScene(SceneName);
            float t0 = BotClock.Now;
            while (SceneManager.GetActiveScene().name != SceneName && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);

            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            SoldierBrain[] soldiers = FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
            Record("ai_loaded", SceneManager.GetActiveScene().name == SceneName && lucy != null && soldiers.Length == 4,
                $"scene={SceneManager.GetActiveScene().name} soldiers={soldiers.Length}");
            if (lucy == null || soldiers.Length == 0) { Finish(); yield break; }
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            string shots = Path.Combine(BotArgs.RunDir, "shots");

            Clip("ai_engage");
            yield return BotClock.Wait(3f);
            Shot(Path.Combine(shots, "ai_01_soldiers_move.png"));
            Record("soldiers_engage", soldiers.All(s => s.Body.HasTarget) && soldiers.Any(s => s.Moves > 0),
                string.Join(" ", soldiers.Select(s => $"{s.name}:{s.Current}/target={s.Body.HasTarget}/moves={s.Moves}")));

            t0 = BotClock.Now;
            // 16 s: one run of 12 s saw the soldiers fire but missed the circle (2026-10-24).
            while (!soldiers.Any(s => s.Gun.Current == DummyShooter.State.Telegraph) && BotClock.Now - t0 < 16f) yield return null;
            yield return null;
            SoldierBrain aiming = soldiers.FirstOrDefault(s => s.Gun.Current == DummyShooter.State.Telegraph);
            Shot(Path.Combine(shots, "ai_02_telegraph.png"));
            Record("telegraph", aiming != null, aiming != null ? $"{aiming.name} counts down" : "no telegraph in 16 s");
            // The long guns in both hands (Bruno, 2026-10-24: "the animation for the shotgunner + assault rifle is
            // incorrect"): a close shot of each while it aims.
            foreach (string gunner in new[] { "Soldier_A", "Gasser_D" })
            {
                SoldierBrain g = soldiers.FirstOrDefault(s => s.name == gunner);
                if (g == null) continue;
                float w = BotClock.Now;
                while (g.Gun.Current != DummyShooter.State.Telegraph && g.Gun.Current != DummyShooter.State.Aiming && BotClock.Now - w < 10f) yield return null;
                Vector3 chest = g.transform.position + Vector3.up * 1.2f;
                CloseShot(chest, g.transform.right + g.transform.forward * 0.8f, Path.Combine(shots, $"ai_02b_pose_{gunner}.png"), 2.2f, 0.2f);
                CloseShot(chest, -g.transform.right + g.transform.forward * 0.8f, Path.Combine(shots, $"ai_02b_pose_{gunner}_left.png"), 2.2f, 0.2f);
            }
            // And while they run (Bruno, 2026-10-24: "incorrect when running"): the gun at a low ready.
            foreach (string gunner in new[] { "Soldier_A", "Gasser_D" })
            {
                SoldierBrain g = soldiers.FirstOrDefault(s => s.name == gunner);
                if (g == null) continue;
                float w = BotClock.Now;
                while (!(g.Body.IsMoving && !g.Gun.HoldsSlot) && BotClock.Now - w < 12f) yield return null;
                Vector3 chest = g.transform.position + Vector3.up * 1.2f;
                CloseShot(chest, g.transform.right + g.transform.forward * 0.8f, Path.Combine(shots, $"ai_02c_run_{gunner}.png"), 2.4f, 0.2f);
                RunTelemetry.Event("pose_run_shot", $"{gunner} moving={g.Body.IsMoving} aim={g.Gun.HoldsSlot}");
            }

            Record("alerted", soldiers.All(s => s.Alerted), string.Join(" ", soldiers.Select(s => $"{s.name}={s.AlertReason}")));
            EndClip();
            Clip("ai_gunfire");

            // The gun: a close shot on the next muzzle flash.
            EnemyGun[] guns = soldiers.Select(s => s.GetComponent<EnemyGun>()).Where(g => g != null).ToArray();
            int before = guns.Sum(g => g.Shots);
            t0 = BotClock.Now;
            while (guns.Sum(g => g.Shots) == before && BotClock.Now - t0 < 10f) yield return null;
            EnemyGun firing = guns.OrderByDescending(g => g.LastFireTime).FirstOrDefault();
            yield return null;
            Shot(Path.Combine(shots, "ai_06_flash_wide.png"));
            // From the soldier's right side: the gun in profile, off the line of fire.
            if (firing != null) CloseShot(firing.Model.position, firing.transform.right, Path.Combine(shots, "ai_07_flash_close.png"));
            Record("muzzle_flash", firing != null && guns.Sum(g => g.Shots) > before, firing != null ? $"{firing.name} shots={firing.Shots}" : "no shot");

            // A miss: she steps aside during a bullet telegraph, the bullet hits the wall behind her.
            Func<SoldierBrain, bool> bulletTelegraph = s => s.Gun.Current == DummyShooter.State.Telegraph
                && (s.Gun.CurrentAttack == DummyShooter.Attack.Shot || s.Gun.CurrentAttack == DummyShooter.Attack.Burst);
            t0 = BotClock.Now;
            while (!soldiers.Any(bulletTelegraph) && BotClock.Now - t0 < 15f) yield return null;
            Vector3 stand = lucy.transform.position;
            lucy.GetComponent<TankController>().Teleport(stand + Vector3.right * 2.5f, 0f);
            Transform impact = null;
            t0 = BotClock.Now;
            while (impact == null && BotClock.Now - t0 < 4f)
            {
                impact = FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name.StartsWith("impactConcrete"));
                yield return null;
            }
            if (impact != null)
            {
                yield return new WaitForSeconds(0.15f); // the particles need a moment
                // The impact faces out of the wall: the camera stands on that side.
                CloseShot(impact.position, impact.forward, Path.Combine(shots, "ai_08_impact.png"), 1.6f);
            }
            Record("impact", impact != null, impact != null ? $"at {impact.position}" : "no impact in 4 s");
            lucy.GetComponent<TankController>().Teleport(stand, 0f);

            yield return BotClock.Wait(4f);
            int fired = soldiers.Sum(s => s.Gun.ShotsFired);
            int drift = soldiers.Sum(s => s.MovedWhileShootingFrames);
            Record("move_or_shoot", fired >= 2 && drift == 0, $"shots={fired} frames moved while aiming={drift} " +
                string.Join(" ", soldiers.Select(s => $"{s.name}:moves={s.Moves}/shoots={s.ShootPhases}")));

            EndClip();
            Clip("ai_weapons");
            yield return Weapons(lucy, soldiers, powers, stats, shots);
            EndClip();
            Clip("ai_lift_slam");

            // The lift, then a slam.
            SoldierBrain lifted = soldiers.FirstOrDefault(s => s.name.StartsWith("Soldier")) ?? soldiers[0]; // not the heavy armour
            stats.DebugSet(100f, 0f);
            powers.Select(PowerKind.Telekinesis);
            lockOn.SetTarget(lifted.GetComponent<LockOnTargetable>());
            powers.Release(1f);
            yield return BotClock.Wait(1f);
            Shot(Path.Combine(shots, "ai_03_lifted.png"));
            Record("lift", lifted.transform.position.y > 1f && lifted.Current == SoldierBrain.Phase.Hold, $"{lifted.name} y={lifted.transform.position.y:0.00} phase={lifted.Current}");
            EnemyRagdoll liftedRagdoll = lifted.GetComponent<EnemyRagdoll>();
            if (liftedRagdoll != null) CloseShot(liftedRagdoll.Hips.position, ClearSide(liftedRagdoll.Hips.position, 3.2f), Path.Combine(shots, "ai_03b_lifted_limp.png"), 3.2f);
            Record("lift_ragdoll", liftedRagdoll != null && liftedRagdoll.Current == EnemyRagdoll.State.Held,
                liftedRagdoll != null ? $"{lifted.name} {liftedRagdoll.Current}" : "no ragdoll");
            stats.DebugSet(100f, 0f);
            Health liftedHealth = lifted.GetComponent<Health>();
            float hp = liftedHealth.CurrentHealth;
            bool slammed = powers.TrySlam();
            yield return BotClock.Wait(2.5f);
            Record("slam", slammed && lifted.transform.position.y < 0.2f && liftedHealth.CurrentHealth < hp,
                $"slam={slammed} y={lifted.transform.position.y:0.00} hp {hp:0} -> {liftedHealth.CurrentHealth:0} phase={lifted.Current}");

            yield return Linger(1.5f);
            EndClip();
            yield return Morale(lucy, soldiers, lifted, shots);
            Clip("ai_knockdown");
            yield return Ragdoll(soldiers, powers, stats, lockOn, shots);
            yield return Linger(1f);
            EndClip();

            // The power armour is heavy.
            SoldierBrain armour = soldiers.FirstOrDefault(s => s.name.StartsWith("Armour"));
            if (armour != null)
            {
                Clip("ai_too_heavy");
                lockOn.SetTarget(armour.GetComponent<LockOnTargetable>());
                powers.Release(1f);
                yield return null;
                yield return null;
                Shot(Path.Combine(shots, "ai_04_too_heavy.png"));
                Record("armour_heavy", powers.LastResult == "too_heavy", $"result={powers.LastResult}");
                yield return Linger(1.5f);
                EndClip();
            }

            Clip("ai_limb_cut");
            yield return LimbCut(soldiers, powers, stats, lockOn, shots);
            yield return Linger(1.5f);
            EndClip();

            // A corpse.
            SoldierBrain victim = soldiers.FirstOrDefault(s => s.name.StartsWith("Guard"));
            if (victim != null)
            {
                Clip("ai_death");
                Health health = victim.GetComponent<Health>();
                health.TakeDamage(health.MaxHealth * 10f);
                yield return BotClock.Wait(2.5f);
                Shot(Path.Combine(shots, "ai_05_corpse.png"));
                Record("death", victim != null && victim.Body.IsDead && victim.Current == SoldierBrain.Phase.Dead, $"{victim.name} dead={victim.Body.IsDead}");
            }
            EndClip();
            Finish();
        }

        /// <summary>
        /// What a death does (§11.4, §11.5) and the care of a body (Bruno, 2026-10-24): the slam killed the soldier,
        /// a gruesome death; the witnesses' shock and barks; the head burst (a telekinesis kill); the guard's retreat (its
        /// fear raised to the limit); a colleague who drags a body and does CPR while she hides.
        /// </summary>
        private IEnumerator Morale(GameObject lucy, SoldierBrain[] soldiers, SoldierBrain corpse, string shots)
        {
            EnemyMorale corpseMorale = corpse.GetComponent<EnemyMorale>();
            var living = soldiers.Where(s => s != corpse && !s.Body.IsDead).ToArray();
            int shocked = living.Count(s => s.ShockCount > 0 || s.Retreating);
            Clip("ai_witnesses");
            Shot(Path.Combine(shots, "ai_19_shock.png"));
            Record("witness_shock", corpse.Body.IsDead && corpseMorale.DiedGruesome && shocked > 0,
                $"gruesome={corpseMorale.DiedGruesome} " + string.Join(" ", living.Select(s => $"{s.name}:seen={s.GetComponent<EnemyMorale>().Witnessed}/shocks={s.ShockCount}/{s.Current}")));
            // The slam is a telekinesis kill: the head bursts (Bruno, 2026-10-24: "tk bursts head").
            GlorifiedBody.Gore.BoneStump stump = corpse.GetComponent<GlorifiedBody.Gore.BoneStump>();
            Transform head = stump != null ? stump.Bone : null;
            float settle = BotClock.Now;
            EnemyRagdoll corpseRagdoll = corpse.GetComponent<EnemyRagdoll>();
            while (corpseRagdoll != null && corpseRagdoll.Current != EnemyRagdoll.State.Corpse && BotClock.Now - settle < 6f) yield return null;
            ClearTheAir();
            if (head != null) CloseShot(head.position, ClearSide(head.position, 1.8f), Path.Combine(shots, "ai_20_head_burst.png"), 1.8f, 0.7f);
            Record("gore_head_burst", GlorifiedBody.Gore.GruesomeDeathFx.HeadsBurst > 0 && head != null && head.localScale.x < 0.05f
                && head == corpse.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head),
                $"heads={GlorifiedBody.Gore.GruesomeDeathFx.HeadsBurst} bone={(head != null ? head.name : "-")} ragdoll={(corpseRagdoll != null ? corpseRagdoll.Current.ToString() : "-")}");
            float poolWait = BotClock.Now;
            while (GlorifiedBody.Gore.BodyBloodPool.Count == 0 && BotClock.Now - poolWait < 8f) yield return null;
            yield return BotClock.Wait(4f); // it grows
            GameObject pool = GlorifiedBody.Gore.BodyBloodPool.Count > 0 ? GlorifiedBody.Gore.BodyBloodPool.Pools[GlorifiedBody.Gore.BodyBloodPool.Pools.Count - 1] : null;
            if (pool != null) CloseShot(pool.transform.position + Vector3.up * 0.2f, ClearSide(pool.transform.position + Vector3.up * 0.2f, 2.6f), Path.Combine(shots, "ai_20b_blood_pool.png"), 2.6f, 1.2f);
            Record("blood_pool", pool != null, pool != null ? $"size={pool.transform.localScale.x:0.00} m at {pool.transform.position}" : "no pool in 12 s");

            EndClip();
            // The guard's fear at its limit: it runs and cowers.
            SoldierBrain guard = living.FirstOrDefault(s => s.GetComponent<EnemyMorale>().RankOf == EnemyMorale.Rank.Guard);
            if (guard != null)
            {
                Clip("ai_guard_runs");
                EnemyMorale fear = guard.GetComponent<EnemyMorale>();
                while (!guard.Retreating && fear.Fear < 6) fear.See(corpseMorale, gruesome: true);
                float t0 = BotClock.Now;
                while (guard.Current != SoldierBrain.Phase.Cower && BotClock.Now - t0 < 10f) yield return null;
                yield return BotClock.Wait(0.3f);
                CloseShot(guard.transform.position + Vector3.up * 0.8f, Camera.main != null ? Camera.main.transform.position - guard.transform.position : Vector3.back, Path.Combine(shots, "ai_21_guard_cowers.png"), 3f);
                Record("guard_retreat", guard.Current == SoldierBrain.Phase.Cower, $"{guard.name} fear={fear.Fear} phase={guard.Current} reason={guard.RetreatReason}");
                EndClip();
                CloseClip("ai_guard_cowers", () => guard.transform, t => Camera.main != null ? Camera.main.transform.position - t.position : Vector3.back, 3f, 0.8f);
                yield return Linger(3f);
                EndClip();
            }

            // She hides: a colleague drags the body away from her and does CPR.
            CharacterController cc = lucy.GetComponent<CharacterController>();
            Vector3 home = lucy.transform.position;
            TankController tank = lucy.GetComponent<TankController>();
            cc.enabled = false;
            tank.enabled = false;
            lucy.transform.position = new Vector3(home.x, -30f, home.z);
            Physics.SyncTransforms();
            CorpseCare care = null;
            float wait = BotClock.Now;
            while (care == null && BotClock.Now - wait < 12f)
            {
                care = FindObjectsByType<CorpseCare>(FindObjectsSortMode.None).FirstOrDefault(c => c.Active);
                yield return null;
            }
            if (care != null)
            {
                CorpseCare carer = care;
                CloseClip("ai_drag_cpr", () => carer != null ? carer.transform : null, t => t.right, 3.2f, 0.7f);
                wait = BotClock.Now;
                while (care.Current != CorpseCare.Step.Drag && care.Active && BotClock.Now - wait < 8f) yield return null;
                yield return BotClock.Wait(0.8f);
                if (care.Active) CloseShot(care.transform.position + Vector3.up * 0.7f, care.transform.right, Path.Combine(shots, "ai_22_drag.png"), 3.2f);
                wait = BotClock.Now;
                while (care.Current != CorpseCare.Step.Cpr && care.Active && BotClock.Now - wait < 10f) yield return null;
                yield return BotClock.Wait(0.6f);
                if (care.Active) CloseShot(care.transform.position + Vector3.up * 0.6f, care.transform.right, Path.Combine(shots, "ai_23_cpr.png"), 2.6f);
                yield return Linger(5f);
                EndClip();
            }
            Record("care_drag_cpr", care != null && care.Current == CorpseCare.Step.Cpr && care.Dragged > 1f,
                care != null ? $"{care.name}>{(care.Patient != null ? care.Patient.name : "-")} step={care.Current} dragged={care.Dragged:0.0} pumps={care.Compressions}" : "no carer in 12 s");
            tank.enabled = true;
            tank.Teleport(home, 0f);
            yield return BotClock.Wait(1f);
        }

        /// <summary>
        /// A full mk_warspite kill (Bruno, 2026-10-24: "blast kill at full charge to cut", "forearm or whole arm,
        /// randomized", "flying piece too"): an arm comes off and flies away from her, and the piece stays.
        /// </summary>
        private IEnumerator LimbCut(SoldierBrain[] soldiers, PsychicPowers powers, PsychicStats stats, LockOnController lockOn, string shots)
        {
            SoldierBrain target = soldiers.FirstOrDefault(s => s != null && !s.Body.IsDead && s.name.StartsWith("Gasser"))
                ?? soldiers.FirstOrDefault(s => s != null && !s.Body.IsDead && s.GetComponent<EnemyRagdoll>() != null && !s.GetComponent<EnemyRagdoll>().DeathOnly && s.GetComponent<EnemyShield>() == null);
            if (target == null) { Record("gore_limb_cut", false, "no living soldier without a shield"); yield break; }
            int cuts = GlorifiedBody.Gore.GruesomeDeathFx.LimbsCut, pieces = GlorifiedBody.Gore.SeveredLimb.Spawned;
            stats.DebugSet(100f, 0f);
            powers.Select(PowerKind.Blast);
            lockOn.SetTarget(target.GetComponent<LockOnTargetable>());
            powers.FireAt(powers.Selected, 1f, target.GetComponent<LockOnTargetable>(), 100f);
            GlorifiedBody.Gore.SeveredLimb piece = GlorifiedBody.Gore.SeveredLimb.Spawned > pieces ? GlorifiedBody.Gore.SeveredLimb.Last : null;
            Vector3 cutAt = piece != null ? piece.transform.position : Vector3.zero;
            yield return BotClock.Wait(0.25f);
            if (piece != null) CloseShot(piece.transform.position, ClearSide(piece.transform.position, 2.4f), Path.Combine(shots, "ai_20c_arm_flies.png"), 2.4f, 0.4f);
            Record("gore_limb_cut", target.Body.IsDead && GlorifiedBody.Gore.GruesomeDeathFx.LimbsCut > cuts && GlorifiedBody.Gore.GruesomeDeathFx.LastLimb.Contains("Arm"),
                $"{target.name} dead={target.Body.IsDead} limb={GlorifiedBody.Gore.GruesomeDeathFx.LastLimb}");
            float t0 = BotClock.Now;
            while (piece != null && !piece.Settled && BotClock.Now - t0 < 8f) yield return null;
            yield return BotClock.Wait(0.5f);
            ClearTheAir();
            if (piece != null) CloseShot(piece.transform.position + Vector3.up * 0.1f, ClearSide(piece.transform.position + Vector3.up * 0.1f, 1.8f), Path.Combine(shots, "ai_20d_arm_lies.png"), 1.8f, 0.8f);
            Transform stump = target.GetComponent<GlorifiedBody.Gore.BoneStump>()?.Bone;
            if (stump != null && stump.parent != null) CloseShot(stump.parent.position, ClearSide(stump.parent.position, 1.8f), Path.Combine(shots, "ai_20e_arm_stump.png"), 1.8f, 0.6f);
            Record("limb_flies", piece != null && piece.Settled && Vector3.Distance(piece.transform.position, cutAt) > 0.5f,
                piece != null ? $"{piece.Triangles} tris, flew {Vector3.Distance(piece.transform.position, cutAt):0.0} m, settled={piece.Settled}" : "no piece");
        }

        /// <summary>
        /// The ragdoll (Bruno, 2026-10-24): a full mk_warspite knocks a soldier down for 2.5 s and he stands up; a
        /// held soldier hangs limp, the throw knocks down the one it hits, both stand up. Tiny damage: nobody dies.
        /// </summary>
        private IEnumerator Ragdoll(SoldierBrain[] soldiers, PsychicPowers powers, PsychicStats stats, LockOnController lockOn, string shots)
        {
            SoldierBrain[] able = soldiers.Where(s => s != null && !s.Body.IsDead && s.GetComponent<EnemyRagdoll>() != null && !s.GetComponent<EnemyRagdoll>().DeathOnly).ToArray();
            if (able.Length == 0) { Record("blast_knockdown", false, "no living soldier with a ragdoll"); yield break; }
            SoldierBrain first = able[0];
            EnemyRagdoll firstRagdoll = first.GetComponent<EnemyRagdoll>();
            stats.DebugSet(100f, 0f);
            powers.Select(PowerKind.Blast);
            lockOn.SetTarget(first.GetComponent<LockOnTargetable>());
            powers.FireAt(powers.Selected, 1f, first.GetComponent<LockOnTargetable>(), 0.01f);
            yield return BotClock.Wait(1.2f);
            bool down = firstRagdoll.Current == EnemyRagdoll.State.Down;
            Shot(Path.Combine(shots, "ai_24_knockdown_room.png"));
            ClearTheAir();
            CloseShot(firstRagdoll.Hips.position, ClearSide(firstRagdoll.Hips.position, 2.6f), Path.Combine(shots, "ai_24b_knockdown_close.png"), 2.6f, 0.9f);
            float t0 = BotClock.Now;
            while (firstRagdoll.Current != EnemyRagdoll.State.StandingUp && BotClock.Now - t0 < 6f) yield return null;
            yield return BotClock.Wait(0.5f);
            CloseShot(first.transform.position + Vector3.up * 0.6f, ClearSide(first.transform.position + Vector3.up * 0.6f, 3f), Path.Combine(shots, "ai_24c_standing_up.png"), 3f);
            while (firstRagdoll.Current != EnemyRagdoll.State.Animated && BotClock.Now - t0 < 10f) yield return null;
            Record("blast_knockdown", down && firstRagdoll.StandUps > 0, $"{first.name} down={down} clip={firstRagdoll.StandUpClip} stands={firstRagdoll.StandUps}");

            if (able.Length < 2) yield break;
            SoldierBrain thrown = able[0], struck = able[1];
            EnemyRagdoll a = thrown.GetComponent<EnemyRagdoll>(), b = struck.GetComponent<EnemyRagdoll>();
            int aDowns = a.Downs, bDowns = b.Downs;
            // The cowering guard hides behind cover, and a wall stops a throw (the rule): the bot puts him in the open.
            Vector3 open = OpenSpotNear(thrown.transform.position, 3.5f);
            UnityEngine.AI.NavMeshAgent struckAgent = struck.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (struckAgent != null && struckAgent.enabled) struckAgent.Warp(open);
            else struck.transform.position = open;
            yield return BotClock.Wait(0.3f);
            stats.DebugSet(100f, 0f);
            powers.Select(PowerKind.Telekinesis);
            lockOn.SetTarget(thrown.GetComponent<LockOnTargetable>());
            powers.FireAt(powers.Selected, 1f, thrown.GetComponent<LockOnTargetable>(), 0.01f);
            yield return BotClock.Wait(0.9f);
            TelekinesisLift lift = thrown.GetComponent<TelekinesisLift>();
            if (lift == null) { Record("throw_knockdown", false, "the lift did not take " + thrown.name); yield break; }
            lift.Launch(struck.GetComponent<LockOnTargetable>().GetLockPoint(), struck.GetComponent<Health>());
            t0 = BotClock.Now;
            while (lift != null && lift.LaunchVictim == null && BotClock.Now - t0 < 3f) yield return null;
            yield return BotClock.Wait(1.3f); // both on the floor
            Shot(Path.Combine(shots, "ai_25_throw_room.png"));
            ClearTheAir();
            Vector3 between = 0.5f * (a.Hips.position + b.Hips.position);
            CloseShot(between, ClearSide(between, 4f), Path.Combine(shots, "ai_25b_throw_both_down.png"), 4f, 1.2f);
            Record("throw_knockdown", a.Downs > aDowns && b.Downs > bDowns, $"{thrown.name}>{struck.name} downs {a.Downs - aDowns}/{b.Downs - bDowns}");
            t0 = BotClock.Now;
            while ((a.Current != EnemyRagdoll.State.Animated || b.Current != EnemyRagdoll.State.Animated) && BotClock.Now - t0 < 10f) yield return null;
            Record("stand_up", a.Current == EnemyRagdoll.State.Animated && b.Current == EnemyRagdoll.State.Animated,
                $"{thrown.name}={a.Current}/{a.StandUpClip} {struck.name}={b.Current}/{b.StandUpClip}");
        }

        /// <summary>The weapon steps: the grenade, the icons, the shield, the melee.</summary>
        private IEnumerator Weapons(GameObject lucy, SoldierBrain[] soldiers, PsychicPowers powers, PsychicStats stats, string shots)
        {
            TankController tank = lucy.GetComponent<TankController>();
            PlayerHud hud = FindFirstObjectByType<PlayerHud>();
            Vector3 home = lucy.transform.position;

            // The grenade: the arc, the red ring on the floor at her feet.
            EnemyGrenade grenade = null;
            float t0 = BotClock.Now;
            while (grenade == null && BotClock.Now - t0 < 25f)
            {
                grenade = FindFirstObjectByType<EnemyGrenade>();
                yield return null;
            }
            if (grenade != null)
            {
                float wait = BotClock.Now;
                while (grenade != null && !grenade.Landed && BotClock.Now - wait < 2f) yield return null;
                Shot(Path.Combine(shots, "ai_09_grenade_ring.png"));
            }
            Record("grenade", grenade != null || EnemyGrenade.Bursts > 0, grenade != null ? $"ring at {grenade.Target}" : $"bursts={EnemyGrenade.Bursts}");

            // The power armour's rocket and the gas grenadier's cloud.
            EnemyRocket rocket = null;
            t0 = BotClock.Now;
            while (rocket == null && BotClock.Now - t0 < 30f)
            {
                rocket = FindFirstObjectByType<EnemyRocket>();
                yield return null;
            }
            if (rocket != null)
            {
                yield return BotClock.Wait(0.2f);
                if (rocket != null) CloseShot(rocket.transform.position, rocket.transform.right, Path.Combine(shots, "ai_16_rocket.png"), 2.5f);
            }
            Record("rocket", rocket != null || EnemyRocket.Bursts > 0, $"bursts={EnemyRocket.Bursts}");
            GasCloud cloud = null;
            t0 = BotClock.Now;
            while (cloud == null && BotClock.Now - t0 < 30f)
            {
                cloud = FindFirstObjectByType<GasCloud>();
                yield return null;
            }
            if (cloud != null)
            {
                yield return BotClock.Wait(0.8f);
                Shot(Path.Combine(shots, "ai_17_gas_cloud.png"));
                // The close look: a harmless cloud in a free corner (the real one often shares her spot with a frag burst).
                GasCloud look = GasCloud.Spawn(new Vector3(-6.5f, 0f, 3.5f), 2.5f, 3f, 0f, "bot");
                yield return BotClock.Wait(1.2f);
                if (look != null) CloseShot(look.transform.position + Vector3.up * 1.0f, new Vector3(1f, 0f, -0.3f), Path.Combine(shots, "ai_18_gas_close.png"), 4.5f);
            }
            Record("gas", cloud != null, cloud != null ? $"cloud r={cloud.Radius}" : "no cloud in 30 s");

            // The icon next to the circle.
            t0 = BotClock.Now;
            while (!(hud != null && hud.ActiveThreatIcons > 0 && soldiers.Any(s => s.Gun.Current == DummyShooter.State.Telegraph)) && BotClock.Now - t0 < 12f) yield return null;
            Shot(Path.Combine(shots, "ai_10_icons.png"));
            Record("attack_icon", hud != null && hud.ActiveThreatIcons > 0, hud != null ? "shown: " + string.Join(",", hud.ShownAttacks) : "no HUD");

            // Attackers out of the frame: she stands in the south-west corner, the north-east camera frames her, the
            // soldiers in the north are behind it. The edge arrows carry the icons.
            // The camera turns away from them: it stands between the soldiers and her, and looks past her.
            Camera main = Camera.main;
            var director = FindFirstObjectByType<GlorifiedBody.Cameras.FixedCameraDirector>();
            if (director != null) director.enabled = false;
            Vector3 keepPos = main != null ? main.transform.position : Vector3.zero;
            Quaternion keepRot = main != null ? main.transform.rotation : Quaternion.identity;
            t0 = BotClock.Now;
            while (!(hud != null && hud.ActiveThreatArrows > 0) && BotClock.Now - t0 < 15f)
            {
                if (main != null)
                {
                    Vector3 centre = Vector3.zero;
                    int live = 0;
                    foreach (SoldierBrain s in soldiers) if (s != null && !s.Body.IsDead) { centre += s.transform.position; live++; }
                    if (live > 0) centre /= live;
                    Vector3 away = lucy.transform.position - centre;
                    away.y = 0f;
                    away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
                    main.transform.position = lucy.transform.position - away * 1.5f + Vector3.up * 3f;
                    main.transform.LookAt(lucy.transform.position + away * 3f);
                }
                yield return null;
            }
            yield return null;
            Shot(Path.Combine(shots, "ai_11_offscreen_icons.png"));
            if (main != null) main.transform.SetPositionAndRotation(keepPos, keepRot);
            if (director != null) director.enabled = true;
            Record("offscreen_icon", hud != null && hud.ActiveThreatArrows > 0, hud != null ? $"arrows={hud.ActiveThreatArrows} shown: {string.Join(",", hud.ShownAttacks)}" : "no HUD");
            tank.Teleport(home, 0f);
            yield return BotClock.Wait(1f);

            // The shield: mk_warspite from the front stops on it.
            SoldierBrain guard = soldiers.FirstOrDefault(s => s.GetComponent<EnemyShield>() != null);
            if (guard != null)
            {
                EnemyShield shield = guard.GetComponent<EnemyShield>();
                Vector3 front = guard.transform.position + guard.transform.forward * 3.5f;
                tank.Teleport(new Vector3(front.x, home.y, front.z), guard.transform.eulerAngles.y + 180f);
                stats.DebugSet(100f, 0f);
                powers.Select(PowerKind.Blast);
                float hp = guard.GetComponent<Health>().CurrentHealth;
                powers.FireAt(powers.Selected, 0.5f, guard.GetComponent<LockOnTargetable>(), 1f);
                yield return null;
                yield return null;
                Shot(Path.Combine(shots, "ai_12_blocked.png"));
                CloseShot(shield.Model.position, (guard.transform.forward - guard.transform.right * 0.6f).normalized, Path.Combine(shots, "ai_13_shield_close.png"), 2.2f);
                Record("shield_block", powers.LastResult == "blocked" && guard.GetComponent<Health>().CurrentHealth == hp,
                    $"result={powers.LastResult} hp={guard.GetComponent<Health>().CurrentHealth:0}/{hp:0} blocks={shield.BlockCount}");
                tank.Teleport(home, 0f);
                yield return BotClock.Wait(1f);
            }

            // Melee: she stands close to a soldier, it strikes with the telegraph.
            SoldierBrain fighter = soldiers.FirstOrDefault(s => s.name.StartsWith("Soldier")) ?? soldiers[0];
            t0 = BotClock.Now;
            while (fighter.Gun.HoldsSlot && BotClock.Now - t0 < 4f) yield return null;
            Vector3 close = fighter.transform.position + fighter.transform.forward * 1.2f;
            tank.Teleport(new Vector3(close.x, home.y, close.z), fighter.transform.eulerAngles.y + 180f);
            t0 = BotClock.Now;
            while (!(fighter.Gun.Current == DummyShooter.State.Telegraph && fighter.Gun.CurrentAttack == DummyShooter.Attack.Melee) && BotClock.Now - t0 < 8f) yield return null;
            Shot(Path.Combine(shots, "ai_14_melee_telegraph.png"));
            int strikes = fighter.Gun.MeleeStrikes;
            t0 = BotClock.Now;
            while (fighter.Gun.MeleeStrikes == strikes && BotClock.Now - t0 < 3f) yield return null;
            yield return BotClock.Wait(0.2f);
            CloseShot(fighter.transform.position + Vector3.up * 1.2f, fighter.transform.right, Path.Combine(shots, "ai_15_melee_strike.png"), 3f);
            Record("melee", fighter.Gun.MeleeStrikes > strikes, $"{fighter.name} strikes={fighter.Gun.MeleeStrikes} swings={fighter.GetComponent<EnemyGun>()?.MeleeSwings}");
            tank.Teleport(home, 0f);
            yield return BotClock.Wait(1f);
        }

        /// <summary>A 1080p shot with the HUD (the telegraph circle, the notice) drawn through the camera.</summary>
        private static void Shot(string path)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            PlayerHud hud = FindFirstObjectByType<PlayerHud>();
            Canvas canvas = hud != null ? hud.Canvas : null;
            RenderMode mode = RenderMode.ScreenSpaceOverlay;
            Camera worldCamera = null;
            float plane = 1f;
            if (canvas != null)
            {
                mode = canvas.renderMode;
                worldCamera = canvas.worldCamera;
                plane = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.05f;
                Canvas.ForceUpdateCanvases();
            }
            try
            {
                ShotRenderer.Render(cam, path, ShotWidth, ShotHeight, useTargetTexture: true);
            }
            finally
            {
                if (canvas != null)
                {
                    canvas.renderMode = mode;
                    canvas.worldCamera = worldCamera;
                    canvas.planeDistance = plane;
                }
            }
        }

        /// <summary>A close shot from a temporary camera: it stands back from the point and looks at it.</summary>
        /// <summary>A side to look from with nothing between the camera and the point (a pillar, the armour).</summary>
        private static Vector3 ClearSide(Vector3 point, float distance)
        {
            Vector3 preferred = Camera.main != null ? Camera.main.transform.position - point : Vector3.back;
            preferred.y = 0f;
            if (preferred.sqrMagnitude < 0.001f) preferred = Vector3.back;
            int mask = ~LayerMask.GetMask("BodyPart");
            for (int i = 0; i < 16; i++)
            {
                Vector3 side = Quaternion.Euler(0f, (i % 2 == 0 ? 1 : -1) * (i + 1) / 2 * 22.5f, 0f) * preferred.normalized;
                Vector3 from = point + side * distance + Vector3.up * 0.5f;
                if (!Physics.Linecast(from, point, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore) || Vector3.Distance(hit.point, point) < 0.45f) return side;
            }
            return preferred;
        }

        /// <summary>A NavMesh point about the distance away with a clear line to the origin (for a staged throw).</summary>
        private static Vector3 OpenSpotNear(Vector3 origin, float distance)
        {
            for (int i = 0; i < 16; i++)
            {
                Vector3 candidate = origin + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * distance;
                if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out UnityEngine.AI.NavMeshHit hit, 1f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                if (Physics.Linecast(origin + Vector3.up * 1.5f, hit.position + Vector3.up * 1.2f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) continue;
                return hit.position;
            }
            return origin + Vector3.forward * distance;
        }

        /// <summary>The gas grenadier keeps firing: the bot removes the clouds before a close shot of a body.</summary>
        private static void ClearTheAir()
        {
            foreach (GasCloud cloud in FindObjectsByType<GasCloud>(FindObjectsSortMode.None)) Destroy(cloud.gameObject);
        }

        // ---- Video clips (tools/unity.sh record AiTest Main_Menu ai): nothing happens in a normal run.

        private static Camera closeClipCamera;

        /// <summary>Starts a clip through the room cameras, with the HUD.</summary>
        private static void Clip(string name)
        {
            if (!FrameRecorder.Enabled) return;
            FrameRecorder.CameraProvider = null;
            FrameRecorder.Ensure().Begin(name);
        }

        /// <summary>Starts a clip through a close camera that follows a target each frame (no HUD).</summary>
        private static void CloseClip(string name, Func<Transform> target, Func<Transform, Vector3> facing, float distance, float height)
        {
            if (!FrameRecorder.Enabled) return;
            if (closeClipCamera == null)
            {
                var go = new GameObject("[CloseClipCamera]");
                closeClipCamera = go.AddComponent<Camera>();
                closeClipCamera.fieldOfView = 40f;
                closeClipCamera.nearClipPlane = 0.05f;
                closeClipCamera.enabled = false; // rendered only by the recorder
            }
            Camera main = Camera.main;
            if (main != null)
            {
                closeClipCamera.clearFlags = main.clearFlags;
                closeClipCamera.backgroundColor = main.backgroundColor;
            }
            FrameRecorder.CameraProvider = () =>
            {
                Transform t = target();
                if (t == null) return null;
                Vector3 point = t.position + Vector3.up * height;
                Vector3 side = facing(t);
                side.y = 0f;
                if (side.sqrMagnitude < 0.001f) side = Vector3.back;
                closeClipCamera.transform.position = point + side.normalized * distance + Vector3.up * 0.3f;
                closeClipCamera.transform.LookAt(point);
                return closeClipCamera;
            };
            FrameRecorder.Ensure().Begin(name);
        }

        private static void EndClip()
        {
            if (FrameRecorder.Instance != null) FrameRecorder.Instance.End();
            FrameRecorder.CameraProvider = null;
        }

        /// <summary>A hold on the bot clock, only on video, so a clip shows the moment.</summary>
        private static IEnumerator Linger(float seconds)
        {
            if (FrameRecorder.Enabled) yield return BotClock.Wait(seconds);
        }

        private static void CloseShot(Vector3 point, Vector3 facing, string path, float distance = 1.4f, float height = 0.25f)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            var go = new GameObject("[CloseShotCamera]");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            Camera main = Camera.main;
            if (main != null)
            {
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
            }
            go.transform.position = point + facing.normalized * distance + Vector3.up * height;
            go.transform.LookAt(point);
            try
            {
                ShotRenderer.Render(cam, path, ShotWidth, ShotHeight, useTargetTexture: true);
            }
            finally
            {
                Destroy(go);
            }
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"ai\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                sb.Append("    {\"name\": \"").Append(Esc(s.name)).Append("\", \"ok\": ").Append(s.ok ? "true" : "false")
                  .Append(", \"t\": ").Append(s.t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(", \"detail\": \"").Append(Esc(s.detail)).Append("\"}").Append(i < steps.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            string dir = BotArgs.RunDir;
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "playthrough.json"), sb.ToString());
            }
            Debug.Log($"[AiBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("ai_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
