namespace MaterialLab.Editor
{
	using System;
	using System.Collections.Generic;

	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	/// <summary>
	/// Row of three save buttons: Save as new, Save with backup, Save in place.
	/// Buttons are inactive when source or texture-to-save is null.
	/// </summary>
	public class TextureThreeWaySaveRow : VisualElement
	{
		private readonly IList<string> createdFiles;
		private readonly Action<Texture2D> onAssetSaved;
		private readonly int buttonWidth;
		private readonly string fileOperation;

		private Texture2D sourceTexture;
		private Func<Texture2D> textureToSaveGetter;
		private Func<string> saveAsNewSuffixGetter;

		private Button saveAsNewButton;
		private Button saveWithBackupButton;
		private Button saveInPlaceButton;

		public TextureThreeWaySaveRow(
			IList<string> createdFiles,
			Action<Texture2D> onAssetSaved = null,
			int buttonWidth = 150,
			string fileOperation = "Save ")
		{
			this.createdFiles = createdFiles;
			this.onAssetSaved = onAssetSaved;
			this.buttonWidth = buttonWidth;
			this.fileOperation = fileOperation ?? "Save ";

			style.flexDirection = FlexDirection.Row;
			style.marginTop = 4;

			var op = this.fileOperation.TrimEnd();
			if (op.Length > 0 && char.IsLower(op[0]))
				op = char.ToUpper(op[0]) + op.Substring(1);

			saveAsNewButton = new Button(OnSaveAsNew) { text = op + " as new", style = { width = buttonWidth } };
			saveWithBackupButton = new Button(OnSaveWithBackup) { text = op + " with backup", style = { width = buttonWidth } };
			saveInPlaceButton = new Button(OnSaveInPlace) { text = op + " in place", style = { width = buttonWidth } };

			Add(saveAsNewButton);
			Add(saveWithBackupButton);
			Add(saveInPlaceButton);

			UpdateButtonState();
		}

		/// <summary>Set the context for saving. When getters return null, buttons are disabled.</summary>
		public void SetContext(Texture2D source, Func<Texture2D> textureToSaveGetter, Func<string> saveAsNewSuffixGetter)
		{
			sourceTexture = source;
			this.textureToSaveGetter = textureToSaveGetter;
			this.saveAsNewSuffixGetter = saveAsNewSuffixGetter;
			UpdateButtonState();
		}

		public void UpdateButtonState()
		{
			var toSave = textureToSaveGetter?.Invoke();
			var active = sourceTexture != null && toSave != null;
			saveAsNewButton.SetEnabled(active);
			saveWithBackupButton.SetEnabled(active);
			saveInPlaceButton.SetEnabled(active);
		}

		private void OnSaveAsNew()
		{
			var toSave = textureToSaveGetter?.Invoke();
			if (sourceTexture == null || toSave == null) return;

			var suffix = saveAsNewSuffixGetter?.Invoke() ?? "saved";
			var result = TextureSaveHelper.SaveAsNew(sourceTexture, toSave, suffix, createdFiles);
			if (result != null)
				onAssetSaved?.Invoke(result);
		}

		private void OnSaveWithBackup()
		{
			var toSave = textureToSaveGetter?.Invoke();
			if (sourceTexture == null || toSave == null) return;

			var backup = TextureSaveHelper.SaveWithBackup(sourceTexture, toSave, createdFiles);
			if (backup != null)
				onAssetSaved?.Invoke(backup);
		}

		private void OnSaveInPlace()
		{
			var toSave = textureToSaveGetter?.Invoke();
			if (sourceTexture == null || toSave == null) return;

			TextureSaveHelper.SaveInPlace(sourceTexture, toSave);
		}
	}
}
