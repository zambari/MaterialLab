namespace MaterialLab.Editor
{
	using UnityEngine;
	using UnityEngine.UIElements;

	public static class InternalElementExtensions
	{
		public static VisualElement AddBorder(this VisualElement source)
		{
			if (source==null) return null;
			source.SetBorderRadius(5);
			source.SetBorderWidth(1);
			source.SetBorderColor(Color.white / 3);
			source.SetPadding(5);
			source.SetMargin(5);
			return source;
		}
	}
}
