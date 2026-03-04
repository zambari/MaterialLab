namespace MaterialLab.Editor
{
	using UnityEngine.UIElements;

	public class Row : VisualElement
	{
		public Row(bool grow = true)
		{
			name = "Row";
			style.flexDirection = FlexDirection.Row;
			if (grow) style.flexGrow = 1;
		}
	}
}
