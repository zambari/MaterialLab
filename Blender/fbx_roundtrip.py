bl_info = {
    "name": "FBX Roundtrip Tools",
    "author": "ZambAI",
    "version": (1, 7, 2),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar (N) > FBX Tools",
    "description": "Roundtrip + pivot + detach + clean export + bone/vertex group sync",
    "category": "Import-Export",
}

ADDON_VERSION = bl_info.get("version", (0, 0))
ADDON_VERSION_STR = ".".join(str(x) for x in ADDON_VERSION)

FBX_SOURCE_SCENE_PROP = "fbx_roundtrip_source_path"

import bpy
import os
import sys
import importlib
import bmesh
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


def get_selected_vertex_index(obj):
    if obj is None or obj.type != 'MESH':
        return None

    mesh = obj.data
    bm = bmesh.from_edit_mesh(mesh)

    active = getattr(bm.select_history, "active", None)
    if isinstance(active, bmesh.types.BMVert) and active.select:
        return active.index

    for vert in bm.verts:
        if vert.select:
            return vert.index

    return None


def get_vertex_group_lines(obj, vertex_index):
    if obj is None or obj.type != 'MESH':
        return []

    obj.update_from_editmode()
    mesh = obj.data

    if vertex_index < 0 or vertex_index >= len(mesh.vertices):
        return []

    vertex = mesh.vertices[vertex_index]
    lines = []

    for assignment in vertex.groups:
        if assignment.weight <= 0:
            continue

        group_name = obj.vertex_groups[assignment.group].name
        lines.append(f"{group_name}: {assignment.weight:.4f}")

    lines.sort()
    return lines


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


# -------- Bone / Vertex Group Sync --------

def get_armature_for_mesh(obj):
    """Return the armature object driving this mesh via its first Armature modifier."""
    if obj is None or obj.type != 'MESH':
        return None
    for mod in obj.modifiers:
        if mod.type == 'ARMATURE' and mod.object:
            return mod.object
    return None


class FBX_OT_create_missing_vgroups(bpy.types.Operator):
    bl_idname = "fbx.create_missing_vgroups"
    bl_label = "Create Missing Vertex Groups"
    bl_description = (
        "Create an empty vertex group for every armature bone "
        "that has no matching group on the active mesh"
    )

    def execute(self, context):
        obj = context.active_object
        if not obj or obj.type != 'MESH':
            self.report({'WARNING'}, "Active object must be a mesh.")
            return {'CANCELLED'}

        arm_obj = get_armature_for_mesh(obj)
        if arm_obj is None:
            self.report({'WARNING'}, "No Armature modifier found on the active mesh.")
            return {'CANCELLED'}

        existing = {vg.name for vg in obj.vertex_groups}
        created = []
        for bone in arm_obj.data.bones:
            if bone.name not in existing:
                obj.vertex_groups.new(name=bone.name)
                created.append(bone.name)

        if created:
            msg = f"Created {len(created)} group(s): {', '.join(created)}"
            self.report({'INFO'}, msg)
            print(f"[FBX Tools] {msg}")
        else:
            self.report({'INFO'}, "All bones already have a vertex group.")
        return {'FINISHED'}


class FBX_OT_remove_unused_vgroups(bpy.types.Operator):
    bl_idname = "fbx.remove_unused_vgroups"
    bl_label = "Remove Unused Vertex Groups"
    bl_description = (
        "Remove vertex groups on the active mesh whose name "
        "no longer matches any bone in the armature"
    )

    def execute(self, context):
        obj = context.active_object
        if not obj or obj.type != 'MESH':
            self.report({'WARNING'}, "Active object must be a mesh.")
            return {'CANCELLED'}

        arm_obj = get_armature_for_mesh(obj)
        if arm_obj is None:
            self.report({'WARNING'}, "No Armature modifier found on the active mesh.")
            return {'CANCELLED'}

        bone_names = {b.name for b in arm_obj.data.bones}
        to_remove = [vg for vg in obj.vertex_groups if vg.name not in bone_names]

        removed = [vg.name for vg in to_remove]
        for vg in to_remove:
            obj.vertex_groups.remove(vg)

        if removed:
            msg = f"Removed {len(removed)} group(s): {', '.join(removed)}"
            self.report({'INFO'}, msg)
            print(f"[FBX Tools] {msg}")
        else:
            self.report({'INFO'}, "No unused vertex groups found.")
        return {'FINISHED'}


