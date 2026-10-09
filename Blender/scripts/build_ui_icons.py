"""
Arayüz ikonları (kalp ikonuyla aynı dil: kabarık, parlak, 3D render, şeffaf 256 px) → Assets/Art/UI/Icons:
  • star.png  — altın yıldız (bölüm yıldızları; boşu arayüzde koyu tonla boyanır)
  • lock.png  — bakır asma kilit (kilitli bölüm)
Çalıştır: blender -b -P Blender/scripts/build_ui_icons.py
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
ICONS = os.path.join(ROOT, "Assets", "Art", "UI", "Icons")


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def mat(name, col, rough=0.25, metal=0.0, coat=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = srgb(col)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if "Coat Weight" in b.inputs:
        b.inputs["Coat Weight"].default_value = coat
    return m


def inside(poly, px, pz):
    c = False
    for i in range(len(poly)):
        (x1, z1), (x2, z2) = poly[i], poly[i - 1]
        if (z1 > pz) != (z2 > pz) and px < (x2 - x1) * (pz - z1) / (z2 - z1 + 1e-9) + x1:
            c = not c
    return c


def puffy(name, outline, material, radius=0.26, step=0.08, flat=0.55):
    """Kalp ikonundaki gibi: kenar + iç metaball'ları → yastık gövde."""
    mb = bpy.data.metaballs.new(name + "Meta")
    mb.resolution = mb.render_resolution = 0.022
    mb.threshold = 0.6
    for x, z in outline[::2]:
        e = mb.elements.new()
        e.co = (x * 0.86, 0, z * 0.86)
        e.radius = radius
    xs = [p[0] for p in outline]
    zs = [p[1] for p in outline]
    gx = min(xs)
    while gx <= max(xs):
        gz = min(zs)
        while gz <= max(zs):
            if inside(outline, gx / 0.85, gz / 0.85):
                e = mb.elements.new()
                e.co = (gx, 0, gz)
                e.radius = radius
            gz += step
        gx += step
    mo = bpy.data.objects.new(name, mb)
    bpy.context.collection.objects.link(mo)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = mo
    mo.select_set(True)
    bpy.context.view_layer.update()
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.view_layer.objects.active
    o.scale = (1.0, flat, 1.0)
    bpy.ops.object.transform_apply(scale=True)
    o.data.materials.clear()
    o.data.materials.append(material)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def setup():
    sc = bpy.context.scene
    engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    sc.render.resolution_x = sc.render.resolution_y = 256
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("W")
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.55, 0.58, 0.65, 1)
    bg.inputs["Strength"].default_value = 0.35
    sc.world = w
    for name, loc, e, s in (("Key", (-2.5, -4, 4), 900, 3), ("Rim", (3, 3, 2.5), 600, 2), ("Fill", (3, -4, -1), 250, 4)):
        ld = bpy.data.lights.new(name, "AREA")
        ld.energy, ld.size = e, s
        lo = bpy.data.objects.new(name, ld)
        lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(lo)
    cd = bpy.data.cameras.new("C")
    cd.type = "ORTHO"
    cam = bpy.data.objects.new("C", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


def shoot(cam, target, name, view=(0.12, 1.0, 0.22)):
    sc = bpy.context.scene
    for o in sc.objects:
        if o.type == "MESH":
            o.hide_render = o is not target
    pts = [target.matrix_world @ Vector(c) for c in target.bound_box]
    c = sum(pts, Vector()) / 8
    d = Vector(view).normalized()
    cam.location = c - d * 10
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    cam.data.ortho_scale = max((p - c).length for p in pts) * 2.0 * 0.82
    sc.render.filepath = os.path.join(ICONS, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("ICON", name)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(ICONS, exist_ok=True)
    gold = mat("Gold", "#F2B533", 0.2, 0.35)
    star_pts = []
    for k in range(10):
        a = math.pi / 2 + k * math.pi / 5
        r = 1.0 if k % 2 == 0 else 0.46
        star_pts.append((r * math.cos(a), r * math.sin(a)))
    # kenarları sıkılaştır: köşeler arası ara noktalar
    dense = []
    for i in range(10):
        a, b = star_pts[i], star_pts[(i + 1) % 10]
        for t in (0.0, 0.33, 0.66):
            dense.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    star = puffy("Star", dense, gold, radius=0.24, step=0.09)
    cam = setup()
    shoot(cam, star, "star")
    # asma kilit: gövde + kemer
    copper = mat("Copper", "#C46A33", 0.3, 0.8)
    steel = mat("Steel", "#BFC6CC", 0.25, 0.9)
    body_out = [(-0.6, -0.55), (0.6, -0.55), (0.6, 0.35), (-0.6, 0.35)]
    dense = []
    for i in range(4):
        a, b = body_out[i], body_out[(i + 1) % 4]
        for t in (0.0, 0.25, 0.5, 0.75):
            dense.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    body = puffy("LockBody", dense, copper, radius=0.22, step=0.1, flat=0.7)
    bm = bmesh.new()
    seg, tube = 24, 10
    R, rr = 0.38, 0.09
    grid = []
    for i in range(seg + 1):
        a = math.pi * i / seg
        row = []
        for j in range(tube):
            t = 2 * math.pi * j / tube
            x = (R + rr * math.cos(t)) * math.cos(a)
            z = (R + rr * math.cos(t)) * math.sin(a) + 0.35
            y = rr * math.sin(t)
            row.append(bm.verts.new((x, y, z)))
        grid.append(row)
    for i in range(seg):
        for j in range(tube):
            bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][(j + 1) % tube], grid[i][(j + 1) % tube]))
    me = bpy.data.meshes.new("Shackle")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(steel)
    for p in me.polygons:
        p.use_smooth = True
    sh = bpy.data.objects.new("Shackle", me)
    bpy.context.collection.objects.link(sh)
    hole = bpy.data.objects.new("Hole", bpy.data.meshes.new("Hole"))
    bpy.ops.object.select_all(action="DESELECT")
    sh.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    shoot(cam, body, "lock", view=(0.0, 1.0, 0.1))
    print("UI_ICONS_OK")


main()
