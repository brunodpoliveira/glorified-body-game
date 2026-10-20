using System.Collections.Generic;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// Difficulty rule (design §3.2): easy = one enemy fires at a time, normal = two, hard = all of them.
    /// A shooter takes a slot before it aims and gives it back when it fires or stops. 0 slots = no limit.
    /// </summary>
    public static class ShooterScheduler
    {
        private static readonly List<DummyShooter> active = new List<DummyShooter>();

        public static int MaxSlots
        {
            get
            {
                int max = DifficultyManager.Instance != null ? DifficultyManager.Instance.MaxSimultaneousShooters : 2;
                return max <= 0 ? int.MaxValue : max;
            }
        }

        public static int ActiveCount
        {
            get
            {
                Prune();
                return active.Count;
            }
        }

        public static bool TryAcquire(DummyShooter shooter)
        {
            if (shooter == null) return false;
            Prune();
            if (active.Contains(shooter)) return true;
            if (active.Count >= MaxSlots) return false;
            active.Add(shooter);
            return true;
        }

        public static void Release(DummyShooter shooter)
        {
            active.Remove(shooter);
        }

        private static void Prune()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                DummyShooter s = active[i];
                if (s == null || !s.isActiveAndEnabled || s.IsDead || !s.HoldsSlot) active.RemoveAt(i);
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            active.Clear();
        }
    }
}