def _assign_auto_weights(operator, context, parent_type):
    """
    Run parent_set with the given ARMATURE_* type to assign auto weights.
    Expects active object to be the mesh; armature is discovered via modifier.
    Switches to Object Mode if needed, restores afterwards.
    """
    obj = context.active_object
    if not obj or obj.type != 'MESH':
        operator.report({'WARNING'}, "Active object must be a mesh.")
        return {'CANCELLED'}

    arm_obj = get_armature_for_mesh(obj)
    if arm_obj is None:
        operator.report({'WARNING'}, "No Armature modifier found on the active mesh.")
        return {'CANCELLED'}

    prev_mode = context.mode
    if prev_mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    # parent_set needs: children selected, parent (armature) active.
    prev_active = context.view_layer.objects.active
    prev_selected = list(context.selected_objects)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    arm_obj.select_set(True)
    context.view_layer.objects.active = arm_obj

    # keep_transform=True prevents Blender from baking the armature's scale
    # into the mesh's local transform (common issue with FBX centimeter rigs).
    bpy.ops.object.parent_set(type=parent_type, keep_transform=True)

    # Restore selection state.
    bpy.ops.object.select_all(action='DESELECT')
    for o in prev_selected:
        try:
            o.select_set(True)
        except Exception:
            pass
    try:
        context.view_layer.objects.active = prev_active
    except Exception:
        context.view_layer.objects.active = obj

    if prev_mode == 'EDIT_MESH':
        bpy.ops.object.mode_set(mode='EDIT')
    elif prev_mode == 'WEIGHT_PAINT':
        bpy.ops.object.mode_set(mode='WEIGHT_PAINT')

    label = "heat-map" if parent_type == 'ARMATURE_AUTO' else "envelope"
    msg = f"Auto weights ({label}) assigned: mesh='{obj.name}', armature='{arm_obj.name}'"
    operator.report({'INFO'}, msg)
    print(f"[FBX Tools] {msg}")
    return {'FINISHED'}


class FBX_OT_auto_weights_heatmap(bpy.types.Operator):
    bl_idname = "fbx.auto_weights_heatmap"
    bl_label = "Auto Weights — Heat Map"
    bl_description = (
        "Assign automatic weights using heat-map diffusion (bone proximity + geometry). "
        "OVERWRITES all existing vertex group weights"
    )

    def invoke(self, context, event):
        return context.window_manager.invoke_confirm(self, event)

    def execute(self, context):
        return _assign_auto_weights(self, context, 'ARMATURE_AUTO')


class FBX_OT_auto_weights_envelope(bpy.types.Operator):
    bl_idname = "fbx.auto_weights_envelope"
    bl_label = "Auto Weights — Envelopes"
    bl_description = (
        "Assign automatic weights using bone envelope radii. "
        "Faster but less accurate than heat-map. "
        "OVERWRITES all existing vertex group weights"
    )

    def invoke(self, context, event):
        return context.window_manager.invoke_confirm(self, event)

    def execute(self, context):
        return _assign_auto_weights(self, context, 'ARMATURE_ENVELOPE')


# -------- Rig Cleanup --------

def _get_mesh_and_armature(context):
    """
    Return (mesh_obj, arm_obj) from context.
    Accepts active object being either a mesh (finds armature via modifier)
    or an armature (finds first selected mesh).
    """
    obj = context.active_object
    if obj is None:
        return None, None
    if obj.type == 'MESH':
        return obj, get_armature_for_mesh(obj)
    if obj.type == 'ARMATURE':
        mesh = next((o for o in context.selected_objects if o.type == 'MESH'), None)
        return mesh, obj
    return None, None


class FBX_OT_clear_animations(bpy.types.Operator):
    bl_idname = "fbx.clear_animations"
    bl_label = "Clear All Animations"
    bl_description = "Remove all animation data (actions, NLA tracks, drivers) from selected objects"

    def invoke(self, context, event):
        return context.window_manager.invoke_confirm(self, event)

    def execute(self, context):
        cleared = []
        for obj in context.selected_objects:
            if obj.animation_data:
                obj.animation_data_clear()
                cleared.append(obj.name)

        if cleared:
            msg = f"Cleared animation from: {', '.join(cleared)}"
            self.report({'INFO'}, msg)
            print(f"[FBX Tools] {msg}")
        else:
            self.report({'INFO'}, "No animation data found on selected objects.")
        return {'FINISHED'}


