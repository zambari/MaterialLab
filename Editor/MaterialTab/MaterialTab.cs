namespace MaterialLab.Editor
{
	using MaterialLab.Tabs;

	public class MaterialTab: EditBaseTab
	{
		/// <inheritdoc />
		public MaterialTab() : base("Material") { }

		/// <inheritdoc />
		public override string Name => "Material";
	}
}
