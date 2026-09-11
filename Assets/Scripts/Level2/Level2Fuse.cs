using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RunawayChimps.Level2
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(XRGrabInteractable))]
    public sealed class Level2Fuse : MonoBehaviour
    {
        public int fuseId = 1;
        public float impactNoiseRadius = 7f;
        public float impactSpeedThreshold = 1.8f;
        public bool IsInserted { get; private set; }
        public bool IsCharged { get; private set; }
        public bool WasHeldByLocalPlayer { get; private set; }
        public Level2FuseSocket Socket { get; private set; }

        XRGrabInteractable grab;
        Rigidbody body;
        float nextImpactNoise;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
        }

        void OnEnable()
        {
            grab.selectEntered.AddListener(OnSelected);
        }

        void OnDisable()
        {
            if (grab != null) grab.selectEntered.RemoveListener(OnSelected);
        }

        void OnSelected(SelectEnterEventArgs args)
        {
            if (args.interactorObject.transform.GetComponentInParent<LocalRigMarker>() != null)
                WasHeldByLocalPlayer = true;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (IsInserted || Time.time < nextImpactNoise || collision.relativeVelocity.magnitude < impactSpeedThreshold) return;
            nextImpactNoise = Time.time + .2f;
            Level2NoiseBus.Emit(transform.position, impactNoiseRadius, Level2NoiseKind.FuseImpact, gameObject);
        }

        internal bool TryInsert(Level2FuseSocket socket, Transform snap)
        {
            if (IsInserted || socket == null || snap == null || !WasHeldByLocalPlayer) return false;
            IsInserted = true;
            Socket = socket;
            if (grab != null) grab.enabled = false;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            transform.SetParent(snap, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            return true;
        }

        internal void MarkCharged()
        {
            if (!IsInserted) return;
            IsCharged = true;
        }
    }
}
