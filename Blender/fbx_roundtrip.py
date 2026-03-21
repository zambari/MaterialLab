bl_info = {
    "name": "FBX Roundtrip Tools",
    "author": "you + chatgpt",
    "version": (1, 4),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar (N) > FBX Tools",
    "description": "Roundtrip + pivot + detach + clean export",
    "category": "Import-Export",
}

ADDON_VERSION = bl_info.get("version", (0, 0))
ADDON_VERSION_STR = ".".join(str(x) for x in ADDON_VERSION)

FBX_SOURCE_SCENE_PROP = "fbx_roundtrip_source_path"

import bpy
import os
import sys
import importlib
from mathutils import Vector


# -------- Helpers --------

def get_last_fbx_path(context):
    wm = context.window_manager
    for op in reversed(wm.operators):
        if op.bl_idname == "IMPORT_SCENE_OT_fbx":
            return op.properties.filepath
    return None


def get_source_fbx_path(context):
    """
    Prefer the FBX path we store in the scene during import.
    This is more reliable than scanning window_manager.operators, especially
    when import was triggered by Blender's --python-expr.
    """
    try:
        path = context.scene.get(FBX_SOURCE_SCENE_PROP)
        if path:
            return path
    except Exception:
        pass

    # Fallback for older sessions / imports.
    return get_last_fbx_path(context)


def process_textures(fbx_path):
    base_dir = os.path.dirname(fbx_path)
    tex_dir = os.path.join(base_dir, "textures")
    os.makedirs(tex_dir, exist_ok=True)

    for img in bpy.data.images:
        if img.source != 'FILE':
            continue

        # unpack if needed
        if img.packed_file:
            filename = os.path.basename(img.name)
            filepath = os.path.join(tex_dir, filename)

            img.filepath_raw = filepath
            img.save()
            img.unpack(method='USE_ORIGINAL')

        # force relative path
        try:
            img.filepath = bpy.path.relpath(img.filepath)
        except:
            pass


# -------- Operator 1: Export Back --------

class FBX_OT_export_back(bpy.types.Operator):
    bl_idname = "fbx.export_back"
    bl_label = "Export Back to Source FBX"

    def execute(self, context):
        path = get_source_fbx_path(context)

        if not path:
            self.report({'WARNING'}, "No FBX import found")
            return {'CANCELLED'}

        bpy.ops.export_scene.fbx(filepath=path, use_selection=False)
        self.report({'INFO'}, f"Exported to: {path}")
        return {'FINISHED'}


# -------- Operator 2: Clean Export (overwrite) --------

class FBX_OT_clean_export(bpy.types.Operator):
    bl_idname = "fbx.clean_export"
    bl_label = "Clean Export (Overwrite)"
    bl_description = "Overwrite FBX without embedded textures"

    def execute(self, context):
        path = get_source_fbx_path(context)

        if not path:
            self.report({'WARNING'}, "No FBX import found")
            return {'CANCELLED'}

        process_textures(path)

        bpy.ops.export_scene.fbx(
            filepath=path,
            use_selection=False,
            path_mode='RELATIVE',
            embed_textures=False
        )

        self.report({'INFO'}, f"Clean exported: {path}")
        return {'FINISHED'}


# -------- Operator 3: Clean Export (_edit) --------

class FBX_OT_clean_export_edit(bpy.types.Operator):
    bl_idname = "fbx.clean_export_edit"
    bl_label = "Clean Export (_edit)"
    bl_description = "Export FBX as *_edit.fbx without embedded textures"

    def execute(self, context):
        path = get_source_fbx_path(context)

        if not path:
            self.report({'WARNING'}, "No FBX import found")
            return {'CANCELLED'}

        base, ext = os.path.splitext(path)
        new_path = base + "_edit" + ext

        process_textures(path)

        bpy.ops.export_scene.fbx(
            filepath=new_path,
            use_selection=False,
            path_mode='RELATIVE',
            embed_textures=False
        )

        self.report({'INFO'}, f"Exported: {new_path}")
        return {'FINISHED'}


# -------- Pivot --------

