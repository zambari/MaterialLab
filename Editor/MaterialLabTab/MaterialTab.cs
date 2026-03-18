namespace MaterialLab.Editor
{
	using System.IO;
	using System.Linq;

	using MaterialLab.Tabs;

	using UnityEditor;
	using UnityEditor.UIElements;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class MaterialTab : BaseLabTab
	{
		private readonly VisualElement content;

		/// <inheritdoc />
		public MaterialTab() : base("Material")
		{
			content = new VisualElement();
			Add(content);
		}

		/// <inheritdoc />
		public override string Name => "Material";

		protected override void OnSelectionChanged()
		{
			RepaintContent();
		}

		private void RepaintContent()
		{
			if (content == null) return;

			content.Clear();

			var selectedTextures = Selection.objects.OfType<Texture2D>().ToArray();
			var selectedMaterials = Selection.objects.OfType<Material>().ToArray();
			var renderersFromSelection = Selection.objects
												  .OfType<GameObject>()
												  .SelectMany(go => go.GetComponents<Renderer>())
												  .Where(r => r != null)
												  .ToArray();

			bool hasRenderersWithMaterials =
				renderersFromSelection.Any(r => r.sharedMaterials != null && r.sharedMaterials.Length > 0);
			bool hasAnything = selectedTextures.Length > 0 || selectedMaterials.Length > 0 || hasRenderersWithMaterials;

			if (!hasAnything)
			{
				content.Add(
					new LabelInfo(
						"Select textures, a material, or a GameObject with a Renderer\nto create or inspect materials."));
				return;
			}

			bool hasAnySection = false;

			// Section 1: from selected textures -> new material creator
			if (selectedTextures.Length > 0)
			{
				var texturesSection = new VisualElement();
				texturesSection.Add(
					new Label("From selected textures") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

				var matcher = new TextureAssetMatcher(selectedTextures);
				texturesSection.Add(new MaterialFromTexturesPreviewElement(matcher));
				texturesSection.Add(new Separator());

				var addTimestampToggle = new Toggle("Add Timestamp") { value = true };
				texturesSection.Add(addTimestampToggle);

				var createButton = new Button(() =>
											  {
												  var mat = CreateMaterialFromMatcher(
													  matcher,
													  addTimestampToggle.value);
												  if (mat != null) Selection.activeObject = mat;
											  }) { text = "Create new material" };
				texturesSection.Add(createButton);

				content.Add(texturesSection);
				hasAnySection = true;
			}

			// Section 2: from selected material(s) -> introspection
			if (selectedMaterials.Length > 0)
			{
				if (hasAnySection) content.Add(new Separator());

				var materialsSection = new VisualElement();
				materialsSection.Add(
					new Label("Selected material") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

				var material = selectedMaterials[0];
				var materialField = new ObjectField
				{
					label = "Material", value = material, objectType = typeof(Material)
				};
				materialField.SetEnabled(false);
				materialsSection.Add(materialField);

				var materialMatcher = new TextureAssetMatcher(material);
				materialsSection.Add(new MaterialFromTexturesPreviewElement(materialMatcher));

				if (selectedMaterials.Length > 1)
				{
					materialsSection.Add(
						new Label($"({selectedMaterials.Length} materials selected, showing first only.)")
						{
							style = { fontSize = 10 }
						});
				}

				content.Add(materialsSection);
				hasAnySection = true;
			}

			// Section 3: from selected GameObjects with Renderers
			if (renderersFromSelection.Length > 0)
			{
				foreach (var renderer in renderersFromSelection)
				{
					var materials = renderer.sharedMaterials;
					if (materials == null || materials.Length == 0) continue;

					if (hasAnySection) content.Add(new Separator());

					var rendererSection = new VisualElement();
					var rendererLabel = new Label($"{renderer.gameObject.name} — {renderer.GetType().Name}")
					{
						style = { unityFontStyleAndWeight = FontStyle.Bold }
					};
					rendererSection.Add(rendererLabel);

					for (int i = 0; i < materials.Length; i++)
					{
						var mat = materials[i];
						if (mat == null) continue;

						var matBlock = new VisualElement { style = { marginTop = 6 } };
						matBlock.Add(new Label(materials.Length > 1 ? $"Material {i}" : "Material"));
						var materialField = new ObjectField
						{
							label = "Material", value = mat, objectType = typeof(Material)
						};
						materialField.SetEnabled(false);
						matBlock.Add(materialField);
						var matMatcher = new TextureAssetMatcher(mat);
						matBlock.Add(new MaterialFromTexturesPreviewElement(matMatcher));
						rendererSection.Add(matBlock);
					}

					content.Add(rendererSection);
					hasAnySection = true;
				}
			}
		}

		public static Material CreateMaterialFromMatcher(
			TextureAssetMatcher matcher,
			bool addTimestamp)
		{
			var primaryTexture = matcher.Albedo ?? matcher.Main;

			var primaryPath = primaryTexture != null
				? AssetDatabase.GetAssetPath(primaryTexture)
				: "Assets";

			if (string.IsNullOrEmpty(primaryPath)) primaryPath = "Assets";

			var dir = Directory.Exists(primaryPath) ? primaryPath : Path.GetDirectoryName(primaryPath);
			if (string.IsNullOrEmpty(dir)) dir = "Assets";

			var baseName = MakeSafeFileName(matcher.SuggestedMaterialName);
			var safeName = addTimestamp
				? baseName + "_" + MaterialLabFileUtils.GetTimestampSuffix()
				: baseName;
			var basePath = Path.Combine(dir, safeName + ".mat");

			string uniquePath = basePath;
			int attempt = 1;
			while (AssetDatabase.LoadAssetAtPath<Material>(uniquePath) != null)
			{
				uniquePath = Path.Combine(dir, $"{safeName}_{attempt}.mat");
				attempt++;
			}

			var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null)
			{
				Debug.LogWarning($"[{nameof(MaterialTab)}] Could not find a suitable shader. Using default.");
				shader = Shader.Find("Standard");
			}

			var material = new Material(shader) { name = safeName };

			AssignTextureIfHasProperty(material, "_MainTex", matcher.Albedo ?? matcher.Main);
			AssignTextureIfHasProperty(material, "_BaseMap", matcher.Albedo ?? matcher.Main);
			AssignTextureIfHasProperty(material, "_MetallicGlossMap", matcher.Metallic);
			AssignTextureIfHasProperty(material, "_MetallicMap", matcher.Metallic);

			EnsureTextureIsImportedAsNormalMap(matcher.Normal);
			AssignTextureIfHasProperty(material, "_BumpMap", matcher.Normal);
			AssignTextureIfHasProperty(material, "_ParallaxMap", matcher.Height);
			AssignTextureIfHasProperty(material, "_OcclusionMap", matcher.Occlusion);

			if (matcher.Emission != null)
			{
				if (material.HasProperty("_EmissionMap"))
				{
					material.SetTexture("_EmissionMap", matcher.Emission);
					material.EnableKeyword("_EMISSION");
				}
			}

			AssetDatabase.CreateAsset(material, uniquePath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			EditorGUIUtility.PingObject(material);
			return material;
		}

		private static void AssignTextureIfHasProperty(Material material, string propertyName, Texture2D texture)
		{
			if (texture == null) return;
			if (!material.HasProperty(propertyName)) return;

			material.SetTexture(propertyName, texture);
		}

		private static void EnsureTextureIsImportedAsNormalMap(Texture2D texture)
		{
			if (texture == null) return;

			var path = AssetDatabase.GetAssetPath(texture);
			if (string.IsNullOrEmpty(path)) return;

			if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
			if (importer.textureType == TextureImporterType.NormalMap) return;

			importer.textureType = TextureImporterType.NormalMap;
			importer.SaveAndReimport();
		}

		private static string MakeSafeFileName(string name)
		{
			var invalid = Path.GetInvalidFileNameChars();
			var chars = name.ToCharArray();
			for (int i = 0; i < chars.Length; i++)
			{
				if (invalid.Contains(chars[i]))
				{
					chars[i] = '_';
				}
			}

			return new string(chars);
		}
	}
}
