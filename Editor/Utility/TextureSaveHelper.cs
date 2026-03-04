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
		/// <summary>Writes texture pixels to the source asset path (overwrites).</summary>
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
			File.WriteAllBytes(path, bytes);
			AssetDatabase.ImportAsset(path);
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
			File.WriteAllBytes(newPath, bytes);
			createdFiles?.Add(newPath);
			createdFiles?.Add(Path.ChangeExtension(newPath, ".meta"));
			AssetDatabase.ImportAsset(newPath);
			return AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
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
			File.Copy(originalPath, backupPath);
			createdFiles?.Add(backupPath);
			createdFiles?.Add(Path.ChangeExtension(backupPath, ".meta"));
			AssetDatabase.ImportAsset(backupPath);

			SaveInPlace(source, textureToSave);
			return AssetDatabase.LoadAssetAtPath<Texture2D>(backupPath);
		}

		private static byte[] EncodeToBytes(Texture2D texture, string ext)
		{
			return ext == ".jpg" || ext == ".jpeg"
				? texture.EncodeToJPG()
				: texture.EncodeToPNG();
		}
	}
}
