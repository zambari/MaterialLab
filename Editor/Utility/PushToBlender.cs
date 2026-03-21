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
		private const string UpdateFbxToolsMenuPath = MaterialLabMenu.MenuPathBase + "Update FBX Tools Script";

		/// <summary>
		/// Shared list of Blender versions, in preference order.
		/// </summary>
		public static readonly string[] BlenderVersions = { "5.2", "5.1", "5.0", "3.6" };

		// Adjust this if Blender is installed in a different location or versioned folder.
		// Returns the root installation directory (no blender.exe) for a specific Blender version.
		private static string GetBlenderInstallationRootForVersion(string version)
		{
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
				"Blender Foundation",
				$"Blender {version}");
		}

		private static string GetExistingBlenderAddonDirForVersion(string appData, string version)
		{
			if (string.IsNullOrEmpty(appData) || string.IsNullOrEmpty(version))
			{
				return null;
			}

			// Blender profile folder naming can vary by install/channel.
			string[] versionFolderCandidates = { version, $"Blender {version}" };
			for (int i = 0; i < versionFolderCandidates.Length; i++)
			{
				var candidate = Path.Combine(
					appData,
					"Blender Foundation",
					"Blender",
					versionFolderCandidates[i],
					"scripts",
					"addons");

				if (Directory.Exists(candidate))
				{
					return candidate;
				}
			}

			return null;
		}

		/// <summary>
		/// Tries known Blender versions in preference order and returns
		/// the first existing installation directory, or null if none are found.
		/// </summary>
		private static string GetBlenderInstallationDir()
		{
			for (int i = 0; i < BlenderVersions.Length; i++)
			{
				var installRoot = GetBlenderInstallationRootForVersion(BlenderVersions[i]);
				if (!string.IsNullOrEmpty(installRoot) && Directory.Exists(installRoot))
				{
					return installRoot;
				}
			}

			return null;
		}

		private static bool TryGetSelectedFbxAssetPath(out string assetPath)
		{
			assetPath = null;

			if (Selection.objects == null || Selection.objects.Length != 1)
			{
				return false;
			}

			var obj = Selection.activeObject;
			if (obj == null)
			{
				return false;
			}

			// Direct FBX asset selection.
			var directPath = AssetDatabase.GetAssetPath(obj);
			if (!string.IsNullOrEmpty(directPath) &&
			    string.Equals(Path.GetExtension(directPath), ".fbx", StringComparison.OrdinalIgnoreCase))
			{
				assetPath = directPath;
				return true;
			}

			// GameObject with MeshRenderer where the Mesh comes from an FBX asset.
			if (obj is GameObject go)
			{
				var meshRenderer = go.GetComponent<MeshRenderer>();
				if (meshRenderer != null)
				{
					var meshFilter = go.GetComponent<MeshFilter>();
					if (meshFilter != null && meshFilter.sharedMesh != null)
					{
						var meshPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
						if (!string.IsNullOrEmpty(meshPath) &&
						    string.Equals(Path.GetExtension(meshPath), ".fbx", StringComparison.OrdinalIgnoreCase))
						{
							assetPath = meshPath;
							return true;
						}
					}
				}
			}

			return false;
		}

		[MenuItem(MenuPath, true)]
		public static bool Validate()
		{
			if (!TryGetSelectedFbxAssetPath(out _))
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

			if (!TryGetSelectedFbxAssetPath(out var assetPath))
			{
				UnityEngine.Debug.LogError("PushToBlender: Could not resolve an FBX asset from the current selection.");
				return;
			}
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

				var fileInfo = new FileInfo(fbxFullPath);
				var fileSizeKb = fileInfo.Length / 1024f;
				var fileName = Path.GetFileName(fbxFullPath);

				UnityEngine.Debug.Log(
					$"PushToBlender: Launched Blender for '{fileName}' ({fileSizeKb:F1} KB)\nFBX: {fbxFullPath} | Blender: {blenderExe}");
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError($"PushToBlender: Exception while starting Blender: {ex}");
			}
		}

		private static bool TryGetBlenderExecutable(out string path)
		{
			var installDir = GetBlenderInstallationDir();
			if (!string.IsNullOrEmpty(installDir))
			{
				var exePath = Path.Combine(installDir, "blender.exe");
				if (File.Exists(exePath))
				{
					path = exePath;
					return true;
				}
			}

			path = null;
			return false;
		}

		[MenuItem(UpdateFbxToolsMenuPath)]
		public static void UpdateFbxToolsScript()
		{
			// Locate the source fbx_roundtrip.py asset within the project, regardless of package root.
			// Use a name-only search so it also matches .py files imported as DefaultAsset.
			var guids = AssetDatabase.FindAssets("fbx_roundtrip");
			if (guids == null || guids.Length == 0)
			{
				UnityEngine.Debug.LogError("PushToBlender: Could not find 'fbx_roundtrip.py' TextAsset in the project.");
				return;
			}

			if (guids.Length > 1)
			{
				UnityEngine.Debug.LogWarning(
					"PushToBlender: Multiple assets named 'fbx_roundtrip' found. Using the first match.");
			}

			var sourcePath = AssetDatabase.GUIDToAssetPath(guids[0]);
			if (string.IsNullOrEmpty(sourcePath))
			{
				UnityEngine.Debug.LogError("PushToBlender: Resolved GUID for 'fbx_roundtrip' but path was empty.");
				return;
			}

			var projectRoot = Path.GetDirectoryName(Application.dataPath);
			if (string.IsNullOrEmpty(projectRoot))
			{
				UnityEngine.Debug.LogError("PushToBlender: Could not resolve project root when updating FBX tools script.");
				return;
			}

			var sourceFullPath = Path.Combine(projectRoot, sourcePath);
			if (!File.Exists(sourceFullPath))
			{
				UnityEngine.Debug.LogError($"PushToBlender: Source FBX tools script not found on disk: {sourceFullPath}");
				return;
			}

			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			if (string.IsNullOrEmpty(appData))
			{
				UnityEngine.Debug.LogError("PushToBlender: Could not resolve %APPDATA% when updating FBX tools script.");
				return;
			}

			int copiedCount = 0;
			for (int i = 0; i < BlenderVersions.Length; i++)
			{
				var version = BlenderVersions[i];
				var targetAddonDir = GetExistingBlenderAddonDirForVersion(appData, version);

				if (!Directory.Exists(targetAddonDir))
				{
					continue;
				}

				try
				{
					Directory.CreateDirectory(targetAddonDir);
					var targetPath = Path.Combine(targetAddonDir, "fbx_roundtrip.py");
					File.Copy(sourceFullPath, targetPath, true);
					copiedCount++;

					UnityEngine.Debug.Log(
						$"PushToBlender: Updated FBX tools script for Blender {version}.\nFrom: {sourceFullPath}\nTo:   {targetPath}");
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogError(
						$"PushToBlender: Error copying FBX tools script for Blender {version} to '{targetAddonDir}': {ex}");
				}
			}

			if (copiedCount == 0)
			{
				UnityEngine.Debug.LogWarning(
					"PushToBlender: FBX tools script was not copied to any Blender add-ons directory. " +
					"Ensure at least one supported Blender version is installed for the current user.");
			}
		}
	}
}
