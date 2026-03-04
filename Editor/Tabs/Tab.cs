namespace MaterialLab.Tabs
{
	using UnityEngine.UIElements;

	public abstract class TabSection
	{
		public VisualElement content = new() { style = { flexGrow = 1 } };

		public abstract string Name { get; }

		public abstract void OnTabEntered();

		public abstract void OnTabLeft();


	}
	
}
