using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The gun in the hands for each class and vendor weapon set (option B, Bruno 2026-11-17: "Yes, build B"). Where the
    /// gun sits in the right hand (the Handler of an Invector character) and how the left hand turns on the gun's
    /// leftHandIK. HoldCalibration measures them on Invector's vBot in the same aim clip: the Humanoid hand frame is the
    /// same on both bodies, so the vBot's grip carries across. The tune fields are the corrections from the shots.
    /// EnemyGun reads them; a class with no entry keeps the procedural hold.
    /// </summary>
    public sealed class HoldOffsets : ScriptableObject
    {
        public const string ResourcePath = "Characters/hold_offsets";

        [System.Serializable]
        public sealed class Entry
        {
            public string classId;
            [Tooltip("The vendor weapon set: 1 handgun, 2 rifle, 3 shotgun (EnemyGun.upperBodyId); 0 the knife (the vendor short sword's grip).")]
            public int upperBody;
            [Tooltip("Measured: the gun's grip from the wrist, in the hand bone's turn, in forearm lengths.")]
            public Vector3 gripOffset;
            [Tooltip("Measured: the gun's turn in the hand bone's turn.")]
            public Vector3 gripEuler;
            [Tooltip("Measured: the left hand bone's turn against the gun's leftHandIK.")]
            public Vector3 leftEuler;
            [Header("Tuned from the shots")]
            [Tooltip("Metres in the gun's own axes: the gun moves this far in the hand.")]
            public Vector3 tuneGripOffset;
            [Tooltip("Degrees in the gun's own axes.")]
            public Vector3 tuneGripEuler;
            [Tooltip("Metres in the gun's own axes, added to leftHandIK.")]
            public Vector3 tuneLeftOffset;
            [Tooltip("Degrees in leftHandIK's own axes.")]
            public Vector3 tuneLeftEuler;

            [Header("The knife (upper body 0): the arms of Invector's one-hand sword clips, on this class's rig")]
            [Tooltip("Local turns of RightShoulder, RightUpperArm, RightLowerArm, RightHand, LeftShoulder, LeftUpperArm, LeftLowerArm, LeftHand (KnifeBones).")]
            public Quaternion[] restArms;   // Melee_StrafeMoveset standing_idle: the blade low at his side
            public Quaternion[] guardArms;  // WeakAttack_SwordA at 0.10: drawn back for the stab (the wind-up)
            public Quaternion[] strikeArms; // WeakAttack_SwordA at 0.55: the thrust

            /// <summary>True when the three knife poses are measured for every bone.</summary>
            public bool HasKnifeArms => restArms != null && guardArms != null && strikeArms != null
                && restArms.Length == KnifeBones.Length && guardArms.Length == KnifeBones.Length && strikeArms.Length == KnifeBones.Length;

            /// <summary>The gun's turn in the hand bone's turn, with the tune.</summary>
            public Quaternion GripRotation => Quaternion.Euler(gripEuler) * Quaternion.Euler(tuneGripEuler);

            /// <summary>The left hand bone's world turn on a leftHandIK with this world turn.</summary>
            public Quaternion LeftHand(Quaternion leftHandIK) => leftHandIK * Quaternion.Euler(tuneLeftEuler) * Quaternion.Euler(leftEuler);
        }

        /// <summary>The bones of the knife poses, in the order of Entry.restArms.</summary>
        public static readonly HumanBodyBones[] KnifeBones =
        {
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        };

        public List<Entry> entries = new List<Entry>();

        public Entry Find(string classId, int upperBody) => entries.Find(e => e.classId == classId && e.upperBody == upperBody);

        private static HoldOffsets cached;

        public static HoldOffsets Load()
        {
            if (cached == null) cached = Resources.Load<HoldOffsets>(ResourcePath);
            return cached;
        }

        /// <summary>The class of a figure: its avatar is "&lt;class&gt;Avatar" (CreateCharacterAssets).</summary>
        public static string ClassOf(Animator animator)
        {
            if (animator == null || animator.avatar == null) return "";
            string name = animator.avatar.name;
            return name.EndsWith("Avatar") ? name.Substring(0, name.Length - "Avatar".Length) : name;
        }
    }
}
