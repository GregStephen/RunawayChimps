"""Read-only checks against actual Bootstrap contacts; not native trigger execution."""
import re

from build_assets import ROOT
from validate_assets import objects, check


def check_filter(source):
    for token in ('!IsHandContact(other.transform, rig.leftHandTransform)',
                  '!IsHandContact(other.transform, rig.rightHandTransform)',
                  'hand != null && contact != hand && contact.IsChildOf(hand)'):
        check(token in source, 'Only children of actual tracked hands are physical contacts')
    check('!other.CompareTag("HandTag")' in source, 'Require hand identity')
    check('rig.GetComponentInParent<LocalRigMarker>() != marker' in source, 'Require the actual local rig')
    check('view == null || view.IsMine' in source, 'Reject remote Photon views')
    check('OnTriggerStay(Collider other) => Track(other, false)' in source, 'Resting overlap cannot become a fresh press')


def main():
    bootstrap = objects((ROOT / 'Assets/Scenes/Bootstrap.unity').read_text(encoding='utf-8-sig'))
    player = next(d for k, d in bootstrap.values() if k == 114 and 'leftHandTransform' in d and 'rightHandTransform' in d)
    hands = {player['leftHandTransform']['fileID'], player['rightHandTransform']['fileID']}
    def descendant(contact, hand):
        if contact == hand:
            return False
        while contact:
            contact = bootstrap[contact][1]['m_Father']['fileID']
            if contact == hand:
                return True
        return False
    fingers = []
    finger_layers = set()
    for _, (kind, go) in bootstrap.items():
        if kind != 1 or go['m_TagString'] != 'HandTag':
            continue
        ids = [c['component']['fileID'] for c in go['m_Component']]
        transform = next(i for i in ids if bootstrap[i][0] == 4)
        colliders = [bootstrap[i][1] for i in ids if bootstrap[i][0] in (65, 135, 136)]
        if transform in hands:
            check(len(colliders) == 1 and abs(colliders[0]['m_Radius'] - .08) < 1e-6, 'Known broad grab volumes excluded')
            check(not any(descendant(transform, hand) for hand in hands), 'Controller-root grab volumes cannot pass filter')
        elif any(descendant(transform, hand) for hand in hands):
            check(len(colliders) == 1 and colliders[0]['m_Enabled'] == 1 and colliders[0]['m_IsTrigger'] == 1, 'Live fingertip collider')
            fingers.append(go['m_Name'])
            finger_layers.add(go['m_Layer'])
    check(set(fingers) == {'LeftFingerCollider', 'RightFingerCollider'}, 'Both actual fingertips available')
    source = (ROOT / 'Assets/Scripts/Toys/PrimateCognitive/CognitivePad.cs').read_text()
    check_filter(source)
    mutations = [source.replace('contact != hand && contact.IsChildOf(hand)', 'true'),
                 source.replace('rig.GetComponentInParent<LocalRigMarker>() != marker', 'false'),
                 source.replace('view == null || view.IsMine', 'true'),
                 source.replace('OnTriggerStay(Collider other) => Track(other, false)', 'OnTriggerStay(Collider other) => Track(other, true)')]
    for mutant in mutations:
        try:
            check_filter(mutant)
        except AssertionError:
            pass
        else:
            raise AssertionError('Contact-filter regression escaped validation')
    machine = (ROOT / 'Assets/Scripts/Toys/PrimateCognitive/CognitiveMachine.cs').read_text()
    check('private static int localSerial;' in machine and 'localSerial = 0;' not in machine,
          'Command numbering survives scene/component replacement')
    check('if (game.Owner == 0) lastCommand.Clear();' in machine, 'Idle recovery discards old command floors')
    check('if (session != game.Session ||' in machine, 'Every nonsync command requires the current session')
    settings = (ROOT / 'ProjectSettings/DynamicsManager.asset').read_text()
    matrix = re.search(r'm_LayerCollisionMatrix: ([0-9a-fA-F]+)', settings)
    check(matrix is not None, 'Serialized physics layer matrix exists')
    rows = bytes.fromhex(matrix[1])
    for layer in finger_layers:
        check(int.from_bytes(rows[:4], 'little') & (1 << layer), 'Default-layer machine receives actual fingertip overlaps')
    print('PASS: both actual Bootstrap hand-child contacts, excluded 8 cm grab spheres, and existing collision matrix.')
    print('PASS: local/remote/stay filters; four deliberately broken contact-filter mutations rejected.')
    print('SOURCE CONTRACTS ONLY: no Unity callbacks or native overlap simulation executed.')


if __name__ == '__main__':
    main()
