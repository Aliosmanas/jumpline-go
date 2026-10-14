"""Mixamo iskeletini (Idle_skin.fbx: sade gövde + iskelet + ağırlık) dokulu asıl modele aktarır, klipleri ekler.
Kullanım: blender -b -P rig_from_mixamo.py -- <hedef_ad> [kaynak_klasor]
  hedef_ad: meshy_processed/<ad>.blend (dokulu model).  Çıktı: Blender/rigged/<ad>.fbx + .blend"""
import bpy, os, sys, glob
ROOT = r"D:\Repolar\junpline"
args = sys.argv[sys.argv.index("--") + 1:]
NAME = args[0]; SRC = os.path.join(ROOT, "Blender", "mixamo_anims", args[1] if len(args) > 1 else "Baran")
CLIPS = args[2].split(",") if len(args) > 2 else None   # boşsa klasördeki hepsi
OUT = os.path.join(ROOT, "Blender", "rigged")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps = 30
# 1) iskeletli sade gövde
bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, "Idle_skin.fbx"), automatic_bone_orientation=False)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
proxy = next(o for o in bpy.context.scene.objects if o.type == "MESH")
arm.name = NAME + "Rig"
idle_act = arm.animation_data.action if arm.animation_data else None
if arm.animation_data: arm.animation_data.action = None
# bind pozuna dön
for pb in arm.pose.bones: pb.matrix_basis.identity()
bpy.context.view_layer.update()
# 2) dokulu asıl model
with bpy.data.libraries.load(os.path.join(ROOT, "Blender", "meshy_processed", NAME + ".blend")) as (src, dst):
    dst.objects = [n for n in src.objects]
body = None
for o in dst.objects:
    if o and o.type == "MESH":
        bpy.context.scene.collection.objects.link(o); body = o
body.name = NAME
# mixamo gövdesinin dünya boyuna/yerine oturt (FBX ölçeği farkı)
def bb(o):
    import mathutils
    pts = [o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]
    return min(p.z for p in pts), max(p.z for p in pts), sum(p.x for p in pts)/8, sum(p.y for p in pts)/8
pz0, pz1, px, py = bb(proxy); bz0, bz1, bx, by = bb(body)
s = 1.0 if os.environ.get("FIT") == "1" else (pz1 - pz0) / (bz1 - bz0)
body.scale = (s, s, s); bpy.context.view_layer.update()
bz0, bz1, bx, by = bb(body)
body.location.x += px - bx; body.location.y += py - by; body.location.z += pz0 - bz0
bpy.context.view_layer.update()
bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); bpy.context.view_layer.objects.active = body
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
# 2b) FIT=1: iskeleti farklı orantılı gövdeye (canavar) oturt — sade Baran gövdesi ile hedefin ölçüleri karşılaştırılır
import numpy as np, mathutils
def dense_verts(o):
    t = o.copy(); t.data = o.data.copy(); bpy.context.scene.collection.objects.link(t)
    t.vertex_groups.clear(); t.modifiers.clear()
    r = t.modifiers.new("r", "REMESH"); r.mode = "VOXEL"; r.voxel_size = o.dimensions.z / 300
    dg = bpy.context.evaluated_depsgraph_get(); e = t.evaluated_get(dg); me = e.to_mesh()
    v = np.array([tuple(t.matrix_world @ x.co) for x in me.vertices]); e.to_mesh_clear()
    bpy.data.objects.remove(t, do_unlink=True); return v
def measure(o):
    v = dense_verts(o)
    x, y, z = v[:, 0], v[:, 1], v[:, 2]; z0 = z.min(); H = z.max() - z0
    cx = float(np.median(x[(z > z0 + 0.6 * H) & (z < z0 + 0.8 * H)])); cy = float(np.median(y[(z > z0 + 0.5 * H) & (z < z0 + 0.8 * H)]))
    crotch = None
    run = 0
    for f in np.arange(0.62, 0.25, -0.005):
        sl = (abs(z - (z0 + f * H)) < 0.004 * H) & (abs(x - cx) < 0.012 * H)
        run = run + 1 if sl.sum() == 0 else 0
        if run == 4: crotch = f + 0.015; break
    crotch = (crotch if crotch else 0.47) * H
    zc = z0 + crotch + 0.55 * (H - crotch)
    sl = abs(z - zc) < 0.006 * H; xs = x[sl] - cx; hw = None
    b = 0.01 * H
    for k in range(1, 200):
        if not ((xs > (k - 1) * b) & (xs < k * b)).any(): hw = (k - 1) * b; break
    hw = hw or 0.15 * H
    tips = {}
    for side in (1, -1):
        m = (z > z0 + crotch - 0.15 * H) & (z < zc) & (side * (x - cx) > hw * 1.1) & (abs(y - cy) < 0.25 * H)
        if m.any():
            i = np.argmax(side * (x - cx) * m - (~m) * 1e9); tips[side] = mathutils.Vector(v[i])
    lx = np.median(abs(x[(abs(z - (z0 + crotch * 0.55)) < 0.01 * H)] - cx))
    return dict(z0=z0, H=H, cx=cx, cy=cy, crotch=crotch, hw=hw, tips=tips, legx=float(lx))
