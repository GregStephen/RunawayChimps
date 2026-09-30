using Photon.Pun;
using Photon.VR;
using UnityEngine;
using UnityEngine.XR;

namespace RunawayChimps.SocialSafety
{
    // Hold left Y for 0.6 s. The panel stays in world space, so it does not follow
    // the head while a hand presses a button. No collider blocks locomotion.
    public sealed class PlayerBoardShortcut : MonoBehaviour
    {
        private PlayerBoard panel;
        private float heldSince = -1;
        private bool consumed;
        private void Update()
        {
            bool down = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed) && pressed;
            if (!down) { heldSince = -1; consumed = false; }
            else if (heldSince < 0) heldSince = Time.unscaledTime;
            bool toggle = down && !consumed && Time.unscaledTime - heldSince >= .6f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            toggle |= Input.GetKeyDown(KeyCode.F4);
#endif
            if (toggle) { consumed = true; Toggle(); }
            if (panel != null && panel.gameObject.activeSelf && (!PhotonNetwork.InRoom ||
                (RunawayChimps.Travel.SectorTravelService.I != null && RunawayChimps.Travel.SectorTravelService.I.IsBusy)))
                panel.gameObject.SetActive(false);
        }
        private void Toggle()
        {
            if (panel != null && panel.gameObject.activeSelf) { panel.gameObject.SetActive(false); return; }
            var manager = PhotonVRManager.Manager;
            var head = manager != null ? manager.Head : null;
            if (head == null || !PhotonNetwork.InRoom) return;
            if (panel == null)
            {
                var prefab = Resources.Load<PlayerBoard>("SocialSafety/PlayerBoard");
                if (prefab == null) { Debug.LogError("Player board prefab missing."); return; }
                panel = Instantiate(prefab);
                panel.portable = true;
                DontDestroyOnLoad(panel.gameObject);
            }
            var direction = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (direction.sqrMagnitude < .1f) direction = Vector3.forward;
            panel.transform.SetPositionAndRotation(head.position + direction * .75f - Vector3.up * .2f,
                Quaternion.LookRotation(-direction, Vector3.up));
            panel.transform.localScale = Vector3.one * .7f;
            panel.gameObject.SetActive(true);
        }
        private void OnDestroy() { if (panel != null) Destroy(panel.gameObject); }
    }
}
