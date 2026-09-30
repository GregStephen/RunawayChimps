using System;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

namespace RunawayChimps.FacilityAnnouncements.Editor
{
    // Explicit, isolated Editor checks. No automatic editor-load hooks, scene save,
    // camera, AudioListener, Photon connection or live user preference changes.
    public static class FacilityAnnouncementEditorChecks
    {
        [MenuItem("Tools/Runaway Chimps/Atmosphere/Run Facility Announcement Regression Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PhotonNetwork.InRoom)
                throw new InvalidOperationException("Run facility Editor checks outside Play Mode and disconnected from Photon.");
            int count = 0;
            Action<bool, string> check = (condition, message) =>
            {
                ++count;
                if (!condition) throw new InvalidOperationException("Facility regression: " + message);
            };
            FacilityAnnouncementCollection collection = null;
            AudioClip clip = null;
            GameObject captionRoot = null;
            GameObject speakerRoot = null;
            try
            {
                collection = ScriptableObject.CreateInstance<FacilityAnnouncementCollection>();
                clip = AudioClip.Create("Temporary test content (not delivered speech)", 22050, 1, 22050, false);
                var first = new FacilityAnnouncementCollection.Entry
                { id = "one", enabled = true, weight = 1, speechAvailable = true, speech = clip, transcript = "One." };
                var second = new FacilityAnnouncementCollection.Entry
                { id = "two", enabled = true, weight = 1, speechAvailable = true, speech = clip, transcript = "Two." };
                check(collection.Choose(null, 0.5) == null, "empty collection is silent");
                collection.entries = new[] { first, second };
                check(collection.FindPlayable("one") == first, "stable ID resolves matching clip/transcript");
                check(collection.Choose("one", 0.5) == second, "exclude last selected ID");
                second.speechAvailable = false;
                check(collection.Choose("one", 0.5) == null, "pending speech is not selected");
                check(collection.Choose(null, 0.5) == first, "single valid entry can play once");
                first.speech = null;
                check(collection.Choose(null, 0.5) == null, "missing clip cannot show caption-only content");
                first.speech = clip;
                second.id = first.id;
                check(collection.FindPlayable("one") == null, "duplicate ID rejected even when one entry is pending");
                check(collection.Choose(null, 0.5) == null, "both ambiguous entries excluded");
                collection.entries = new[] { first };
                first.enabled = false;
                check(!collection.HasPlayableContent(), "disabled entry excluded");
                first.enabled = true;
                first.transcript = string.Empty;
                check(!collection.HasPlayableContent(), "missing transcript excluded");
                first.transcript = "One.";
                collection.allowSingleLineRepeat = true;
                check(collection.Choose("one", 0.5) == first, "explicit single-line repeat opt-in");

                captionRoot = PrefabUtility.LoadPrefabContents(FacilityAnnouncementPreviewWindow.Root + "Prefabs/FacilityAnnouncementCaptions.prefab");
                var captions = captionRoot.GetComponent<FacilityAnnouncementCaptions>();
                check(captions != null && captions.label != null && captions.panel != null && captions.canvas != null,
                    "serialized caption component references load");
                captions.label.text = "Simulated stale caption";
                captions.panel.gameObject.SetActive(true);
                captions.Clear();
                check(captions.label.text == string.Empty && !captions.panel.gameObject.activeSelf, "clear resets text and visibility");
                check(!captions.Bind(null), "missing existing XR camera fails closed");
                check(captionRoot.GetComponentsInChildren<Camera>(true).Length == 0, "caption prefab never supplies another camera");

                speakerRoot = PrefabUtility.LoadPrefabContents(FacilityAnnouncementPreviewWindow.SpeakerPath);
                var speaker = speakerRoot.GetComponent<FacilitySpeaker>();
                check(speaker != null && speaker.source != null && speaker.collection != null && speaker.captionPrefab != null,
                    "serialized speaker references import");
                speaker.ConfigureSource();
                check(speaker.source.spatialBlend == 1 && speaker.source.dopplerLevel == 0 && !speaker.source.loop,
                    "spatial environmental source settings");
                speaker.source.clip = clip;
                speaker.StopPlayback();
                check(speaker.source.clip == null && !speaker.source.isPlaying, "stop clears assigned playback clip");
                Debug.Log("PASS: " + count + " facility Editor regression assertions. These checks do not audition speech or exercise Photon transport, scene travel or a headset.");
            }
            finally
            {
                if (captionRoot != null) PrefabUtility.UnloadPrefabContents(captionRoot);
                if (speakerRoot != null) PrefabUtility.UnloadPrefabContents(speakerRoot);
                if (collection != null) UnityEngine.Object.DestroyImmediate(collection);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            }
        }
    }
}