if os.environ.get("FIT") == "1":
    A = measure(proxy); B = measure(body)
    def mz(zv):
        r = zv - A["z0"]
        if r < A["crotch"]: return B["z0"] + r * B["crotch"] / A["crotch"]
        return B["z0"] + B["crotch"] + (r - A["crotch"]) * (B["H"] - B["crotch"]) / (A["H"] - A["crotch"])
    kt = B["hw"] / A["hw"]; kl = B["legx"] / max(A["legx"], 1e-4)
    def torso(p, k): return mathutils.Vector((B["cx"] + (p.x - A["cx"]) * k, B["cy"] + (p.y - A["cy"]) * kt, mz(p.z)))
    bpy.ops.object.select_all(action="DESELECT"); arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    Mw = arm.matrix_world; Mi = Mw.inverted(); eb = arm.data.edit_bones
    for e in eb: e.use_connect = False
    W = {e.name: (Mw @ e.head, Mw @ e.tail) for e in eb}
    sh = {}
    for side, tag in ((1, "Left"), (-1, "Right")):
        # karakter -Y'ye bakar; Mixamo "Left" = karakterin solu = +X
        n = f"mixamorig:{tag}Arm"
        if n in W: sh[side] = (W[n][0], torso(W[n][0], kt))
    def new_pos(name, p):
        side = 1 if "Left" in name else (-1 if "Right" in name else 0)
        armpart = any(k in name for k in ("Arm", "Hand", "Thumb", "Index", "Middle", "Ring", "Pinky")) and "Shoulder" not in name
        if armpart and side in sh and side in A["tips"] and side in B["tips"]:
            sb, sm = sh[side]; tb, tm = A["tips"][side], B["tips"][side]
            q = (tb - sb).rotation_difference(tm - sm); sc = (tm - sm).length / max((tb - sb).length, 1e-4)
            return sm + q @ ((p - sb) * sc)
        if any(k in name for k in ("UpLeg", "Leg", "Foot", "Toe")): return torso(p, kl)
        return torso(p, kt)
    for e in eb:
        h, t = W[e.name]; e.head = Mi @ new_pos(e.name, h); e.tail = Mi @ new_pos(e.name, t)
    bpy.ops.object.mode_set(mode="OBJECT")
    os.environ["HEAT"] = "1"
    HIPS_K = B["crotch"] / A["crotch"]
else:
    HIPS_K = 1.0
# 3) ağırlık aktarımı (sade gövdeden en yakın yüz, enterpolasyonlu)
#    HEAT=1: kostümün kendi kapalı kopyasında kemik-ısı ağırlığı (pelerin/yelek kola yapışmasın)
if os.environ.get("HEAT") == "1":
    bpy.data.objects.remove(proxy, do_unlink=True)
    proxy = body.copy(); proxy.data = body.data.copy(); bpy.context.scene.collection.objects.link(proxy)
    proxy.vertex_groups.clear()
    proxy.parent = None; proxy.matrix_world = body.matrix_world.copy()
    r = proxy.modifiers.new("r", "REMESH"); r.mode = "VOXEL"; r.voxel_size = proxy.dimensions.z / 150
    bpy.ops.object.select_all(action="DESELECT"); proxy.select_set(True); bpy.context.view_layer.objects.active = proxy
    bpy.ops.object.modifier_apply(modifier="r")
    arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    mw = proxy.matrix_world.copy(); proxy.modifiers.clear(); proxy.parent = None; proxy.matrix_world = mw
    print("HEATW", sum(1 for v in proxy.data.vertices if any(g.weight > 0.01 for g in v.groups)), len(proxy.data.vertices))
    bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); bpy.context.view_layer.objects.active = body
