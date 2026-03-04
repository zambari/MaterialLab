namespace MaterialLab.Editor
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	using Object = UnityEngine.Object;

	public class TextureLabWindow : MaterialLabBaseWindow
	{
		[MenuItem(MenuPathBase + "Texture Lab", false)]
		private static void OpenWindow()
		{
			var inspectorType = Type.GetType("UnityEditor.InspectorWindow,UnityEditor.dll");
			var window = EditorWindow.GetWindow<TextureLabWindow>(new Type[] { inspectorType });
			window.titleContent = new GUIContent("Texture Lab");
		}

		private VisualElement contextElement;

		private List<string> createdFiles = new();

		private bool resetCreatedOnSelectionChanged = true;

		private int elementWidth = 150;

		private Object originalSelected;

		private const string Metallic = "metall";

		private Toggle lockSelection;

		public void CreateGUI()
		{
			rootVisualElement.Add(HeaderLabel("Texture Editing"));
			lockSelection = new Toggle() { text = "Lock Selection", value = false };
			contextElement = new VisualElement();
			rootVisualElement.Add(lockSelection);

			rootVisualElement.Add(contextElement);

			RepaintContent();
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

		private void RepaintContent()
		{
			if (contextElement == null) return;

			contextElement.Clear();
			if (createdFiles.Count > 0)
			{
				contextElement.Add(
					new Button(DeleteFilesCreatedInThisSession)
					{
						text = "Undo File Creation", style = { width = elementWidth }
					});
			}

			if (Selection.activeObject is Texture2D texture)
			{
				contextElement.Add(new Label($"Selected Texture: '{texture.name}'"));
				contextElement.Add(new Label($"{texture.width} {texture.height} {texture.format}"));

				contextElement.Add(new TexturePreviewElement(texture));
				contextElement.Add(
					new Button(() => EditorGUIUtility.PingObject(texture))
					{
						text = "Locate", style = { width = elementWidth }
					});

				var optionSelect = new TextureOperationSelection();
				contextElement.Add(optionSelect);
				optionSelect.actionRequested += (x) => { PerformOperation(texture, x); };
				var selectedTextures =
					Selection.objects.Select(x => x as Texture2D).Where((x) => x != null).ToArray() as Texture2D[];
				if (selectedTextures.Length == 2 || selectedTextures.Length == 3)
				{
					var sortedTexture = FindMetallicAndRougness(selectedTextures);
					contextElement.Add(
						new MetallicGlossTextureCombiner(
							sortedTexture.Item1,
							sortedTexture.Item2,
							sortedTexture.Item3));
				}
				else contextElement.Add(new Label("Select 2 or 3 textures for combiner options"));
			}
			else
			{
				contextElement.Add(new Label("No texture selected"));
				resetCreatedOnSelectionChanged = true;
				createdFiles.Clear();
			}
		}

		/// <summary>
		/// Returns main texture, metallic texture and gloss texture, based on names
		/// </summary>
		/// <returns></returns>
		private (Texture2D, Texture2D, Texture2D) FindMetallicAndRougness(Texture2D[] textures)
		{
			var metallic = textures.FirstOrDefault(t => t.name.ToLower().Contains("metall"));
			var roughness = textures.FirstOrDefault(t =>
														t != metallic &&
														(t.name.ToLower().Contains("rough") ||
														 t.name.ToLower().Contains("gloss")));
			var main = textures.FirstOrDefault(t => t != metallic && t != roughness);

			return (main, metallic, roughness);
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

			var textureFormat = texture.format;
			textureFormat = TextureFormat.RGBA32;
			var newTex = new Texture2D(newW, newH, textureFormat, texture.mipmapCount > 1);
			newTex.SetPixels(dst);
			newTex.Apply();

			var saved = SaveAsAsset(texture, newTex, operation);
			resetCreatedOnSelectionChanged = false;
			Selection.activeObject = saved;

			// resetCreatedOnSelectionChanged = true;
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

			string GetPath(int attempt = 0)
			{
				if (attempt > 0)
					return Path.Combine(
						dir,
						$"{Path.GetFileNameWithoutExtension(path)}_{op}_{attempt}{ext}");

				return Path.Combine(
					dir,
					$"{Path.GetFileNameWithoutExtension(path)}_{op}{ext}");
			}
		}

		private void OnSelectionChange()
		{
			if (lockSelection == null)
			{
				Selection.selectionChanged -= OnSelectionChange;
				return;
			}

			if (lockSelection.value) return;

			if (resetCreatedOnSelectionChanged) createdFiles.Clear();
			if (originalSelected == null) originalSelected = Selection.activeObject;
			RepaintContent();
		}

		private void OnEnable()
		{
			Selection.selectionChanged += OnSelectionChange;
		}

		private void OnDisable()
		{
			Selection.selectionChanged -= OnSelectionChange;
		}
	}
}
