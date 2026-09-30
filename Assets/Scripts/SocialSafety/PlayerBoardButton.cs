using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RunawayChimps.SocialSafety
{
    public enum PlayerBoardAction { Mute, Report, Previous, Next, Reason, Submit, Cancel, Close }

    [RequireComponent(typeof(BoxCollider))]
    public sealed class PlayerBoardButton : MonoBehaviour
    {
        public PlayerBoard board;
        public PlayerBoardAction action;
        public int index;
        public TMP_Text label;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private bool available = true;
        private float nextPress;
        private float enabledAt;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            if (label != null) label.richText = false;
        }
        private void OnEnable() { enabledAt = Time.unscaledTime + 0.35f; }
        public void SetAvailable(bool value)
        {
            available = value;
            if (label != null) label.color = value ? new Color(.83f, .95f, .87f) : new Color(.36f, .42f, .4f);
        }
        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<LocalRigMarker>() == null) return;
            int layer = other.gameObject.layer;
            if (!other.CompareTag("HandTag") && layer != LayerMask.NameToLayer("FingerTip") &&
                layer != LayerMask.NameToLayer("Left Hand") && layer != LayerMask.NameToLayer("Right Hand")) return;
            bool first = contacts.Count == 0;
            if (!contacts.Add(other) || !first || !available || Time.unscaledTime < enabledAt || Time.unscaledTime < nextPress) return;
            Activate();
        }
        private void OnTriggerExit(Collider other) => contacts.Remove(other);
        private void Update() => contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        // Also useful for inspector-driven non-headset testing, but no runtime mouse auto-click path.
        [ContextMenu("Press (Play Mode)")]
        public void Activate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !available || board == null || Time.unscaledTime < nextPress) return;
            nextPress = Time.unscaledTime + .5f;
            board.Press(action, index);
        }
        private void OnDisable() => contacts.Clear();
    }
}
