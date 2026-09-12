using UnityEngine;

namespace RunawayChimps.Level2
{
    public sealed class Level2NoisySearchContainer : MonoBehaviour
    {
        public float noiseRadius = 10f;
        public float cooldown = .35f;
        public Transform movingPart;
        public Vector3 openLocalOffset = new Vector3(0, 0, -.18f);
        public float moveSeconds = .25f;
        public bool IsOpen { get; private set; }
        Vector3 closed;
        Vector3 target;
        float nextNoise;

        void Awake()
        {
            if (movingPart == null) movingPart = transform;
            closed = movingPart.localPosition;
            target = closed;
        }

        void Update()
        {
            if (movingPart == null || movingPart.localPosition == target) return;
            movingPart.localPosition = Vector3.MoveTowards(movingPart.localPosition, target,
                openLocalOffset.magnitude / Mathf.Max(.05f, moveSeconds) * Time.deltaTime);
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            target = closed + (IsOpen ? openLocalOffset : Vector3.zero);
            NotifyOpened();
        }

        public void NotifyOpened()
        {
            if (Time.time < nextNoise) return;
            nextNoise = Time.time + cooldown;
            Level2NoiseBus.Emit(transform.position, noiseRadius, Level2NoiseKind.Search, gameObject);
        }
    }
}
