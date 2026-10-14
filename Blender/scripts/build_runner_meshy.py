"""Yeni koşucu: Meshy Baran + 5 kostüm, tek Mixamo iskeleti (RunnerRig), Baran'ın 9 klibi.
Kostüm gövdeleri ayrı mesh: Runner_Body_<kostüm id> (RunnerSkin seçili olanı açar).
Kaynak: Blender/rigged/{Baran, Baran_*}.blend (rig_from_mixamo.py çıktısı — hepsi aynı Idle_skin iskeletinden).
Çıktı: Assets/Art/Characters/Runner/Runner.fbx (+ Blender/source/runner_meshy.blend)
Çalıştır: blender -b -P Blender/scripts/build_runner_meshy.py"""
import bpy, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tex_export

ROOT = r"D:\Repolar\junpline"
RIG = os.path.join(ROOT, "Blender", "rigged")
FBX = os.path.join(ROOT, "Assets", "Art", "Characters", "Runner", "Runner.fbx")
BLEND = os.path.join(ROOT, "Blender", "source", "runner_meshy.blend")
# oyundaki kostüm id → kostüm modeli (glitch / frost: Baran gövdesi + renk tonu, RunnerSkin'de)
BODIES = {"rookie": "Baran", "stealth": "Baran_SurBekcisi", "velocity": "Baran_SokakKosucusu",
          "titanium": "Baran_EfsaneAvcisi", "crimson": "Baran_MardinUsta", "blaze": "Baran_Altin"}

bpy.ops.wm.open_mainfile(filepath=os.path.join(RIG, "Baran.blend"))
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
arm.name = "RunnerRig"; arm.data.name = "RunnerRig"
base = next(o for o in bpy.data.objects if o.type == "MESH")


def tidy(mesh_obj, sid):
    mesh_obj.name = f"Runner_Body_{sid}"; mesh_obj.data.name = mesh_obj.name
    for i, slot in enumerate(mesh_obj.material_slots):
        if slot.material:
            m = slot.material.copy(); m.name = f"M_Body_{sid}" + (f"_{i}" if i else "")
            slot.material = m


tidy(base, "rookie")
for sid, name in BODIES.items():
    if sid == "rookie": continue
    with bpy.data.libraries.load(os.path.join(RIG, name + ".blend")) as (src, dst):
        dst.objects = [n for n in src.objects]
    mesh = next(o for o in dst.objects if o and o.type == "MESH")
    bpy.context.scene.collection.objects.link(mesh)
    for o in dst.objects:   # kostümün kendi iskeleti gereksiz
        if o and o.type == "ARMATURE": bpy.data.objects.remove(o, do_unlink=True)
    mw = mesh.matrix_world.copy()
    mesh.parent = arm; mesh.matrix_world = mw
    for md in mesh.modifiers:
        if md.type == "ARMATURE": md.object = arm
    tidy(mesh, sid)
# kostümlerin getirdiği fazladan action'lar (Baran'ınkilerle aynı adlar → .001) silinir
for a in list(bpy.data.actions):
    if "." in a.name: bpy.data.actions.remove(a)
arm.animation_data.action = None
tex_export.save_images(FBX, "Runner")
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
for o in arm.children: o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
    use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode="RELATIVE", embed_textures=False)
print("RUNNER_OK", [o.name for o in arm.children], [a.name for a in bpy.data.actions], os.path.getsize(FBX) // 1024, "KB")
