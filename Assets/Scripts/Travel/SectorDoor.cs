using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RunawayChimps.Travel
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SectorDoor : MonoBehaviour
    {
        public string destinationScene;
        [Tooltip("Optional label on the accessible side of a directly selectable door.")]
        public string label;
        private XRSimpleInteractable interactable;

        // The Hub -> Level 1 route is intentionally button-only. This keeps the
        // headset interaction identical to non-headset editor testing: touch the
        // physical StartLevel1Button with the local monkey hand/fingertip.
        public bool AllowsDirectXRSelection =>
            !(gameObject.scene.name == SectorDestinations.Hub &&
              destinationScene == SectorDestinations.LevelOne);

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;

            if (!AllowsDirectXRSelection)
            {
                // Defensive: if an interactable was ever serialized on this door,
                // do not leave a second Grip/Select path active beside the button.
                interactable = GetComponent<XRSimpleInteractable>();
                if (interactable != null) interactable.enabled = false;
                return;
            }

            interactable = GetComponent<XRSimpleInteractable>();
            if (interactable == null) interactable = gameObject.AddComponent<XRSimpleInteractable>();
            if (SectorTravelService.I != null)
                interactable.interactionManager = SectorTravelService.I.InteractionManager;
            interactable.selectEntered.AddListener(Selected);

            if (string.IsNullOrEmpty(label)) return;
            var sign = new GameObject("Travel sign");
            sign.transform.SetParent(transform, false);
            sign.transform.localPosition = new Vector3(0, 0.85f, 0.05f);
            sign.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var text = sign.AddComponent<TextMeshPro>();
            text.text = label;
            text.fontSize = 2.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(1.8f, 0.35f);
        }

        private void Selected(SelectEnterEventArgs args)
        {
            if (args.interactorObject.transform.GetComponentInParent<LocalRigMarker>() != null) Travel();
        }

        private void OnDestroy()
        {
            if (interactable != null) interactable.selectEntered.RemoveListener(Selected);
        }

        // Used by the Hub's local-hand PhysicalButton UnityEvent and by direct
        // XR selection on routes that still intentionally allow it.
        public void Travel()
        {
            if (SectorTravelService.I != null)
                SectorTravelService.I.TravelTo(destinationScene, ArrivalRoute.Door);
            else
                Debug.LogWarning("Start from Bootstrap before using a sector door.", this);
        }
    }
}
