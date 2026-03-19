namespace MaterialLab.Editor
{
	using System;
	using System.Diagnostics;
	using System.IO;

	using UnityEditor;

	using UnityEngine;

	public static class PushToBlender
	{
		private const string MenuPath = MaterialLabMenu.MenuPathBase + "Push To Blender";

		// Adjust this if Blender is installed in a different location or versioned folder.
		// We intentionally prefer blender.exe for CLI usage; the launcher is a fallback.
		private static readonly string[] BlenderExecutableCandidates =
		{
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
				"Blender Foundation", "Blender 5.0", "blender.exe"),	
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
				"Blender Foundation", "Blender 3.6", "blender.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
				"Blender Foundation", "Blender 5.0", "blender-launcher.exe")
		};

		[MenuItem(MenuPath, true)]
		public static bool Validate()
		{
			if (Selection.objects == null || Selection.objects.Length != 1)
			{
				return false;
			}

			var obj = Selection.activeObject;
			if (obj == null)
			{
				return false;
			}

			var assetPath = AssetDatabase.GetAssetPath(obj);
			if (string.IsNullOrEmpty(assetPath))
			{
				return false;
			}

			// Require a single FBX "mesh asset" selection.
			if (!string.Equals(Path.GetExtension(assetPath), ".fbx", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			return TryGetBlenderExecutable(out _);
		}

		[MenuItem(MenuPath)]
		public static void Execute()
		{
			if (!Validate())
			{
				UnityEngine.Debug.LogWarning("PushToBlender: Menu executed with invalid state. Aborting.");
				return;
			}

			if (!TryGetBlenderExecutable(out var blenderExe))
			{
				UnityEngine.Debug.LogError("PushToBlender: Blender executable not found. Please adjust the search paths.");
				return;
			}

			var obj = Selection.activeObject;
			var assetPath = AssetDatabase.GetAssetPath(obj);
			var projectRoot = Path.GetDirectoryName(Application.dataPath);

			if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(assetPath))
			{
				UnityEngine.Debug.LogError("PushToBlender: Could not resolve project root or asset path.");
				return;
			}

			var fbxFullPath = Path.Combine(projectRoot, assetPath);
			if (!File.Exists(fbxFullPath))
			{
				UnityEngine.Debug.LogError($"PushToBlender: FBX not found on disk: {fbxFullPath}");
				return;
			}

			// Use forward slashes for Blender/ Python path handling.
			var fbxPathForPython = fbxFullPath.Replace("\\", "/");

			// Inline Python: wipe default scene content, then import the selected FBX.
			// Use a raw Python string with single quotes to avoid escaping issues.
			var pythonExpr =
				$"import bpy; bpy.context.scene['fbx_roundtrip_source_path']=r'{fbxPathForPython}'; bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(); bpy.ops.import_scene.fbx(filepath=r'{fbxPathForPython}')";

			// Run with full UI (no --background) so the imported scene stays open for editing.
			var arguments = $"--python-expr \"{pythonExpr}\""; //--factory-startup 

			var startInfo = new ProcessStartInfo
			{
				FileName = blenderExe,
				Arguments = arguments,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			try
			{
				UnityEngine.Debug.Log($"PushToBlender: Executing:\n\"{blenderExe}\" {arguments}");

				var process = Process.Start(startInfo);
				if (process == null)
				{
					UnityEngine.Debug.LogError("PushToBlender: Failed to start Blender process.");
					return;
				}

				UnityEngine.Debug.Log($"PushToBlender: Launched Blender for '{assetPath}' using '{blenderExe}'.");
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError($"PushToBlender: Exception while starting Blender: {ex}");
			}
		}

		private static bool TryGetBlenderExecutable(out string path)
		{
			for (int i = 0; i < BlenderExecutableCandidates.Length; i++)
			{
				var candidate = BlenderExecutableCandidates[i];
				if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
				{
					path = candidate;
					return true;
				}
			}

			path = null;
			return false;
		}
	}
}
