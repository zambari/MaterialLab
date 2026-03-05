namespace MaterialLab.Editor
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	using MaterialLab.Tabs;

	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class TextureCombinerTab : MaterialLabTab
	{
		private const int ElementWidth = 150;

		private readonly VisualElement content;
		private readonly Label recognizedRolesLabel;
		private readonly List<string> createdFiles = new();

		// Single-texture decode state: when user clicks Decode on one texture
		private Texture2D decodedSource;
		private Texture2D decodedRgb;
		private Texture2D decodedAlpha;

		/// <inheritdoc />
		public TextureCombinerTab() : base("Texture Combiner")
		{
			recognizedRolesLabel = new LabelInfo();
			Add(recognizedRolesLabel);

			content = new VisualElement();
			Add(content);
		}

		/// <inheritdoc />
		public override string Name => "Texture Combiner";

		protected override void OnSelectionChanged()
		{
			if (content == null) return;

			content.Clear();

			if (createdFiles.Count > 0)
			{
				content.Add(
					new Button(DeleteFilesCreatedInThisSession)
					{
						text = "Undo File Creation",
						style = { width = ElementWidth }
					});
			}

			if (Selection.activeObject is not Texture2D)
			{
				recognizedRolesLabel.text = "Recognized texture roles: None";
				content.Add(new Label("Select one texture to decode (RGB/Alpha), or 2–3 textures to combine."));
				return;
			}

			var selectedTextures = Selection.objects.OfType<Texture2D>().ToArray();
			var matcher = new TextureAssetMatcher(selectedTextures);
			var roles = matcher.GetRecognizedTextures();
			recognizedRolesLabel.text = roles.Count > 0
				? $"Recognized texture roles: {string.Join(", ", roles)}"
				: "Recognized texture roles: None";

			// Clear decoded state if selection no longer matches
			if (decodedSource != null && (selectedTextures.Length != 1 || selectedTextures[0] != decodedSource))
				decodedSource = decodedRgb = decodedAlpha = null;

			void OnAssetSaved(Texture2D savedAsset)
			{
				Selection.activeObject = savedAsset;
				EditorGUIUtility.PingObject(savedAsset);
			}

			if (selectedTextures.Length == 1)
			{
				var single = selectedTextures[0];
				if (decodedRgb != null && decodedAlpha != null && decodedSource == single)
				{
					// Already decoded: show same editor with RGB + Alpha (in-memory pair → "Save as two assets" available)
					void OnTwoAssetsSaved(Texture2D metallicAsset, Texture2D smoothnessAsset)
					{
						Selection.objects = new Object[] { metallicAsset, smoothnessAsset };
						EditorGUIUtility.PingObject(metallicAsset);
					}
					content.Add(new MetallicGlossTextureCombiner(
						decodedRgb,
						decodedAlpha,
						single,
						"RGB",
						"Alpha",
						single,
						createdFiles,
						OnAssetSaved,
						OnTwoAssetsSaved,
						ElementWidth));
				}
				else
				{
					// Show Decode button to split into RGB and Alpha
					content.Add(new Label("One texture selected. Decode to edit RGB and Alpha separately."));
					content.Add(new Button(() =>
					{
						DecodeToRgbAlpha(single);
						RepaintContent();
					})
					{
						text = "Decode (RGB + Alpha)",
						style = { width = ElementWidth }
					});
				}
				return;
			}

			if (selectedTextures.Length == 2 || selectedTextures.Length == 3)
			{
				// Two or three textures: show combiner with assignment from matcher
				content.Add(new MetallicGlossTextureCombiner(matcher, createdFiles, OnAssetSaved, ElementWidth));
			}
			else
			{
				content.Add(new Label("Select 1 texture to decode, or 2–3 textures to combine."));
			}
		}

		private void DecodeToRgbAlpha(Texture2D source)
		{
			if (source == null) return;

			bool wasReadable = source.EnsureReadableWithStatus();
			Color[] pixels;
			try
			{
				pixels = source.GetPixels();
			}
			finally
			{
				if (!wasReadable) RestoreReadable(source);
			}

			int w = source.width, h = source.height;
			var rgbCopy = new Texture2D(w, h, TextureFormat.RGBA32, false);
			var alphaCopy = new Texture2D(w, h, TextureFormat.RGBA32, false);
			var rgbPixels = new Color[pixels.Length];
			var alphaPixels = new Color[pixels.Length];
			for (int i = 0; i < pixels.Length; i++)
			{
				var p = pixels[i];
				rgbPixels[i] = new Color(p.r, p.g, p.b, 1f);
				alphaPixels[i] = new Color(p.a, p.a, p.a, 1f);
			}
			rgbCopy.SetPixels(rgbPixels);
			rgbCopy.Apply();
			alphaCopy.SetPixels(alphaPixels);
			alphaCopy.Apply();

			decodedSource = source;
			decodedRgb = rgbCopy;
			decodedAlpha = alphaCopy;
		}

		private static void RestoreReadable(Texture2D texture)
		{
			var path = AssetDatabase.GetAssetPath(texture);
			if (string.IsNullOrEmpty(path)) return;
			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			importer.isReadable = false;
			importer.SaveAndReimport();
		}

		private void RepaintContent() => OnSelectionChanged();

		private void DeleteFilesCreatedInThisSession()
		{
			foreach (var path in createdFiles)
			{
				if (Path.GetExtension(path) != ".meta")
					Debug.Log($"Deleting {path}");
				File.Delete(path);
			}
			createdFiles.Clear();
			AssetDatabase.Refresh();
		}
	}
}
