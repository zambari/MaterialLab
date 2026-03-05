namespace MaterialLab.Editor
{
	using MaterialLab.UIExtensions;

	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class TexturePreviewElement : VisualElement
	{
		private readonly Texture2D sourceTexture;

		private readonly TexturePreview preview;

		private Texture2D alphaTexture;

		private Texture2D alphaNormalizedTexture;

		private float alphaMin = 0f;

		private float alphaMax = 1f;

		private bool alphaAnalyzed;

		private bool hasAlpha;

		private int alphaMode; // 0 = color, 1 = alpha, 2 = normalized alpha

		private Label alphaToggleLabel;

		private Label alphaInfoLabel;

		public TexturePreviewElement(Texture2D texture)
		{
			sourceTexture = texture;

			int padding = 5;
			int margin = 5;
			var (sizex, sizey) = TexturePreview.GetScaledTextureSize(texture);
			var checker = new CheckerElement(sizex, sizey);
			preview = new TexturePreview(GetDisplayTexture(texture));
			Add(checker);
			checker.Add(preview);
			this.SetPadding(padding);
			this.SetMargin(margin);
			this.SetBackgroundColor(Color.black * 0.3f);
			this.style.width = Length.Pixels(sizex + 2 * padding);
			this.style.height = Length.Pixels(sizey + 2 * padding);

			// Alpha tools overlay
			if (true || MightHaveAlpha(texture))
			{
				var overlay = new VisualElement { style = { position = Position.Absolute, top = 2, right = 2 } };

				alphaToggleLabel = new Label("[A]")
				{
					style =
					{
						fontSize = 10,
						unityTextAlign = TextAnchor.MiddleCenter,
						backgroundColor = new Color(0f, 0f, 0f, 0.6f),
						color = Color.white,
						top = 2,
						right = 2,
						opacity = 0.5f,
						position = Position.Absolute,
					}
				};
				alphaToggleLabel.RegisterCallback<ClickEvent>(_ => CycleAlphaMode());

				alphaInfoLabel = new Label()
				{
					style =
					{
						opacity = 0.5f,
						fontSize = 9,
						color = Color.white,
						unityTextAlign = TextAnchor.MiddleCenter,
						backgroundColor = new Color(0f, 0f, 0f, 0.6f),
						bottom = 2,
						right = 2,
						position = Position.Absolute,
					}
				};

				overlay.Add(alphaToggleLabel);
				overlay.Add(alphaInfoLabel);
				Add(overlay);
			}
		}

		private static Texture2D GetDisplayTexture(Texture2D texture)
		{
			if (texture == null) return null;

			// For normal maps, use Unity's generated preview rather than the raw texture,
			// to avoid the magenta/encoded normal map look in UI Toolkit.
			var path = AssetDatabase.GetAssetPath(texture);
			if (!string.IsNullOrEmpty(path))
			{
				if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
					importer.textureType == TextureImporterType.NormalMap)
				{
					var preview = AssetPreview.GetAssetPreview(texture);
					if (preview != null) return preview;
				}
			}

			return texture;
		}

		private static bool MightHaveAlpha(Texture2D texture)
		{
			if (texture == null) return false;

			// Simple heuristic based on format name; avoids expensive reads until user clicks.
			var formatName = texture.format.ToString();
			return formatName.Contains("RGBA") ||
				   formatName.Contains("ARGB") ||
				   formatName.Contains("BGRA") ||
				   formatName.Contains("Alpha");
		}

		private void CycleAlphaMode()
		{
			if (sourceTexture == null || alphaToggleLabel == null) return;

			if (!alphaAnalyzed)
			{
				AnalyzeAlpha();
			}

			if (!hasAlpha)
			{
				// No meaningful alpha – show hint and keep original texture.
				if (alphaInfoLabel != null)
				{
					alphaInfoLabel.style.display = DisplayStyle.Flex;
					alphaInfoLabel.text = "no alpha";
				}

				preview.Texture = sourceTexture;
				alphaMode = 0;
				return;
			}

			alphaMode = (alphaMode + 1) % 3;

			switch (alphaMode)
			{
				case 0:
					preview.Texture = sourceTexture;
					if (alphaInfoLabel != null) alphaInfoLabel.style.display = DisplayStyle.None;
					break;

				case 1:
					preview.Texture = alphaTexture;
					if (alphaInfoLabel != null)
					{
						alphaInfoLabel.style.display = DisplayStyle.Flex;
						alphaInfoLabel.text = $"α [{alphaMin:0.000}..{alphaMax:0.000}]";
					}

					break;

				case 2:
					preview.Texture = alphaNormalizedTexture;
					if (alphaInfoLabel != null)
					{
						alphaInfoLabel.style.display = DisplayStyle.Flex;
						alphaInfoLabel.text = $"α norm [{alphaMin:0.000}..{alphaMax:0.000}]";
					}

					break;
			}
		}

		private void AnalyzeAlpha()
		{
			alphaAnalyzed = true;
			hasAlpha = false;

			if (sourceTexture == null)
			{
				return;
			}

			if (!sourceTexture.isReadable)
			{
				Debug.LogWarning(
					$"[{nameof(TexturePreviewElement)}] Texture '{sourceTexture.name}' is not readable. Enable Read/Write to preview alpha.");
				return;
			}

			var pixels = sourceTexture.GetPixels();
			if (pixels == null || pixels.Length == 0) return;

			alphaMin = 1f;
			alphaMax = 0f;

			for (int i = 0; i < pixels.Length; i++)
			{
				var a = pixels[i].a;
				if (a < alphaMin) alphaMin = a;
				if (a > alphaMax) alphaMax = a;
			}

			// If all alpha values are effectively 1, treat as no meaningful alpha.
			if (Mathf.Approximately(alphaMin, 1f) && Mathf.Approximately(alphaMax, 1f))
			{
				hasAlpha = false;
				if (alphaToggleLabel != null)
				{
					alphaToggleLabel.style.display = DisplayStyle.None;
				}

				if (alphaInfoLabel != null)
				{
					alphaInfoLabel.style.display = DisplayStyle.Flex;
					alphaInfoLabel.text = "no alpha";
				}

				return;
			}

			hasAlpha = true;

			alphaTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
			alphaNormalizedTexture = new Texture2D(
				sourceTexture.width,
				sourceTexture.height,
				TextureFormat.RGBA32,
				false);

			var raw = new Color[pixels.Length];
			var norm = new Color[pixels.Length];

			float range = Mathf.Max(0.0001f, alphaMax - alphaMin);

			for (int i = 0; i < pixels.Length; i++)
			{
				var a = pixels[i].a;
				raw[i] = new Color(a, a, a, 1f);

				var na = (a - alphaMin) / range;
				na = Mathf.Clamp01(na);
				norm[i] = new Color(na, na, na, 1f);
			}

			alphaTexture.SetPixels(raw);
			alphaTexture.Apply();

			alphaNormalizedTexture.SetPixels(norm);
			alphaNormalizedTexture.Apply();
		}
	}
}
