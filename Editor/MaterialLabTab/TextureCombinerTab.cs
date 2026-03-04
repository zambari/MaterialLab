
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

		/// <inheritdoc />
		public TextureCombinerTab() : base("Texture Combiner")
		{
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
				content.Add(new Label("No texture selected"));
				return;
			}

			var selectedTextures =
				Selection.objects
						 .OfType<Texture2D>()
						 .ToArray();

			if (selectedTextures.Length == 2 || selectedTextures.Length == 3)
			{
				var sortedTexture = FindMetallicAndRougness(selectedTextures);
				content.Add(
					new MetallicGlossTextureCombiner(
						sortedTexture.Item1,
						sortedTexture.Item2,
						sortedTexture.Item3));
			}
			else
			{
				content.Add(new Label("Select 2 or 3 textures for combiner options"));
			}
		}

		/// <summary>
		/// Returns main texture, metallic texture and gloss texture, based on names.
		/// </summary>
		private (Texture2D main, Texture2D metallic, Texture2D gloss) FindMetallicAndRougness(Texture2D[] textures)
		{
			var metallic = textures.FirstOrDefault(t => t != null && t.name.ToLower().Contains("metall"));
			var roughness = textures.FirstOrDefault(
				t =>
					t != null &&
					t != metallic &&
					(t.name.ToLower().Contains("rough") ||
					 t.name.ToLower().Contains("gloss")));
			var main = textures.FirstOrDefault(t => t != null && t != metallic && t != roughness);

			return (main, metallic, roughness);
		}
	}
}
