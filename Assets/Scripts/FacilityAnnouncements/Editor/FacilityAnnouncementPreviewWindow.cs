using System;
using System.Reflection;
using RunawayChimps.Travel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunawayChimps.FacilityAnnouncements.Editor
{
    public sealed class FacilityAnnouncementPreviewWindow : EditorWindow
    {
        public const string Root = "Assets/RunawayChimps/FacilityAnnouncements/";
        public const string SpeakerPath = Root + "Prefabs/FacilitySpeaker.prefab";
        public const string CollectionPath = Root + "Data/FacilityAnnouncements.asset";
        private FacilityAnnouncementCollection collection;
        private int selected;
        private string status;
        private bool ownsPreview;
        private double previewUntil;

        [MenuItem("Tools/Runaway Chimps/Atmosphere/Facility Announcement Preview")]
        public static void OpenPreview() => GetWindow<FacilityAnnouncementPreviewWindow>("Facility Announcements");

        [MenuItem("Tools/Runaway Chimps/Atmosphere/Place Facility Speaker in Hub")]
        public static void PlaceSpeaker()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpeakerPath);
            if (prefab == null) { Debug.LogError("Missing facility speaker prefab: " + SpeakerPath); return; }
            Scene hub = SceneManager.GetSceneByName("Hub_Base");
            if (!hub.isLoaded) hub = EditorSceneManager.OpenScene("Assets/Scenes/Hub_Base.unity", OpenSceneMode.Additive);
            var context = SectorScene.Find(hub);
            if (context == null || context.arrivalSpawn == null)
            { Debug.LogError("Hub needs its existing SectorScene and arrivalSpawn. No placement was made."); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hub);
            Transform anchor = context.arrivalSpawn;
            instance.transform.position = anchor.TransformPoint(new Vector3(1.5f, 2.1f, 2.5f));
            Vector3 forward = anchor.position - instance.transform.position;
            forward.y = 0;
            instance.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            instance.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(instance, "Place facility speaker");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(hub);
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            // No auto-save, automatic editor-load work, removal, or existing-placement rewrite.
        }

        [MenuItem("Tools/Runaway Chimps/Atmosphere/Validate Facility Announcement Assets")]
        public static void ValidateAssets()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(SpeakerPath);
            var speaker = root != null ? root.GetComponent<FacilitySpeaker>() : null;
            if (speaker == null || speaker.source == null || speaker.collection == null || speaker.captionPrefab == null)
                throw new InvalidOperationException("Facility prefab has missing assigned references.");
            if (root.GetComponentsInChildren<AudioSource>(true).Length != 1 ||
                root.GetComponentsInChildren<Camera>(true).Length != 0 ||
                root.GetComponentsInChildren<AudioListener>(true).Length != 0)
                throw new InvalidOperationException("Facility prefab must have exactly one source and no camera/listener.");
            var content = speaker.collection;
            if (!content.Valid) throw new InvalidOperationException("Invalid facility collection.");
            var ids = new System.Collections.Generic.HashSet<string>();
            int spoken = 0;
            foreach (var entry in content.entries)
            {
                if (entry == null || !FacilityAnnouncementRules.ValidId(entry.id) || !ids.Add(entry.id))
                    throw new InvalidOperationException("Facility IDs must be nonempty and unique.");
                if (entry.speechAvailable && (entry.speech == null || string.IsNullOrWhiteSpace(entry.transcript) ||
                    string.IsNullOrWhiteSpace(entry.provenance) || entry.speech.length <= 0.05f || entry.speech.length > 30))
                    throw new InvalidOperationException("Available speech needs a real clip, transcript and provenance.");
                if (entry.speechAvailable) ++spoken;
            }
            var captions = speaker.captionPrefab;
            if (captions.canvas == null || captions.panel == null || captions.label == null || captions.backdrop == null)
                throw new InvalidOperationException("Caption prefab references are incomplete.");
            Debug.Log("Facility asset references validated: " + spoken + " declared spoken clips. Listening, Play Mode, Photon and headset acceptance are separate.");
        }

        private void OnEnable()
        {
            collection = AssetDatabase.LoadAssetAtPath<FacilityAnnouncementCollection>(CollectionPath);
            EditorApplication.update += TickPreview;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }
        private void OnDisable()
        {
            StopPreview();
            EditorApplication.update -= TickPreview;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
        }
        private void OnLostFocus() => StopPreview();
        private void PlayModeChanged(PlayModeStateChange _) => StopPreview();
        private void TickPreview()
        {
            if (ownsPreview && EditorApplication.timeSinceStartup >= previewUntil) StopPreview();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Editor audition is non-spatial. Use Bootstrap and diagnostic scheduling to test the real sector/audio/caption path. Speech quality is prototype synthetic, not a validated final performance.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var next = (FacilityAnnouncementCollection)EditorGUILayout.ObjectField("Collection", collection, typeof(FacilityAnnouncementCollection), false);
            if (EditorGUI.EndChangeCheck()) { StopPreview(); collection = next; selected = 0; }
            if (collection == null || collection.entries == null || collection.entries.Length == 0)
            { EditorGUILayout.HelpBox("Empty collection: normal playback stays silent.", MessageType.Warning); return; }
            string[] names = new string[collection.entries.Length];
            for (int i = 0; i < names.Length; ++i) names[i] = collection.entries[i]?.id ?? "(empty entry)";
            int newIndex = EditorGUILayout.Popup("Selected announcement", Mathf.Clamp(selected, 0, names.Length - 1), names);
            if (newIndex != selected) { StopPreview(); selected = newIndex; }
            var entry = collection.entries[selected];
            EditorGUILayout.LabelField("Transcript (editable in the collection asset)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(entry?.transcript ?? "No transcript", MessageType.None);
            bool available = entry != null && entry.speechAvailable && entry.speech != null;
            EditorGUILayout.LabelField("Speech content", available ? "Recorded synthetic prototype clip present" : "PENDING - no voiced announcement");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !available))
                if (GUILayout.Button("Audition selected recorded speech (Editor only)")) Audition(entry.speech, false);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("TEXT / START-CUE PREVIEW - not a voiced announcement")) Audition(collection.startCue, true);
            if (GUILayout.Button("Stop preview")) StopPreview();
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            bool diagnostic = EditorGUILayout.Toggle("Diagnostic scheduling", collection.diagnosticScheduling);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(collection, "Toggle facility diagnostic scheduling");
                collection.diagnosticScheduling = diagnostic;
                EditorUtility.SetDirty(collection);
            }
            EditorGUILayout.HelpBox("Diagnostic intervals are 8-12 seconds by default, after a conservative 36-second authority settling guard. Only Editor/development builds honor this toggle. Normal intervals default to 180-300 seconds of quiet.", MessageType.None);
            FacilityAnnouncementCaptions.CaptionsEnabled = EditorGUILayout.Toggle("Local captions", FacilityAnnouncementCaptions.CaptionsEnabled);
            FacilityAnnouncementCaptions.TextScale = EditorGUILayout.Slider("Local text scale", FacilityAnnouncementCaptions.TextScale, 0.8f, 1.5f);
            FacilityAnnouncementCaptions.VerticalPosition = EditorGUILayout.Slider("Local caption height", FacilityAnnouncementCaptions.VerticalPosition, 0.18f, 0.42f);
            EditorGUILayout.LabelField("Local options are PlayerPrefs, never room properties.", EditorStyles.wordWrappedMiniLabel);
        }

        private static MethodInfo AudioMethod(string name, Type[] types)
        {
            Type utility = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            return utility?.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, types, null);
        }

        private void Audition(AudioClip clip, bool textOnly)
        {
            StopPreview();
            status = textOnly ? "DEVELOPMENT TEXT/CUE PREVIEW. The displayed transcript is NOT being spoken." : "Auditioning the selected recorded clip; this does not validate spatial or headset playback.";
            if (clip == null) return;
            MethodInfo play = AudioMethod("PlayPreviewClip", new[] { typeof(AudioClip), typeof(int), typeof(bool) });
            if (play == null) { status = "Editor audio-preview API unavailable. Select the clip in the Inspector to audition it."; return; }
            try
            {
                play.Invoke(null, new object[] { clip, 0, false });
                ownsPreview = true;
                previewUntil = EditorApplication.timeSinceStartup + clip.length + 0.2;
            }
            catch (Exception exception) { status = "Editor preview could not start: " + exception.GetBaseException().Message; }
        }
        private void StopPreview()
        {
            if (ownsPreview)
            {
                try { AudioMethod("StopAllPreviewClips", Type.EmptyTypes)?.Invoke(null, null); }
                catch (Exception) { /* Editor internal preview API only; no runtime impact. */ }
            }
            ownsPreview = false;
            status = null;
            Repaint();
        }
    }
}
