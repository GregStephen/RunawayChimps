using RunawayChimps.Travel;
using UnityEngine;
using UnityEngine.Audio;

namespace RunawayChimps.FacilityAnnouncements
{
    [DisallowMultipleComponent]
    public sealed class FacilitySpeaker : MonoBehaviour
    {
        public SectorId sector = SectorId.Hub;
        public FacilityAnnouncementCollection collection;
        public AudioSource source;
        public FacilityAnnouncementCaptions captionPrefab;
        [Header("Local environmental audio (never Photon Voice)")]
        [Range(0, 0.7f)] public float volume = 0.38f;
        [Min(0.1f)] public float minimumDistance = 1.5f;
        [Min(1)] public float maximumDistance = 14;
        public AudioMixerGroup outputGroup;

        public bool Ready => isActiveAndEnabled && source != null && source.isActiveAndEnabled &&
            collection != null && collection.Valid && gameObject.scene.isLoaded;

        private void Awake() => ConfigureSource();
        private void OnEnable()
        {
            ConfigureSource();
            FacilityAnnouncementDirector.Register(this);
        }
        private void OnDisable()
        {
            StopPlayback();
            FacilityAnnouncementDirector.Unregister(this);
        }

        public void ConfigureSource()
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.loop = false;
            source.pitch = 1;
            source.spatialBlend = 1;
            source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(0.1f, minimumDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 0.1f, maximumDistance);
            source.volume = Mathf.Clamp(volume, 0, 0.7f);
            source.priority = 180;
            source.outputAudioMixerGroup = outputGroup;
            source.ignoreListenerPause = false;
            source.ignoreListenerVolume = false;
        }

        public bool PlayClip(AudioClip clip, bool cue)
        {
            if (!Ready || clip == null || clip.loadState != AudioDataLoadState.Loaded) return false;
            ConfigureSource();
            source.Stop();
            source.clip = clip;
            source.volume *= cue ? 0.55f : 1;
            source.Play();
            return true;
        }

        public void StopPlayback()
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
        }
    }
}