for vg in proxy.vertex_groups: body.vertex_groups.new(name=vg.name)
m = body.modifiers.new("dt", "DATA_TRANSFER"); m.object = proxy
m.use_vert_data = True; m.data_types_verts = {"VGROUP_WEIGHTS"}; m.vert_mapping = "POLYINTERP_NEAREST"
m.layers_vgroup_select_src = "ALL"; m.layers_vgroup_select_dst = "NAME"
bpy.ops.object.modifier_apply(modifier="dt")
# ağırlıksız kalan köşeler (ince dallar, kopuk parçalar) en yakın ağırlıklı köşeden kopyalanır — yoksa sivri uzar
from mathutils import kdtree
me = body.data
wsum = [sum(g.weight for g in v.groups) for v in me.vertices]
good = [i for i, w in enumerate(wsum) if w > 0.01]
bad = [i for i, w in enumerate(wsum) if w <= 0.01]
if bad and good:
    kd = kdtree.KDTree(len(good))
    for k, i in enumerate(good): kd.insert(me.vertices[i].co, k)
    kd.balance()
    for i in bad:
        _, k, _ = kd.find(me.vertices[i].co); src = me.vertices[good[k]]
        for g in src.groups: body.vertex_groups[g.group].add([i], g.weight, "REPLACE")
print("UNWEIGHTED fixed", len(bad))
# temizle: 4 etkiden fazlasını kırp, normalize
bpy.ops.object.vertex_group_limit_total(limit=4); bpy.ops.object.vertex_group_normalize_all(lock_active=False)
am = body.modifiers.new("Armature", "ARMATURE"); am.object = arm
body.parent = arm; body.matrix_parent_inverse = arm.matrix_world.inverted()
bpy.data.objects.remove(proxy, do_unlink=True)
# 4) klipler
names = {"Idle_skin": "Idle"}
acts = []
if idle_act: idle_act.name = "Idle"; idle_act.use_fake_user = True; acts.append("Idle")
if CLIPS and "B_Idle" in CLIPS and idle_act:   # canavar: Baran'ın beklemesi yerine canavar beklemesi
    bpy.data.actions.remove(idle_act); acts.remove("Idle"); idle_act = None
for f in sorted(glob.glob(os.path.join(SRC, "*.fbx"))):
    key = os.path.basename(f)[:-4]
    if key == "Idle_skin": continue
    clip = key[2:] if key.startswith("B_") else key
    if CLIPS and key not in CLIPS: continue
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=f, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in before]
    a2 = next((o for o in new if o.type == "ARMATURE"), None)
    if a2 and a2.animation_data and a2.animation_data.action:
        act = a2.animation_data.action; act.name = clip; act.use_fake_user = True; acts.append(clip)
    for o in new: bpy.data.objects.remove(o, do_unlink=True)
# yerinde koşu: oyun ileri hareketi kodla verir → kalçanın yatay kayması silinir (ölüm/yenilgi hariç).
# Doğrudan anahtar karelerde (fcurve) yapılır — Blender 5'te poz değerlendirmesi slot bağlamasına takılıyor.
def fcurves_of(act):
    try:
        return list(act.fcurves)
    except AttributeError:
        out = []
        for l in act.layers:
            for st in l.strips:
                for cb in st.channelbags: out += list(cb.fcurves)
        return out
def lock_root(act):
    hips = next((p for p in arm.pose.bones if p.name.endswith("Hips")), None)
    if hips is None: return
    path = f'pose.bones["{hips.name}"].location'
    fc = {f.array_index: f for f in fcurves_of(act) if f.data_path == path}
    if len(fc) < 3: return
    R = arm.matrix_world.to_3x3() @ hips.bone.matrix_local.to_3x3()   # bone-yerel → dünya
    Ri = R.inverted()
    frames = sorted({round(k.co[0], 3) for f in fc.values() for k in f.keyframe_points})
    l0 = mathutils.Vector([fc[i].evaluate(frames[0]) for i in range(3)])
    newv = {}
    for fr in frames:
        l = mathutils.Vector([fc[i].evaluate(fr) for i in range(3)])
        d = R @ (l - l0); dz = d.z
        corr = mathutils.Vector((d.x, d.y, dz * (1.0 - HIPS_K)))
        newv[fr] = l - Ri @ corr
    for i in range(3):
        for k in fc[i].keyframe_points:
            v = newv.get(round(k.co[0], 3))
            if v is not None:
                k.co[1] = v[i]; k.handle_left_type = k.handle_right_type = "AUTO_CLAMPED"
        fc[i].update()
import mathutils
arm.animation_data_create()
for act in bpy.data.actions:
    if act.name not in ("Death", "Defeat"): lock_root(act)
arm.animation_data.action = None
for pb in arm.pose.bones: pb.matrix_basis.identity()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + ".blend"))
bpy.ops.object.select_all(action="DESELECT"); arm.select_set(True); body.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
    bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0, path_mode="COPY", embed_textures=True)
print("RIGGED", NAME, "verts", len(body.data.vertices), "groups", len(body.vertex_groups), "actions", acts)
