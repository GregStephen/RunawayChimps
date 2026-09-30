using UnityEngine;
using UnityEngine.XR;

namespace RunawayChimps.Toys.KnockBack
{
    // Reads only the Bootstrap rig. Physics contacts, remote avatars and audio cannot call this.
    internal sealed class KnockBackVentHandInput
    {
        private readonly VentContactGate gate = new VentContactGate();
        private bool seeded;
        private Vector3 previousLocal, previousWorld;
        private double previousTime;

        public void Reset() { seeded = false; gate.Reset(); }

        public bool Sample(GorillaLocomotion.Player player, Transform rig, Transform hand,
            Vector3 offset, XRNode node, BoxCollider face, double now,
            float minimumSpeed, float maximumSpeed, float maximumStep, float releaseSeconds)
        {
            Transform follower = node == XRNode.LeftHand ? player.leftHandFollower : player.rightHandFollower;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (hand == null || !hand.gameObject.activeInHierarchy || follower == null ||
                !follower.gameObject.activeInHierarchy || face == null || !face.enabled || !device.isValid ||
                !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked ||
                !device.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState tracking) ||
                (tracking & InputTrackingState.Position) == 0)
            { Reset(); return false; }

            Vector3 world = hand.position + hand.rotation * offset;
            Vector3 local = rig.InverseTransformPoint(world);
            Vector3 point = face.transform.InverseTransformPoint(world) - face.center;
            double dt = now - previousTime;
            // Rig-relative speed survives Gorilla's collision-driven body translation. World
            // displacement is checked separately so recenter/teleport cannot manufacture taps.
            Vector3 displacement = rig.TransformVector(local - previousLocal);
            float speed = dt > 0 ? displacement.magnitude / (float)dt : 0f;
            bool valid = seeded && dt > 0 && dt <= 0.12d &&
                VentModel.Finite(speed) && speed <= maximumSpeed &&
                displacement.magnitude <= maximumStep && Vector3.Distance(world, previousWorld) <= maximumStep &&
                Vector3.Distance(world, player.headCollider.transform.position) <= player.maxArmLength + 0.35f;
            seeded = true;
            previousTime = now;
            previousLocal = local;
            previousWorld = world;

            Vector3 half = face.size * 0.5f;
            // Work in the collider's local frame; the prefab's +Z is its front. No absolute
            // world axis or hand tag/layer is trusted. Keep root scale uniform and positive.
            float scale = Mathf.Max(0.01f, face.transform.lossyScale.z);
            float radius = 0.055f / scale;
            bool withinFace = Mathf.Abs(point.x) <= half.x + radius && Mathf.Abs(point.y) <= half.y + radius;
            bool touching = withinFace && point.z <= half.z + radius && point.z >= -half.z - radius;
            // Passing through to the rear never rearms a hand. It must come out in front.
            bool released = point.z > half.z + radius + 0.035f / scale ||
                (!withinFace && point.z >= half.z);
            // A tracked controller can pass through a wall or exceed the arm limit while
            // Gorilla's solved virtual hand stays blocked elsewhere. Only the latter can
            // establish contact. The controller still supplies intent/velocity, not the
            // collision-constrained follower's near-zero impact velocity.
            if (touching)
            {
                Vector3 solved = face.transform.InverseTransformPoint(follower.position) - face.center;
                Vector3 fromFront = new Vector3(Mathf.Max(0f, Mathf.Abs(solved.x) - half.x),
                    Mathf.Max(0f, Mathf.Abs(solved.y) - half.y), solved.z - half.z);
                float contactRadius = (Mathf.Max(0.05f, player.minimumRaycastDistance) + 0.015f) / scale;
                valid &= solved.z >= half.z - 0.005f / scale && fromFront.magnitude <= contactRadius;
            }
            float inward = dt > 0 ? Vector3.Dot(-displacement / (float)dt, face.transform.forward) : 0f;
            return gate.Sample(now, valid, touching, released, inward, minimumSpeed, releaseSeconds);
        }
    }
}
