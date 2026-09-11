using UnityEngine;
using UnityEngine.Events;

namespace RunawayChimps.Level2
{
    public sealed class Level2PowerState : MonoBehaviour
    {
        public static Level2PowerState Instance { get; private set; }
        public UnityEvent OnPowered;
        public bool IsPowered { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void RestorePower()
        {
            if (IsPowered) return;
            IsPowered = true;
            OnPowered?.Invoke();
        }
    }
}