class FBX_OT_reset_rest_pose(bpy.types.Operator):
    bl_idname = "fbx.reset_rest_pose"
    bl_label = "Reset to Rest Pose"
    bl_description = (
        "Clear all pose-bone transforms (location, rotation, scale) "
        "to restore the armature's rest/T-pose. "
        "Active object may be the armature or a mesh with an Armature modifier"
    )

    def execute(self, context):
        obj = context.active_object
        arm_obj = None
        if obj and obj.type == 'ARMATURE':
            arm_obj = obj
        elif obj and obj.type == 'MESH':
            arm_obj = get_armature_for_mesh(obj)

        if arm_obj is None:
            self.report({'WARNING'}, "Active object must be an armature or a mesh with an Armature modifier.")
            return {'CANCELLED'}

        prev_active = context.view_layer.objects.active
        prev_mode = context.mode

        if prev_mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')

        context.view_layer.objects.active = arm_obj
        bpy.ops.object.mode_set(mode='POSE')
        bpy.ops.pose.select_all(action='SELECT')
        bpy.ops.pose.transforms_clear()
        bpy.ops.object.mode_set(mode='OBJECT')

        try:
            context.view_layer.objects.active = prev_active
        except Exception:
            context.view_layer.objects.active = arm_obj

        msg = f"Rest pose restored on '{arm_obj.name}'"
        self.report({'INFO'}, msg)
        print(f"[FBX Tools] {msg}")
        return {'FINISHED'}


class FBX_OT_apply_scale(bpy.types.Operator):
    bl_idname = "fbx.apply_scale"
    bl_label = "Apply Scale (Mesh + Armature)"
    bl_description = (
        "Apply scale transform on the mesh and its armature — sets scale to (1,1,1) "
        "while preserving visual size. You may need to redo Auto Weights afterwards"
    )

    def invoke(self, context, event):
        return context.window_manager.invoke_confirm(self, event)

    def execute(self, context):
        mesh_obj, arm_obj = _get_mesh_and_armature(context)

        if mesh_obj is None and arm_obj is None:
            self.report({'WARNING'}, "Select a mesh with an Armature modifier, or an armature.")
            return {'CANCELLED'}

        prev_mode = context.mode
        if prev_mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')

        prev_active = context.view_layer.objects.active
        prev_selected = list(context.selected_objects)

        applied = []
        bpy.ops.object.select_all(action='DESELECT')

        # Apply armature first so bone positions bake at the right scale.
        if arm_obj:
            arm_obj.select_set(True)
            context.view_layer.objects.active = arm_obj
            bpy.ops.object.transform_apply(scale=True)
            applied.append(f"armature '{arm_obj.name}'")
            arm_obj.select_set(False)

        if mesh_obj:
            mesh_obj.select_set(True)
            context.view_layer.objects.active = mesh_obj
            bpy.ops.object.transform_apply(scale=True)
            applied.append(f"mesh '{mesh_obj.name}'")
            mesh_obj.select_set(False)

        bpy.ops.object.select_all(action='DESELECT')
        for o in prev_selected:
            try:
                o.select_set(True)
            except Exception:
                pass
        try:
            context.view_layer.objects.active = prev_active
        except Exception:
            pass

        msg = f"Scale applied to: {', '.join(applied)}"
        self.report({'INFO'}, msg)
        print(f"[FBX Tools] {msg}")
        return {'FINISHED'}


# -------- Weight Repair --------

_ZERO_WEIGHT_THRESHOLD = 1e-4


def _build_adjacency(mesh):
    adj = {v.index: [] for v in mesh.vertices}
    for e in mesh.edges:
        a, b = e.vertices
        adj[a].append(b)
        adj[b].append(a)
    return adj


def _read_weight_map(mesh):
    """Return {vert_index: {group_index: weight}} for weights above threshold."""
    return {
        v.index: {g.group: g.weight for g in v.groups if g.weight > _ZERO_WEIGHT_THRESHOLD}
        for v in mesh.vertices
    }


