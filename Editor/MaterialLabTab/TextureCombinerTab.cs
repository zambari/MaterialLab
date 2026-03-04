
namespace MaterialLab.Editor
{
	using System.Linq;

	using MaterialLab.Tabs;

	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class TextureCombinerTab : MaterialLabTab
	{
		private readonly VisualElement content;

		private readonly Label recognizedRolesLabel;

		/// <inheritdoc />
		public TextureCombinerTab() : base("Texture Combiner")
		{
			recognizedRolesLabel = new Label();
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

			if (Selection.activeObject is not Texture2D)
			{
				recognizedRolesLabel.text = "Recognized texture roles: None";
				content.Add(new Label("No texture selected"));
				return;
			}

			var selectedTextures =
				Selection.objects
						 .OfType<Texture2D>()
						 .ToArray();

			var matcher = new TextureAssetMatcher(selectedTextures);
			var roles = matcher.GetRecognizedTextures();
			recognizedRolesLabel.text = roles.Count > 0
				? $"Recognized texture roles: {string.Join(", ", roles)}"
				: "Recognized texture roles: None";

			if (selectedTextures.Length == 2 || selectedTextures.Length == 3)
			{
				content.Add(
					new MetallicGlossTextureCombiner(
						matcher.Main,
						matcher.Metallic,
						matcher.Gloss));
			}
			else
			{
				content.Add(new Label("Select 2 or 3 textures for combiner options"));
			}
		}
	}
}
