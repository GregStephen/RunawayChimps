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
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (hand == null || !hand.gameObject.activeInHierarchy || !device.isValid ||
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
            float radius = 0.055f / Mathf.Max(0.01f, face.transform.lossyScale.z);
            bool withinFace = Mathf.Abs(point.x) <= half.x + radius && Mathf.Abs(point.y) <= half.y + radius;
            bool touching = withinFace && point.z <= half.z + radius && point.z >= -half.z - radius;
            // Passing through to the rear never rearms a hand. It must come out in front.
            bool released = point.z > half.z + radius + 0.035f ||
                (!withinFace && point.z >= half.z);
            float inward = dt > 0 ? Vector3.Dot(-displacement / (float)dt, face.transform.forward) : 0f;
            return gate.Sample(now, valid, touching, released, inward, minimumSpeed, releaseSeconds);
        }
    }
}
