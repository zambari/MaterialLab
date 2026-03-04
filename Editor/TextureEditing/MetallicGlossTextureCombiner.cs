namespace MaterialLab.Editor
{
	using System.IO;

	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class MetallicGlossTextureCombiner : VisualElement
	{
		private TextureAdjustElement metallicAdjust;

		private TextureAdjustElement glossAdjust;

		public MetallicGlossTextureCombiner(Texture2D mainTexture, Texture2D metallic, Texture2D gloss)
		{
			var content = new VisualElement();
			var texturePreviewRow = new VisualElement() { style = { flexDirection = FlexDirection.Row } };
			texturePreviewRow.Add(GetPreviewWithLabel(mainTexture, "Main texture"));
			texturePreviewRow.Add(GetPreviewWithLabel(metallic, "Metallic texture"));
			texturePreviewRow.Add(GetPreviewWithLabel(gloss, "Gloss"));
			texturePreviewRow.AddBorder();
			metallicAdjust = new TextureAdjustElement(metallic, "Mettalic");
			glossAdjust = new TextureAdjustElement(gloss, "Gloss");

			Add(texturePreviewRow);
			Add(metallicAdjust);
			Add(glossAdjust);
			var addTimeStamp = new Toggle("Add TimeStamp") { value = true };
			var swapAlphaColor = new Toggle("Swap alpha and color") { value = false };
			if (metallic != null && gloss != null)
			{
				if (metallic.width != gloss.width || metallic.height != gloss.height)
				{
					Add(new Label("Error, metallic and gloss have different sizes"));
				}
				else
				{
					Add(new Button(GetCombinedGlossMetallic) { text = "Save combined gloss and metallic" });
					Add(addTimeStamp);
				}
			}

			void GetCombinedGlossMetallic()
			{
				var metallicSource = metallicAdjust.ProcessedTexture;
				var glossSource = glossAdjust.ProcessedTexture;

				var metallicPixels = metallicSource.GetPixels();
				var glossPixels = glossSource.GetPixels();
				if (swapAlphaColor.value) (metallicPixels, glossPixels) = (glossPixels, metallicPixels);
				
				var resultTexture = new Texture2D(
					metallicSource.width,
					metallicSource.height,
					TextureFormat.RGBA32,
					false);
				var resultPixels = new Color[metallicPixels.Length];

				for (int i = 0; i < metallicPixels.Length; i++)
				{
					var m = metallicPixels[i];
					var g = glossPixels[i];
					var glossAvg = (g.r + g.g + g.b) / 3f;
					resultPixels[i] = new Color(m.r, m.g, m.b, glossAvg);
				}

				resultTexture.SetPixels(resultPixels);
				resultTexture.Apply();

				var metallicPath = AssetDatabase.GetAssetPath(metallic);
				var directory = Path.GetDirectoryName(metallicPath);
				var fileName = Path.GetFileNameWithoutExtension(metallicPath);
				var dateSuffix = addTimeStamp.value ? System.DateTime.Now.ToString("yyyyMMddHHmmss") : "combined";
				var newPath = Path.Combine(directory, $"{fileName}_{dateSuffix}.png");

				var bytes = resultTexture.EncodeToPNG();
				File.WriteAllBytes(newPath, bytes);
				AssetDatabase.Refresh();

				var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
				EditorGUIUtility.PingObject(asset);
				Debug.Log($"[{nameof(MetallicGlossTextureCombiner)}] Saved combined texture to {newPath}");
			}
		}

		private VisualElement GetPreviewWithLabel(Texture2D texture, string label)
		{
			if (texture == null) return null;

			var content = new VisualElement();
			content.style.marginLeft = 10;
			content.Add(new Label(label));
			content.Add(new TexturePreview(texture));
			return content;
		}
	}
}
