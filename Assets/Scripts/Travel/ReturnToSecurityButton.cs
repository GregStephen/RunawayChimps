using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RunawayChimps.Travel
{
    // The scene owns the simple editable plate/cap geometry. Only the label is created at runtime.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ReturnToSecurityButton : MonoBehaviour
    {
        public LevelTerminalActions actions;
        public Transform buttonVisual;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private Vector3 restingPosition;
        private float nextPressTime;

        private void Awake()
        {
            if (actions == null || buttonVisual == null)
            {
                Debug.LogError("Return button needs its visual and safe-entry actions.", this);
                enabled = false;
                return;
            }
            restingPosition = buttonVisual.localPosition;
            GetComponent<BoxCollider>().isTrigger = true;
            var body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var labelObject = new GameObject("RETURN TO SECURITY label");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(-0.04f, 0.28f, 0);
            labelObject.transform.localRotation = Quaternion.Euler(0, -90, 0);
            var label = labelObject.AddComponent<TextMeshPro>();
            label.text = "RETURN TO\nSECURITY";
            label.fontSize = 1.1f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.76f, 0.75f, 0.67f);
            label.rectTransform.sizeDelta = new Vector2(0.55f, 0.20f);
            label.enableWordWrapping = false;
            label.raycastTarget = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!enabled || other.GetComponentInParent<LocalRigMarker>() == null) return;
            int layer = other.gameObject.layer;
            if (!other.CompareTag("HandTag") && layer != LayerMask.NameToLayer("FingerTip") &&
                layer != LayerMask.NameToLayer("Left Hand") && layer != LayerMask.NameToLayer("Right Hand")) return;
            bool firstContact = contacts.Count == 0;
            if (!contacts.Add(other) || !firstContact || Time.unscaledTime < nextPressTime) return;
            var travel = SectorTravelService.I;
            if (travel == null || travel.IsBusy) return;
            nextPressTime = Time.unscaledTime + 1f;
            buttonVisual.localPosition = restingPosition + Vector3.left * 0.018f;
            actions.ReturnToHub();
        }

        private void OnTriggerExit(Collider other) => contacts.Remove(other);

        private void Update()
        {
            contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            if (contacts.Count == 0)
                buttonVisual.localPosition = Vector3.MoveTowards(buttonVisual.localPosition,
                    restingPosition, Time.unscaledDeltaTime * 0.15f);
        }

        private void OnDisable()
        {
            contacts.Clear();
            if (buttonVisual != null) buttonVisual.localPosition = restingPosition;
        }
    }
}
