"""Legacy v0.1 renderer: Blender --background --python render_level2.py -- OUTPUT_DIR.
Only handles the original box-only layout, not linked Unity gate assets.
"""
import bpy, json, sys, math
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent
OUT=Path(sys.argv[sys.argv.index('--')+1]) if '--' in sys.argv else HERE/'previews'
OUT.mkdir(parents=True,exist_ok=True)
d=json.loads((HERE/'layout.json').read_text())
if d.get('gates'):
    raise RuntimeError('This legacy renderer cannot resolve linked Unity gate meshes. Use the current Unity scene and overhead plan; bundled Blender previews are v0.1 references.')
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.name!='Collection': bpy.data.collections.remove(c)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1
scene.render.engine='CYCLES'; scene.cycles.samples=40
scene.cycles.use_denoising=True
scene.render.resolution_x=1600; scene.render.resolution_y=1200; scene.render.resolution_percentage=100
scene.world.color=(.25,.25,.25)
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
mats={}
for name,c in d['colors'].items():
    m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Roughness'].default_value=.78
    if name=='LightStrip':
        bs.inputs['Emission Color'].default_value=(*c,1);bs.inputs['Emission Strength'].default_value=3
    mats[name]=m
collections={}
byid={n['id']:n for n in d['nodes']}
objects={}
def collection_for(n):
    p=n
    while p['parent'] and byid[p['parent']]['parent']:
        p=byid[p['parent']]
    key=p['name']
    if key not in collections:
        c=bpy.data.collections.new(key);scene.collection.children.link(c);collections[key]=c
    return collections[key]
def to_blender(v): return (v[0],v[2],v[1])
for n in d['nodes']:
    if n['kind']=='light':continue
    c=collection_for(n)
    if n['material']:
        bpy.ops.mesh.primitive_cube_add(size=1,location=to_blender(n['position']))
        o=bpy.context.object;o.name=n['name'];o.dimensions=to_blender(n['scale'])
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        o.data.materials.append(mats[n['material']])
    else:
        o=bpy.data.objects.new(n['name'],None);scene.collection.objects.link(o);o.location=to_blender(n['position'])
        o.empty_display_type='PLAIN_AXES';o.empty_display_size=.3
    for old in list(o.users_collection):old.objects.unlink(o)
    c.objects.link(o)
    o['unity_name']=n['name'];o['unity_box_collider']=n['collider'];o['marker_only']=n['trigger']
    objects[n['id']]=o
    if '09_Listener' in c.name:c.hide_render=True;c.hide_viewport=True
def camera(name,pos,target,ortho=None):
    data=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,data);scene.collection.objects.link(o)
    o.location=pos;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    if ortho:data.type='ORTHO';data.ortho_scale=ortho
    else:data.lens=23
    data.clip_start=.05;data.clip_end=150
    return o
cam=camera('Overview_Cutaway',(29,-29,32),(8,5,0),29)
interior=camera('Hall_Eye_Level',(3,1.15,1.7),(12,10.3,1.8))
repaircam=camera('Repair_Eye_Level',(16.5,11.0,1.7),(13.5,13.5,1.3))
def area(name,pos,power,size,target):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.location=pos
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();return o
studio=[]
studio.append(area('Preview_Studio_Key',(3,-3,20),3500,14,(8,5,0)))
studio.append(area('Preview_Studio_Fill',(20,14,14),2400,10,(8,5,0)))
roomlights=[]
for x,z in [(3,-2),(3,12),(3,5),(9,8),(16,5),(14,12)]:
    roomlights.append(area('Preview_Ceiling_Light',(x,z,3.98),130,2.8,(x,z,0)))
scene.camera=cam
# Save full-height geometry, editable by named collections; roof hidden only in viewport.
for c in collections.values():
    if c.name.startswith('03_Ceilings'):c.hide_viewport=True
for a in bpy.context.screen.areas if bpy.context.screen else []:
    if a.type=='VIEW_3D':
        a.spaces.active.region_3d.view_distance=28
        a.spaces.active.region_3d.view_location=Vector((9,5,0))
notes=bpy.data.texts.new('START_HERE')
notes.write('Runaway Chimps Level 2 map blockout v0.1\nSame box geometry as the Unity package; 1 Blender unit = 1 metre.\nCeilings are hidden in the viewport for inspection. Toggle collection 03.\nThe overview is a cutaway preview. Full-height walls are saved here.\nGameplay markers, safety, repair and shutter are unwired placeholders.\nPreview lights are presentation-only; use the Unity scene for game testing.\n')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Level2_BehavioralConditioning.blend'))

# Cutaway is a rendering choice only, not an alteration to the saved asset.
restore=[]
for n in d['nodes']:
    o=objects.get(n['id'])
    if not o or not n['material']:continue
    c=o.users_collection[0].name
    if c.startswith('03_') or n['name'].startswith(('Ceiling_Light','Conduit_','Entry_Door_Lintel','Exit_Door_Lintel','Repair_Doors_Lintel')):
        restore.append((o,o.hide_render,o.location.copy(),o.scale.copy()));o.hide_render=True
    elif c.startswith('02_') or c.startswith('04_') or c.startswith('06_') or '_Trim_' in n['name'] or n['name'].startswith('Obstacle_'):
        restore.append((o,o.hide_render,o.location.copy(),o.scale.copy()))
        bottom=n['position'][1]-n['scale'][1]/2
        if bottom>=1.35:o.hide_render=True
        elif n['position'][1]+n['scale'][1]/2>1.35:
            h=1.35-bottom;o.scale.z=h/n['scale'][1];o.location.z=bottom+h/2
scene.render.filepath=str(OUT/'Level2_Cutaway.png');bpy.ops.render.render(write_still=True)
for o,hide,loc,scale in restore:o.hide_render=hide;o.location=loc;o.scale=scale
for o in studio:o.hide_render=True
for c in collections.values():
    if c.name.startswith('03_'):c.hide_render=False
scene.render.resolution_x=1500;scene.render.resolution_y=1000
scene.camera=interior;scene.render.filepath=str(OUT/'Level2_Hall_View.png');bpy.ops.render.render(write_still=True)
scene.camera=repaircam;scene.render.filepath=str(OUT/'Level2_Repair_View.png');bpy.ops.render.render(write_still=True)
print('RENDERS_COMPLETE')
