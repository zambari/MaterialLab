namespace MaterialLab.Tabs
{
	using UnityEngine.UIElements;

	/// <summary>
	/// Base class for tabs.
	/// </summary>
	public abstract class BaseTab : VisualElement
	{
		public abstract string Name { get; }

		public virtual void OnTabEntered() { }

		public virtual void OnTabLeft() { }

		/// <summary>
		/// Use constructor to fill the content.
		/// TabSelector will set its display mode to either FlexDisplay.Row or FlexDisplay.None, but the
		/// content will stay created, so if you pass a serialized property in the constructor, it should stay bound. 
		/// </summary>
		public BaseTab()
		{
			style.flexDirection = FlexDirection.Column;
			style.flexGrow = 1;
			style.flexShrink = 1;
		}
	}

}
