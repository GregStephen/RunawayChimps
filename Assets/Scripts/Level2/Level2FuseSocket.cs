using UnityEngine;
using UnityEngine.Events;

namespace RunawayChimps.Level2
{
    [RequireComponent(typeof(Collider))]
    public sealed class Level2FuseSocket : MonoBehaviour
    {
        public int socketId = 1;
        public Transform snapPoint;
        [Min(.1f)] public float chargeSeconds = 10f;
        public float chargeNoiseRadius = 28f;
        public UnityEvent<float> OnChargeProgress;
        public UnityEvent OnCharged;

        public Level2Fuse Fuse { get; private set; }
        public float ChargeProgress { get; private set; }
        public bool IsCharged => Fuse != null && Fuse.IsCharged;
        public bool IsCharging { get; private set; }

        float nextNoisePulse;

        void Awake()
        {
            var c = GetComponent<Collider>();
            c.isTrigger = true;
            if (snapPoint == null) snapPoint = transform;
        }

        void OnTriggerEnter(Collider other)
        {
            if (Fuse != null) return;
            var fuse = other.GetComponentInParent<Level2Fuse>();
            if (fuse != null && fuse.TryInsert(this, snapPoint))
            {
                Fuse = fuse;
                Level2FuseObjective.Instance?.RegisterSocket(this);
            }
        }

        public void SetLeverHeld(bool held)
        {
            IsCharging = held && Fuse != null && !IsCharged;
        }

        void Update()
        {
            if (!IsCharging || Fuse == null || IsCharged) return;
            ChargeProgress = Mathf.Clamp01(ChargeProgress + Time.deltaTime / chargeSeconds);
            OnChargeProgress?.Invoke(ChargeProgress);

            if (Time.time >= nextNoisePulse)
            {
                nextNoisePulse = Time.time + .35f;
                Level2NoiseBus.Emit(transform.position, chargeNoiseRadius, Level2NoiseKind.Charging, gameObject);
            }

            if (ChargeProgress < 1f) return;
            IsCharging = false;
            Fuse.MarkCharged();
            OnCharged?.Invoke();
            Level2FuseObjective.Instance?.NotifyCharged(this);
        }
    }
}
