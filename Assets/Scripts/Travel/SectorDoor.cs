using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RunawayChimps.Travel
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SectorDoor : MonoBehaviour
    {
        private const string HubEntranceButtonRootName = "StartLevel1Button";

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
                EnsureHubEntranceButtonAvailable();

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

        private void EnsureHubEntranceButtonAvailable()
        {
            GameObject buttonRoot = null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name != HubEntranceButtonRootName) continue;
                buttonRoot = root;
                break;
            }

            if (buttonRoot == null)
            {
                Debug.LogError("Hub is missing the StartLevel1Button root required for button-only Level 1 entry.", this);
                return;
            }

            // The original authored button was left inactive in Hub_Base. Reactivate
            // it before play so the existing visible BigRedButton and trigger can be
            // used in both headset and non-headset Play Mode testing.
            if (!buttonRoot.activeSelf) buttonRoot.SetActive(true);

            var button = buttonRoot.GetComponentInChildren<PhysicalButton>(true);
            if (button == null)
            {
                Debug.LogError("StartLevel1Button is missing its PhysicalButton component.", buttonRoot);
                return;
            }

            if (!button.enabled) button.enabled = true;

            bool boundToThisDoor = false;
            if (button.OnPressed != null)
            {
                for (int i = 0; i < button.OnPressed.GetPersistentEventCount(); i++)
                {
                    if (button.OnPressed.GetPersistentTarget(i) == this &&
                        button.OnPressed.GetPersistentMethodName(i) == nameof(Travel))
                    {
                        boundToThisDoor = true;
                        break;
                    }
                }
            }

            if (!boundToThisDoor)
                Debug.LogError("StartLevel1Button must call this Hub SectorDoor.Travel method.", button);
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
