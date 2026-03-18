namespace MaterialLab.Editor
{
	using UnityEditor;

	using UnityEngine.UIElements;

	public class MaterialLabBaseWindow: EditorWindow
	{
		internal Label HeaderLabel(string text)
		{
			var label = new Label(text);
			label.style.fontSize = 20;
			return label;
		}
	}
}
