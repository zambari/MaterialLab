namespace MaterialLab.Editor
{
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;

	using UnityEditor;

	using UnityEngine;

	public static class UnpackZip
	{
		// I repeatedly import assets which are packed as zip files that contain folders
		// default flow with explorer right click, unpack all, creates a folder with a name of the zip, which contains another folder, which contains my files,
		// I want to automate flattening that structure, shortening the paths and filenames (there were cases where git refused to commit a file because of a too long path, despite me deliberately trying to locate the project near the root of my project

		// Optional automation for FBX material/texture extraction.
		// Left disabled by default because behaviour differs slightly between FBX exports
		// and Unity versions, and a broken material reference is worse than doing it manually.
		private const bool k_AutoExtractFbxAssets = false;

		private static readonly List<string> stringsToRemoveFromNames =
			new List<string> { "Meshy_AI_", "_texture", "_fbx" };

		private const string MenuPath = MaterialLabMenu.MenuPathBase + "Unpack Zip";

		[MenuItem(MenuPath, true)]
		public static bool UnpackValidate()
		{
			return Selection.objects.Any(x =>
										 {
											 if (x is not DefaultAsset) return false;

											 var assetPath = AssetDatabase.GetAssetPath(x);
											 if (string.IsNullOrEmpty(assetPath)) return false;

											 return string.Equals(Path.GetExtension(assetPath), ".zip");
										 });
		}

		[MenuItem(MenuPath)]
		public static void Unpack()
		{
			foreach (var obj in Selection.objects)
			{
				if (obj is not DefaultAsset)
				{
					continue;
				}

				var assetPath = AssetDatabase.GetAssetPath(obj);
				if (string.IsNullOrEmpty(assetPath) || !string.Equals(Path.GetExtension(assetPath), ".zip"))
				{
					continue;
				}

				HandleZip(assetPath);
			}

			AssetDatabase.Refresh();
		}

		private static void HandleZip(string assetPath)
		{
			// unpack zip into the current folder.
			// see if upacked folder contains a sub-directory, if so, move all the files from the inner directory to the outer directory.
			// run file rename on all the files, replacing all strings from a list above with "", rename the folder also.
			// the end result should be one folder with files, and no subfolders. if anything unexpected happens - log error and abort, do not try to recover.
			// finally, if all went well, Log 'finished unpacking xx.zip' and remove .zip file from drive

			try
			{
				var projectRoot = Path.GetDirectoryName(Application.dataPath);
				if (string.IsNullOrEmpty(projectRoot))
				{
					Debug.LogError("UnpackZip: Could not resolve project root.");
					return;
				}

				var zipFullPath = Path.Combine(projectRoot, assetPath);
				if (!File.Exists(zipFullPath))
				{
					Debug.LogError($"UnpackZip: Zip file not found on disk: {zipFullPath}");
					return;
				}

				var targetDirRelative = Path.GetDirectoryName(assetPath);
				if (string.IsNullOrEmpty(targetDirRelative))
				{
					Debug.LogError($"UnpackZip: Could not resolve target directory for {assetPath}");
					return;
				}

				var targetDirFull = Path.Combine(projectRoot, targetDirRelative);

				if (!Directory.Exists(targetDirFull))
				{
					Directory.CreateDirectory(targetDirFull);
				}

				var dirsBefore = new HashSet<string>(Directory.GetDirectories(targetDirFull));

				ZipFile.ExtractToDirectory(zipFullPath, targetDirFull);

				var dirsAfter = new HashSet<string>(Directory.GetDirectories(targetDirFull));
				dirsAfter.ExceptWith(dirsBefore);
				var newDirs = dirsAfter.ToArray();

				if (newDirs.Length == 0)
				{
					Debug.LogError($"UnpackZip: No new directory created when unpacking {assetPath}. Aborting.");
					return;
				}

				if (newDirs.Length > 1)
				{
					Debug.LogError(
						$"UnpackZip: More than one top-level directory created when unpacking {assetPath}. Aborting.");
					return;
				}

				var rootDir = newDirs[0];

				// Flatten a single nested directory chain, if present.
				while (true)
				{
					var subDirs = Directory.GetDirectories(rootDir);
					var filesInRoot = Directory.GetFiles(rootDir);

					if (subDirs.Length != 1 || filesInRoot.Length > 0)
					{
						break;
					}

					var inner = subDirs[0];
					var innerSubDirs = Directory.GetDirectories(inner);
					if (innerSubDirs.Length > 0)
					{
						Debug.LogError($"UnpackZip: Nested directories found under {inner}. Aborting.");
						return;
					}

					var innerFiles = Directory.GetFiles(inner);
					foreach (var file in innerFiles)
					{
						var dest = Path.Combine(rootDir, Path.GetFileName(file));
						if (File.Exists(dest))
						{
							Debug.LogError($"UnpackZip: Destination file already exists: {dest}. Aborting.");
							return;
						}

						File.Move(file, dest);
					}

					Directory.Delete(inner, true);
				}

				if (Directory.GetDirectories(rootDir).Length > 0)
				{
					Debug.LogError($"UnpackZip: {rootDir} still contains subdirectories after flatten. Aborting.");
					return;
				}

				// Rename files inside the root directory.
				foreach (var filePath in Directory.GetFiles(rootDir))
				{
					var directory = Path.GetDirectoryName(filePath);
					var fileName = Path.GetFileName(filePath);
					var newName = SanitizeName(fileName);

					if (newName == fileName)
					{
						continue;
					}

					var destPath = Path.Combine(directory, newName);
					if (File.Exists(destPath))
					{
						Debug.LogError(
							$"UnpackZip: Cannot rename {filePath} to {destPath} because destination exists. Aborting.");
						return;
					}

					File.Move(filePath, destPath);
				}

				// Rename the root directory itself.
				var parentDir = Path.GetDirectoryName(rootDir);
				var folderName = Path.GetFileName(rootDir);
				var newFolderName = SanitizeName(folderName);

				if (!string.IsNullOrEmpty(parentDir) && newFolderName != folderName)
				{
					var newRootDir = Path.Combine(parentDir, newFolderName);
					if (Directory.Exists(newRootDir))
					{
						Debug.LogError(
							$"UnpackZip: Cannot rename folder {rootDir} to {newRootDir} because destination exists. Aborting.");
						return;
					}

					Directory.Move(rootDir, newRootDir);
					rootDir = newRootDir;
				}

				if (k_AutoExtractFbxAssets)
				{
					ExtractFbxAssetsUnder(rootDir);
				}

				Debug.Log($"UnpackZip: Finished unpacking {assetPath} into {rootDir}.");

				// Remove the zip both from disk and from the AssetDatabase.
				File.Delete(zipFullPath);
				AssetDatabase.DeleteAsset(assetPath);

				var createdMaterial = TryCreateMaterialFromUnpackedTextures(rootDir);
				if (createdMaterial != null)
				{
					TryRemapFbxMaterialsUnder(rootDir, createdMaterial);
				}
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"UnpackZip: Exception while handling {assetPath}: {ex}");
			}
		}

		private static Material TryCreateMaterialFromUnpackedTextures(string rootDirFullPath)
		{
			const bool k_Verbose = true;

			static void Log(string msg)
			{
				if (k_Verbose) Debug.Log($"UnpackZip(Material): {msg}");
			}

			var projectRoot = Path.GetDirectoryName(Application.dataPath);

			Log($"begin. rootDirFullPath='{rootDirFullPath}'");

			var folderAssetPath = Path.GetRelativePath(projectRoot, rootDirFullPath).Replace("\\", "/");

			// Ensure Unity has imported the freshly-extracted/renamed textures.
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

			var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderAssetPath });
			if (textureGuids == null || textureGuids.Length == 0)
			{
				Log("bail: FindAssets returned no Texture2D GUIDs.");
				return null;
			}

			Log($"found {textureGuids.Length} Texture2D GUID(s).");

			var textures = new List<Texture2D>(textureGuids.Length);
			for (int i = 0; i < textureGuids.Length; i++)
			{
				var texPath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
				if (string.IsNullOrEmpty(texPath))
				{
					Log($"skip: empty asset path for guid[{i}]='{textureGuids[i]}'");
					continue;
				}

				var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
				if (tex == null)
				{
					Log($"skip: LoadAssetAtPath<Texture2D> returned null for '{texPath}'");
					continue;
				}

				textures.Add(tex);
			}

			Log($"loaded {textures.Count} Texture2D(s). First='{textures[0].name}'");

			var matcher = new TextureAssetMatcher(textures);
			Log($"calling MaterialTab.CreateMaterialFromMatcher(suggested='{matcher.SuggestedMaterialName}')");
			try
			{
				return MaterialTab.CreateMaterialFromMatcher(matcher, addTimestamp: true);
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"UnpackZip(Material): exception creating material: {ex}");
			}

			return null;
		}

		private static void TryRemapFbxMaterialsUnder(string rootDirFullPath, Material materialOverride)
		{
			if (materialOverride == null) return;
			if (string.IsNullOrEmpty(rootDirFullPath)) return;
			if (!Directory.Exists(rootDirFullPath)) return;

			const bool k_Verbose = true;
			static void Log(string msg)
			{
				if (k_Verbose) Debug.Log($"UnpackZip(FBX Remap): {msg}");
			}

			var projectRoot = Path.GetDirectoryName(Application.dataPath);
			if (string.IsNullOrEmpty(projectRoot)) return;

			string folderAssetPath;
			try
			{
				folderAssetPath = Path.GetRelativePath(projectRoot, rootDirFullPath).Replace("\\", "/");
			}
			catch (System.Exception ex)
			{
				Log($"bail: Path.GetRelativePath failed: {ex.Message}");
				return;
			}

			if (string.IsNullOrEmpty(folderAssetPath) || !folderAssetPath.StartsWith("Assets/"))
			{
				Log($"bail: folderAssetPath not under Assets. folderAssetPath='{folderAssetPath}'");
				return;
			}

			var modelGuids = AssetDatabase.FindAssets("t:Model", new[] { folderAssetPath });
			if (modelGuids == null || modelGuids.Length == 0)
			{
				Log("bail: found 0 models under folder.");
				return;
			}

			var fbxPaths = modelGuids.Select(AssetDatabase.GUIDToAssetPath)
									 .Where(p => !string.IsNullOrEmpty(p) && p.EndsWith(".fbx"))
									 .Distinct()
									 .ToArray();

			if (fbxPaths.Length != 1)
			{
				Log($"bail: expected exactly 1 .fbx under '{folderAssetPath}', found {fbxPaths.Length}.");
				return;
			}

			var modelPath = fbxPaths[0];
			if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
			{
				Log($"bail: could not get ModelImporter for '{modelPath}'.");
				return;
			}

			if (TryGetTriangleCountAndMeshCount(modelPath, out var triangles, out var meshCount))
			{
				var extra = meshCount == 1 ? string.Empty : $", meshes={meshCount}";
				Log($"triangles={triangles}{extra}");
			}

			var embeddedMaterials = AssetDatabase.LoadAllAssetsAtPath(modelPath)
												 .OfType<Material>()
												 .Where(m => m != null && AssetDatabase.GetAssetPath(m) == modelPath)
												 .ToArray();

			if (embeddedMaterials.Length != 1)
			{
				Log($"bail: expected exactly 1 embedded material in '{modelPath}', found {embeddedMaterials.Length}.");
				return;
			}

			var embeddedName = embeddedMaterials[0].name;
			if (string.IsNullOrWhiteSpace(embeddedName))
			{
				Log($"bail: embedded material name is empty for '{modelPath}'.");
				return;
			}

			var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), embeddedName);
			importer.AddRemap(id, materialOverride);
			importer.SaveAndReimport();
		}

		private static bool TryGetTriangleCountAndMeshCount(string modelAssetPath, out long triangles, out int meshCount)
		{
			triangles = 0;
			meshCount = 0;
			if (string.IsNullOrEmpty(modelAssetPath)) return false;

			var root = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
			if (root == null) return false;

			var meshFilters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
			var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);

			var meshes = new HashSet<Mesh>();

			for (int i = 0; i < meshFilters.Length; i++)
			{
				var mf = meshFilters[i];
				if (mf == null) continue;
				var mesh = mf.sharedMesh;
				if (mesh == null) continue;
				meshes.Add(mesh);
			}

			for (int i = 0; i < skinned.Length; i++)
			{
				var skinnedRenderer = skinned[i];
				if (skinnedRenderer == null) continue;
				var mesh = skinnedRenderer.sharedMesh;
				if (mesh == null) continue;
				meshes.Add(mesh);
			}

			foreach (var mesh in meshes)
			{
				if (mesh == null) continue;
				for (int s = 0; s < mesh.subMeshCount; s++)
				{
					triangles += (long)(mesh.GetIndexCount(s) / 3);
				}
			}

			meshCount = meshes.Count;
			return true;
		}

		private static void ExtractFbxAssetsUnder(string rootDirFullPath)
		{
			if (!Directory.Exists(rootDirFullPath))
			{
				return;
			}

			var projectRoot = Path.GetDirectoryName(Application.dataPath);
			if (string.IsNullOrEmpty(projectRoot))
			{
				return;
			}

			// Get all FBX files in this folder.
			var fbxFiles = Directory.GetFiles(rootDirFullPath, "*.fbx");
			if (fbxFiles.Length == 0)
			{
				return;
			}

			foreach (var fbxFullPath in fbxFiles)
			{
				var relativePath = fbxFullPath.Substring(projectRoot.Length + 1).Replace("\\", "/");
				var importer = AssetImporter.GetAtPath(relativePath) as ModelImporter;
				if (importer == null)
				{
					// This can happen if Unity hasn't imported the freshly-created FBX yet.
					AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceSynchronousImport);
					importer = AssetImporter.GetAtPath(relativePath) as ModelImporter;
					if (importer == null)
					{
						Debug.LogWarning(
							$"UnpackZip: Could not get ModelImporter for FBX at {relativePath} even after import.");
						continue;
					}
				}

				// Ensure materials are stored externally and searched broadly so Unity can wire them up.
				importer.materialLocation = ModelImporterMaterialLocation.External;
				importer.materialSearch = ModelImporterMaterialSearch.Everywhere;

				// Extract embedded textures, if any, directly into the FBX folder (no extra subfolder).
				var fbxDirOnDisk = Path.GetDirectoryName(fbxFullPath);
				var fbxDirAsset = Path.GetDirectoryName(relativePath)?.Replace("\\", "/");

				if (!string.IsNullOrEmpty(fbxDirOnDisk) && !string.IsNullOrEmpty(fbxDirAsset))
				{
					var texturesDirOnDisk = fbxDirOnDisk; // working directory on disk
					var texturesDirAsset = fbxDirAsset;   // same folder as FBX in the AssetDatabase

					try
					{
						var extracted = importer.ExtractTextures(texturesDirAsset);
						if (extracted)
						{
							Debug.Log($"UnpackZip: Extracted textures for {relativePath} into {texturesDirAsset}");
						}
						else
						{
							Debug.Log($"UnpackZip: No embedded textures to extract for {relativePath}");
						}
					}
					catch (System.Exception ex)
					{
						Debug.LogError(
							$"UnpackZip: Failed to extract textures for {relativePath} into {texturesDirAsset}: {ex}");
					}
				}

				// Reimport to apply material/texture changes.
				AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);

				// Rename single extracted material to match mesh name (in place; do not move or delete Materials folder or .fbm – that breaks FBX/material references).
				RenameSingleMaterialForModel(relativePath);

				Debug.Log($"UnpackZip: Reimported FBX {relativePath} with external materials and texture extraction.");
			}
		}

		private static void RenameSingleMaterialForModel(string modelAssetPath)
		{
			if (string.IsNullOrEmpty(modelAssetPath)) return;

			var modelName = Path.GetFileNameWithoutExtension(modelAssetPath);
			var modelDir = Path.GetDirectoryName(modelAssetPath)?.Replace("\\", "/");
			if (string.IsNullOrEmpty(modelDir)) return;

			var materialsFolder = modelDir + "/Materials";
			if (!AssetDatabase.IsValidFolder(materialsFolder)) return;

			var matGuids = AssetDatabase.FindAssets("t:Material", new[] { materialsFolder });
			if (matGuids == null || matGuids.Length != 1) return;

			var matPath = AssetDatabase.GUIDToAssetPath(matGuids[0]);
			if (string.IsNullOrEmpty(matPath)) return;

			var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
			if (mat == null) return;

			if (mat.name == modelName) return;

			AssetDatabase.RenameAsset(matPath, modelName);
		}

		private static string SanitizeName(string name)
		{
			var result = name;
			for (int i = 0; i < stringsToRemoveFromNames.Count; i++)
			{
				var toRemove = stringsToRemoveFromNames[i];
				if (!string.IsNullOrEmpty(toRemove))
				{
					result = result.Replace(toRemove, string.Empty);
				}
			}

			return result;
		}
	}
}
