using System.Collections.Generic;
using Photon.Pun;
using Photon.Voice.PUN;
using UnityEngine;

namespace RunawayChimps.Travel
{
    // Keep PhotonViews alive so late arrivals and property updates still work.
    // Only presentation/collision is gated; voice bandwidth filtering is separate.
    [DefaultExecutionOrder(10000)]
    public sealed class SectorAvatarVisibility : MonoBehaviour
    {
        private PhotonView view;
        private PhotonVoiceView voice;
        private readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Collider, bool> hiddenColliders = new Dictionary<Collider, bool>();
        private AudioSource mutedSpeaker;
        private bool previousMute;
        private Renderer[] renderers;
        private Collider[] colliders;
        private float nextRefresh;

        private void Awake()
        {
            view = GetComponent<PhotonView>();
            voice = GetComponent<PhotonVoiceView>();
            RefreshComponents();
        }

        private void OnTransformChildrenChanged() => nextRefresh = 0;
        private void RefreshComponents()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            nextRefresh = Time.unscaledTime + 0.5f;
        }

        private void LateUpdate()
        {
            if (view == null || view.IsMine) return;
            var local = SectorTravelService.I;
            bool visible = PhotonNetwork.InRoom && local != null && local.CurrentSector != SectorId.None &&
                           SectorPresence.Get(view.Owner) == local.CurrentSector;
            if (visible) Restore();
            else
            {
                if (Time.unscaledTime >= nextRefresh) RefreshComponents();
                // Includes newly enabled cosmetics and name meshes without disabling their parents.
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    if (!hiddenRenderers.ContainsKey(renderer)) hiddenRenderers.Add(renderer, renderer.enabled);
                    renderer.enabled = false;
                }
                foreach (var collider in colliders)
                {
                    if (collider == null) continue;
                    if (!hiddenColliders.ContainsKey(collider)) hiddenColliders.Add(collider, collider.enabled);
                    collider.enabled = false;
                }
                var speaker = voice != null && voice.SpeakerInUse != null
                    ? voice.SpeakerInUse.GetComponent<AudioSource>() : null;
                if (speaker != mutedSpeaker)
                {
                    RestoreSpeaker();
                    mutedSpeaker = speaker;
                    if (speaker != null) previousMute = speaker.mute;
                }
                if (mutedSpeaker != null) mutedSpeaker.mute = true;
            }
        }

        private void RestoreSpeaker()
        {
            if (mutedSpeaker != null) mutedSpeaker.mute = previousMute;
            mutedSpeaker = null;
        }

        private void Restore()
        {
            foreach (var pair in hiddenRenderers) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in hiddenColliders) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenRenderers.Clear();
            hiddenColliders.Clear();
            RestoreSpeaker();
        }

        private void OnDisable() => Restore();
    }
}
