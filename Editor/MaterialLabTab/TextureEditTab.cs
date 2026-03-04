namespace MaterialLab.Editor
{
	using System.Collections.Generic;
	using System.IO;

	using MaterialLab.Tabs;

	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class TextureEditTab : MaterialLabTab
	{
		private readonly VisualElement content;

		private readonly List<string> createdFiles = new();

		private bool resetCreatedOnSelectionChanged = true;

		private int elementWidth = 150;

		private Object originalSelected;


		/// <inheritdoc />
		public TextureEditTab() : base("Texture")
		{
			content = new VisualElement();
			Add(content);
		}

		/// <inheritdoc />
		public override string Name => "Texture";

		protected override void OnSelectionChanged()
		{
			if (resetCreatedOnSelectionChanged)
				createdFiles.Clear();

			if (originalSelected == null)
				originalSelected = Selection.activeObject;

			RepaintContent();
		}

		private void RepaintContent()
		{
			if (content == null) return;

			content.Clear();

			if (createdFiles.Count > 0)
			{
				content.Add(
					new Button(DeleteFilesCreatedInThisSession)
					{
						text = "Undo File Creation", style = { width = elementWidth }
					});
			}

			if (Selection.activeObject is Texture2D texture)
			{
				content.Add(new Label($"Selected Texture: '{texture.name}'"));
				content.Add(new Label($"{texture.width} {texture.height} {texture.format}"));

				// content.Add(new TexturePreviewElement(texture));
				content.Add(
					new Button(() => EditorGUIUtility.PingObject(texture))
					{
						text = "Locate", style = { width = elementWidth }
					});

				void OnAssetSaved(Texture2D savedAsset)
				{
					resetCreatedOnSelectionChanged = false;
					Selection.activeObject = savedAsset;
					EditorGUIUtility.PingObject(savedAsset);
				}

				var optionSelect = new TextureOperationSelection(
					texture,
					createdFiles,
					OnAssetSaved,
					PerformOperationToMemory,
					null,
					elementWidth);
				content.Add(optionSelect);

				content.Add(new Separator());

				var adjustElement = new TextureAdjustElement(texture, texture.name);
				content.Add(adjustElement);

				var editSaveRow = new TextureThreeWaySaveRow(createdFiles, OnAssetSaved, elementWidth);
				editSaveRow.SetContext(
					texture,
					() => adjustElement?.ProcessedTexture,
					() => "edit_" + MaterialLabFileUtils.GetTimestampSuffix());
				content.Add(editSaveRow);
			}
			else
			{
				content.Add(new Label("No texture selected"));
				resetCreatedOnSelectionChanged = true;
				createdFiles.Clear();
			}
			
		}
		private void DeleteFilesCreatedInThisSession()
		{
			foreach (var thisFile in createdFiles)
			{
				if (Path.GetExtension(thisFile) != ".meta") Debug.Log($"Deleting {thisFile}");
				File.Delete(thisFile);
			}

			createdFiles.Clear();
			AssetDatabase.Refresh();
			Selection.activeObject = originalSelected;
			originalSelected = null;
		}

		private void RestoreReadable(Texture2D texture, bool readable)
		{
			if (readable) return;

			var path = AssetDatabase.GetAssetPath(texture);
			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			importer.isReadable = false;
			importer.SaveAndReimport();
		}

		/// <summary>Applies the operation in memory and returns the result texture. Caller is responsible for saving (e.g. via TextureThreeWaySaveRow).</summary>
		private Texture2D PerformOperationToMemory(Texture2D texture, TextureOperation operation)
		{
			bool wasReadable = texture.EnsureReadableWithStatus();
			var src = texture.GetPixels();
			RestoreReadable(texture, wasReadable);

			int w = texture.width;
			int h = texture.height;

			bool swap = operation == TextureOperation.Rotate90CW ||
						operation == TextureOperation.Rotate90CCW;

			int newW = swap ? h : w;
			int newH = swap ? w : h;

			var dst = new Color[newW * newH];
			if (operation == TextureOperation.Invert)
			{
				for (int i = 0; i < src.Length; i++)
					dst[i] = new Color(1f - src[i].r, 1f - src[i].g, 1f - src[i].b, src[i].a);
			}
			else
				for (int y = 0; y < newH; y++)
				{
					for (int x = 0; x < newW; x++)
					{
						int srcX = 0, srcY = 0;
						switch (operation)
						{
							case TextureOperation.FlipHorizontal:
								srcX = w - 1 - x;
								srcY = y;
								break;
							case TextureOperation.FilpVertical:
								srcX = x;
								srcY = h - 1 - y;
								break;
							case TextureOperation.Rotate180:
								srcX = w - 1 - x;
								srcY = h - 1 - y;
								break;
							case TextureOperation.Rotate90CW:
								srcX = y;
								srcY = h - 1 - x;
								break;
							case TextureOperation.Rotate90CCW:
								srcX = w - 1 - y;
								srcY = x;
								break;
						}
						dst[x + y * newW] = src[srcX + srcY * w];
					}
				}

			var textureFormat = TextureFormat.RGBA32;
			var newTex = new Texture2D(newW, newH, textureFormat, texture.mipmapCount > 1);
			newTex.SetPixels(dst);
			newTex.Apply();
			return newTex;
		}
	}
}
