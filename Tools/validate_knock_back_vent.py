#!/usr/bin/env python3
"""Read-only Knock-Back Vent source/serialized-asset checks. Not a Unity importer."""
from collections import Counter
from pathlib import Path
import hashlib
import re
import struct
import sys
import wave

ROOT = Path(__file__).resolve().parents[1]
ASSET = Path('Assets/RunawayChimps/KnockBackVent')
RUNTIME = Path('Assets/Scripts/KnockBackVent')


def validate(root):
    errors = []

    def require(value, reason):
        if not value:
            errors.append(reason)

    def text(path):
        return (root / path).read_text(encoding='utf-8-sig')

    def guid(path):
        return re.search(r'^guid: ([0-9a-f]{32})$', text(str(path) + '.meta'), re.M)[1]

    prefab = text(ASSET / 'KnockBackVent.prefab')
    matches = re.findall(r'^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)', prefab, re.M | re.S)
    ids = [ident for _, ident, _ in matches]
    require(all(count == 1 for count in Counter(ids).values()), 'duplicate prefab object ID')
    blocks = {ident: (kind, body) for kind, ident, body in matches}
    local_refs = set(re.findall(r'\{fileID: (-?\d+)\}', prefab)) - {'0'}
    require(local_refs <= set(ids), 'unresolved prefab local reference: ' + str(local_refs - set(ids)))
    # TextMeshPro's script is package-owned; this exact GUID is already used in Hub_Base.
    metadata_guids = {'9541d86e2fd84c1d9990edf0852d74ab'}
    for meta in (root / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8-sig'), re.M)
        if match:
            metadata_guids.add(match[1])
    external = set(re.findall(r'guid: ([0-9a-f]{32})', prefab))
    require(all(g.startswith('0000000000000000') or g in metadata_guids for g in external), 'missing external prefab GUID')
    script = guid(RUNTIME / 'KnockBackVent.cs')
    controllers = [b for k, b in blocks.values() if k == '114' and 'guid: ' + script in b]
    require(len(controllers) == 1, 'exactly one toy controller required')
    if len(controllers) != 1:
        return errors
    controller = controllers[0]
    require(prefab.count('guid: 9803f62141aa590419125376fb147df4') == 1, 'existing BlockHandSurfaceAudio marker required')
    normalized = prefab.replace('\\n', ' ')
    require('DO NOT COMMUNICATE WITH OCCUPANTS.' in normalized, 'warning text missing')

    def reference(body, name):
        return re.search(r'^  ' + name + r': \{fileID: (\d+)\}', body, re.M)[1]

    root_id = reference(controller, 'm_GameObject')
    collider = blocks[reference(controller, 'panelCollider')]
    require(collider[0] == '65' and 'm_IsTrigger: 0' in collider[1] and reference(collider[1], 'm_GameObject') == root_id,
            'fixed root BoxCollider must be assigned, not a trigger or moving visual')
    visual = blocks[reference(controller, 'panelVisual')]
    require(visual[0] == '4' and reference(visual[1], 'm_GameObject') != root_id, 'visual movement must not move the sensing root')
    sources = []
    for field, count in [('tapVoices', 8), ('replyVoices', 9)]:
        section = re.search(r'^  ' + field + r':\n((?:  - \{fileID: \d+\}\n)+)', controller, re.M)[1]
        voices = re.findall(r'fileID: (\d+)', section)
        require(len(voices) == count, field + ' has incorrect bound')
        sources.extend(voices)
        for source in voices:
            kind, body = blocks[source]
            require(kind == '82' and 'm_PlayOnAwake: 0' in body and 'Loop: 0' in body and 'DopplerLevel: 0' in body,
                    'audio source must be explicit, nonlooping and without Doppler pitch shift')
            require(re.search(r'panLevelCustomCurve:.*?value: 1\n', body, re.S), 'audio must be fully spatial')
            owner = reference(body, 'm_GameObject')
            transform = next(b for k, b in blocks.values() if k == '4' and reference(b, 'm_GameObject') == owner)
            z = float(re.search(r'm_LocalPosition: \{x: [^,]+, y: [^,]+, z: ([^}]+)\}', transform)[1])
            require(z < 0 if field == 'replyVoices' else z > 0, 'audio source on wrong side of panel')
    require(len(set(sources)) == 17, 'audio slots must be distinct, fixed voices')
    require(sum(k == '82' for k, _ in blocks.values()) == 17, 'unexpected extra audio sources')
    require(not any(k in ('20', '54', '81') for k, _ in blocks.values()), 'prefab must not add camera, rigidbody or AudioListener')
    require(external & {script, '9803f62141aa590419125376fb147df4', '9541d86e2fd84c1d9990edf0852d74ab'} ==
            {script, '9803f62141aa590419125376fb147df4', '9541d86e2fd84c1d9990edf0852d74ab'}, 'required scripts not assigned')
    checksums = set()
    for name in ['Tap', 'Reply', 'Bang']:
        path = ASSET / 'Audio' / (name + '.wav')
        require('guid: ' + guid(path) in controller, name + ' clip not assigned')
        with wave.open(str(root / path), 'rb') as audio:
            require(audio.getnchannels() == 1 and audio.getsampwidth() == 2 and audio.getframerate() == 22050, name + ': expected mono 16-bit PCM / 22050 Hz')
            require(0.05 < audio.getnframes() / audio.getframerate() <= 0.5, name + ': audio exceeds bounded tail')
            pcm = audio.readframes(audio.getnframes())
            values = struct.unpack('<' + 'h' * (len(pcm) // 2), pcm)
            require(1000 < max(abs(v) for v in values) < 30000, name + ': silence or excessive peak')
            checksums.add(hashlib.sha256(pcm).hexdigest())
        require('preloadAudioData: 1' in text(str(path) + '.meta'), name + ': clips must be preloaded')
    require(len(checksums) == 3, 'three distinct usable clips required')
    for material in (root / ASSET / 'Materials').glob('*.mat'):
        require('fileID: 46, guid: 0000000000000000f000000000000000' in material.read_text(), 'use built-in Standard materials')

    runtime = text(RUNTIME / 'KnockBackVent.cs')
    hand = text(RUNTIME / 'KnockBackVentHandInput.cs')
    editor = text('Assets/Scripts/Editor/KnockBackVentEditor.cs')
    for contract in ['EventCode = 189', '"rc.knock.v1"', 'SectorPresence.ElectController',
                     'ReferenceEquals(room, observedRoom)', 'message.Sender == controller',
                     'SectorPresence.Get(observedRoom.GetPlayer(message.Sender)) != sector',
                     'delivery.Header', 'model.ReadyToReply(now) ? variation.NextDouble()',
                     'OnPlayerPropertiesUpdate', 'OnPlayerLeftRoom', 'OnApplicationPause',
                     'isActiveAndEnabled && panelCollider != null && panelCollider.enabled',
                     'ReferenceEquals(PhotonNetwork.CurrentRoom, observedRoom)',
                     'delivery.AcceptsInput(inputTime)', 'VentModel.LatestPlanIssue', 'VentModel.RecordingTimeout',
                     'ResetAll(now, issued)', 'diagnosticTap = SampleOffsets.Length',
                     'double dspStart = AudioSettings.dspTime + lead', 'dspStart + plan.Offsets[i]',
                     'replyVisualStart = now + lead',
                     'voice.Stop()', 'voice.PlayScheduled', 'public override void OnDisable()']:
        require(contract in runtime, 'missing runtime safety contract: ' + contract)
    require(runtime.count('PhotonNetwork.RaiseEvent(') == runtime.count('CachingOption = EventCaching.DoNotCache'), 'all event sends must explicitly avoid caching')
    require('GorillaLocomotion.Player.Instance' in runtime and 'GetComponentInParent<LocalRigMarker>()' in runtime,
            'input must originate in the local Bootstrap rig')
    for contract in ['CommonUsages.isTracked', 'CommonUsages.trackingState', 'InputTrackingState.Position',
                     'dt <= 0.12d', 'Vector3.Distance(world, previousWorld) <= maximumStep', 'gate.Sample',
                     'player.leftHandFollower', 'player.rightHandFollower', 'fromFront.magnitude <= contactRadius',
                     '!face.enabled', '0.035f / scale', 'trackedReleased && solvedReleased']:
        require(contract in hand, 'missing hand safety contract: ' + contract)
    require(not any(c in runtime + hand for c in ['OnTriggerEnter(', 'OnCollisionEnter(', 'OnAudioFilterRead(']), 'do not accept arbitrary collisions or audio as taps')
    for contract in ['Tools/Runaway Chimps/Toys/', 'Place Knock-Back Vent in Hub', 'Undo.RegisterCreatedObjectUndo',
                     'PrefabUtility.InstantiatePrefab', 'existing != null', 'Play Selected Knock-Back Vent Sample']:
        require(contract in editor, 'missing explicit authoring contract: ' + contract)
    require('SaveScene(' not in editor and 'SaveOpenScenes(' not in editor and
            'InitializeOnLoad' not in editor + runtime and 'RuntimeInitializeOnLoad' not in runtime,
            'toy must not install on load or force scene saves')
    return errors


def main():
    try:
        errors = validate(ROOT)
    except (OSError, ValueError, KeyError, TypeError, AttributeError, wave.Error) as failure:
        errors = ['incomplete/malformed feature asset: ' + str(failure)]
    for error in errors:
        print('ERROR:', error)
    if errors:
        return 1
    print('PASS: Knock-Back Vent source boundaries; serialized prefab references, spatial voice bounds, warning/materials and three non-silent PCM assets.')
    print('Source/serialization checks only; Unity import, listening, Photon and headset acceptance remain separate.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
