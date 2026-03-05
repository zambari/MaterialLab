namespace MaterialLab.Editor
{
	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	/// <summary>
	/// Compact preview of textures matched by TextureAssetMatcher, grouped by role.
	/// Used as a "what will go into the material" summary.
	/// </summary>
	internal class MaterialFromTexturesPreviewElement : VisualElement
	{
		public MaterialFromTexturesPreviewElement(TextureAssetMatcher matcher)
		{
			this.AddBorder();
			style.marginTop = 4;
			style.marginBottom = 4;

			Add(new Label("Matched textures for new material"));

			var roles = matcher.GetRecognizedTextures();
			if (roles == null || roles.Count == 0)
			{
				Add(new Label("No recognizable texture roles in current selection."));
				return;
			}

			var grid = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					flexWrap = Wrap.Wrap
				}
			};

			foreach (var role in roles)
			{
				var tex = matcher.GetTextureByRole(role);
				if (role == TextureRole.Albedo) continue;
				if (tex == null) continue;

				var card = new VisualElement();
				card.style.marginRight = 6;
				card.style.marginBottom = 6;

				var roleLabel = new Label(role.ToString())
				{
					style =
					{
						unityFontStyleAndWeight = FontStyle.Bold,
						marginBottom = 2
					}
				};
				card.Add(roleLabel);

				var preview = new TexturePreviewElement(tex);
				preview.RegisterCallback<ClickEvent>(_ => { EditorGUIUtility.PingObject(tex); });
				card.Add(preview);

				grid.Add(card);
			}

			Add(grid);
		}
	}
}

