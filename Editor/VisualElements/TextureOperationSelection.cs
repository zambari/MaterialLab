namespace MaterialLab.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class TextureOperationSelection : VisualElement
	{
		public TextureOperation Operation =>
			(TextureOperation)Enum.Parse(typeof(TextureOperation), dropdown.value);

		private readonly DropdownField dropdown;
		private readonly TextureThreeWaySaveRow saveRow;
		private Texture2D sourceTexture;
		private Texture2D lastResultTexture;

		private string PlayerPrefsKey => "TextureOperationSelection" + id;
		private readonly string id;

		/// <summary>Dropdown + Apply + three-way save row. When Apply is clicked, performToMemory is called and the save row is fed the result.</summary>
		public TextureOperationSelection(
			Texture2D sourceTexture,
			IList<string> createdFiles,
			Action<Texture2D> onAssetSaved,
			Func<Texture2D, TextureOperation, Texture2D> performToMemory,
			string id = null,
			int buttonWidth = 150)
		{
			this.id = id ?? "";
			this.sourceTexture = sourceTexture;

			dropdown = new DropdownField();
			dropdown.style.width = Length.Pixels(120);
			var vals = Enum.GetNames(typeof(TextureOperation)).ToList();
			dropdown.choices = vals;
			var selectedOp = PlayerPrefs.GetString(PlayerPrefsKey, vals[0]);
			dropdown.value = selectedOp;
			dropdown.RegisterValueChangedCallback(x => PlayerPrefs.SetString(PlayerPrefsKey, x.newValue));

			saveRow = new TextureThreeWaySaveRow(createdFiles, onAssetSaved, buttonWidth);
			saveRow.SetContext(null, null, null);

			style.flexDirection = FlexDirection.Column;

			var applyRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
			applyRow.Add(dropdown);
			applyRow.Add(new Button(OnApply) { text = "Apply" });
			Add(applyRow);
			Add(saveRow);

			void OnApply()
			{
				if (sourceTexture == null) return;
				lastResultTexture = performToMemory(sourceTexture, Operation);
				if (lastResultTexture != null)
					saveRow.SetContext(sourceTexture, () => lastResultTexture, () => Operation.ToString());
			}
		}
	}
}
