#!/usr/bin/env python3
"""One-time, reviewable documentation patch for this kit. Refuses duplicate insertion."""
from pathlib import Path
import argparse
import json

ROOT='Assets/RunawayChimps/Environment/RegenerationLab'
DESIGN_SECTION='''## September 30 regeneration lab prop kit — asset review, not final room dressing

**Confirmed choice:** create a small cohesive modular art kit for the existing Level 1 regeneration/tissue-damage keycard safe room. The first kit specifically contains a treatment/utility trolley, a chemical-delivery stand and a specimen cold-storage cabinet. These three choices are now confirmed; the optional waste container and regeneration diagram are deferred. This is a working experimental laboratory, not another surveillance room or a conventional operating theater.

**Implemented on `art/regeneration-lab-prop-kit`, pending review/merge:** three independently movable static prefabs, six baked native mesh assets (body plus editable label per prop), three shared opaque Built-in Standard materials and three original textures. An explicit **Tools > Runaway Chimps > Environment > Place Regeneration Lab Review...** window adds a new group of linked prefab instances to the chosen loaded scene with Undo; it never rebuilds existing placements, opens/saves scenes or runs on Editor startup. The actual Level 1 scene remains unchanged. See the [kit guide and measured inventory](regeneration-lab-prop-kit.md).

**Proposed dressing, not new lore:** the short labels, inert sample forms, surface wear, delivery layout and preview arrangement are art proposals. No subject number, staff identity, exact treatment sequence or connection to the individual Crawler is established. There are no broken restraints, escape trails or second Crawler origin. Existing cards, readers, doors, safe boundaries, arrival markers and monster behavior are not modified.

**Validation boundary:** the produced mesh data, UVs, normals, dimensions, pivots, material/texture bindings, simple collider bounds and GUIDs were inspected with offline tools; VTK previews render those actual serialized assets. These are not Unity screenshots. Unity import/Editor compilation, placement Undo/Redo, Gorilla-relative scale, Level 1 lighting/readability, both safe-room approaches, collision comfort and target Quest performance remain pending. Prefab availability does not mean the room is finished or its final placements are approved.

'''
PLAN_SECTION='''## September 30 regeneration laboratory kit — separate environment-art review

**Confirmed scope:** Greg selected the three-piece first kit: treatment trolley, chemical-delivery stand and specimen cold-storage cabinet. Waste handling and a restrained diagram remain optional/deferred. Other chats' toys, specimen interactions and facility announcements are not dependencies. Preserve the regeneration/tissue-damage room theme and all existing safety/objective rules; exact labels, wear and arrangement are still proposed dressing, not a named subject or Crawler history.

**Implemented on `art/regeneration-lab-prop-kit`, not yet merged:** original baked meshes, assigned opaque Standard materials/textures, separate static prefabs, floor-contact pivots and coarse box collision. The explicit environment placement window adds only a fresh linked arrangement to a selected loaded scene, supports grouped Undo and does not save scenes. No scene, keycard, reader, door, boundary, arrival, networking, runtime script, package, XR or render-pipeline changes are part of this kit. The optional supporting props were not added in preference to completing the core three.

**Source baseline:** branch starts at `930a8f830cae99e06dcaa0a78ab9edc968c42ae5`; `ProjectVersion.txt` declares Unity 2022.3.62f3 (`96770f904ca7`), GraphicsSettings and every quality-tier pipeline reference are null, and existing native-mesh/Standard-material conventions were used. Inspected the actual `SmallRoom` floor/casing and the persistent Bootstrap Gorilla rig rather than choosing dimensions from old planning sketches. Historical URP assumptions in the earlier material plan remain superseded.

**Executed asset checks:** independent native-Mesh read-back validates indexed topology, nondegenerate geometry/UVs, normal winding, unit normals/tangent orthogonality, meter-scale bounds and floor pivots; validates prefab material/mesh references, static collider proxies and unique metadata identities. Nine negative-control corruptions are rejected. Original output was visually inspected in VTK front/rear and individual-prop renders, including correction of an initially folded handle bend and thin-tube atlas bleed. See [inventory, previews and acceptance](regeneration-lab-prop-kit.md) and [machine-readable read-back evidence](regeneration-lab-prop-kit-validation.json). Counts are not measured Quest performance.

**Still open:** import/compile on the real 62f3 project; non-magenta rendering; menu target-scene and Undo/Redo testing; player-relative scale and labels under actual Level 1 lighting; unobstructed two-route safe-room navigation and unchanged fixed-card access; comfortable Gorilla hand/head/body contact; and target Quest CPU/GPU/memory measurements. Final room placement, lighting, composition and any additional storytelling props remain a separate review. Do not close the room-authoring work merely because reusable assets exist.

'''


def write(repo):
    paths=[repo/'docs/design-and-lore.md',repo/'docs/repository-improvement-plan.md']
    for path,section in zip(paths,(DESIGN_SECTION,PLAN_SECTION)):
        text=path.read_text(encoding='utf-8')
        if section.splitlines()[0] in text:raise RuntimeError('Documentation section already exists: '+str(path))
        text=text.replace('Last updated: 2026-09-22.','Last updated: 2026-09-30.',1)
        marker='## September 22 Unity 62f3 baseline adoption'
        if text.count(marker)!=1:raise RuntimeError('Unexpected documentation baseline: '+str(path))
        text=text.replace(marker,section+marker,1)
        if path.name=='design-and-lore.md':
            old='**Proposed dressing, not yet locked:** a restraint/testing platform, chemical-delivery equipment, specimen cold storage, biological-waste containers, skeletal/regeneration diagrams, utility carts, treatment supplies, and a believable staff-owned card presentation such as a lab coat, clipboard, badge reel, or workstation dock. Exact props, labels, subject IDs, card placement, lighting, and room layout remain open for the environment-art pass.'
            new='**September 30 refinement:** the treatment trolley, chemical-delivery stand and specimen cold-storage cabinet are confirmed first-kit choices and now have review assets; see the kit checkpoint above. Optional waste handling, diagrams and any further testing hardware remain proposals. Labels, wear and final arrangement still need review. The kit does not authorize card relocation, a restraint escape story, a subject ID or a specific Crawler treatment history; existing objective placement and safe-room layout are preserved.'
            if old not in text:raise RuntimeError('Expected design dressing paragraph not found')
            text=text.replace(old,new,1)
        else:
            old='**Planned environment work, not implemented:** dress the room with a restrained low-poly laboratory language that communicates regeneration testing without another monitor wall: possible testing/restraint hardware, chemical-delivery equipment, sample/cold storage, biological-waste handling, anatomy/regeneration diagrams, carts and treatment supplies. Integrate the fixed card into a believable staff-owned location such as a lab coat, clipboard, badge reel or workstation dock. Those individual prop choices are proposals until the room layout/art pass confirms them.'
            new='**September 30 refinement of this earlier plan:** the first three prop choices (treatment trolley, chemical-delivery stand and specimen cold-storage cabinet) are confirmed and implemented as unmerged review assets, not final scene dressing. Other hardware, waste handling, diagrams and alternative staff-card presentation remain deferred proposals. This kit does not relocate any card or approve a new room arrangement. See the September 30 checkpoint above for the actual assets and pending acceptance.'
            if old not in text:raise RuntimeError('Expected plan dressing paragraph not found')
            text=text.replace(old,new,1)
        path.write_text(text,encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--repo',type=Path,required=True)
    args=parser.parse_args();write(args.repo)
