"""Rendus de contrôle (Blender en module Python « bpy », Cycles CPU) ; positions en repère Unity."""
import bpy, numpy as np, math, mathutils

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 24
    try:
        sc.cycles.use_denoising = True
    except Exception:
        pass
    sc.view_settings.view_transform = 'AgX' if 'AgX' in [i.identifier for i in sc.view_settings.bl_rna.properties['view_transform'].enum_items] else 'Filmic'
    world = bpy.data.worlds.new("W"); sc.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.05, 0.055, 0.07, 1)
    world.node_tree.nodes['Background'].inputs[1].default_value = 1.0
    return sc

def u2b(p):
    p = np.asarray(p, dtype=float)
    return np.stack([p[..., 0], p[..., 2], p[..., 1]], axis=-1)

def material(name, color, rough=0.5, sss=0.0, image=None, normal=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*color, 1)
    b.inputs['Roughness'].default_value = rough
    if sss > 0:
        for key in ('Subsurface Weight', 'Subsurface'):
            if key in b.inputs:
                b.inputs[key].default_value = sss
        if 'Subsurface Radius' in b.inputs:
            b.inputs['Subsurface Radius'].default_value = (0.012, 0.005, 0.003)
        if 'Subsurface Scale' in b.inputs:
            b.inputs['Subsurface Scale'].default_value = 0.6
    if image:
        tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(image)
        nt.links.new(tex.outputs['Color'], b.inputs['Base Color'])
    if normal:
        tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(normal)
        tex.image.colorspace_settings.name = 'Non-Color'
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(tex.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
    return m

def add_mesh(name, positions, uvs, submeshes, materials):
    me = bpy.data.meshes.new(name)
    verts = u2b(positions).tolist()
    faces = []; mat_idx = []
    names = list(submeshes.keys())
    for k, n in enumerate(names):
        for t in submeshes[n]:
            faces.append(t); mat_idx.append(k)
    me.from_pydata(verts, [], faces)
    me.update()
    uvl = me.uv_layers.new(name="UV")
    loops_v = np.zeros(len(me.loops), dtype=int); me.loops.foreach_get("vertex_index", loops_v)
    uvl.data.foreach_set("uv", np.asarray(uvs)[loops_v].reshape(-1))
    me.polygons.foreach_set("material_index", mat_idx)
    me.polygons.foreach_set("use_smooth", [True]*len(me.polygons))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    for n in names:
        ob.data.materials.append(materials.get(n) or materials['*'])
    return ob

def camera(pos_u, look_u, fov=50, clip=0.01):
    cam = bpy.data.cameras.new("C"); cam.angle = math.radians(fov); cam.clip_start = clip
    ob = bpy.data.objects.new("C", cam); bpy.context.scene.collection.objects.link(ob)
    p = mathutils.Vector(u2b(pos_u).tolist()); t = mathutils.Vector(u2b(look_u).tolist())
    ob.location = p
    ob.rotation_euler = (t - p).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.camera = ob
    return ob

def light(kind, pos_u, energy, color=(1,1,1), size=0.5, look_u=None):
    l = bpy.data.lights.new("L", kind); l.energy = energy; l.color = color
    if hasattr(l, 'size'): l.size = size
    ob = bpy.data.objects.new("L", l); bpy.context.scene.collection.objects.link(ob)
    ob.location = mathutils.Vector(u2b(pos_u).tolist())
    if look_u is not None:
        t = mathutils.Vector(u2b(look_u).tolist())
        ob.rotation_euler = (t - ob.location).to_track_quat('-Z', 'Y').to_euler()
    return ob

def render(path, w=900, h=900):
    sc = bpy.context.scene
    sc.render.resolution_x = w; sc.render.resolution_y = h; sc.render.resolution_percentage = 100
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)

def add_mesh_fv(name, positions, faces, face_uvs, face_mats, materials):
    """Faces (ordre MakeHuman) avec UV par coin ; face_mats = nom de matiere par face."""
    me = bpy.data.meshes.new(name)
    me.from_pydata(u2b(positions).tolist(), [], [list(map(int, f)) for f in faces])
    me.update()
    uvl = me.uv_layers.new(name="UV")
    flat = np.concatenate([np.asarray(u, dtype=float) for u in face_uvs])
    uvl.data.foreach_set("uv", flat.reshape(-1))
    names = sorted(set(face_mats))
    idx = {n: i for i, n in enumerate(names)}
    me.polygons.foreach_set("material_index", [idx[m] for m in face_mats])
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    for n in names:
        ob.data.materials.append(materials.get(n) or materials['*'])
    return ob
