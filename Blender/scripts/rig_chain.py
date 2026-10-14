"""Yılan / ejderha gövdeli canavarlar için otomatik zincir iskelet + prosedürel animasyon (Mixamo bunları yapamaz).
  • Omurga: gövde içinden baştan kuyruk ucuna en uzun jeodezik yol → kemik zinciri (kökten başa ve kuyruğa iki kol).
  • Dallar: kanat / bacak / kol uçlarından omurgaya en kısa jeodezik yol → 2-3 kemik.
  • Ağırlık: kapalı vekil gövdede kemik-ısı + aktarım + ağırlıksız köşe düzeltme.
  • Klipler (BossChase tetikleri): Idle, Run, Roar, Throw, Hit, Defeat — 30 fps, sinüs dalgalı.
Kullanım: blender -b -P rig_chain.py -- <VanMonster|Dragon|Sahmeran>
Çıktı: Blender/rigged/<ad>.fbx + .blend"""
import bpy, os, sys, math, heapq
import numpy as np
import mathutils
from mathutils import Vector, Quaternion, Matrix, kdtree

ROOT = r"D:\Repolar\junpline"
NAME = sys.argv[sys.argv.index("--") + 1]
OUT = os.path.join(ROOT, "Blender", "rigged")
FPS = 30

CFG = {
    # head: baş noktası seçimi; spine: omurga kemik sayısı; root_at: kök (yer teması / gövde ortası)
    "VanMonster": dict(head="top", spine=16, root_at="ground", branches=[]),
    "Sahmeran": dict(head="top", spine=16, root_at="ground", branches=["arms"]),
    "Dragon": dict(head="front", spine=14, root_at="center", branches=["wings", "legs"]),
}[NAME]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps = FPS
with bpy.data.libraries.load(os.path.join(ROOT, "Blender", "meshy_processed", NAME + ".blend")) as (src, dst):
    dst.objects = list(src.objects)
body = next(o for o in dst.objects if o and o.type == "MESH")
bpy.context.scene.collection.objects.link(body)
body.name = NAME


# ---------------------------------------------------------------- yoğun nokta bulutu + jeodezik grafik
def voxel_points(o, div=110):
    t = o.copy(); t.data = o.data.copy(); bpy.context.scene.collection.objects.link(t)
    t.modifiers.clear()
    r = t.modifiers.new("r", "REMESH"); r.mode = "VOXEL"; r.voxel_size = max(o.dimensions) / div
    dg = bpy.context.evaluated_depsgraph_get(); e = t.evaluated_get(dg); me = e.to_mesh()
    pts = np.array([tuple(t.matrix_world @ v.co) for v in me.vertices])
    e.to_mesh_clear(); bpy.data.objects.remove(t, do_unlink=True)
    # ızgara seyreltme (~ yüzey + iç değil; yüzey yeterli: jeodezik yüzey boyunca)
    g = max(o.dimensions) / 70
    keys = np.floor(pts / g).astype(np.int64)
    _, idx = np.unique(keys, axis=0, return_index=True)
    return pts[np.sort(idx)], g


P, GRID = voxel_points(body)
N = len(P)
kd = kdtree.KDTree(N)
for i, p in enumerate(P): kd.insert(p, i)
kd.balance()
NB = []
for i, p in enumerate(P):
    NB.append([(j, d) for (_, j, d) in kd.find_n(p, 12) if j != i and d < GRID * 2.6])


def dijkstra(src):
    dist = np.full(N, np.inf); prev = np.full(N, -1)
    dist[src] = 0.0; h = [(0.0, src)]
    while h:
        d, i = heapq.heappop(h)
        if d > dist[i]: continue
        for j, w in NB[i]:
            nd = d + w
            if nd < dist[j]: dist[j] = nd; prev[j] = i; heapq.heappush(h, (nd, j))
    return dist, prev


def path_to(prev, j):
    out = []
    while j != -1: out.append(j); j = prev[j]
    return out[::-1]