class OBJECT_OT_pivot_bottom_center(bpy.types.Operator):
    bl_idname = "object.pivot_bottom_center"
    bl_label = "Pivot → Bottom Center + Zero Z"

    def execute(self, context):
        obj = context.active_object

        if not obj or obj.type != 'MESH':
            return {'CANCELLED'}

        # `origin_set` is an operator that requires Object Mode, not Edit Mode.
        if context.mode != 'OBJECT':
            try:
                context.view_layer.objects.active = obj
                bpy.ops.object.mode_set(mode='OBJECT')
            except RuntimeError as e:
                self.report({'ERROR'}, f"Failed to switch to Object Mode: {e}")
                return {'CANCELLED'}

        bbox = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]

        min_z = min(v.z for v in bbox)
        center_x = sum(v.x for v in bbox) / 8
        center_y = sum(v.y for v in bbox) / 8

        target = Vector((center_x, center_y, min_z))
        context.scene.cursor.location = target

        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        obj.location.z = 0

        return {'FINISHED'}


# -------- Detach --------

class OBJECT_OT_detach_and_fix(bpy.types.Operator):
    bl_idname = "object.detach_and_fix"
    bl_label = "Detach Selection → New Object + Pivot Fix"

    def execute(self, context):
        obj = context.active_object

        if not obj or context.mode != 'EDIT_MESH':
            return {'CANCELLED'}

        bpy.ops.mesh.separate(type='SELECTED')
        bpy.ops.object.mode_set(mode='OBJECT')

        selected = context.selected_objects
        new_obj = [o for o in selected if o != obj][0]

        context.view_layer.objects.active = new_obj

        bbox = [new_obj.matrix_world @ Vector(corner) for corner in new_obj.bound_box]

        min_z = min(v.z for v in bbox)
        center_x = sum(v.x for v in bbox) / 8
        center_y = sum(v.y for v in bbox) / 8

        target = Vector((center_x, center_y, min_z))
        context.scene.cursor.location = target

        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        new_obj.location.z = 0

        return {'FINISHED'}


# -------- Reload --------
class FBX_OT_reload_addon(bpy.types.Operator):
    bl_idname = "fbx.reload_addon"
    bl_label = "Reload FBX Roundtrip Tools"
    bl_description = "Reload this addon module so UI/operator changes appear immediately"

    def execute(self, context):
        module_name = __name__

        try:
            # Unregister the old classes so the reloaded definitions can be registered cleanly.
            try:
                unregister()
            except Exception:
                # Best-effort: Blender may be mid-refresh.
                pass

            module = sys.modules.get(module_name)
            if module is None:
                raise RuntimeError(f"Addon module not found in sys.modules: {module_name}")

            module = importlib.reload(module)
            module.register()

            self.report({'INFO'}, f"Reloaded {module_name} (v{module.ADDON_VERSION_STR})")
            return {'FINISHED'}
        except Exception as e:
            # Fallback: Blender addon modules are sometimes loaded in a way that makes
            # importlib.reload unreliable. Toggling the addon forces Blender to reload it.
            try:
                addon_id = module_name
                try:
                    addon_id = os.path.splitext(os.path.basename(__file__))[0]
                except Exception:
                    pass

                bpy.ops.wm.addon_disable(module=addon_id)
                bpy.ops.wm.addon_enable(module=addon_id)

                self.report({'INFO'}, f"Reloaded via addon toggle ({addon_id})")
                return {'FINISHED'}
            except Exception as e2:
                self.report({'ERROR'}, f"Reload failed: {e} (fallback: {e2})")
                return {'CANCELLED'}


# -------- UI --------

class FBX_PT_tools_panel(bpy.types.Panel):
    bl_label = "FBX Tools"
    bl_idname = "FBX_PT_tools_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "FBX Tools"

    def draw(self, context):
        layout = self.layout
        col = layout.column(align=True)

        col.label(text=f"FBX Roundtrip Tools v{ADDON_VERSION_STR}")
        col.label(text="Roundtrip:")
        col.operator("fbx.export_back", icon='EXPORT')

        col.separator()
        col.operator("fbx.reload_addon")
        col.label(text="Export:")
        col.operator("fbx.clean_export", icon='FILE_TICK')
        col.operator("fbx.clean_export_edit", icon='FILE_NEW')

        col.separator()
        col.label(text="Pivot:")
        col.operator("object.pivot_bottom_center", icon='EMPTY_AXIS')

        col.separator()
        col.label(text="Mesh Ops:")
        col.operator("object.detach_and_fix", icon='MESH_CUBE')


# -------- Register --------

classes = (
    FBX_OT_export_back,
    FBX_OT_clean_export,
    FBX_OT_clean_export_edit,
    OBJECT_OT_pivot_bottom_center,
    OBJECT_OT_detach_and_fix,
    FBX_OT_reload_addon,
    FBX_PT_tools_panel,
)

def register():
    for cls in classes:
        bpy.utils.register_class(cls)

def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)

if __name__ == "__main__":
    register()