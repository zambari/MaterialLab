namespace MaterialLab.Editor
{
	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class MetallicGlossTextureCombiner : VisualElement
	{
		private TextureAdjustElement leftAdjust;
		private TextureAdjustElement rightAdjust;
		private Texture2D pathSource;
		private Texture2D lastCombinedResult;

		/// <summary>Build from matcher (2 or 3 textures): metallic + smoothness/roughness.</summary>
		public MetallicGlossTextureCombiner(
			TextureAssetMatcher matcher,
			System.Collections.Generic.IList<string> createdFiles,
			System.Action<Texture2D> onAssetSaved = null,
			int buttonWidth = 150)
		{
			var main = matcher.Main;
			var metallic = matcher.Metallic;
			var smoothness = matcher.Smoothness;
			var roughness = matcher.Roughness;
			var smoothSource = smoothness ?? roughness;
			BuildEditor(
				metallic,
				smoothSource,
				metallic,
				"Metallic (RGB):",
				"Smoothness (Alpha):",
				main,
				createdFiles,
				onAssetSaved,
				null,
				buttonWidth);
		}

		/// <summary>Build from two channel textures (e.g. decoded RGB + Alpha from one texture). When onTwoAssetsSaved is set, shows "Save as two assets" button.</summary>
		public MetallicGlossTextureCombiner(
			Texture2D leftChannel,
			Texture2D rightChannel,
			Texture2D pathSourceForSave,
			string leftLabel,
			string rightLabel,
			Texture2D mainPreview,
			System.Collections.Generic.IList<string> createdFiles,
			System.Action<Texture2D> onAssetSaved = null,
			System.Action<Texture2D, Texture2D> onTwoAssetsSaved = null,
			int buttonWidth = 150)
		{
			BuildEditor(
				leftChannel,
				rightChannel,
				pathSourceForSave,
				leftLabel,
				rightLabel,
				mainPreview,
				createdFiles,
				onAssetSaved,
				onTwoAssetsSaved,
				buttonWidth);
		}

		private void BuildEditor(
			Texture2D leftTexture,
			Texture2D rightTexture,
			Texture2D pathSourceForSave,
			string leftLabel,
			string rightLabel,
			Texture2D mainPreview,
			System.Collections.Generic.IList<string> createdFiles,
			System.Action<Texture2D> onAssetSaved,
			System.Action<Texture2D, Texture2D> onTwoAssetsSaved,
			int buttonWidth)
		{
			pathSource = pathSourceForSave;

			var texturePreviewRow = new VisualElement() { style = { flexDirection = FlexDirection.Row } };
			if (mainPreview != null)
				texturePreviewRow.Add(GetPreviewWithLabel(mainPreview, "Source"));
			texturePreviewRow.Add(GetPreviewWithLabel(leftTexture, leftLabel));
			texturePreviewRow.Add(GetPreviewWithLabel(rightTexture, rightLabel));
			texturePreviewRow.AddBorder();

			leftAdjust = new TextureAdjustElement(leftTexture, leftLabel);
			rightAdjust = new TextureAdjustElement(rightTexture, rightLabel);

			Add(texturePreviewRow);
			Add(leftAdjust);
			Add(rightAdjust);

			if (leftTexture == null || rightTexture == null)
			{
				Add(new Label("Error: both channel textures are required."));
				return;
			}
			if (leftTexture.width != rightTexture.width || leftTexture.height != rightTexture.height)
			{
				Add(new Label("Error: channel textures have different sizes."));
				return;
			}

			var addTimeStamp = new Toggle("Add Timestamp") { value = true };
			var swapAlphaColor = new Toggle("Swap alpha and color") { value = false };

			Texture2D GetCombinedTexture()
			{
				var leftSource = leftAdjust?.ProcessedTexture;
				var rightSource = rightAdjust?.ProcessedTexture;
				if (leftSource == null || rightSource == null) return null;

				var leftPixels = leftSource.GetPixels();
				var rightPixels = rightSource.GetPixels();
				if (swapAlphaColor.value) (leftPixels, rightPixels) = (rightPixels, leftPixels);

				var result = new Texture2D(leftSource.width, leftSource.height, TextureFormat.RGBA32, false);
				var resultPixels = new Color[leftPixels.Length];
				for (int i = 0; i < leftPixels.Length; i++)
				{
					var m = leftPixels[i];
					var g = rightPixels[i];
					var glossAvg = (g.r + g.g + g.b) / 3f;
					resultPixels[i] = new Color(m.r, m.g, m.b, glossAvg);
				}
				result.SetPixels(resultPixels);
				result.Apply();
				return result;
			}

			var saveRow = new TextureThreeWaySaveRow(createdFiles, onAssetSaved, buttonWidth, "combine");
			saveRow.SetContext(
				pathSource,
				GetCombinedTexture,
				() => addTimeStamp.value ? MaterialLabFileUtils.GetTimestampSuffix() : "combined");

			Add(addTimeStamp);
			Add(swapAlphaColor);
			// Add(new Button(OnCombine) { text = "Combine (gloss in alpha)", tooltip = "Build combined texture; then use the save buttons below." });
			Add(saveRow);

			// void OnCombine()
			// {
			// 	lastCombinedResult = GetCombinedTexture();
			// 	if (lastCombinedResult != null)
			// 	{
			// 		saveRow.SetContext(
			// 			pathSource,
			// 			() => lastCombinedResult,
			// 			() => addTimeStamp.value ? MaterialLabFileUtils.GetTimestampSuffix() : "combined");
			// 	}
			// 	else
			// 		Debug.LogWarning("[MetallicGlossTextureCombiner] Combine failed. Ensure both textures are readable (enable Read/Write or click Fix in the channel editors above).");
			// }

			if (onTwoAssetsSaved != null)
			{
				Add(new Button(() =>
				{
					var leftTex = leftAdjust?.ProcessedTexture;
					var rightTex = rightAdjust?.ProcessedTexture;
					if (leftTex == null || rightTex == null) return;
					var (metallicAsset, smoothnessAsset) = TextureSaveHelper.SaveAsTwoAssets(
						pathSource, leftTex, rightTex, createdFiles);
					if (metallicAsset != null && smoothnessAsset != null)
						onTwoAssetsSaved(metallicAsset, smoothnessAsset);
				})
				{
					text = "Split to two textures (_metallic, _smoothness)",
				});
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
