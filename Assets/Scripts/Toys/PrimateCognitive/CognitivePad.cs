using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace RunawayChimps.Toys.PrimateCognitive
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CognitivePad : MonoBehaviour
    {
        public CognitiveMachine machine;
        [Range(0, 4)] public int index;
        public Transform cap;
        public Renderer capRenderer;
        public Color restingColor = Color.gray;
        public Color illuminatedColor = Color.white;
        [Range(.03f, .3f)] public float releaseSeconds = .08f;
        [Range(.005f, .025f)] public float pressDepth = .012f;
        private CognitiveContactGate gate;
        private readonly Dictionary<int, Collider> colliders = new Dictionary<int, Collider>();
        private readonly List<int> expired = new List<int>();
        private MaterialPropertyBlock properties;
        private Vector3 rest;
        private bool lit, wasLit;
        private const int EditorCollider = int.MinValue;
        private double Clock => Time.unscaledTimeAsDouble;

        private void Awake()
        {
            gate = new CognitiveContactGate(releaseSeconds);
            properties = new MaterialPropertyBlock();
            if (cap == null || capRenderer == null || machine == null)
            {
                Debug.LogError("Cognitive pad requires its authored cap, renderer and machine.", this);
                enabled = false;
                return;
            }
            rest = cap.localPosition;
            GetComponent<BoxCollider>().isTrigger = true;
            gate.Reset(Clock);
            Paint(false);
        }

        private void OnEnable() { if (gate != null) gate.Reset(Clock); }
        public void RequireRelease() { if (gate != null) gate.RequireRelease(Clock); }

        private bool LocalHand(Collider other)
        {
            if (other == null || !other.enabled || !other.gameObject.activeInHierarchy || !other.CompareTag("HandTag")) return false;
            var marker = other.GetComponentInParent<LocalRigMarker>();
            var rig = GorillaLocomotion.Player.Instance;
            if (marker == null || rig == null || rig.GetComponentInParent<LocalRigMarker>() != marker) return false;
            // HandTag also marks the controller-root 8 cm grab spheres. Use only
            // child contacts of the actual tracked hands, not those root volumes.
            // This follows the authored rig even when layer names and indices differ.
            if (!IsHandContact(other.transform, rig.leftHandTransform) &&
                !IsHandContact(other.transform, rig.rightHandTransform)) return false;
            var view = other.GetComponentInParent<PhotonView>();
            return view == null || view.IsMine;
        }

        private static bool IsHandContact(Transform contact, Transform hand)
        {
            return hand != null && contact != hand && contact.IsChildOf(hand);
        }

        private void Track(Collider other, bool fresh)
        {
            if (gate == null || !LocalHand(other)) return;
            int id = other.GetInstanceID();
            colliders[id] = other;
            if (gate.Enter(id, Clock, fresh)) machine.ContactPressed(index);
        }
        private void OnTriggerEnter(Collider other) => Track(other, true);
        // Covers enable/travel overlaps without manufacturing a new press from a resting hand.
        private void OnTriggerStay(Collider other) => Track(other, false);
        private void OnTriggerExit(Collider other)
        {
            int id = other.GetInstanceID();
            colliders.Remove(id);
            if (gate != null) gate.Exit(id, Clock);
        }

        private void Update()
        {
            if (gate == null || cap == null) return;
            expired.Clear();
            foreach (var pair in colliders) if (!LocalHand(pair.Value)) expired.Add(pair.Key);
            foreach (int id in expired) { colliders.Remove(id); gate.Exit(id, Clock); }
            gate.Tick(Clock);
            cap.localPosition = Vector3.Lerp(cap.localPosition, rest + (gate.Count > 0 ? Vector3.forward * pressDepth : Vector3.zero),
                1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
        }

        public void Present(bool illuminated)
        {
            lit = illuminated;
            if (properties != null && capRenderer != null && wasLit != lit) Paint(lit);
        }

        private void Paint(bool illuminated)
        {
            wasLit = illuminated;
            capRenderer.GetPropertyBlock(properties);
            properties.SetColor("_Color", illuminated ? illuminatedColor : restingColor);
            properties.SetColor("_EmissionColor", illuminated ? illuminatedColor * 2.5f : restingColor * .1f);
            capRenderer.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            colliders.Clear();
            if (gate != null) gate.Reset(Clock);
            if (cap != null) cap.localPosition = rest;
        }

#if UNITY_EDITOR
        public void EditorContact(bool held)
        {
            if (gate == null || !isActiveAndEnabled) return;
            if (held)
            {
                if (gate.Enter(EditorCollider, Clock)) machine.ContactPressed(index);
            }
            else gate.Exit(EditorCollider, Clock);
        }
#endif
    }
}
