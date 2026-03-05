namespace MaterialLab.Editor
{
	using System.Collections.Generic;
	using System.IO;

	using UnityEditor;

	using UnityEngine;

	/// <summary>
	/// Shared file handling for texture save-in-place, save-as-new, and save-with-backup.
	/// </summary>
	internal static class TextureSaveHelper
	{
		/// <summary>Writes texture pixels to the source asset path (overwrites). Saved asset is imported with Read/Write enabled.</summary>
		public static void SaveInPlace(Texture2D source, Texture2D textureToSave)
		{
			if (source == null || textureToSave == null) return;

			var path = AssetDatabase.GetAssetPath(source);
			if (string.IsNullOrEmpty(path))
			{
				Debug.LogWarning($"[{nameof(TextureSaveHelper)}] Cannot save in place, texture has no asset path.");
				return;
			}

			var ext = Path.GetExtension(path).ToLowerInvariant();
			byte[] bytes = EncodeToBytes(textureToSave, ext);
			MaterialLabFileUtils.WriteBytes(path, bytes, allowOverwrite: true);
			ImportAndSetReadable(path);
			EditorUtility.SetDirty(source);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		/// <summary>Saves to a unique path with the given suffix. Adds paths to createdFiles. Returns the new asset.</summary>
		public static Texture2D SaveAsNew(
			Texture2D source,
			Texture2D textureToSave,
			string suffix,
			IList<string> createdFiles)
		{
			if (source == null || textureToSave == null) return null;

			var originalPath = AssetDatabase.GetAssetPath(source);
			if (string.IsNullOrEmpty(originalPath))
			{
				Debug.LogWarning($"[{nameof(TextureSaveHelper)}] Cannot save as new, texture has no asset path.");
				return null;
			}

			var newPath = MaterialLabFileUtils.GetUniquePathWithSuffix(originalPath, suffix);
			var ext = Path.GetExtension(newPath).ToLowerInvariant();
			byte[] bytes = EncodeToBytes(textureToSave, ext);
			var writtenPath = MaterialLabFileUtils.WriteBytes(newPath, bytes, allowOverwrite: false);
			createdFiles?.Add(writtenPath);
			createdFiles?.Add(Path.ChangeExtension(writtenPath, ".meta"));
			ImportAndSetReadable(writtenPath);
			return AssetDatabase.LoadAssetAtPath<Texture2D>(writtenPath);
		}

		/// <summary>Backs up the original file, then overwrites original with textureToSave. Adds backup path to createdFiles. Returns the backup asset.</summary>
		public static Texture2D SaveWithBackup(Texture2D source, Texture2D textureToSave, IList<string> createdFiles)
		{
			if (source == null || textureToSave == null) return null;

			var originalPath = AssetDatabase.GetAssetPath(source);
			if (string.IsNullOrEmpty(originalPath))
			{
				Debug.LogWarning($"[{nameof(TextureSaveHelper)}] Cannot save with backup, texture has no asset path.");
				return null;
			}

			var backupSuffix = $"backup_{MaterialLabFileUtils.GetTimestampSuffix()}";
			var backupPath = MaterialLabFileUtils.GetUniquePathWithSuffix(originalPath, backupSuffix);
			if (File.Exists(backupPath))
				backupPath = MaterialLabFileUtils.GetUniquePath(backupPath);
			File.Copy(originalPath, backupPath);
			createdFiles?.Add(backupPath);
			createdFiles?.Add(Path.ChangeExtension(backupPath, ".meta"));
			AssetDatabase.ImportAsset(backupPath);

			SaveInPlace(source, textureToSave);
			return AssetDatabase.LoadAssetAtPath<Texture2D>(backupPath);
		}

		/// <summary>Saves two textures as separate assets with _metallic and _smoothness suffixes. Path is taken from pathSource. Adds to createdFiles. Returns (metallicAsset, smoothnessAsset).</summary>
		public static (Texture2D metallic, Texture2D smoothness) SaveAsTwoAssets(
			Texture2D pathSource,
			Texture2D metallicTexture,
			Texture2D smoothnessTexture,
			IList<string> createdFiles)
		{
			if (pathSource == null || metallicTexture == null || smoothnessTexture == null)
				return (null, null);

			var originalPath = AssetDatabase.GetAssetPath(pathSource);
			if (string.IsNullOrEmpty(originalPath))
			{
				Debug.LogWarning($"[{nameof(TextureSaveHelper)}] Cannot save as two assets, path source has no asset path.");
				return (null, null);
			}

			var ext = Path.GetExtension(originalPath).ToLowerInvariant();
			var metallicPath = MaterialLabFileUtils.GetUniquePathWithSuffix(originalPath, "metallic");
			var smoothnessPath = MaterialLabFileUtils.GetUniquePathWithSuffix(originalPath, "smoothness");

			var metallicBytes = EncodeToBytes(metallicTexture, ext);
			var smoothnessBytes = EncodeToBytes(smoothnessTexture, ext);

			metallicPath = MaterialLabFileUtils.WriteBytes(metallicPath, metallicBytes, allowOverwrite: false);
			smoothnessPath = MaterialLabFileUtils.WriteBytes(smoothnessPath, smoothnessBytes, allowOverwrite: false);

			createdFiles?.Add(metallicPath);
			createdFiles?.Add(Path.ChangeExtension(metallicPath, ".meta"));
			createdFiles?.Add(smoothnessPath);
			createdFiles?.Add(Path.ChangeExtension(smoothnessPath, ".meta"));

			ImportAndSetReadable(metallicPath);
			ImportAndSetReadable(smoothnessPath);

			return (
				AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath),
				AssetDatabase.LoadAssetAtPath<Texture2D>(smoothnessPath));
		}

		private static void ImportAndSetReadable(string path)
		{
			AssetDatabase.ImportAsset(path);
			if (AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.isReadable)
			{
				importer.isReadable = true;
				importer.SaveAndReimport();
			}
		}

		private static byte[] EncodeToBytes(Texture2D texture, string ext)
		{
			Texture2D toEncode = texture.isReadable ? texture : GetReadableCopy(texture);
			if (toEncode == null) return null;
			byte[] result = ext == ".jpg" || ext == ".jpeg"
				? toEncode.EncodeToJPG()
				: toEncode.EncodeToPNG();
			if (toEncode != texture)
				UnityEngine.Object.DestroyImmediate(toEncode);
			return result;
		}

		private static Texture2D GetReadableCopy(Texture2D source)
		{
			var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
			RenderTexture.active = rt;
			Graphics.Blit(source, rt);
			var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
			copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
			copy.Apply();
			RenderTexture.active = null;
			RenderTexture.ReleaseTemporary(rt);
			return copy;
		}
	}
}