z0 = P[:, 2].min(); H = P[:, 2].max() - z0
cx = float(np.median(P[:, 0])); cy = float(np.median(P[:, 1]))
if CFG["head"] == "top":
    head = int(np.argmax(P[:, 2]))
else:   # ejderha: yüksek bölgede en öndeki (-Y) nokta = burun
    m = P[:, 2] > z0 + 0.45 * H
    head = int(np.where(m)[0][np.argmin(P[m, 1])])
dh, ph = dijkstra(head)
fin = np.isfinite(dh)
tail = int(np.argmax(np.where(fin, dh, -1)))
spath = path_to(ph, tail)            # baş → kuyruk
L = dh[tail]


def band_centroid(path, dist, k, n, radius):
    """Yol üzerindeki k/n konumunun etrafındaki noktaların merkezi (kesitin ortası)."""
    target = L * k / n
    node = min(path, key=lambda i: abs(dist[i] - target))
    sel = [j for (_, j, d) in kd.find_range(P[node], radius) if abs(dist[j] - target) < L / n]
    return Vector(P[sel].mean(axis=0)) if sel else Vector(P[node])


R_SPINE = 0.09 * H
spine_pts = [band_centroid(spath, dh, k, CFG["spine"], R_SPINE) for k in range(CFG["spine"] + 1)]   # baş → kuyruk
spine_pts[0] = Vector(P[head]) * 0.5 + spine_pts[1] * 0.5
# kök: yer teması (dik yükselen gövdenin tabanı) ya da gövde ortası
if CFG["root_at"] == "ground":
    ri = next((k for k, p in enumerate(spine_pts) if p.z < z0 + 0.14 * H), len(spine_pts) // 2)
else:
    c = Vector((cx, cy, z0 + 0.5 * H))
    ri = min(range(len(spine_pts)), key=lambda k: (spine_pts[k] - c).length)
ri = max(2, min(ri, len(spine_pts) - 3))


def branch(tip_idx, n_bones):
    """Uçtan omurgaya en kısa jeodezik yol → n_bones kemik (omurga tarafından uca)."""
    d, pr = dijkstra(tip_idx)
    sp_nodes = [min(range(N), key=lambda i: (Vector(P[i]) - s).length_squared) for s in spine_pts]
    attach = min(sp_nodes, key=lambda i: d[i])
    path = path_to(pr, attach)[::-1]        # omurga → uç
    if len(path) < 2: return None
    dd = np.array([d[i] for i in path]); tot = dd[0]
    pts = []
    for k in range(n_bones + 1):
        tgt = tot * (1 - k / n_bones)
        node = path[int(np.argmin(abs(dd - tgt)))]
        sel = [j for (_, j, dist) in kd.find_range(P[node], 0.05 * H)]
        pts.append(Vector(P[sel].mean(axis=0)) if (sel and 0 < k < n_bones) else Vector(P[node]))
    sk = min(range(len(spine_pts)), key=lambda k: (spine_pts[k] - pts[0]).length)
    return pts, sk, d, d[attach]


branches = {}
if "wings" in CFG["branches"]:
    for side, tag in ((1, "l"), (-1, "r")):
        tip = int(np.argmax(side * (P[:, 0] - cx) + 0.2 * (P[:, 2] - z0)))
        b = branch(tip, 3)
        if b: branches["wing_" + tag] = b
if "legs" in CFG["branches"]:
    low = np.where(P[:, 2] < z0 + 0.06 * H)[0]
    for fb, ftag in ((-1, "f"), (1, "b")):          # -Y ön, +Y arka
        for side, tag in ((1, "l"), (-1, "r")):
            # kuyruk da yere değiyor → yalnız gövde altı (kuyruk ucundan uzak) ve gövde y aralığında
            cand = [i for i in low if side * (P[i, 0] - cx) > 0.05 * H and fb * (P[i, 1] - cy) > 0.0 and abs(P[i, 1] - cy) < 0.35 * H]
            if not cand: continue
            tip = min(cand, key=lambda i: fb * -(P[i, 1] - cy) * 0 + P[i, 2])
            b = branch(tip, 3)
            if b: branches[f"leg_{ftag}{tag}"] = b
if "arms" in CFG["branches"]:
    up = P[:, 2] > z0 + 0.45 * H
    for side, tag in ((1, "l"), (-1, "r")):
        idx = np.where(up)[0]
        tip = int(idx[np.argmax(side * (P[idx, 0] - cx))])
        b = branch(tip, 3)
        if b: branches["arm_" + tag] = b

# ---------------------------------------------------------------- iskelet
arm_data = bpy.data.armatures.new(NAME + "Rig")
arm = bpy.data.objects.new(NAME + "Rig", arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
eb = arm_data.edit_bones
rootb = eb.new("Root"); rootb.head = Vector((spine_pts[ri].x, spine_pts[ri].y, 0)); rootb.tail = rootb.head + Vector((0, 0, 0.15 * H))
hips = eb.new("pelvis"); hips.head = spine_pts[ri]; hips.tail = spine_pts[ri - 1]; hips.parent = rootb
prevb = hips
UP = []   # pelvisten başa
for k in range(ri - 1, 0, -1):
    b = eb.new(f"spine_{len(UP) + 1:02d}"); b.head = spine_pts[k]; b.tail = spine_pts[k - 1]
    b.parent = prevb; b.use_connect = True; prevb = b; UP.append(b.name)
UP[-1:] = UP[-1:]
TAIL = []
prevb = hips
for k in range(ri, len(spine_pts) - 1):
    b = eb.new(f"tail_{len(TAIL) + 1:02d}"); b.head = spine_pts[k]; b.tail = spine_pts[k + 1]
    b.parent = prevb if TAIL else hips; prevb = b; TAIL.append(b.name)
BR = {}
for bn, (pts, sk, _d, _L) in branches.items():
    parent = hips if sk >= ri else eb[UP[min(len(UP) - 1, max(0, ri - 1 - sk))]]
    prevb = parent; names = []
    for k in range(len(pts) - 1):
        b = eb.new(f"{bn}_{k + 1}"); b.head = pts[k]; b.tail = pts[k + 1]
        b.parent = prevb; prevb = b; names.append(b.name)
    BR[bn] = names
for b in eb:
    if (b.tail - b.head).length < 1e-3: b.tail = b.head + Vector((0, 0, 0.02))
bpy.ops.object.mode_set(mode="OBJECT")
print("BONES", len(arm_data.bones), "up", len(UP), "tail", len(TAIL), "branches", {k: len(v) for k, v in BR.items()})

# ---------------------------------------------------------------- ağırlık (jeodezik: omurga boyunca konum + dallar)
NSEG = len(spine_pts) - 1
def seg_bone(jj):
    jj = max(0, min(NSEG - 1, jj))
    if jj <= ri - 2: return f"spine_{ri - 1 - jj:02d}"
    if jj == ri - 1: return "pelvis"
    return f"tail_{jj - ri + 1:02d}"
groups = {b.name: body.vertex_groups.new(name=b.name) for b in arm_data.bones if b.name != "Root"}
seg_len = L / NSEG
mw_b = body.matrix_world
for v in body.data.vertices:
    co = mw_b @ v.co
    _, gi, _ = kd.find(co)
    w = {}
    sv = dh[gi] if np.isfinite(dh[gi]) else 0.0
    f = sv / seg_len - 0.5; j0 = math.floor(f); t = f - j0
    for jj, ww in ((j0, 1 - t), (j0 + 1, t)):
        b = seg_bone(jj); w[b] = w.get(b, 0) + ww
    # dallar
    for bn, (pts, sk, dd, Lb) in branches.items():
        dt = dd[gi]
        if not np.isfinite(dt) or dt >= Lb: continue
        wb = min(1.0, (Lb - dt) / (0.3 * Lb))
        u = (1 - dt / Lb) * len(BR[bn]) - 0.5; k0 = math.floor(u); tt = u - k0
        for k in w: w[k] *= (1 - wb)
        for kk, ww in ((k0, 1 - tt), (k0 + 1, tt)):
            kk = max(0, min(len(BR[bn]) - 1, kk)); nm = BR[bn][kk]
            w[nm] = w.get(nm, 0) + wb * ww
    for b, ww in w.items():
        if ww > 0.001 and b in groups: groups[b].add([v.index], ww, "REPLACE")
bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); bpy.context.view_layer.objects.active = body
bpy.ops.object.vertex_group_limit_total(limit=4); bpy.ops.object.vertex_group_normalize_all(lock_active=False)
am = body.modifiers.new("Armature", "ARMATURE"); am.object = arm
body.parent = arm
print("WEIGHTS geodesic ok")

# ---------------------------------------------------------------- prosedürel animasyon
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
pb = arm.pose.bones
for p in pb: p.rotation_mode = "QUATERNION"


def rot_world(bname, axis, ang):
    """Dünya ekseni etrafında dönüşü kemiğin yerel uzayına çevirip mevcut dönüşle birleştirir."""
    b = arm_data.bones[bname]; R = b.matrix_local.to_3x3()
    ql = (R.inverted() @ Quaternion(axis, ang).to_matrix() @ R).to_quaternion()
    pb[bname].rotation_quaternion = ql @ pb[bname].rotation_quaternion


def reset():
    for p in pb: p.rotation_quaternion = (1, 0, 0, 0); p.location = (0, 0, 0)


def key(frame):
    for p in pb:
        p.keyframe_insert("rotation_quaternion", frame=frame)
        p.keyframe_insert("location", frame=frame)


def side_axis(bname):
    return Z


def up_chain_pitch(ang, start=0.0):
    """Gövdenin dik kısmını öne(+)/arkaya(-) eğ: kademeli."""
    n = len(UP)
    for i, bn in enumerate(UP):
        w = max(0.0, (i / max(1, n - 1)) - start) / max(1e-3, 1 - start)
        rot_world(bn, X, ang * w / max(1, n) * 4.2)


def wave(names, amp, phase, freq=1.0, axis=Z, grow=True):
    n = len(names)
    for i, bn in enumerate(names):
        a = 1.7 * amp * math.sin(phase - freq * i * 0.7) * ((0.4 + 0.6 * i / max(1, n - 1)) if grow else 1.0)
        rot_world(bn, axis, a)


def wings(flap):
    for s, sg in (("l", 1), ("r", -1)):
        ns = BR.get("wing_" + s, [])
        for i, bn in enumerate(ns):
            rot_world(bn, Y, -sg * flap * (1.0 if i == 0 else 0.5))


def legs(ph, amp):
    for nm, off in (("leg_fl", 0), ("leg_br", 0), ("leg_fr", math.pi), ("leg_bl", math.pi)):
        ns = BR.get(nm, [])
        if ns:
            rot_world(ns[0], X, amp * math.sin(ph + off))
            if len(ns) > 1: rot_world(ns[1], X, -amp * 0.6 * max(0.0, math.sin(ph + off + 0.8)))


def arms(lift_l, lift_r, fwd_l=0.0, fwd_r=0.0):
    for s, lift, fwd, sg in (("l", lift_l, fwd_l, 1), ("r", lift_r, fwd_r, -1)):
        ns = BR.get("arm_" + s, [])
        if ns:
            rot_world(ns[0], Y, -sg * lift); rot_world(ns[0], X, -fwd)
            if len(ns) > 1: rot_world(ns[1], X, -fwd * 0.5)


def root_bob(dz):
    pb["Root"].location = (0, dz, 0)   # Root kemiği +Z'ye bakar → yerel Y = dünya Z


def make(name, frames, fn, loop):
    act = bpy.data.actions.new(name); act.use_fake_user = True
    arm.animation_data.action = act
    try: arm.animation_data.action_slot = act.slots[0] if act.slots else arm.animation_data.action_slot
    except Exception: pass
    for f in range(0, frames + 1, 2):
        reset(); fn(f / frames); key(f)
    act["loop"] = loop


bpy.context.view_layer.objects.active = arm
arm.animation_data_create()
TAU = 2 * math.pi
UPW = UP          # dik gövde
TL = TAIL
DR = NAME == "Dragon"

make("Idle", 80, lambda t: (wave(UPW, 0.035, TAU * t), wave(TL, 0.06, TAU * t + 1), wings(0.12 * math.sin(TAU * t)) if DR else None,
                            arms(0.05 * math.sin(TAU * t), 0.05 * math.sin(TAU * t)), root_bob(0.01 * H * math.sin(TAU * t))), True)


def run(t):
    ph = TAU * t
    if DR:
        legs(ph, 0.55); wave(TL, 0.18, ph * 1 + 0.5); wave(UPW, 0.05, ph); wings(0.35 * math.sin(ph))
        root_bob(0.03 * H * abs(math.sin(ph)))
    else:   # kıvrılarak kayma: yan dalga gövdeden kuyruğa akar, dik kısım öne eğik
        up_chain_pitch(0.35); wave(UPW, 0.10, ph, axis=Z, grow=False); wave(TL, 0.28, ph * 1 + 1.0, freq=1.3)
        arms(0.25 + 0.15 * math.sin(ph), 0.25 - 0.15 * math.sin(ph), 0.3 * math.sin(ph), -0.3 * math.sin(ph))
        root_bob(0.015 * H * math.sin(2 * ph))


make("Run", 24, run, True)


def roar(t):
    k = math.sin(min(1.0, t / 0.3) * math.pi / 2) if t < 0.85 else (1 - (t - 0.85) / 0.15)
    shake = 0.05 * math.sin(TAU * t * 9) * (1 if 0.3 < t < 0.85 else 0)
    up_chain_pitch(-0.55 * k, start=0.3); wave(UPW[-4:], shake, 0, grow=False)
    wave(TL, 0.12 * k, TAU * t * 2)
    if DR: wings(0.6 * k + shake * 3)
    arms(0.9 * k, 0.9 * k, -0.2 * k, -0.2 * k)


make("Roar", 52, roar, False)


def throw(t):
    if t < 0.35: k = -t / 0.35            # geri yaslan
    elif t < 0.6: k = -1 + (t - 0.35) / 0.25 * 2.2   # öne savur
    else: k = 1.2 * (1 - (t - 0.6) / 0.4)
    up_chain_pitch(0.45 * k, start=0.2); wave(TL, 0.15, TAU * t)
    arms(0.2, 0.6 + 0.4 * max(0, -k), 0.0, 1.4 * k)
    if DR: wings(0.25 * k)


make("Throw", 26, throw, False)


def hit(t):
    k = math.sin(min(1.0, t / 0.15) * math.pi / 2) * (1 - t) ** 1.5
    up_chain_pitch(-0.6 * k); wave(UPW, 0.15 * k, TAU * t * 3, grow=False); wave(TL, 0.3 * k, TAU * t * 2)
    arms(0.8 * k, 0.8 * k, -0.6 * k, -0.6 * k)
    if DR: wings(0.5 * k)


make("Hit", 20, hit, False)


def defeat(t):
    k = min(1.0, t / 0.7); s = k * k * (3 - 2 * k)
    bounce = 0.04 * math.sin(max(0, t - 0.7) / 0.3 * math.pi) if t > 0.7 else 0
    up_chain_pitch(1.6 * s - bounce, start=0.0); wave(TL, 0.2 * (1 - s), TAU * t)
    arms(0.6 * s, 0.6 * s, 0.4 * s, 0.4 * s)
    if DR: wings(-0.5 * s); legs(0, 0); root_bob(-0.1 * H * s)
    else: root_bob(-0.05 * H * s)


make("Defeat", 30, defeat, False)
arm.animation_data.action = None
reset()

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + ".blend"))
bpy.ops.object.select_all(action="DESELECT"); arm.select_set(True); body.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
    bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0, path_mode="COPY", embed_textures=True)
print("RIGGED", NAME, [a.name for a in bpy.data.actions])
