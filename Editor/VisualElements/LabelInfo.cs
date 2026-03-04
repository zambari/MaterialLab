namespace MaterialLab.Editor
{
	using UnityEngine.UIElements;

	public class LabelInfo : Label
	{
		public LabelInfo(string text) : base(text)
		{
			style.marginTop = 10;
			style.marginBottom = 10;
			style.whiteSpace = WhiteSpace.Normal;
		}

		public LabelInfo() : this(null) { }
	}
}