class FBX_OT_select_zero_weight_verts(bpy.types.Operator):
    bl_idname = "fbx.select_zero_weight_verts"
    bl_label = "Select Zero-Weight Verts"
    bl_description = (
        "Switch to Edit Mode and select all vertices whose total "
        "vertex group weight is effectively zero"
    )

    def execute(self, context):
        obj = context.active_object
        if not obj or obj.type != 'MESH':
            self.report({'WARNING'}, "Active object must be a mesh.")
            return {'CANCELLED'}

        if context.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')

        mesh = obj.data
        weight_map = _read_weight_map(mesh)
        zero_indices = [vi for vi, w in weight_map.items() if not w]

        # Deselect all in Edit Mode, then select the zero-weight verts.
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='DESELECT')
        bpy.ops.object.mode_set(mode='OBJECT')

        for vi in zero_indices:
            mesh.vertices[vi].select = True

        bpy.ops.object.mode_set(mode='EDIT')

        msg = f"Selected {len(zero_indices)} zero-weight vert(s)"
        self.report({'INFO'}, msg)
        print(f"[FBX Tools] {msg}")
        return {'FINISHED'}


class FBX_OT_heal_zero_weight_verts(bpy.types.Operator):
    bl_idname = "fbx.heal_zero_weight_verts"
    bl_label = "Heal Zero-Weight Verts"
    bl_description = (
        "Fill vertices with no weight by averaging weights from connected neighbours. "
        "Runs multiple passes so chains of zero-weight verts (e.g. a new edge loop) "
        "all propagate correctly"
    )

    def execute(self, context):
        obj = context.active_object
        if not obj or obj.type != 'MESH':
            self.report({'WARNING'}, "Active object must be a mesh.")
            return {'CANCELLED'}

        if not obj.vertex_groups:
            self.report({'WARNING'}, "Mesh has no vertex groups.")
            return {'CANCELLED'}

        if context.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')

        mesh = obj.data
        vgroups = obj.vertex_groups
        adj = _build_adjacency(mesh)

        total_healed = 0
        MAX_PASSES = 20

        for pass_num in range(MAX_PASSES):
            weight_map = _read_weight_map(mesh)
            zero_verts = [vi for vi, w in weight_map.items() if not w]

            if not zero_verts:
                break

            healed_this_pass = 0
            for vi in zero_verts:
                donors = [ni for ni in adj[vi] if weight_map[ni]]
                if not donors:
                    continue

                # Average each group weight across all donating neighbours.
                merged = {}
                for ni in donors:
                    for gi, w in weight_map[ni].items():
                        merged[gi] = merged.get(gi, 0.0) + w
                for gi in merged:
                    merged[gi] /= len(donors)

                for gi, w in merged.items():
                    vgroups[gi].add([vi], w, 'REPLACE')

                healed_this_pass += 1

            total_healed += healed_this_pass
            if healed_this_pass == 0:
                break  # no progress — isolated cluster with no weighted neighbours

        if total_healed:
            msg = f"Healed {total_healed} vert(s) over {pass_num + 1} pass(es)"
            self.report({'INFO'}, msg)
            print(f"[FBX Tools] {msg}")
        else:
            self.report({'INFO'}, "No zero-weight vertices found.")
        return {'FINISHED'}


