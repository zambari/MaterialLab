namespace MaterialLab.Tabs
{
	using UnityEditor;
	using UnityEditor.UIElements;

	public class DefaultInspectorTab : MaterialLabTab
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
