"""One-time, feature-scoped delivery edits; removed before the asset commit."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def replace(path, old, new):
    target = ROOT / path
    text = target.read_text()
    if old not in text:
        raise RuntimeError('Expected source has changed: ' + path)
    target.write_text(text.replace(old, new, 1))


replace('Tools/PrimateCognitive/build_assets.py', '\n\nclass Prefab:', '''

def audio_curve(value):
    return dict(serializedVersion=2, m_Curve=[dict(serializedVersion=3, time=0,
        value=value, inSlope=0, outSlope=0, tangentMode=0, weightedMode=0,
        inWeight=.33333334, outWeight=.33333334)],
        m_PreInfinity=2, m_PostInfinity=2, m_RotationOrder=4)


class Prefab:''')
replace('Tools/PrimateCognitive/build_assets.py',
        'BypassEffects=0, BypassListenerEffects=0, BypassReverbZones=0))',
        '''BypassEffects=0, BypassListenerEffects=0, BypassReverbZones=0,
        panLevelCustomCurve=audio_curve(1), spreadCustomCurve=audio_curve(0),
        reverbZoneMixCustomCurve=audio_curve(1)))''')
replace('Assets/Scripts/Toys/PrimateCognitive/CognitivePad.cs',
        'rest + (lit ? Vector3.forward * pressDepth : Vector3.zero)',
        'rest + (gate.Count > 0 ? Vector3.forward * pressDepth : Vector3.zero)')
editor = 'Assets/Scripts/Toys/PrimateCognitive/Editor/CognitiveMachineEditor.cs'
replace(editor, '                Selection.activeGameObject = existing.gameObject;',
        '''                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                Selection.activeGameObject = existing.gameObject;''')
replace(editor, '            int group = Undo.GetCurrentGroup();',
        '            Undo.IncrementCurrentGroup();\n            int group = Undo.GetCurrentGroup();')
replace(editor,
        '            instance.transform.SetPositionAndRotation(new Vector3(2.4f, .15f, -1.2f), Quaternion.identity);',
        '            instance.transform.SetPositionAndRotation(FindTestFloor(hub), Quaternion.identity);')
replace(editor, '            instance.transform.localScale = Vector3.one;',
        '''            instance.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);''')
replace(editor, '        [MenuItem("Tools/Runaway Chimps/Toys/Validate Cognitive Evaluation Prefab")]', '''        private static Vector3 FindTestFloor(Scene hub)
        {
            Physics.SyncTransforms();
            var floor = Physics.RaycastAll(new Vector3(2.4f, 2.5f, -1.2f), Vector3.down, 5f,
                    ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider != null && h.collider.gameObject.scene == hub &&
                    h.normal.y > .9f && h.point.y < .5f)
                .OrderByDescending(h => h.point.y).FirstOrDefault();
            if (floor.collider != null) return floor.point + Vector3.up * .005f;
            Debug.LogWarning("No Hub floor found at the cognitive test position. Inspect and adjust the placement before saving.");
            return new Vector3(2.4f, 0f, -1.2f);
        }

        [MenuItem("Tools/Runaway Chimps/Toys/Validate Cognitive Evaluation Prefab")]''')
replace(editor, '                var collider = pad != null ? pad.GetComponent<BoxCollider>() : null;',
        '''                var collider = pad != null ? pad.GetComponent<BoxCollider>() : null;
                var body = pad != null ? pad.GetComponent<Rigidbody>() : null;''')
replace(editor, 'pad.capRenderer.sharedMaterial == null || collider == null || !collider.isTrigger)',
        '''pad.capRenderer.sharedMaterial == null || collider == null || !collider.isTrigger ||
                    body == null || !body.isKinematic || body.useGravity)''')
replace(editor, '            foreach (var clip in machine.clips)',
        '''            if (root.transform.localScale != Vector3.one || machine.display.font == null ||
                machine.audioSource.playOnAwake || machine.audioSource.spatialBlend < .99f)
                throw new InvalidOperationException("Cognitive root scale, display font or spatial audio authoring is invalid.");
            foreach (var clip in machine.clips)''')

section = '''## September 30 Primate Cognitive Evaluation optional toy

**Confirmed decision:** Greg requested an optional four-pad extending-sequence memory machine, independent of the strength tester and every other toy branch. Either hand can repeat the sequence; show round reached and dry facility assessments; provide an obvious physical restart and bounded completion. No voice, rewards, progression, leaderboard or shared minigame framework.

**Correction, September 30:** Greg explicitly required finishing the missing assets after the source-only delivery. A rejected authoring operation was not evidence that these assets could not be created. The old missing-prefab status is superseded by the serialized asset delivery below; lack of Unity execution remains a separate validation boundary.

**Implemented, unmerged on `feature/primate-cognitive-test` / PR #77:** the original branch remains based on main `930a8f830cae99e06dcaa0a78ab9edc968c42ae5`. It now includes the actual movable `Assets/RunawayChimps/Toys/PrimateCognitive/Prefabs/PrimateCognitiveEvaluation.prefab`, eight built-in Standard materials, six original mono PCM WAV clips, permanent high-contrast 1-4/start labels, an assigned LiberationSans SDF assessment display, five kinematic trigger controls and a spatial speaker. Source includes bounded rules, held/duplicate-contact rejection, local-rig filtering, one recoverable operator, same-sector arbitration and Editor inputs through the same logic. Illuminated idle START no longer holds its moving cap mechanically depressed. The opt-in Hub placement command preserves existing instances, supports Undo, records prefab overrides, samples the Hub floor and never forces a save. No Hub/Bootstrap scene, packages, engine, rendering, XR or other toy changes.

**Validated source/asset data:** local reproduction and structural checks passed for 214 serialized objects, all local references, five pad bindings, assigned materials/font/clip identities, bounded PCM waveforms and four distinct primary pitches. Four deliberately broken prefab mutations were rejected. The unchanged production rules/contact/replica harness previously passed 57,013 assertions at `4c7a056`; fresh full-branch CI evidence is recorded on PR #77. The retained authoring tool defaults to read-only comparison; running `--write` explicitly regenerates feature assets, not scene placements. The shipped prefab requires no generation step to use.

**Pending validation:** Unity 2022.3.62f3 import/compilation, actual TMP layout/material rendering/audio output, placement/Undo, solo Editor play, native hand triggers, seated/standing reach, two-client Photon cue/ownership/authority/sector recovery and Quest acceptance. These are not passed by managed or serialized-data checks. [Feature notes](primate-cognitive-evaluation.md) give the exact setup, dimensions, pacing/recovery controls and short acceptance checklists. The PR remains unmerged.

'''
for name in ('design-and-lore.md', 'repository-improvement-plan.md'):
    path = ROOT / 'docs' / name
    text = path.read_text()
    start = text.index('## September 30 Primate Cognitive Evaluation optional toy\n')
    end = text.index('## September 22 Unity 62f3 baseline adoption\n', start)
    path.write_text(text[:start] + section + text[end:])
