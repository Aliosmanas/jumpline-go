"""rigged/<canavar>.blend → Assets/Art/Boss/<canavar>.fbx; malzemeler benzersiz ad alır (<ad>_Mat) — Unity Boss/Materials'a çıkarırken çakışmasın."""
import bpy, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tex_export
ROOT = r"D:\Repolar\junpline"
NAMES = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["Tepegoz", "Humbaba", "Karakoncolos", "StoneGuardian", "Lamassu", "VanMonster", "Sahmeran", "Dragon"]
for n in NAMES:
    bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "Blender", "rigged", n + ".blend"))
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE"); arm.name = n + "Rig"
    for o in bpy.data.objects:
        if o.type == "MESH":
            for i, sl in enumerate(o.material_slots):
                if sl.material: sl.material.name = f"{n}_Mat" + (f"_{i}" if i else "")
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.data.objects:
        if o.type in ("ARMATURE", "MESH"): o.select_set(True)
    out = os.path.join(ROOT, "Assets", "Art", "Boss", n + ".fbx")
    tex_export.save_images(out, n)
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0, path_mode="RELATIVE", embed_textures=False)
    print("BOSS", n, os.path.getsize(out) // 1024, "KB", [a.name for a in bpy.data.actions])
