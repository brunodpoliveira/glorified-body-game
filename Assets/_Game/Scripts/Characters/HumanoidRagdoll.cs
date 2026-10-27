using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// A ragdoll on a Humanoid rig, from code (the enemy factory in the editor, Lucy at her death at run time). The same parts as Unity's ragdoll wizard (GameObject > 3D
    /// Object > Ragdoll), which cannot run outside its window: a box on the hips and the chest, capsules on the
    /// thighs, shins, upper arms and forearms, a sphere on the head, CharacterJoints to the parent part. Each joint
    /// turns about the axis across its bone, and its limits open toward the side a body bends (the knee back, the
    /// elbow and the hip forward), found by a test turn, so an A-pose and a T-pose both work. Sizes are in metres
    /// and converted to each bone's own scale (the rig's lossyScale does not tell the truth, TransformVector does).
    /// Numbers PROPOSED.
    /// </summary>
    public static class HumanoidRagdoll
    {
        public sealed class Result
        {
            public readonly List<Rigidbody> Bodies = new List<Rigidbody>(); // the hips first, parents before children
            public string Error = "";
        }

        private sealed class Part
        {
            public Transform Bone, End;   // the segment: from Bone to End
            public Part Parent;
            public float MassShare;
            public Rigidbody Body;
        }

        public static Result Build(Animator animator, float totalMass)
        {
            var result = new Result();
            if (animator == null || !animator.isHuman)
            {
                result.Error = "no Humanoid animator";
                return result;
            }
            Transform root = animator.transform;
            Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
            Transform hips = B(HumanBodyBones.Hips);
            Transform chest = B(HumanBodyBones.Chest) ?? B(HumanBodyBones.Spine);
            Transform neck = B(HumanBodyBones.Neck);
            Transform head = B(HumanBodyBones.Head);
            if (hips == null || chest == null || head == null)
            {
                result.Error = "the rig lacks the hips, the chest or the head";
                return result;
            }
            Clean(hips);

            Vector3 up = root.up, forward = root.forward;
            var parts = new List<Part>();
            Part Add(Transform bone, Transform end, Part parent, float share)
            {
                if (bone == null || end == null) return null;
                var p = new Part { Bone = bone, End = end, Parent = parent, MassShare = share };
                parts.Add(p);
                return p;
            }
            Part pelvis = Add(hips, chest, null, 0.20f);
            Part torso = Add(chest, neck ?? head, pelvis, 0.20f);
            Part skull = Add(head, head, torso, 0.08f);
            Part lThigh = Add(B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg), pelvis, 0.10f);
            Part rThigh = Add(B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg), pelvis, 0.10f);
            Part lShin = Add(B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot), lThigh, 0.07f);
            Part rShin = Add(B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot), rThigh, 0.07f);
            Part lArm = Add(B(HumanBodyBones.LeftUpperArm), B(HumanBodyBones.LeftLowerArm), torso, 0.04f);
            Part rArm = Add(B(HumanBodyBones.RightUpperArm), B(HumanBodyBones.RightLowerArm), torso, 0.04f);
            Part lFore = Add(B(HumanBodyBones.LeftLowerArm), B(HumanBodyBones.LeftHand), lArm, 0.03f);
            Part rFore = Add(B(HumanBodyBones.RightLowerArm), B(HumanBodyBones.RightHand), rArm, 0.03f);
            if (lThigh == null || rThigh == null || lShin == null || rShin == null || lArm == null || rArm == null || lFore == null || rFore == null)
            {
                result.Error = "the rig lacks a limb bone";
                return result;
            }

            // The colliders.
            Transform lUpLeg = lThigh.Bone, rUpLeg = rThigh.Bone, lUpArm = lArm.Bone, rUpArm = rArm.Bone;
            Box(hips, new[] { hips.position - up * 0.08f, lUpLeg.position, rUpLeg.position, chest.position }, forward, 0.22f);
            Box(chest, new[] { chest.position, (neck ?? head).position, lUpArm.position, rUpArm.position }, forward, 0.24f);
            Sphere(head, head.position + up * 0.09f, 0.11f);
            Capsule(lThigh, 0.075f); Capsule(rThigh, 0.075f);
            Capsule(lShin, 0.055f); Capsule(rShin, 0.055f);
            Capsule(lArm, 0.05f); Capsule(rArm, 0.05f);
            Capsule(lFore, 0.045f); Capsule(rFore, 0.045f);

            // The bodies, then the joints (a joint needs its parent's body).
            foreach (Part p in parts)
            {
                p.Body = p.Bone.gameObject.AddComponent<Rigidbody>();
                p.Body.mass = totalMass * p.MassShare;
                // More solver passes than the default 6: the joint limits hold under the death push and the jerks.
                // A heavy body (the 220 kg power armour) needs twice that: its knee folded to 180 degrees in its fall.
                p.Body.solverIterations = totalMass > 120f ? 32 : 16;
                p.Body.solverVelocityIterations = 4;
            }
            Joint(torso, root.right, forward, -20f, 30f, 15f);
            Joint(skull, root.right, forward, -40f, 40f, 30f);
            Joint(lThigh, Across(lThigh, forward), forward, -30f, 100f, 30f);
            Joint(rThigh, Across(rThigh, forward), forward, -30f, 100f, 30f);
            Joint(lShin, Across(lShin, forward), -forward, -2f, 130f, 5f);
            Joint(rShin, Across(rShin, forward), -forward, -2f, 130f, 5f);
            Joint(lArm, Across(lArm, forward), forward, -60f, 90f, 60f);
            Joint(rArm, Across(rArm, forward), forward, -60f, 90f, 60f);
            Joint(lFore, Across(lFore, forward), forward, -2f, 140f, 5f);
            Joint(rFore, Across(rFore, forward), forward, -2f, 140f, 5f);

            foreach (Part p in parts) result.Bodies.Add(p.Body);
            return result;
        }

        /// <summary>Removes an older build: joints, then bodies, then colliders under the hips.</summary>
        private static void Clean(Transform hips)
        {
            foreach (CharacterJoint j in hips.GetComponentsInChildren<CharacterJoint>(true)) Object.DestroyImmediate(j);
            foreach (Rigidbody r in hips.GetComponentsInChildren<Rigidbody>(true))
                if (r.GetComponentInParent<Animator>() != null && !r.GetComponent<Animator>()) Object.DestroyImmediate(r);
            foreach (Collider c in hips.GetComponentsInChildren<Collider>(true))
            {
                // Only the bone colliders: a gun or a shield model under a hand keeps its own.
                if (c is CapsuleCollider || c is BoxCollider || c is SphereCollider)
                    if (c.GetComponent<Renderer>() == null && c.GetComponentInChildren<Renderer>() == null) Object.DestroyImmediate(c);
            }
        }

        /// <summary>The axis across the segment, square to the facing (the axis a knee or an elbow turns about).</summary>
        private static Vector3 Across(Part p, Vector3 forward)
        {
            Vector3 along = p.End.position - p.Bone.position;
            Vector3 axis = Vector3.Cross(along, forward);
            return axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.right;
        }

        /// <summary>
        /// A CharacterJoint about the axis: the twist range [low, high] in degrees opens toward the bend direction
        /// (the sign comes from a test turn of the segment), swing in degrees on the other two axes.
        /// </summary>
        private static void Joint(Part p, Vector3 axis, Vector3 bend, float low, float high, float swing)
        {
            if (p == null || p.Parent == null) return;
            Vector3 along = p.End != p.Bone ? (p.End.position - p.Bone.position).normalized : p.Bone.position - p.Parent.Bone.position;
            // A CharacterJoint counts its twist the other way round from Quaternion.AngleAxis (Unity's ragdoll wizard
            // gives a knee -80..0 about the body's right). So the axis points where AngleAxis turns AWAY from the bend.
            // Found 2026-10-27 (Bruno: "the lower leg bends inwards"): every knee bent 130 degrees the wrong way.
            Vector3 turned = Quaternion.AngleAxis(10f, axis) * along;
            if (Vector3.Dot(turned - along, bend) > 0f) axis = -axis;
            Vector3 swingAxis = Vector3.Cross(axis, along);
            if (swingAxis.sqrMagnitude < 1e-6f) swingAxis = Vector3.Cross(axis, Vector3.up);

            var joint = p.Bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = p.Parent.Body;
            joint.anchor = Vector3.zero;
            joint.axis = p.Bone.InverseTransformDirection(axis.normalized);
            joint.swingAxis = p.Bone.InverseTransformDirection(swingAxis.normalized);
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            // The projection puts a bone back inside its limits when the solver lets it slip past them. The default
            // angle (180) never acts: the 220 kg armour's knee settled 24 degrees bent the wrong way.
            joint.projectionAngle = 10f;
            joint.projectionDistance = 0.05f;
            joint.lowTwistLimit = new SoftJointLimit { limit = low };
            joint.highTwistLimit = new SoftJointLimit { limit = high };
            joint.swing1Limit = new SoftJointLimit { limit = swing };
            joint.swing2Limit = new SoftJointLimit { limit = swing };
        }

        private static void Capsule(Part p, float radiusMetres)
        {
            Transform t = p.Bone;
            Vector3 localEnd = t.InverseTransformPoint(p.End.position);
            int axis = Dominant(localEnd);
            float unitAlong = UnitLength(t, axis);
            float unitAcross = UnitLength(t, (axis + 1) % 3);
            var c = t.gameObject.AddComponent<CapsuleCollider>();
            c.direction = axis;
            c.center = localEnd * 0.5f;
            c.radius = radiusMetres / unitAcross;
            c.height = Mathf.Abs(localEnd[axis]) + 2f * radiusMetres / unitAlong * 0.6f;
        }

        private static void Box(Transform t, Vector3[] worldPoints, Vector3 forward, float depthMetres)
        {
            var bounds = new Bounds(t.InverseTransformPoint(worldPoints[0]), Vector3.zero);
            foreach (Vector3 w in worldPoints) bounds.Encapsulate(t.InverseTransformPoint(w));
            Vector3 size = bounds.size * 1.1f;
            int depthAxis = Dominant(t.InverseTransformDirection(forward));
            size[depthAxis] = depthMetres / UnitLength(t, depthAxis);
            for (int i = 0; i < 3; i++) size[i] = Mathf.Max(size[i], 0.05f / UnitLength(t, i));
            Vector3 center = bounds.center;
            center[depthAxis] = 0f;
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        private static void Sphere(Transform t, Vector3 worldCenter, float radiusMetres)
        {
            var c = t.gameObject.AddComponent<SphereCollider>();
            c.center = t.InverseTransformPoint(worldCenter);
            c.radius = radiusMetres / UnitLength(t, 1);
        }

        /// <summary>The world length of one local unit along the axis (the rig's own scale).</summary>
        private static float UnitLength(Transform t, int axis)
        {
            Vector3 unit = Vector3.zero;
            unit[axis] = 1f;
            return Mathf.Max(1e-5f, t.TransformVector(unit).magnitude);
        }

        private static int Dominant(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
        }
    }
}
