"""One-time reviewed-source finalization; remove after the resulting commit."""
from pathlib import Path

S = Path('Assets/Scripts/FacilityAnnouncements')


def change(path, old, new):
    path = Path(path)
    text = path.read_text()
    assert text.count(old) == 1, (str(path), old)
    path.write_text(text.replace(old, new), encoding='utf-8')


change('Tools/FacilityAnnouncements/build_assets.py',
       "'runaway-chimps/facility-announcements/' + str(path)",
       "'runaway-chimps/facility-announcements/' + Path(path).as_posix()")

change(S / 'FacilitySpeaker.cs', '        private void Awake() => ConfigureSource();',
       '        private bool registered;\n\n'
       '        private void Awake()\n        {\n            if (Application.isPlaying) ConfigureSource();\n        }')
change(S / 'FacilitySpeaker.cs', '        private void OnEnable()\n        {\n            ConfigureSource();',
       '        private void OnEnable()\n        {\n            if (!Application.isPlaying) return;\n            registered = true;\n            ConfigureSource();')
change(S / 'FacilitySpeaker.cs', '        private void OnDisable()\n        {\n            StopPlayback();',
       '        private void OnDisable()\n        {\n            if (!registered) return;\n            registered = false;\n            StopPlayback();')
change(S / 'FacilityAnnouncementDirector.cs',
       '                travel.CurrentSector != SectorId.None && (AppState.I == null || AppState.I.IsReady);',
       '                (int)travel.CurrentSector >= 1 && (int)travel.CurrentSector <= 3 &&\n'
       '                (AppState.I == null || AppState.I.IsReady);')

helper = '''        public enum PlaybackCompletion { Waiting, Finished, Interrupted }

        // AudioSource.isPlaying can begin and end between two rendered frames for
        // very short cues. Speech must actually have been observed playing before
        // it can complete normally or be accompanied by a caption.
        public static PlaybackCompletion CompletedClip(bool speech, bool observedPlaying, double elapsed, double length)
        {
            if (!Finite(elapsed) || !Finite(length) || elapsed < 0 || length <= 0)
                return PlaybackCompletion.Interrupted;
            if (!observedPlaying && elapsed < 0.3) return PlaybackCompletion.Waiting;
            if ((speech && !observedPlaying) || elapsed < length - Math.Min(0.05, length * 0.1))
                return PlaybackCompletion.Interrupted;
            return PlaybackCompletion.Finished;
        }

'''
change(S / 'FacilityAnnouncementRules.cs', '        public sealed class Schedule\n', helper + '        public sealed class Schedule\n')
change(S / 'FacilityAnnouncementDirector.cs',
       '            if (!observedPlaying && elapsed < 0.3f) return;\n'
       '            if ((stage == Stage.Speech && !observedPlaying) || elapsed < stageLength - 0.15f) { CancelPlayback(); return; }',
       '            var completion = FacilityAnnouncementRules.CompletedClip(stage == Stage.Speech, observedPlaying, elapsed, stageLength);\n'
       '            if (completion == FacilityAnnouncementRules.PlaybackCompletion.Waiting) return;\n'
       '            if (completion == FacilityAnnouncementRules.PlaybackCompletion.Interrupted) { CancelPlayback(); return; }')
change('Tools/FacilityAnnouncementsHarness/Program.cs', '        Scheduling();\n', '        Scheduling();\n        PlaybackCompletion();\n')
checks = '''    private static void PlaybackCompletion()
    {
        Check(Rules.CompletedClip(false, false, 0.32, 0.22) == Rules.PlaybackCompletion.Finished,
            "short cue completed between frames must not discard selected speech");
        Check(Rules.CompletedClip(true, false, 0.32, 5) == Rules.PlaybackCompletion.Interrupted,
            "unobserved speech cannot create caption-only playback");
        Check(Rules.CompletedClip(true, false, 0.1, 5) == Rules.PlaybackCompletion.Waiting,
            "bounded initial source-start grace");
        Check(Rules.CompletedClip(true, true, 2, 5) == Rules.PlaybackCompletion.Interrupted,
            "externally interrupted speech cancels instead of completing");
        Check(Rules.CompletedClip(false, true, 0.1, 0.22) == Rules.PlaybackCompletion.Interrupted,
            "externally interrupted cue cancels");
        Check(Rules.CompletedClip(true, true, 5, 5) == Rules.PlaybackCompletion.Finished,
            "observed natural speech completion");
        Check(Rules.CompletedClip(true, true, double.NaN, 5) == Rules.PlaybackCompletion.Interrupted,
            "invalid playback clock fails closed");
        Check(Rules.CompletedClip(false, false, 1, 0) == Rules.PlaybackCompletion.Interrupted,
            "empty cue cannot advance a caption");
    }
'''
change('Tools/FacilityAnnouncementsHarness/Program.cs', '    private static void Scheduling()\n', checks + '    private static void Scheduling()\n')

extra = '''    for path in ROOT.rglob('*'):
        if path.suffix not in ('.prefab', '.asset', '.mat'):
            continue
        for object_id, asset_guid, asset_type in re.findall(r'fileID: (\\d+), guid: ([0-9a-f]{32}), type: (\\d+)', path.read_text()):
            target = guids.get(asset_guid)
            if target is None:
                continue
            expected_type = '3' if target.suffix in ('.cs', '.wav', '.prefab') else '2'
            require(asset_type == expected_type, 'Unity imported/native PPtr type: ' + str(target))
            if target.suffix == '.wav':
                require(object_id == '8300000', 'AudioClip main object ID')
            elif target.suffix in ('.asset', '.mat', '.prefab'):
                anchors = re.findall(r'^--- !u!\\d+ &(\\d+)$', target.read_text(), re.M)
                require(object_id in anchors, 'external object ID exists: ' + str(target))
    director = (SCRIPTS / 'FacilityAnnouncementDirector.cs').read_text()
    require('output.source.clip != expected' in director, 'clip replacement clears stale caption playback')
    require('CompletedClip(stage == Stage.Speech' in director, 'runtime uses the tested completion policy')
    for callback in ('OnApplicationPause', 'OnApplicationFocus', 'OnDisable'):
        require(callback in director, 'lifecycle cancellation hook: ' + callback)
    for path in Path('Assets/Scripts').rglob('*.cs'):
        if SCRIPTS in path.parents:
            continue
        text = path.read_text(encoding='utf-8-sig')
        require(not re.search(r'\\bconst\\s+(?:byte|int)\\s+\\w*(?:Event|Code)\\w*\\s*=\\s*197\\s*;|\\bRaiseEvent\\(\\s*197\\b', text),
                'event 197 is not assigned by another first-party feature: ' + str(path))
'''
change('Tools/FacilityAnnouncements/validate_assets.py', '    speaker = (ROOT / \'Prefabs/FacilitySpeaker.prefab\').read_text()\n',
       extra + '    speaker = (ROOT / \'Prefabs/FacilitySpeaker.prefab\').read_text()\n')
print('Final review: tested cue completion/interruption policy, editor-only mutation guard, sector bounds, portable GUID generation and stronger PPtr/event-code checks.')
