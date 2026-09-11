using UnityEngine;

namespace RunawayChimps.Level2
{
    [RequireComponent(typeof(Collider))]
    public sealed class Level2SearchContainerHandle : MonoBehaviour
    {
        public Level2NoisySearchContainer container;
        float nextToggle;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (Time.time < nextToggle || other.GetComponentInParent<LocalRigMarker>() == null || container == null) return;
            nextToggle = Time.time + .5f;
            container.Toggle();
        }
    }
}
