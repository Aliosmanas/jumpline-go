"""
Koşucu animasyonları — Desktop/Yeni klasör animasyon videoları.
runner.blend'deki RunnerRig (MPFB game_engine iskeleti) üzerine keyframe'li action'lar:
  Idle (nefes, döngü), Run (koşu, döngü), Crouch (zıplama şarjı), Jump (kalkış), Fall (havada tuck),
  Land (iniş), Zipline (halatta, döngü), Death (çöküş), Slide (aşağı kaydırma: yere yatıp kayma, 24 kare = 0.8 sn).
Pozlar: kemik → dünya yönü (karakter -Y'ye bakar, sol taraf +X, yukarı +Z).
Çıktı: Runner.fbx (meshler + tüm action'lar) ve runner.blend.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

ROOT = r"D:\Repolar\junpline"
BLEND = os.path.join(ROOT, "Blender", "source", "runner.blend")
FBX = os.path.join(ROOT, "Assets", "Art", "Characters", "Runner", "Runner.fbx")
FPS = 30

ORDER = ["pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head",
         "upperarm_l", "lowerarm_l", "hand_l", "upperarm_r", "lowerarm_r", "hand_r",
         "thigh_l", "calf_l", "foot_l", "thigh_r", "calf_r", "foot_r"]
FINGERS = [f"{f}_0{i}_{s}" for f in ("index", "middle", "ring", "pinky") for i in (1, 2, 3) for s in ("l", "r")]


def V(x, y, z):
    return Vector((x, y, z)).normalized()


def mirror(pose):
    """Sol ↔ sağ ayna (x işareti ters)."""
    out = {}
    for k, v in pose.items():
        if k in ("pelvis_z", "pelvis_y", "root"):
            out[k] = v
            continue
        nk = k[:-2] + ("_r" if k.endswith("_l") else "_l") if k[-2:] in ("_l", "_r") else k
        out[nk] = Vector((-v.x, v.y, v.z))
    return out


# --- Temel duruşlar -------------------------------------------------------
STAND = {
    "spine_01": V(0, 0.02, 1), "spine_02": V(0, 0.03, 1), "spine_03": V(0, 0.04, 1), "neck_01": V(0, -0.05, 1), "head": V(0, -0.02, 1),
    "upperarm_l": V(0.2, 0.02, -1), "lowerarm_l": V(0.12, -0.18, -1), "upperarm_r": V(-0.2, 0.02, -1), "lowerarm_r": V(-0.12, -0.18, -1),
    "thigh_l": V(0.06, 0, -1), "calf_l": V(0.03, 0.03, -1), "foot_l": V(0, -1, -0.45),
    "thigh_r": V(-0.06, 0, -1), "calf_r": V(-0.03, 0.03, -1), "foot_r": V(0, -1, -0.45),
}


def P(base=STAND, **kw):
    d = dict(base)
    d.update(kw)
    return d


RUN_A = P(  # sağ bacak önde (temas), sol geride; kollar karşıt
    spine_01=V(0, -0.12, 1), spine_02=V(0, -0.15, 1), spine_03=V(0, -0.12, 1), neck_01=V(0, -0.1, 1), head=V(0, -0.05, 1),
    thigh_r=V(-0.04, -0.7, -0.72), calf_r=V(-0.02, -0.15, -1), foot_r=V(0, -1, -0.2),
    thigh_l=V(0.05, 0.5, -0.86), calf_l=V(0.03, 0.95, -0.35), foot_l=V(0, 0.4, -1),
    upperarm_l=V(0.18, -0.55, -0.82), lowerarm_l=V(0.05, -1, 0.35),
    upperarm_r=V(-0.18, 0.55, -0.82), lowerarm_r=V(-0.08, -0.25, -1),
    pelvis_z=-0.04,
)
RUN_B = P(  # geçiş: sağ bacak altta taşıyor, sol öne savruluyor
    spine_01=V(0, -0.12, 1), spine_02=V(0, -0.15, 1), spine_03=V(0, -0.12, 1), neck_01=V(0, -0.1, 1), head=V(0, -0.05, 1),
    thigh_r=V(-0.03, 0.05, -1), calf_r=V(-0.02, 0.1, -1), foot_r=V(0, -1, -0.5),
    thigh_l=V(0.05, -0.35, -0.94), calf_l=V(0.03, 0.9, -0.45), foot_l=V(0, 0.2, -1),
    upperarm_l=V(0.2, -0.1, -1), lowerarm_l=V(0.08, -0.8, -0.6),
    upperarm_r=V(-0.2, 0.1, -1), lowerarm_r=V(-0.08, -0.8, -0.6),
    pelvis_z=0.05,
)
CROUCH = P(
    spine_01=V(0, -0.35, 0.94), spine_02=V(0, -0.38, 0.92), spine_03=V(0, -0.3, 0.95), neck_01=V(0, -0.2, 1), head=V(0, -0.35, 0.94),
    thigh_l=V(0.18, -0.78, -0.6), calf_l=V(0.06, 0.62, -0.78), thigh_r=V(-0.18, -0.78, -0.6), calf_r=V(-0.06, 0.62, -0.78),
    foot_l=V(0.05, -1, -0.15), foot_r=V(-0.05, -1, -0.15),
    upperarm_l=V(0.3, 0.45, -0.84), lowerarm_l=V(0.1, 0.1, -1), upperarm_r=V(-0.3, 0.45, -0.84), lowerarm_r=V(-0.1, 0.1, -1),
    pelvis_z=-0.38,
)
CROUCH_LIGHT = P(CROUCH, thigh_l=V(0.1, -0.4, -0.9), calf_l=V(0.04, 0.35, -0.94), thigh_r=V(-0.1, -0.4, -0.9),
                 calf_r=V(-0.04, 0.35, -0.94), spine_02=V(0, -0.2, 1), pelvis_z=-0.12)
JUMP_UP = P(
    spine_01=V(0, 0, 1), spine_02=V(0, 0.02, 1), spine_03=V(0, 0.05, 1), head=V(0, 0.05, 1),
    upperarm_l=V(0.3, -0.35, 0.88), lowerarm_l=V(0.1, -0.2, 1), upperarm_r=V(-0.3, -0.35, 0.88), lowerarm_r=V(-0.1, -0.2, 1),
    thigh_l=V(0.05, 0.15, -1), calf_l=V(0.03, 0.3, -1), thigh_r=V(-0.05, 0.1, -1), calf_r=V(-0.03, 0.25, -1),
    foot_l=V(0, 0.3, -1), foot_r=V(0, 0.3, -1), pelvis_z=0.02,
)
TUCK = P(
    spine_01=V(0, -0.2, 1), spine_02=V(0, -0.25, 1), spine_03=V(0, -0.2, 1), head=V(0, -0.1, 1),
    thigh_l=V(0.12, -0.85, -0.5), calf_l=V(0.05, 0.75, -0.65), thigh_r=V(-0.12, -0.85, -0.5), calf_r=V(-0.05, 0.75, -0.65),
    upperarm_l=V(0.75, -0.3, 0.1), lowerarm_l=V(0.5, -0.8, 0.2), upperarm_r=V(-0.75, -0.3, 0.1), lowerarm_r=V(-0.5, -0.8, 0.2),
)
FALL = P(TUCK, thigh_l=V(0.08, -0.45, -0.9), calf_l=V(0.04, 0.2, -1), thigh_r=V(-0.08, -0.3, -0.95), calf_r=V(-0.04, 0.15, -1),
         upperarm_l=V(0.8, 0, 0.35), upperarm_r=V(-0.8, 0, 0.35), lowerarm_l=V(0.8, -0.3, 0.5), lowerarm_r=V(-0.8, -0.3, 0.5))
ZIP = P(
    upperarm_r=V(-0.08, -0.05, 1), lowerarm_r=V(-0.05, 0, 1),
    upperarm_l=V(0.35, -0.3, -0.88), lowerarm_l=V(0.2, -0.6, -0.78),
    spine_01=V(0.05, 0.05, 1), spine_02=V(0.05, 0.05, 1),
    thigh_l=V(0.06, -0.3, -0.95), calf_l=V(0.03, 0.4, -0.92), thigh_r=V(-0.02, -0.15, -1), calf_r=V(0, 0.3, -0.95),
    foot_l=V(0, 0, -1), foot_r=V(0, 0, -1),
)
STAGGER = P(spine_01=V(0, 0.3, 0.95), spine_02=V(0, 0.4, 0.92), spine_03=V(0, 0.35, 0.94), head=V(0, 0.5, 0.86),
            upperarm_l=V(0.8, 0.2, -0.3), upperarm_r=V(-0.8, 0.2, -0.3), lowerarm_l=V(0.7, -0.2, 0.2), lowerarm_r=V(-0.7, -0.2, 0.2),
            thigh_l=V(0.08, -0.15, -1), thigh_r=V(-0.1, 0.15, -1))
KNEEL = P(spine_01=V(0, -0.3, 0.95), spine_02=V(0, -0.4, 0.92), spine_03=V(0, -0.45, 0.9), head=V(0, -0.6, 0.8),
          thigh_l=V(0.12, -0.98, -0.15), calf_l=V(0.05, 0.98, -0.1), thigh_r=V(-0.12, -0.98, -0.15), calf_r=V(-0.05, 0.98, -0.1),
          upperarm_l=V(0.2, -0.3, -0.93), upperarm_r=V(-0.2, -0.3, -0.93), lowerarm_l=V(0.1, -0.4, -0.9), lowerarm_r=V(-0.1, -0.4, -0.9),
          pelvis_z=-0.55)
DOWN = P(KNEEL, root=(-1.45, -0.95, 0.12),  # (X dönüşü rad, y kayma, z) — yüzüstü
         thigh_l=V(0.12, -0.2, -1), calf_l=V(0.05, 0.1, -1), thigh_r=V(-0.15, -0.1, -1), calf_r=V(-0.05, 0.1, -1),
         upperarm_l=V(0.9, -0.2, 0.3), upperarm_r=V(-0.9, -0.2, 0.3), lowerarm_l=V(0.9, -0.3, 0.2), lowerarm_r=V(-0.9, -0.3, 0.2),
         spine_02=V(0, 0, 1), spine_03=V(0, 0, 1), head=V(0.3, -0.1, 1), pelvis_z=0.0)

# Kayma: gövde geriye yaslı, sağ bacak önde düz, sol bacak altta katlı, sol el yere destek, sağ kol önde denge
SLIDE = P(
    spine_01=V(0, 0.55, 0.83), spine_02=V(0, 0.6, 0.8), spine_03=V(0, 0.5, 0.86), neck_01=V(0, 0.15, 1), head=V(0, 0.05, 1),
    thigh_r=V(-0.08, -0.95, -0.3), calf_r=V(-0.05, -1, -0.12), foot_r=V(0, -0.3, 0.95),
    thigh_l=V(0.14, -0.85, -0.45), calf_l=V(0.05, 0.99, -0.05), foot_l=V(0, 0.5, 0.85),
    upperarm_l=V(0.45, 0.5, -0.74), lowerarm_l=V(0.3, 0.3, -0.9),
    upperarm_r=V(-0.45, -0.6, 0.2), lowerarm_r=V(-0.3, -0.8, 0.4),
    pelvis_z=-0.7,
)
SLIDE_B = P(SLIDE, spine_02=V(0, 0.64, 0.77), upperarm_r=V(-0.5, -0.55, 0.3), head=V(0, 0.0, 1), pelvis_z=-0.72)

BREATH_IN = P(spine_03=V(0, 0.07, 1), upperarm_l=V(0.23, 0.02, -1), upperarm_r=V(-0.23, 0.02, -1), pelvis_z=0.006)

ACTIONS = {
    #  ad: ([(kare, poz)], döngü)
    "Idle": ([(0, STAND), (60, BREATH_IN), (120, STAND)], True),
    "Run": ([(0, RUN_A), (5, RUN_B), (10, mirror(RUN_A)), (15, mirror(RUN_B)), (20, RUN_A)], True),
    "Crouch": ([(0, CROUCH_LIGHT), (12, CROUCH)], False),
    "Jump": ([(0, CROUCH), (5, JUMP_UP), (14, TUCK)], False),
    "Fall": ([(0, TUCK), (12, FALL)], False),
    "Land": ([(0, CROUCH), (8, CROUCH_LIGHT), (16, STAND)], False),
    "Zipline": ([(0, ZIP), (30, P(ZIP, spine_01=V(-0.05, 0.08, 1), thigh_l=V(0.08, -0.35, -0.94))), (60, ZIP)], True),
    "Death": ([(0, STAND), (8, STAGGER), (22, KNEEL), (34, KNEEL), (48, DOWN)], False),
    # Unity: PlayerController.slideDuration ile senkron (SlideSpeed = 0.8 sn / kayma süresi)
    "Slide": ([(0, CROUCH), (4, SLIDE), (12, SLIDE_B), (19, SLIDE), (24, CROUCH_LIGHT)], False),
}


def apply_pose(rig, pose):
    pb = rig.pose.bones
    for b in pb:
        b.rotation_mode = "QUATERNION"
        b.rotation_quaternion = (1, 0, 0, 0)
        b.location = (0, 0, 0)
    bpy.context.view_layer.update()
    if "root" in pose:
        ax, dy, dz = pose["root"]
        r = pb["Root"]
        r.matrix = Matrix.Translation((0, dy, dz)) @ Matrix.Rotation(ax, 4, "X") @ r.matrix
        bpy.context.view_layer.update()
    if pose.get("pelvis_z"):
        p = pb["pelvis"]
        p.matrix = Matrix.Translation((0, 0, pose["pelvis_z"])) @ p.matrix
        bpy.context.view_layer.update()
    for name in ORDER:
        if name not in pose or name not in pb:
            continue
        b = pb[name]
        cur = (b.tail - b.head).normalized()
        target = (rig.matrix_world.to_3x3().inverted() @ pose[name]).normalized()
        if "root" in pose:  # yerde: hedef yönleri de devrilmiş gövdeye göre çevir
            target = (Matrix.Rotation(pose["root"][0], 3, "X") @ target).normalized()
        q = cur.rotation_difference(target)
        m = b.matrix.copy()
        loc = m.to_translation()
        b.matrix = Matrix.Translation(loc) @ q.to_matrix().to_4x4() @ Matrix.Translation(-loc) @ m
        bpy.context.view_layer.update()
    for f in FINGERS:  # yumruk
        if f in pb:
            pb[f].rotation_quaternion = Matrix.Rotation(math.radians(75), 3, "X").to_quaternion()


def key_all(rig, frame):
    for b in rig.pose.bones:
        b.keyframe_insert("rotation_quaternion", frame=frame)
        b.keyframe_insert("location", frame=frame)


def build():
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    rig = bpy.data.objects["RunnerRig"]
    for o in rig.children:
        o.location.z = 0.0
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    rig.animation_data_create()
    made = []
    for name, (keys, loop) in ACTIONS.items():
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        rig.animation_data.action = act
        for frame, pose in keys:
            apply_pose(rig, pose)
            key_all(rig, frame)
        act["loop"] = loop
        made.append((name, keys[-1][0]))
    # Varsayılan duruş (bind) için action'ı ayır
    rig.animation_data.action = None
    for b in rig.pose.bones:
        b.rotation_quaternion = (1, 0, 0, 0)
        b.location = (0, 0, 0)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.scene.render.fps = FPS
    return rig, made


def export(rig):
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    for o in rig.children:
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=FBX, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=False,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False,
        use_armature_deform_only=True, bake_anim=True, bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
    )
    bpy.ops.wm.save_mainfile()


_rig, _made = build()
export(_rig)
print("ANIMS_OK", _made)
