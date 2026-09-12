"""Read the active local Gorilla locomotion interaction scale from Bootstrap.unity."""
from pathlib import Path
import json
import re

def _blocks(text):
    return [b for b in re.split(r'(?=^--- !u!\d+ &-?\d+)', text, flags=re.M) if b.startswith('--- !u!')]

def _block_id(block):
    m = re.match(r'^--- !u!\d+ &(-?\d+)', block)
    if not m:
        raise ValueError('Serialized Unity block is missing a fileID header.')
    return int(m.group(1))

def _vec3(block, field):
    m = re.search(rf'^  {re.escape(field)}: \{{x: ([^,]+), y: ([^,]+), z: ([^}}]+)\}}$', block, re.M)
    if not m:
        raise ValueError(f'Missing {field} in Unity block {_block_id(block)}')
    return tuple(float(v) for v in m.groups())

def read_player_scale(root):
    root = Path(root)
    path = root / 'Assets/Scenes/Bootstrap.unity'
    text = path.read_text(encoding='utf-8-sig')
    blocks = _blocks(text)
    by_id = {_block_id(b): b for b in blocks}
    players = [b for b in blocks if 'bodyCollider: {fileID:' in b and 'maxArmLength:' in b and 'minimumRaycastDistance:' in b]
    if len(players) != 1:
        raise ValueError(f'Expected one serialized GorillaLocomotion.Player in Bootstrap, found {len(players)}')
    player = players[0]
    body_id = int(re.search(r'bodyCollider: \{fileID: (-?\d+)\}', player).group(1))
    max_arm = float(re.search(r'^  maxArmLength: ([^\n]+)$', player, re.M).group(1))
    hand_radius = float(re.search(r'^  minimumRaycastDistance: ([^\n]+)$', player, re.M).group(1))
    body = by_id[body_id]
    if not body.startswith('--- !u!136 '):
        raise ValueError('Gorilla bodyCollider no longer references a CapsuleCollider.')
    body_go = int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body).group(1))
    radius = float(re.search(r'^  m_Radius: ([^\n]+)$', body, re.M).group(1))
    height = float(re.search(r'^  m_Height: ([^\n]+)$', body, re.M).group(1))
    direction = int(re.search(r'^  m_Direction: (\d+)$', body, re.M).group(1))

    def transform_for_game_object(go_id):
        go = by_id[go_id]
        for cid in map(int, re.findall(r'- component: \{fileID: (-?\d+)\}', go)):
            block = by_id.get(cid, '')
            if block.startswith('--- !u!4 '):
                return cid
        raise ValueError(f'GameObject {go_id} has no Transform component.')

    memo = {}
    def world_scale(transform_id):
        if transform_id in memo:
            return memo[transform_id]
        t = by_id[transform_id]
        local = _vec3(t, 'm_LocalScale')
        parent_match = re.search(r'^  m_Father: \{fileID: (-?\d+)\}$', t, re.M)
        parent = int(parent_match.group(1)) if parent_match else 0
        if parent == 0:
            result = local
        else:
            p = world_scale(parent)
            result = tuple(local[i] * p[i] for i in range(3))
        memo[transform_id] = result
        return result

    sx, sy, sz = map(abs, world_scale(transform_for_game_object(body_go)))
    if direction == 0:
        body_height = max(height * sx, 2 * radius * max(sy, sz)); body_width = 2 * radius * max(sy, sz)
    elif direction == 1:
        body_height = max(height * sy, 2 * radius * max(sx, sz)); body_width = 2 * radius * max(sx, sz)
    elif direction == 2:
        body_height = max(height * sz, 2 * radius * max(sx, sy)); body_width = 2 * radius * max(sx, sy)
    else:
        raise ValueError(f'Unsupported CapsuleCollider direction {direction}')
    return {
        'hand_contact_diameter_m': round(hand_radius * 2.0, 6),
        'body_width_m': round(body_width, 6),
        'body_capsule_height_m': round(body_height, 6),
        'max_arm_length_m': round(max_arm, 6),
        'source': 'Assets/Scenes/Bootstrap.unity',
        'hand_source': 'GorillaLocomotion.Player.minimumRaycastDistance x 2',
    }

if __name__ == '__main__':
    root = Path(__file__).resolve().parents[2]
    print(json.dumps(read_player_scale(root), indent=2))
