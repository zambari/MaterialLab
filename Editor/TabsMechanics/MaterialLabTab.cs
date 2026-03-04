namespace MaterialLab.Tabs
{
	using MaterialLab.Editor;

	using UnityEditor;

	using UnityEngine.UIElements;

	/// <summary>
	/// Base tab for MaterialLab features.
	/// Adds 'lock selection' toggle, and virtual OnSelectionChanged method which is connected to editor events when tab
	/// is entered, and disconnected when we leave.
	/// </summary>
	public abstract class MaterialLabTab : BaseTab
	{
		private FlickToggle lockSelectionToggle;

		protected bool isSelectionLocked => lockSelectionToggle.Value;

		public MaterialLabTab(string name) : base()
		{
			this.Add(GetHeader(name));
			lockSelectionToggle = new FlickToggle("SelectionLock", false, "SelectionLock" + GetType().Name);
			lockSelectionToggle.valueChanged += (x) =>
												{
													if (!x) OnSelectionChanged();
												};
			Add(lockSelectionToggle);
		}

		protected virtual void OnSelectionChanged() { }

		protected Label GetHeader(string label)
		{
			return new Label(label) { style = { fontSize = 16, paddingBottom = 5 } };
		}

		private void OnSelectionChangedInternal()
		{
			if (!isSelectionLocked) OnSelectionChanged();
		}

		public override void OnTabEntered()
		{
			Selection.selectionChanged += OnSelectionChangedInternal;
			OnSelectionChangedInternal();
		}

		public override void OnTabLeft()
		{
			Selection.selectionChanged -= OnSelectionChangedInternal;
		}
	}
}
