using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RunawayChimps.Level2
{
    public enum Level2FuseState { Available, Carried, Inserted, Charged }

    [RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(XRGrabInteractable))]
    public sealed class Level2Fuse : MonoBehaviour
    {
        public int fuseId = 1;
        public float impactNoiseRadius = 7f;
        public float impactSpeedThreshold = 1.8f;
        public float recoveryY = -4f;
        public Level2FuseState State { get; private set; } = Level2FuseState.Available;
        public bool IsInserted => State == Level2FuseState.Inserted || State == Level2FuseState.Charged;
        public bool IsCharged => State == Level2FuseState.Charged;
        public bool WasHeldByLocalPlayer { get; private set; }
        public Level2FuseSocket Socket { get; private set; }

        XRGrabInteractable grab;
        Rigidbody body;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float nextImpactNoise;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void OnEnable()
        {
            grab.selectEntered.AddListener(OnSelected);
            grab.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            if (grab == null) return;
            grab.selectEntered.RemoveListener(OnSelected);
            grab.selectExited.RemoveListener(OnReleased);
        }

        void Update()
        {
            if (!IsInserted && transform.position.y < recoveryY) ResetToSpawn();
        }

        void OnSelected(SelectEnterEventArgs args)
        {
            if (IsInserted || args.interactorObject.transform.GetComponentInParent<LocalRigMarker>() == null) return;
            WasHeldByLocalPlayer = true;
            State = Level2FuseState.Carried;
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (!IsInserted && State == Level2FuseState.Carried) State = Level2FuseState.Available;
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
            if (grab != null && grab.isSelected) grab.interactionManager?.SelectExit(grab.firstInteractorSelecting, grab);
            State = Level2FuseState.Inserted;
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
            if (State == Level2FuseState.Inserted) State = Level2FuseState.Charged;
        }

        public void ResetToSpawn()
        {
            if (IsInserted) return;
            if (grab != null && grab.isSelected) grab.interactionManager?.SelectExit(grab.firstInteractorSelecting, grab);
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            body.isKinematic = false;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Socket = null;
            State = Level2FuseState.Available;
            WasHeldByLocalPlayer = false;
            if (grab != null) grab.enabled = true;
        }
    }
}