# -------- Vertex Groups --------
class FBX_OT_inspect_selected_vertex_groups(bpy.types.Operator):
    bl_idname = "fbx.inspect_selected_vertex_groups"
    bl_label = "Inspect Selected Vertex Groups"
    bl_description = "Show vertex groups with weight for the selected vertex"

    def execute(self, context):
        obj = context.active_object
        scene = context.scene

        if not obj or obj.type != 'MESH':
            scene.fbx_vertex_group_inspector_status = "Active object must be a mesh."
            scene.fbx_vertex_group_inspector_output = ""
            return {'CANCELLED'}

        if context.mode != 'EDIT_MESH':
            scene.fbx_vertex_group_inspector_status = "Enter Edit Mode and select a vertex."
            scene.fbx_vertex_group_inspector_output = ""
            return {'CANCELLED'}

        vertex_index = get_selected_vertex_index(obj)
        if vertex_index is None:
            scene.fbx_vertex_group_inspector_status = "No selected vertex found."
            scene.fbx_vertex_group_inspector_output = ""
            return {'CANCELLED'}

        lines = get_vertex_group_lines(obj, vertex_index)
        scene.fbx_vertex_group_inspector_status = f"Vertex {vertex_index}"
        scene.fbx_vertex_group_inspector_output = "\n".join(lines) if lines else "No non-zero vertex group weights."
        return {'FINISHED'}


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

        col.separator()
        box = col.box()
        row = box.row()
        row.prop(
            context.scene,
            "fbx_show_vertex_group_inspector",
            icon='TRIA_DOWN' if context.scene.fbx_show_vertex_group_inspector else 'TRIA_RIGHT',
            icon_only=True,
            emboss=False,
        )
        row.label(text="Selected Vertex Groups")

        if context.scene.fbx_show_vertex_group_inspector:
            box.operator("fbx.inspect_selected_vertex_groups", icon='GROUP_VERTEX')
            if context.scene.fbx_vertex_group_inspector_status:
                box.label(text=context.scene.fbx_vertex_group_inspector_status)

            if context.scene.fbx_vertex_group_inspector_output:
                for line in context.scene.fbx_vertex_group_inspector_output.splitlines():
                    box.label(text=line)

        col.separator()
        box2 = col.box()
        row2 = box2.row()
        row2.prop(
            context.scene,
            "fbx_show_bone_vgroup_tools",
            icon='TRIA_DOWN' if context.scene.fbx_show_bone_vgroup_tools else 'TRIA_RIGHT',
            icon_only=True,
            emboss=False,
        )
        row2.label(text="Bone ↔ Vertex Group Sync")

        if context.scene.fbx_show_bone_vgroup_tools:
            box2.operator("fbx.create_missing_vgroups", icon='ADD')
            box2.operator("fbx.remove_unused_vgroups", icon='REMOVE')
            box2.separator()
            box2.label(text="Weight Repair:")
            box2.operator("fbx.select_zero_weight_verts", icon='VERTEXSEL')
            box2.operator("fbx.heal_zero_weight_verts", icon='BRUSH_DATA')
            box2.separator()
            box2.label(text="Destructive — overwrites weights:")
            box2.operator("fbx.auto_weights_heatmap", icon='WPAINT_HLT')
            box2.operator("fbx.auto_weights_envelope", icon='BONE_DATA')

        col.separator()
        box3 = col.box()
        row3 = box3.row()
        row3.prop(
            context.scene,
            "fbx_show_rig_cleanup",
            icon='TRIA_DOWN' if context.scene.fbx_show_rig_cleanup else 'TRIA_RIGHT',
            icon_only=True,
            emboss=False,
        )
        row3.label(text="Rig Cleanup")

        if context.scene.fbx_show_rig_cleanup:
            box3.operator("fbx.clear_animations", icon='CANCEL')
            box3.operator("fbx.reset_rest_pose", icon='ARMATURE_DATA')
            box3.separator()
            box3.label(text="Destructive — re-do weights after:")
            box3.operator("fbx.apply_scale", icon='EMPTY_AXIS')


# -------- Register --------

classes = (
    FBX_OT_export_back,
    FBX_OT_clean_export,
    FBX_OT_clean_export_edit,
    OBJECT_OT_pivot_bottom_center,
    OBJECT_OT_detach_and_fix,
    FBX_OT_reload_addon,
    FBX_OT_inspect_selected_vertex_groups,
    FBX_OT_create_missing_vgroups,
    FBX_OT_remove_unused_vgroups,
    FBX_OT_select_zero_weight_verts,
    FBX_OT_heal_zero_weight_verts,
    FBX_OT_auto_weights_heatmap,
    FBX_OT_auto_weights_envelope,
    FBX_OT_clear_animations,
    FBX_OT_reset_rest_pose,
    FBX_OT_apply_scale,
    FBX_PT_tools_panel,
)

def register():
    bpy.types.Scene.fbx_show_vertex_group_inspector = bpy.props.BoolProperty(
        name="Selected Vertex Groups",
        default=False,
    )
    bpy.types.Scene.fbx_vertex_group_inspector_status = bpy.props.StringProperty(
        name="Vertex Group Inspector Status",
        default="",
    )
    bpy.types.Scene.fbx_vertex_group_inspector_output = bpy.props.StringProperty(
        name="Vertex Group Inspector Output",
        default="",
    )
    bpy.types.Scene.fbx_show_bone_vgroup_tools = bpy.props.BoolProperty(
        name="Bone Vertex Group Sync",
        default=False,
    )
    bpy.types.Scene.fbx_show_rig_cleanup = bpy.props.BoolProperty(
        name="Rig Cleanup",
        default=False,
    )

    for cls in classes:
        bpy.utils.register_class(cls)

def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)

    del bpy.types.Scene.fbx_show_rig_cleanup
    del bpy.types.Scene.fbx_show_bone_vgroup_tools
    del bpy.types.Scene.fbx_vertex_group_inspector_output
    del bpy.types.Scene.fbx_vertex_group_inspector_status
    del bpy.types.Scene.fbx_show_vertex_group_inspector

if __name__ == "__main__":
    register()
