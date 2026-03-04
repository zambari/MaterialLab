namespace MaterialLab.Editor
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

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

		private const string ManualEditSuffix = "edit";

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

				content.Add(new TexturePreviewElement(texture));
				content.Add(
					new Button(() => EditorGUIUtility.PingObject(texture))
					{
						text = "Locate", style = { width = elementWidth }
					});

				var optionSelect = new TextureOperationSelection();
				content.Add(optionSelect);
				optionSelect.actionRequested += operation => { PerformOperation(texture, operation); };

				content.Add(new Separator());

				var editToggle = new Toggle("Edit texture");
				content.Add(editToggle);

				var editArea = new VisualElement();
				content.Add(editArea);

				editToggle.RegisterValueChangedCallback(evt =>
				{
					editArea.Clear();
					if (!evt.newValue) return;

					var adjustElement = new TextureAdjustElement(texture, texture.name);
					editArea.Add(adjustElement);

					var buttonsRow = new VisualElement
					{
						style =
						{
							flexDirection = FlexDirection.Row,
							marginTop = 4
						}
					};
					buttonsRow.style.display = DisplayStyle.None;

					var saveInPlace = new Button(
						() =>
						{
							if (adjustElement == null) return;
							SaveAdjustedTextureInPlace(texture, adjustElement.ProcessedTexture);
						})
					{
						text = "Save in place", style = { width = elementWidth }
					};

					var saveAsNew = new Button(
						() =>
						{
							if (adjustElement == null) return;
							var resultTex = SaveAdjustedTextureAsNew(texture, adjustElement.ProcessedTexture);
							if (resultTex != null)
							{
								resetCreatedOnSelectionChanged = false;
								Selection.activeObject = resultTex;
							}
						})
					{
						text = "Save as new", style = { width = elementWidth }
					};

					var saveWithBackup = new Button(
						() =>
						{
							if (adjustElement == null) return;
							SaveAdjustedTextureWithBackup(texture, adjustElement.ProcessedTexture);
						})
					{
						text = "Save with backup", style = { width = elementWidth }
					};

					buttonsRow.Add(saveInPlace);
					buttonsRow.Add(saveAsNew);
					buttonsRow.Add(saveWithBackup);
					editArea.Add(buttonsRow);

					adjustElement.OnUserChange += () =>
					{
						if (buttonsRow == null) return;
						buttonsRow.style.display = adjustElement.HasUserChanges
							? DisplayStyle.Flex
							: DisplayStyle.None;
					};
				});
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

		private void PerformOperation(Texture2D texture, TextureOperation operation)
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
						int srcX = 0,
							srcY = 0;

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

			var saved = SaveAsAsset(texture, newTex, operation);
			resetCreatedOnSelectionChanged = false;
			Selection.activeObject = saved;
		}

		private Texture2D SaveAsAsset(Texture2D originalPathSource, Texture2D result, TextureOperation op)
		{
			var path = AssetDatabase.GetAssetPath(originalPathSource);
			var dir = Path.GetDirectoryName(path);
			var ext = Path.GetExtension(path).ToLowerInvariant();

			int attempt = 0;
			var newPath = GetPath(attempt);
			while (File.Exists(newPath)) newPath = GetPath(attempt++);

			byte[] bytes = ext == ".jpg"
				? result.EncodeToJPG()
				: result.EncodeToPNG();

			File.WriteAllBytes(newPath, bytes);
			createdFiles.Add(newPath);
			createdFiles.Add(Path.ChangeExtension(newPath, ".meta"));
			AssetDatabase.ImportAsset(newPath);

			return AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);

			string GetPath(int attemptIndex)
			{
				if (attemptIndex > 0)
					return Path.Combine(
						dir,
						$"{Path.GetFileNameWithoutExtension(path)}_{op}_{attemptIndex}{ext}");

				return Path.Combine(
					dir,
					$"{Path.GetFileNameWithoutExtension(path)}_{op}{ext}");
			}
		}

		private void SaveAdjustedTextureInPlace(Texture2D original, Texture2D adjusted)
		{
			if (original == null || adjusted == null) return;

			var path = AssetDatabase.GetAssetPath(original);
			if (string.IsNullOrEmpty(path))
			{
				Debug.LogWarning($"[{nameof(TextureEditTab)}] Cannot save in place, texture has no asset path.");
				return;
			}

			var ext = Path.GetExtension(path).ToLowerInvariant();
			byte[] bytes = ext == ".jpg" || ext == ".jpeg"
				? adjusted.EncodeToJPG()
				: adjusted.EncodeToPNG();

			File.WriteAllBytes(path, bytes);
			AssetDatabase.ImportAsset(path);
			EditorUtility.SetDirty(original);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		private Texture2D SaveAdjustedTextureAsNew(Texture2D original, Texture2D adjusted)
		{
			if (original == null || adjusted == null) return null;

			var originalPath = AssetDatabase.GetAssetPath(original);
			if (string.IsNullOrEmpty(originalPath))
			{
				Debug.LogWarning($"[{nameof(TextureEditTab)}] Cannot save as new, texture has no asset path.");
				return null;
			}

			var timestamp = MaterialLabFileUtils.GetTimestampSuffix();
			var suffix = $"{ManualEditSuffix}_{timestamp}";
			var newPath = MaterialLabFileUtils.GetUniquePathWithSuffix(originalPath, suffix);

			var ext = Path.GetExtension(newPath).ToLowerInvariant();
			byte[] bytes = ext == ".jpg" || ext == ".jpeg"
				? adjusted.EncodeToJPG()
				: adjusted.EncodeToPNG();

			File.WriteAllBytes(newPath, bytes);
			createdFiles.Add(newPath);
			createdFiles.Add(Path.ChangeExtension(newPath, ".meta"));
			AssetDatabase.ImportAsset(newPath);

			return AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
		}

		private void SaveAdjustedTextureWithBackup(Texture2D original, Texture2D adjusted)
		{
			if (original == null || adjusted == null) return;

			var originalPath = AssetDatabase.GetAssetPath(original);
			if (string.IsNullOrEmpty(originalPath))
			{
				Debug.LogWarning($"[{nameof(TextureEditTab)}] Cannot save with backup, texture has no asset path.");
				return;
			}

			// Create backup next to original.
			var backupSuffix = $"backup_{MaterialLabFileUtils.GetTimestampSuffix()}";
			var backupPath = MaterialLabFileUtils.GetUniquePathWithSuffix(originalPath, backupSuffix);

			File.Copy(originalPath, backupPath);
			createdFiles.Add(backupPath);
			createdFiles.Add(Path.ChangeExtension(backupPath, ".meta"));
			AssetDatabase.ImportAsset(backupPath);

			// Now overwrite original in place.
			SaveAdjustedTextureInPlace(original, adjusted);
		}
	}
}
