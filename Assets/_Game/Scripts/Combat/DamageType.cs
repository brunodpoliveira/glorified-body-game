namespace GlorifiedBody.Combat
{
    public enum DamageType
    {
        // ── From Arena ───────────────────────────────────────────────────
        Unknown,
        PlayerMelee,
        PlayerBullet,
        EnemyMelee,
        EnemyBullet,
        Explosion,
        FallingHazard,
        PropImpact,
        Debug,

        // ── Warspite P2 extensions ───────────────────────────────────────
        TimeDilationMelee,
        Divekick,
        Slam,
        AudioPulse,
        SonicBoom,
        Throw,
        Grenade,
        EnemyProjectile,

        // ── THE GLORIFIED BODY: the psychic powers and the short ─────────
        PsychicBlast,
        PsychicFire,
        PsychicTelekinesis,
        ShortShock
    }
}
