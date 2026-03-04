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

		public static Texture2D EnsureReadableWarningModifiesAsset(this Texture2D texture)
		{
			if (texture == null) return null;
			if (texture.isReadable) return texture;

			var path = AssetDatabase.GetAssetPath(texture);
			if (string.IsNullOrEmpty(path)) return texture; // Runtime texture

			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			if (importer == null) return texture; //null?

			bool wasReadable = importer.isReadable;

			if (!wasReadable)
			{
				importer.isReadable = true;
				importer.SaveAndReimport();
			}

			AssetDatabase.SaveAssets(); //?

			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

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
