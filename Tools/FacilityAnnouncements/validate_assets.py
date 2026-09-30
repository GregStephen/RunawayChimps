#!/usr/bin/env python3
"""Static asset checks; not a Unity importer, renderer, audio audition or network test."""
import hashlib
import json
from pathlib import Path
import re
import struct
import wave

ROOT = Path('Assets/RunawayChimps/FacilityAnnouncements')
SCRIPTS = Path('Assets/Scripts/FacilityAnnouncements')
checks = 0


def require(condition, message):
    global checks
    checks += 1
    if not condition:
        raise AssertionError(message)


def main():
    guids = {}
    for root in (ROOT, SCRIPTS):
        require(root.exists(), str(root) + ' exists')
        for path in [root, *sorted(root.rglob('*'))]:
            if path.suffix == '.meta':
                continue
            metadata = Path(str(path) + '.meta')
            require(metadata.exists(), 'metadata: ' + str(path))
            match = re.search(r'^guid: ([0-9a-f]{32})$', metadata.read_text(), re.M)
            require(match is not None, 'valid GUID: ' + str(path))
            require(match[1] not in guids, 'unique GUID: ' + str(path))
            guids[match[1]] = path
    allowed_external = {'0000000000000000e000000000000000', '0000000000000000f000000000000000',
                        '8f586378b4e144a9851e7b34d9b748ee', '0cd44c1031e13a943bb63640046fad76',
                        'fe87c0e1cc204ed48ad3b37840f39efc', 'f4688fdb7df04437aeb418b961361dc5'}
    for path in ROOT.rglob('*'):
        if path.suffix not in ('.prefab', '.mat', '.asset'):
            continue
        text = path.read_text()
        anchors = re.findall(r'^--- !u!\d+ &(\d+)$', text, re.M)
        require(len(anchors) == len(set(anchors)), 'unique object IDs: ' + str(path))
        for value in re.findall(r'\{fileID: (\d+)\}', text):
            require(value == '0' or value in anchors, 'local reference: ' + str(path) + '/' + value)
        for value in re.findall(r'guid: ([0-9a-f]{32})', text):
            require(value in guids or value in allowed_external, 'asset reference: ' + value)
    for path in ROOT.rglob('*'):
        if path.suffix not in ('.prefab', '.asset', '.mat'):
            continue
        for object_id, asset_guid, asset_type in re.findall(r'fileID: (\d+), guid: ([0-9a-f]{32}), type: (\d+)', path.read_text()):
            target = guids.get(asset_guid)
            if target is None:
                continue
            expected_type = '3' if target.suffix in ('.cs', '.wav', '.prefab') else '2'
            require(asset_type == expected_type, 'Unity imported/native PPtr type: ' + str(target))
            if target.suffix == '.wav':
                require(object_id == '8300000', 'AudioClip main object ID')
            elif target.suffix in ('.asset', '.mat', '.prefab'):
                anchors = re.findall(r'^--- !u!\d+ &(\d+)$', target.read_text(), re.M)
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
        require(not re.search(r'\bconst\s+(?:byte|int)\s+\w*(?:Event|Code)\w*\s*=\s*197\s*;|\bRaiseEvent\(\s*197\b', text),
                'event 197 is not assigned by another first-party feature: ' + str(path))
    speaker = (ROOT / 'Prefabs/FacilitySpeaker.prefab').read_text()
    require(len(re.findall(r'^--- !u!82 ', speaker, re.M)) == 1, 'exactly one AudioSource')
    require(not re.search(r'^--- !u!(20|81|65|54|135|136) ', speaker, re.M), 'no camera/listener/collision/gameplay body')
    require(speaker.count('Grille louver') == 9 and 'Rear mounting lug' in speaker, 'industrial geometry serialized')
    require('captionPrefab: {fileID: 0}' not in speaker and 'collection: {fileID: 0}' not in speaker, 'assigned speaker assets')
    captions = (ROOT / 'Prefabs/FacilityAnnouncementCaptions.prefab').read_text()
    require('m_RenderMode: 1' in captions and 'm_ReceivesEvents: 0' in captions, 'camera-bound noninteractive UI')
    require('m_RaycastTarget: 1' not in captions and 'm_isRichText: 0' in captions, 'caption input safety')
    require('m_Camera: {fileID: 0}' in captions, 'existing local camera assigned only at runtime')
    data = (ROOT / 'Data/FacilityAnnouncements.asset').read_text()
    ids = re.findall(r'^  - id: (\S+)$', data, re.M)
    require(len(ids) == 4 and len(set(ids)) == 4, 'four distinct prototype IDs')
    require(data.count('speechAvailable: 1') == 4 and 'diagnosticScheduling: 0' in data, 'spoken content and quiet defaults')
    manifest = json.loads((ROOT / 'Audio/provenance.json').read_text())
    require(len(manifest['clips']) == 6, 'four speech clips and two cues')
    for clip in manifest['clips']:
        path = Path(clip['path'])
        require(hashlib.sha256(path.read_bytes()).hexdigest() == clip['sha256'], 'audio provenance checksum: ' + path.name)
        with wave.open(str(path), 'rb') as stream:
            require(stream.getnchannels() == 1 and stream.getsampwidth() == 2 and stream.getframerate() == 22050, 'mono PCM format')
            samples = struct.unpack('<%dh' % stream.getnframes(), stream.readframes(stream.getnframes()))
            require(0 < len(samples) / stream.getframerate() <= 30, 'bounded clip length')
        require(max(map(abs, samples)) > 1000 and max(map(abs, samples)) < 30000, 'non-silent and unclipped audio')
        if clip['id'] in ids:
            require(clip['transcript'] in data, 'matching selected clip transcript')
        importer = Path(str(path) + '.meta').read_text()
        require('preloadAudioData: 1' in importer and 'loadInBackground: 0' in importer, 'preloaded bounded speech')
    for path in [SCRIPTS / 'FacilityAnnouncementDirector.cs', SCRIPTS / 'FacilitySpeaker.cs']:
        source = path.read_text()
        require('Microphone.' not in source and 'Photon.Voice' not in source, 'no recording/voice dependencies')
        require('FindObjectsOfType' not in source and 'Camera.main' not in source, 'no scene-wide polling')
    print('PASS: %d facility asset/source assertions; 2 prefabs, 4 materials, 4 spoken WAVs, 2 cues. Unity import/render/audio listening still pending.' % checks)


if __name__ == '__main__':
    main()
