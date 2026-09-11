using UnityEngine;

namespace RunawayChimps.Level2
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Level2ChargeLever : MonoBehaviour
    {
        public Level2FuseSocket socket;
        Collider holdingCollider;

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (holdingCollider != null || other.GetComponentInParent<LocalRigMarker>() == null) return;
            holdingCollider = other;
            socket?.SetLeverHeld(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (other != holdingCollider) return;
            Release();
        }

        void Update()
        {
            if (holdingCollider != null && (!holdingCollider.enabled || !holdingCollider.gameObject.activeInHierarchy)) Release();
        }

        void OnDisable() => Release();

        void Release()
        {
            holdingCollider = null;
            socket?.SetLeverHeld(false);
        }
    }
}
