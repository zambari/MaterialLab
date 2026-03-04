namespace MaterialLab.Editor
{
	using System;
	using System.Linq;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class TextureOperationSelection : VisualElement
	{
		public TextureOperation Operation =>
			(TextureOperation)System.Enum.Parse(typeof(TextureOperation), dropdown.value);

		private DropdownField dropdown;

		private string PlayerPrefsKey => "TextureOperationSelection" + id;

		private string id;

		public Action<TextureOperation> actionRequested;

		public TextureOperationSelection(string id = null)
		{
			this.id = id;
			dropdown = new DropdownField();
			dropdown.style.width = Length.Pixels(120);
			var vals = System.Enum.GetNames(typeof(TextureOperation)).ToList();
			dropdown.choices = vals;
			var selectedOp = PlayerPrefs.GetString(PlayerPrefsKey, vals[0]);
			dropdown.value = selectedOp;
			dropdown.RegisterValueChangedCallback((x) => { PlayerPrefs.SetString(PlayerPrefsKey, x.newValue); });
			style.flexDirection = FlexDirection.Row;
			Add(dropdown);
			Add(new Button(() => actionRequested?.Invoke(Operation)) { text = "Apply" });
		}
	}
}
