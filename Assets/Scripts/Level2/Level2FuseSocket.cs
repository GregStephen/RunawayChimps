using UnityEngine;
using UnityEngine.Events;

namespace RunawayChimps.Level2
{
    public enum Level2SocketState { Empty, Inserted, Charging, Charged }

    [RequireComponent(typeof(Collider))]
    public sealed class Level2FuseSocket : MonoBehaviour
    {
        public int socketId = 1;
        public Transform snapPoint;
        [Min(.1f)] public float chargeSeconds = 10f;
        public float chargeNoiseRadius = 28f;
        public Renderer indicator;
        public Color emptyColor = new Color(.08f,.08f,.08f);
        public Color insertedColor = new Color(.95f,.45f,.08f);
        public Color chargedColor = new Color(.15f,.85f,.25f);
        public UnityEvent<float> OnChargeProgress;
        public UnityEvent OnCharged;

        public Level2Fuse Fuse { get; private set; }
        public float ChargeProgress { get; private set; }
        public bool IsCharged => State == Level2SocketState.Charged;
        public Level2SocketState State { get; private set; } = Level2SocketState.Empty;
        float nextNoisePulse;
        MaterialPropertyBlock block;

        void Awake()
        {
            var c = GetComponent<Collider>();
            c.isTrigger = true;
            if (snapPoint == null) snapPoint = transform;
            block = new MaterialPropertyBlock();
            RefreshIndicator();
        }

        void OnTriggerEnter(Collider other)
        {
            if (State != Level2SocketState.Empty || Fuse != null) return;
            var fuse = other.GetComponentInParent<Level2Fuse>();
            if (fuse != null && fuse.TryInsert(this, snapPoint))
            {
                Fuse = fuse;
                State = Level2SocketState.Inserted;
                Level2FuseObjective.Instance?.RegisterSocket(this);
                RefreshIndicator();
            }
        }

        public void SetLeverHeld(bool held)
        {
            if (IsCharged || Fuse == null) { if (!held && State == Level2SocketState.Charging) State = Level2SocketState.Inserted; return; }
            State = held ? Level2SocketState.Charging : Level2SocketState.Inserted;
            RefreshIndicator();
        }

        void Update()
        {
            if (State != Level2SocketState.Charging || Fuse == null || IsCharged) return;
            ChargeProgress = Mathf.Clamp01(ChargeProgress + Time.deltaTime / chargeSeconds);
            OnChargeProgress?.Invoke(ChargeProgress);
            RefreshIndicator();

            if (Time.time >= nextNoisePulse)
            {
                nextNoisePulse = Time.time + .35f;
                Level2NoiseBus.Emit(transform.position, chargeNoiseRadius, Level2NoiseKind.Charging, gameObject);
            }

            if (ChargeProgress < 1f) return;
            State = Level2SocketState.Charged;
            Fuse.MarkCharged();
            RefreshIndicator();
            OnCharged?.Invoke();
            Level2FuseObjective.Instance?.NotifyCharged(this);
        }

        void RefreshIndicator()
        {
            if (indicator == null) return;
            Color c = State == Level2SocketState.Charged ? chargedColor :
                State == Level2SocketState.Empty ? emptyColor : insertedColor;
            if (State == Level2SocketState.Charging) c *= Mathf.Lerp(.45f, 1f, .5f + .5f * Mathf.Sin(Time.time * 10f));
            indicator.GetPropertyBlock(block);
            block.SetColor("_Color", c);
            block.SetColor("_BaseColor", c);
            block.SetColor("_EmissionColor", c * Mathf.Lerp(.3f, 1.2f, ChargeProgress));
            indicator.SetPropertyBlock(block);
        }
    }
}
