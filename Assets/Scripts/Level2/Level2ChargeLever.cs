using System.Collections.Generic;
using UnityEngine;

namespace RunawayChimps.Level2
{
    [RequireComponent(typeof(Collider))]
    public sealed class Level2ChargeLever : MonoBehaviour
    {
        public Level2FuseSocket socket;
        readonly HashSet<Collider> localHands = new HashSet<Collider>();

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<LocalRigMarker>() == null || !localHands.Add(other)) return;
            socket?.SetLeverHeld(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (!localHands.Remove(other)) return;
            if (localHands.Count == 0) socket?.SetLeverHeld(false);
        }

        void Update()
        {
            localHands.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            if (localHands.Count == 0) socket?.SetLeverHeld(false);
        }

        void OnDisable()
        {
            localHands.Clear();
            socket?.SetLeverHeld(false);
        }
    }
}
