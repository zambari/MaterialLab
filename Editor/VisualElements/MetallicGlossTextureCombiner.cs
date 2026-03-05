namespace MaterialLab.Editor
{
	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class MetallicGlossTextureCombiner : VisualElement
	{
		private TextureAdjustElement metallicAdjust;
		private TextureAdjustElement glossAdjust;
		private Texture2D lastCombinedResult;

		public MetallicGlossTextureCombiner(
			TextureAssetMatcher matcher,
			System.Collections.Generic.IList<string> createdFiles,
			System.Action<Texture2D> onAssetSaved = null,
			int buttonWidth = 150)
		{
			var mainTexture = matcher.Main;
			var metallic = matcher.Metallic;
			var roughness = matcher.Roughness;
			var smoothness = matcher.Smoothness;
			var smoothSource = smoothness ?? roughness;

			var texturePreviewRow = new VisualElement() { style = { flexDirection = FlexDirection.Row } };
			texturePreviewRow.Add(GetPreviewWithLabel(mainTexture, "Main texture"));
			texturePreviewRow.Add(GetPreviewWithLabel(metallic, "Metallic texture"));
			texturePreviewRow.Add(GetPreviewWithLabel(smoothSource, smoothness != null ? "Smoothness" : "Roughness"));
			texturePreviewRow.AddBorder();
			metallicAdjust = new TextureAdjustElement(metallic, "Metallic (RGB):");
			glossAdjust = new TextureAdjustElement(smoothSource, "Smoothness (Alpha):");

			Add(texturePreviewRow);
			Add(metallicAdjust);
			Add(glossAdjust);

			var addTimeStamp = new Toggle("Add Timestamp") { value = true };
			var swapAlphaColor = new Toggle("Swap alpha and color") { value = false };

			if (metallic != null && smoothSource != null)
			{
				if (metallic.width != smoothSource.width || metallic.height != smoothSource.height)
				{
					Add(new Label("Error, metallic and smooth/rough have different sizes"));
				}
				else
				{
					Add(addTimeStamp);
					Add(swapAlphaColor);

					var saveRow = new TextureThreeWaySaveRow(createdFiles, onAssetSaved, buttonWidth);
					saveRow.SetContext(null, null, null);
					Add(new Button(OnCombine) { text = "Combine (gloss in alpha)" });
					Add(saveRow);

					void OnCombine()
					{
						var metallicSource = metallicAdjust?.ProcessedTexture;
						var glossSource = glossAdjust?.ProcessedTexture;
						if (metallicSource == null || glossSource == null) return;

						var metallicPixels = metallicSource.GetPixels();
						var glossPixels = glossSource.GetPixels();
						if (swapAlphaColor.value) (metallicPixels, glossPixels) = (glossPixels, metallicPixels);

						lastCombinedResult = new Texture2D(
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
						lastCombinedResult.SetPixels(resultPixels);
						lastCombinedResult.Apply();

						saveRow.SetContext(
							metallic,
							() => lastCombinedResult,
							() => addTimeStamp.value ? MaterialLabFileUtils.GetTimestampSuffix() : "combined");
					}
				}
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
