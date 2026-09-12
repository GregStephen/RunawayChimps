import bpy
import math
import os
import sys
from mathutils import Vector

OUT_DIR = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp/vent_blower_out'
os.makedirs(OUT_DIR, exist_ok=True)
BLEND_PATH = os.path.join(OUT_DIR, 'RunawayChimps_VentBlower.blend')
FBX_PATH = os.path.join(OUT_DIR, 'RunawayChimps_VentBlower.fbx')
PREVIEW_PATH = os.path.join(OUT_DIR, 'RunawayChimps_VentBlower_Preview.png')
README_PATH = os.path.join(OUT_DIR, 'README.txt')

# Clean scene
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    pass

scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.render.resolution_x = 800
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False

asset_collection = bpy.data.collections.new('VENT_BLOWER_ASSET')
scene.collection.children.link(asset_collection)
preview_collection = bpy.data.collections.new('PREVIEW_ONLY')
scene.collection.children.link(preview_collection)


def move_to_collection(obj, collection):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    collection.objects.link(obj)


def make_mat(name, color, metallic=0.0, roughness=0.7, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    if emission:
        bsdf.inputs['Emission Color'].default_value = (*emission, 1.0)
        bsdf.inputs['Emission Strength'].default_value = emission_strength
    return mat


def add_bevel(obj, amount=0.02, segments=1):
    mod = obj.modifiers.new('Small bevels', 'BEVEL')
    mod.width = amount
    mod.segments = segments
    mod.limit_method = 'ANGLE'
    return mod


def add_cube(name, loc, scale, mat, parent=None, bevel=0.015):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        add_bevel(obj, bevel, 1)
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    move_to_collection(obj, asset_collection)
    return obj


def add_cylinder(name, loc, radius, depth, mat, parent=None, rotation=(math.radians(90), 0, 0), vertices=16, bevel=0.008):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    if bevel:
        add_bevel(obj, bevel, 1)
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    move_to_collection(obj, asset_collection)
    return obj


def add_torus(name, loc, major, minor, mat, parent=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=24, minor_segments=6,
                                    location=loc, rotation=(math.radians(90), 0, 0))
    obj = bpy.context.object
    obj.name = name
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    move_to_collection(obj, asset_collection)
    return obj


def add_rod(name, start, end, radius, mat, parent=None, vertices=8):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=direction.length, location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = direction.to_track_quat('Z', 'Y')
    obj.rotation_mode = 'XYZ'
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    move_to_collection(obj, asset_collection)
    return obj


def make_blade_mesh(name, mat, parent, angle):
    # Coarse trapezoid blade, intentionally blocky rather than polished.
    y0, y1 = -0.345, -0.285
    verts = [
        (-0.055, y0, 0.105), (0.055, y0, 0.105), (0.145, y0, 0.47), (-0.075, y0, 0.47),
        (-0.055, y1, 0.105), (0.055, y1, 0.105), (0.145, y1, 0.47), (-0.075, y1, 0.47),
    ]
    faces = [
        (0,1,2,3), (4,7,6,5), (0,4,5,1), (1,5,6,2), (2,6,7,3), (4,0,3,7)
    ]
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    asset_collection.objects.link(obj)
    obj.parent = parent
    obj.rotation_euler[1] = angle
    obj.data.materials.append(mat)
    add_bevel(obj, 0.008, 1)
    return obj


def add_curve_conduit(name, points, bevel_depth, mat, parent=None):
    curve = bpy.data.curves.new(name + '_Curve', type='CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 1
    curve.bevel_depth = bevel_depth
    curve.bevel_resolution = 1
    poly = curve.splines.new('POLY')
    poly.points.add(len(points) - 1)
    for p, co in zip(poly.points, points):
        p.co = (*co, 1.0)
    obj = bpy.data.objects.new(name, curve)
    asset_collection.objects.link(obj)
    if parent:
        obj.parent = parent
    obj.data.materials.append(mat)
    return obj


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

# Materials: intentionally simple, no texture maps.
mat_dark = make_mat('M_DarkPaintedMetal', (0.075, 0.082, 0.085), metallic=0.25, roughness=0.82)
mat_metal = make_mat('M_DullSteel', (0.17, 0.18, 0.18), metallic=0.55, roughness=0.74)
mat_blade = make_mat('M_FanBlade', (0.12, 0.125, 0.12), metallic=0.45, roughness=0.79)
mat_rubber = make_mat('M_Conduit', (0.035, 0.038, 0.04), metallic=0.1, roughness=0.9)
mat_red = make_mat('M_RedMaintenanceLens', (0.35, 0.01, 0.008), metallic=0.0, roughness=0.45,
                   emission=(1.0, 0.015, 0.01), emission_strength=2.0)

root = bpy.data.objects.new('VentBlower_Root', None)
asset_collection.objects.link(root)
root.empty_display_type = 'PLAIN_AXES'
root['unity_note'] = 'Wall plane is local Y=0; asset extends toward negative Y.'
root['fan_rotor_object'] = 'FanRotor'
root['style'] = 'Runaway Chimps simple industrial prototype prop'

# Back plate and chunky housing.
add_cube('Housing', (0, -0.14, 0), (1.25, 0.28, 1.18), mat_dark, root, 0.025)
add_cube('Housing_LeftLip', (-0.59, -0.29, 0), (0.07, 0.11, 1.04), mat_metal, root, 0.01)
add_cube('Housing_RightLip', (0.59, -0.29, 0), (0.07, 0.11, 1.04), mat_metal, root, 0.01)

# Mounting brackets that read clearly at VR distance.
for idx, (x, z) in enumerate([(-0.53, 0.68), (0.53, 0.68), (-0.53, -0.68), (0.53, -0.68)], 1):
    add_cube(f'Mount_{idx:02d}', (x, -0.035, z), (0.16, 0.07, 0.26), mat_dark, root, 0.012)
    add_cylinder(f'MountBolt_{idx:02d}', (x, -0.08, z), 0.04, 0.035, mat_metal, root,
                 rotation=(math.radians(90), 0, 0), vertices=8, bevel=0.004)

# Intake surround.
add_torus('FanOuterRing', (0, -0.32, 0), 0.51, 0.055, mat_metal, root)
add_cylinder('FanRecess', (0, -0.285, 0), 0.49, 0.035, mat_rubber, root, vertices=24, bevel=0.004)

# Rotor hierarchy is clean for Unity animation.
rotor = bpy.data.objects.new('FanRotor', None)
asset_collection.objects.link(rotor)
rotor.parent = root
rotor.rotation_mode = 'XYZ'
for i in range(6):
    make_blade_mesh(f'Blade_{i+1:02d}', mat_blade, rotor, math.radians(i * 60.0))
add_cylinder('FanHub', (0, -0.34, 0), 0.14, 0.09, mat_metal, rotor, vertices=16, bevel=0.01)
add_cylinder('HubCap', (0, -0.395, 0), 0.09, 0.025, mat_dark, rotor, vertices=12, bevel=0.006)

# Coarse safety guard: three rings and six spokes.
guard_y = -0.42
for i, r in enumerate((0.20, 0.35, 0.50), 1):
    add_torus(f'GuardRing_{i:02d}', (0, guard_y, 0), r, 0.011, mat_metal, root)
for i in range(6):
    a = math.radians(i * 60.0)
    end = (math.sin(a) * 0.50, guard_y, math.cos(a) * 0.50)
    add_rod(f'GuardSpoke_{i+1:02d}', (0, guard_y, 0), end, 0.012, mat_metal, root, 8)

# Side motor/control block. Keep this deliberately boxy.
add_cube('MotorBox', (0.53, -0.06, -0.06), (0.26, 0.34, 0.42), mat_dark, root, 0.02)
add_cube('MotorCap', (0.67, -0.07, -0.06), (0.08, 0.27, 0.30), mat_metal, root, 0.015)

# Simple conduit with only a few bends.
add_curve_conduit('PowerConduit', [
    (0.61, -0.11, -0.24),
    (0.69, -0.10, -0.34),
    (0.76, -0.07, -0.50),
    (0.78, -0.03, -0.67),
], 0.028, mat_rubber, root)
add_cube('ConduitWallPlate', (0.78, -0.025, -0.70), (0.16, 0.055, 0.14), mat_dark, root, 0.01)

# Small maintenance lamp, not a fancy fixture.
add_cube('RedLightHousing', (-0.47, -0.34, 0.47), (0.19, 0.10, 0.26), mat_dark, root, 0.012)
add_cylinder('RedLightLens', (-0.47, -0.405, 0.47), 0.065, 0.045, mat_red, root, vertices=12, bevel=0.004)
for xoff in (-0.075, 0.075):
    add_rod('LampGuard_L' if xoff < 0 else 'LampGuard_R', (-0.47 + xoff, -0.44, 0.37),
            (-0.47 + xoff, -0.44, 0.57), 0.008, mat_metal, root, 6)

# Actual red point light stays in the .blend for reference; Unity can replace/control it.
bpy.ops.object.light_add(type='POINT', location=(-0.47, -0.49, 0.47))
red_light = bpy.context.object
red_light.name = 'RedMaintenanceLight_REFERENCE'
red_light.data.color = (1.0, 0.015, 0.01)
red_light.data.energy = 22.0
red_light.data.shadow_soft_size = 0.25
red_light.parent = root
move_to_collection(red_light, asset_collection)

# Preview-only wall, camera and lights.
preview_mat = make_mat('PREVIEW_Wall', (0.025, 0.028, 0.03), metallic=0.0, roughness=0.95)
wall = add_cube('PREVIEW_Wall', (0, 0.06, 0), (3.2, 0.08, 3.0), preview_mat, None, 0)
move_to_collection(wall, preview_collection)

bpy.ops.object.light_add(type='AREA', location=(2.2, -2.3, 2.1))
key = bpy.context.object
key.name = 'PREVIEW_Key'
key.data.energy = 600
key.data.shape = 'DISK'
key.data.size = 2.2
look_at(key, (0, -0.2, 0))
move_to_collection(key, preview_collection)

bpy.ops.object.light_add(type='AREA', location=(-2.0, -1.2, 0.8))
fill = bpy.context.object
fill.name = 'PREVIEW_Fill'
fill.data.energy = 250
fill.data.size = 1.8
look_at(fill, (0, -0.25, 0))
move_to_collection(fill, preview_collection)

bpy.ops.object.camera_add(location=(2.25, -2.65, 1.55))
cam = bpy.context.object
cam.name = 'PREVIEW_Camera'
cam.data.lens = 56
look_at(cam, (0, -0.20, 0.02))
scene.camera = cam
move_to_collection(cam, preview_collection)

scene.world.color = (0.012, 0.014, 0.016)
scene.render.filepath = PREVIEW_PATH
bpy.ops.render.render(write_still=True)

# Remove preview helpers before saving the deliverable .blend.
for obj in list(preview_collection.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
if preview_collection.name in bpy.data.collections:
    bpy.data.collections.remove(preview_collection)
if preview_mat.name in bpy.data.materials:
    bpy.data.materials.remove(preview_mat)

# Save clean editable source.
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

# Export FBX: no camera/lights, retain empty hierarchy so FanRotor remains independently rotatable.
bpy.ops.object.select_all(action='DESELECT')
for obj in asset_collection.all_objects:
    if obj.type in {'MESH', 'EMPTY'}:
        obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={'MESH', 'EMPTY'},
    apply_unit_scale=True,
    use_space_transform=True,
    axis_forward='-Z',
    axis_up='Y',
    add_leaf_bones=False,
    bake_anim=False,
    path_mode='AUTO',
    embed_textures=False,
)

# Delivery notes.
with open(README_PATH, 'w', encoding='utf-8') as f:
    f.write('Runaway Chimps - Vent Blower\n')
    f.write('Built with Blender 4.2.23 LTS.\n\n')
    f.write('Files:\n')
    f.write('- RunawayChimps_VentBlower.blend: editable source asset\n')
    f.write('- RunawayChimps_VentBlower.fbx: Unity-ready export\n')
    f.write('- RunawayChimps_VentBlower_Preview.png: quick reference render\n\n')
    f.write('Design intent:\n')
    f.write('- deliberately simple/chunky Runaway Chimps industrial style\n')
    f.write('- no baked grime or high-detail texture maps\n')
    f.write('- wall plane is local Y=0; prop extends toward negative Y\n')
    f.write('- overall housing is about 1.25 m wide x 1.18 m tall\n')
    f.write('- FanRotor is a separate parent object; rotate that object in Unity\n')
    f.write('- RedMaintenanceLight_REFERENCE is only a Blender reference light; Unity PR #17 owns runtime lighting/audio behavior\n')
    f.write('- static housing/mount geometry is intentionally coarse for Quest\n')

# Sanity report to workflow log.
mesh_objs = [o for o in asset_collection.all_objects if o.type == 'MESH']
triangles = 0
for obj in mesh_objs:
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    eval_mesh = evaluated.to_mesh()
    eval_mesh.calc_loop_triangles()
    triangles += len(eval_mesh.loop_triangles)
    evaluated.to_mesh_clear()
print(f'Created {len(mesh_objs)} mesh objects; approx {triangles} evaluated triangles')
print(BLEND_PATH)
print(FBX_PATH)
print(PREVIEW_PATH)
