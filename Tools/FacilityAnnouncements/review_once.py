"""One-time branch preparation, removed after its reviewed changes are committed."""
from pathlib import Path
import re
from build_assets import ROOT, SCRIPTS, meta


def replace(path, old, new, count=1):
    path = Path(path)
    text = path.read_text()
    if text.count(old) != count:
        raise RuntimeError('Unexpected review patch context: ' + str(path) + ' / ' + repr(old))
    path.write_text(text.replace(old, new), encoding='utf-8')


# Unity imported WAV/prefab references use type 3, not NativeFormat asset type 2.
replace('Tools/FacilityAnnouncements/build_assets.py',
        "return '{fileID: %d, guid: %s, type: 2}' % (file_id, guid(path))",
        "asset_type = 3 if Path(path).suffix in ('.wav', '.prefab') else 2\n    return '{fileID: %d, guid: %s, type: %d}' % (file_id, guid(path), asset_type)")
path = ROOT / 'Data/FacilityAnnouncements.asset'
text = path.read_text()
text, count = re.subn(r'(fileID: 8300000, guid: [0-9a-f]{32}, type:) 2', r'\1 3', text)
assert count == 6
path.write_text(text)
path = ROOT / 'Prefabs/FacilitySpeaker.prefab'
text = path.read_text()
text, count = re.subn(r'(captionPrefab: \{fileID: \d+, guid: [0-9a-f]{32}, type:) 2', r'\1 3', text)
assert count == 1
path.write_text(text)

# A short cue can finish between two rendered frames. Do not mislabel that as a
# failed speech start; actual speech still requires an observed playing source.
replace(SCRIPTS / 'FacilityAnnouncementDirector.cs',
        'if (!observedPlaying || elapsed < stageLength - 0.15f)',
        'if ((stage == Stage.Speech && !observedPlaying) || elapsed < stageLength - 0.15f)')
replace(SCRIPTS / 'FacilityAnnouncementDirector.cs',
        '            if (stage == Stage.Waiting)\n',
        '            AudioClip expected = stage == Stage.StartCue ? startCue : stage == Stage.EndCue ? endCue : speech;\n'
        '            if (stage != Stage.Waiting && output.source.clip != expected) { CancelPlayback(); return; }\n'
        '            if (stage == Stage.Waiting)\n')
for old in ['        private FacilitySpeaker configuration;\n', '            configuration = null;\n', '            configuration = chosen;\n']:
    path = SCRIPTS / 'FacilityAnnouncementDirector.cs'
    text = path.read_text()
    path.write_text(text.replace(old, ''))

# More vertical space at the largest locally selectable caption font size.
replace(SCRIPTS / 'FacilityAnnouncementCaptions.cs', 'label.fontSize = 30 * TextScale;', 'label.fontSize = 26 * TextScale;')
replace(SCRIPTS / 'FacilityAnnouncementCaptions.cs', 'VerticalPosition - 0.055f', 'VerticalPosition - 0.12f')
replace(SCRIPTS / 'FacilityAnnouncementCaptions.cs', 'VerticalPosition + 0.055f', 'VerticalPosition + 0.12f')
replace(SCRIPTS / 'FacilityAnnouncementCaptions.cs', '            canvas.renderMode = RenderMode.ScreenSpaceCamera;',
        '            gameObject.layer = camera.gameObject.layer;\n'
        '            panel.gameObject.layer = camera.gameObject.layer;\n'
        '            label.gameObject.layer = camera.gameObject.layer;\n'
        '            canvas.renderMode = RenderMode.ScreenSpaceCamera;')
# The authored panel is only an idle default; runtime uses the local preference.
replace(ROOT / 'Prefabs/FacilityAnnouncementCaptions.prefab', 'm_AnchorMin: {x: 0.19, y: 0.15}', 'm_AnchorMin: {x: 0.19, y: 0.13}')
replace(ROOT / 'Prefabs/FacilityAnnouncementCaptions.prefab', 'm_AnchorMax: {x: 0.81, y: 0.35}', 'm_AnchorMax: {x: 0.81, y: 0.37}')
replace('Tools/FacilityAnnouncements/build_assets.py', 'm_AnchorMin: {x: 0.19, y: 0.15}', 'm_AnchorMin: {x: 0.19, y: 0.13}')
replace('Tools/FacilityAnnouncements/build_assets.py', 'm_AnchorMax: {x: 0.81, y: 0.35}', 'm_AnchorMax: {x: 0.81, y: 0.37}')

section = '''## September 30 optional facility announcement prototype

**Confirmed choice:** Greg approved prototyping occasional prerecorded facility announcements as optional atmosphere, not a launch requirement. This work is independent of the toys, regeneration-lab art kit and security-console branches. It must not change objectives, rewards, monster hearing, player voice/reporting, surveillance-monitor silence or canonical staff/experiment/monster history.

**Proposed wording:** the four editable lines about enrichment privileges, unexpected tissue movement, assigned containment and recorded cooperation are prototypes, not canonical lore commitments. Full wording, stable IDs and content-authoring rules are in [the facility announcement guide](facility-announcements.md).

**Implemented on `feature/facility-announcements` (not a merge or runtime acceptance):** real industrial speaker and local-camera caption prefabs, assigned materials/collection/audio references, long quiet intervals, stable-ID repeat prevention, one eligible sector authority independent of the Master Client's location, uncached sector/visit-filtered announcements, bounded playback, interruption cleanup, local caption preferences, Undo-aware explicit Hub placement, Editor audition and development-only diagnostic timing. No existing scene, Unity 2022.3.62f3 baseline, package, rendering or XR configuration is changed.

**Available audio:** four actually rendered native eSpeak synthetic prototype speech WAVs and two original nonverbal relay/chime cues are committed with transcripts, commands, durations and checksums. Synthetic speech is present; final performance, intelligibility and mix approval are not. Missing/unloaded/pending speech is skipped silently in normal playback. A separately labelled text/cue preview is development-only and does not count as voiced content.

**Validation boundary:** the production policy harness and static asset/source checks are executable separately from Unity. The initial authored-asset check passed 781 assertions. Final revision-specific automated results and self-review fixes are recorded in the PR/guide; source checks do not prove Unity import, Editor execution, actual audio playback, Photon transport, XR rendering or Quest performance. Those solo/two-client/headset checks remain pending in the guide. This prototype adds no release gate and preserves unrelated decisions below.

'''
for name in ['docs/design-and-lore.md', 'docs/repository-improvement-plan.md']:
    path = Path(name)
    text = path.read_text(encoding='utf-8-sig')
    assert '## September 30 optional facility announcement prototype' not in text
    index = text.index('\n## ') + 1
    text = text[:index] + section + text[index:]
    text = re.sub(r'Last updated: \d{4}-\d{2}-\d{2}', 'Last updated: 2026-09-30', text, count=1)
    path.write_text(text, encoding='utf-8')
for root in (ROOT, SCRIPTS):
    for path in root.rglob('*'):
        meta(path, folder=path.is_dir())
print('Applied focused self-review corrections and additive maintained-document sections. No scene or baseline edits.')
