using System;
using System.Collections.Generic;
using UnityEngine;

namespace RunawayChimps.FacilityAnnouncements
{
    [CreateAssetMenu(menuName = "Runaway Chimps/Atmosphere/Facility Announcement Collection")]
    public sealed class FacilityAnnouncementCollection : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Permanent lowercase identifier. Do not reuse an ID for a different line.")]
            public string id;
            public bool enabled = true;
            [Range(1, 10)] public int weight = 1;
            [Tooltip("False for pending speech. Cues, silence and transcripts are not spoken content.")]
            public bool speechAvailable;
            public AudioClip speech;
            [TextArea(2, 5)] public string transcript;
            [TextArea(2, 5)] public string provenance;
        }

        public string collectionId = "facility-prototype";
        [Min(1), Tooltip("Increment when changing clips/transcripts; all clients need matching content.")]
        public int contentRevision = 1;
        public Entry[] entries = Array.Empty<Entry>();
        public AudioClip startCue;
        public AudioClip endCue;
        [Min(60)] public float minimumQuietSeconds = 180;
        [Min(60)] public float maximumQuietSeconds = 300;
        [Tooltip("Default false: a collection with only one playable line becomes silent after one play.")]
        public bool allowSingleLineRepeat;
        [Header("Development only (ignored in release players)")]
        public bool diagnosticScheduling;
        [Min(5)] public float diagnosticMinimumSeconds = 8;
        [Min(5)] public float diagnosticMaximumSeconds = 12;

        public bool Diagnostic
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return diagnosticScheduling;
#else
                return false;
#endif
            }
        }

        public bool Valid => FacilityAnnouncementRules.ValidId(collectionId) && contentRevision > 0 &&
            entries != null && entries.Length <= FacilityAnnouncementRules.MaxEntries;

        public static bool HasSpeech(Entry entry) => entry != null && entry.enabled && entry.speechAvailable &&
            FacilityAnnouncementRules.ValidId(entry.id) && !string.IsNullOrWhiteSpace(entry.transcript) &&
            entry.transcript.Length <= 240 && entry.speech != null && entry.speech.length > 0.05f &&
            entry.speech.length <= FacilityAnnouncementRules.MaxSpeechSeconds &&
            entry.speech.loadState == AudioDataLoadState.Loaded;

        public Entry FindPlayable(string id)
        {
            if (!Valid) return null;
            Entry found = null;
            foreach (Entry entry in entries)
            {
                if (entry == null || entry.id != id) continue;
                if (found != null || !HasSpeech(entry)) return null;
                found = entry;
            }
            return found;
        }

        public bool HasPlayableContent()
        {
            if (!Valid) return false;
            foreach (Entry entry in entries)
                if (entry != null && FindPlayable(entry.id) != null) return true;
            return false;
        }

        public Entry Choose(string previous, double roll)
        {
            if (!Valid) return null;
            var choices = new List<FacilityAnnouncementRules.Choice>(entries.Length);
            foreach (Entry e in entries)
                choices.Add(e == null ? default : new FacilityAnnouncementRules.Choice(e.id, e.weight, HasSpeech(e)));
            int index = FacilityAnnouncementRules.Select(choices, previous, roll, allowSingleLineRepeat);
            return index < 0 ? null : entries[index];
        }

        public static double CueLength(AudioClip cue) => cue != null && cue.length > 0 && cue.length <= 1 &&
            cue.loadState == AudioDataLoadState.Loaded ? cue.length : 0;

        public double Duration(Entry entry) => entry.speech.length + CueLength(startCue) + CueLength(endCue) + 0.3;
        public double Quiet(double roll) => FacilityAnnouncementRules.QuietSeconds(
            Diagnostic ? diagnosticMinimumSeconds : minimumQuietSeconds,
            Diagnostic ? diagnosticMaximumSeconds : maximumQuietSeconds, roll, Diagnostic);
    }
}
