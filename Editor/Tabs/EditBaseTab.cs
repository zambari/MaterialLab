namespace MaterialLab.Tabs
{
	using UnityEngine.UIElements;


	public abstract class EditBaseTab : BaseTab
	{

		public EditBaseTab(string name) : base()
		{
			this.Add(GetHeader(name));
		}
		
		protected Label GetHeader(string label)
		{
			return new Label(label) { style = { fontSize = 16, paddingBottom = 5 } };
		}

		
	}
}


