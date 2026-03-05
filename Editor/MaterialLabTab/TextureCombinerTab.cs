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
				content.Add(new Label("No texture selected"));
				return;
			}

			var selectedTextures = Selection.objects.OfType<Texture2D>().ToArray();
			var matcher = new TextureAssetMatcher(selectedTextures);
			var roles = matcher.GetRecognizedTextures();
			recognizedRolesLabel.text = roles.Count > 0
				? $"Recognized texture roles: {string.Join(", ", roles)}"
				: "Recognized texture roles: None";

			if (selectedTextures.Length == 2 || selectedTextures.Length == 3)
			{
				void OnAssetSaved(Texture2D savedAsset)
				{
					Selection.activeObject = savedAsset;
					EditorGUIUtility.PingObject(savedAsset);
				}
				content.Add(new MetallicGlossTextureCombiner(matcher, createdFiles, OnAssetSaved, ElementWidth));
			}
			else
			{
				content.Add(new Label("Select 2 or 3 textures for combiner options"));
			}
		}

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
