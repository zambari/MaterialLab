namespace MaterialLab.Tabs
{
	using UnityEditor;
	using UnityEditor.UIElements;

	public class DefaultInspectorTab : BaseLabTab
	{
		/// <inheritdoc />
		public DefaultInspectorTab(SerializedObject serializedObject, UnityEditor.Editor editor) : base(
			"Default Inspector")
		{
			InspectorElement.FillDefaultInspector(this, serializedObject, editor);
		}

		/// <inheritdoc />
		public override string Name => "Default inspector";
	}
}
