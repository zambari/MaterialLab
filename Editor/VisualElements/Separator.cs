namespace MaterialLab.Editor
{
	using UnityEngine;
	using UnityEngine.UIElements;

	/// <summary>
	/// Simple horizontal separator line for editor layouts.
	/// </summary>
	internal class Separator : VisualElement
	{
		public Separator()
		{
			style.height = 1;
			style.marginTop = 4;
			style.marginBottom = 4;
			style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
			style.flexGrow = 1;
		}
	}
}

