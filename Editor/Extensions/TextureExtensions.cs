namespace MaterialLab.Editor
{
	using UnityEditor;

	using UnityEngine;

	public static class TextureExtensions
	{
		public static Texture2D ResizeTexture(this Texture2D texture, int newWidth, int newHeight)
		{
			if (texture == null) return null;

			RenderTexture tmp = RenderTexture.GetTemporary(
				newWidth,
				newHeight,
				0,
				RenderTextureFormat.ARGB32,
				RenderTextureReadWrite.Default);
			RenderTexture.active = tmp;
			Graphics.Blit(texture, tmp);
			texture.Reinitialize(newWidth, newHeight, texture.format, false);
			texture.filterMode = FilterMode.Bilinear;
			texture.ReadPixels(new Rect(Vector2.zero, new Vector2(newWidth, newHeight)), 0, 0);
			texture.Apply();
			RenderTexture.ReleaseTemporary(tmp);
			return texture;
		}


		/// <summary>
		/// Ensures the imported texture asset is marked readable.
		/// This modifies the asset on disk (import settings) and reloads it.
		/// </summary>
		public static Texture2D MakeReadableInPlace(this Texture2D texture)
		{
			if (texture == null) return null;

			var path = AssetDatabase.GetAssetPath(texture);
			if (string.IsNullOrEmpty(path)) return texture; // Runtime texture

			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			if (importer == null) return texture;

			bool needReadable = !importer.isReadable;
			var settings = importer.GetDefaultPlatformTextureSettings();
			bool needUncompressed = settings.textureCompression != TextureImporterCompression.Uncompressed;

			if (!needReadable && !needUncompressed) return texture;

			if (needReadable) importer.isReadable = true;
			if (needUncompressed)
			{
				settings.format = TextureImporterFormat.RGBA32;
				settings.textureCompression = TextureImporterCompression.Uncompressed;
				importer.SetPlatformTextureSettings(settings);
			}

			importer.SaveAndReimport();
			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		/// simpler version - does not check compression:

		// public static Texture2D MakeReadableInPlace(this Texture2D texture)
		// {
		// 	if (texture == null) return null;
		// 	if (texture.isReadable) return texture;
		//
		// 	var path = AssetDatabase.GetAssetPath(texture);
		// 	if (string.IsNullOrEmpty(path))
		// 	{
		// 		Debug.LogWarning($"[{nameof(TextureExtensions)}] Cannot make runtime texture readable in-place.");
		// 		return texture;
		// 	}
		//
		// 	if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
		// 	{
		// 		Debug.LogWarning($"[{nameof(TextureExtensions)}] Failed to get TextureImporter for '{path}'.");
		// 		return texture;
		// 	}
		//
		// 	if (!importer.isReadable)
		// 	{
		// 		importer.isReadable = true;
		// 		importer.SaveAndReimport();
		// 		Debug.Log($"[{nameof(TextureExtensions)}] Marked '{path}' as Read/Write Enabled.");
		// 	}
		//
		// 	return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		// }
		public static bool EnsureReadableWithStatus(this Texture2D texture)
		{
			var path = AssetDatabase.GetAssetPath(texture);
			var importer = (TextureImporter)AssetImporter.GetAtPath(path);

			bool wasReadable = importer.isReadable;

			if (!wasReadable)
			{
				importer.isReadable = true;
				importer.SaveAndReimport();
				return false;
			}

			return true;
		}
	}
}
