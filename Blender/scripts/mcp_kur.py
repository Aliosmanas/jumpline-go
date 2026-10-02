"""Blender MCP eklentisini kurar ve etkinleştirir (bir kez).
Çalıştırma: blender -b -P Blender/scripts/mcp_kur.py
"""
import os
import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ADDON = os.path.join(ROOT, "Blender", "addons", "blender_mcp.py")

bpy.ops.preferences.addon_install(filepath=ADDON, overwrite=True)
bpy.ops.preferences.addon_enable(module="blender_mcp")
bpy.context.preferences.use_preferences_save = True
bpy.ops.wm.save_userpref()

import addon_utils
enabled = addon_utils.check("blender_mcp")
print("MCP_EKLENTI:", enabled)
