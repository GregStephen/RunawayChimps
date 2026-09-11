using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RunawayChimps.Level2
{
    public sealed class Level2FuseObjective : MonoBehaviour
    {
        public static Level2FuseObjective Instance { get; private set; }
        public UnityEvent OnPowerRestored;
        public int ChargedCount => charged.Count;
        public bool PowerRestored { get; private set; }

        readonly HashSet<Level2FuseSocket> sockets = new HashSet<Level2FuseSocket>();
        readonly HashSet<Level2FuseSocket> charged = new HashSet<Level2FuseSocket>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RegisterSocket(Level2FuseSocket socket)
        {
            if (socket != null) sockets.Add(socket);
        }

        public void NotifyCharged(Level2FuseSocket socket)
        {
            if (socket == null || !socket.IsCharged || !sockets.Contains(socket) || !charged.Add(socket)) return;
            if (charged.Count < 4 || PowerRestored) return;
            PowerRestored = true;
            OnPowerRestored?.Invoke();
        }
    }
}
