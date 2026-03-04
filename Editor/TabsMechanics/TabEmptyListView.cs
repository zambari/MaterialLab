namespace MaterialLab.Tabs
{
	using System.Collections.Generic;

	using MaterialLab.Editor;
	using MaterialLab.UIExtensions;

	using UnityEngine;
	using UnityEngine.UIElements;


	public class TabEmptyListView : TabSection
	{
		/// <inheritdoc />
		public override string Name => name;

		/// <inheritdoc />
		public override void OnTabEntered()
		{
		}

		/// <inheritdoc />
		public override void OnTabLeft()
		{
		}

		public string name { get; set; }

		public TabEmptyListView(string name)
		{
			// content = new() { style = { flexGrow = 1 } };

			this.name = name;
			
			var listView = new ListView();
			listView.SetBackgroundColor(Color.green / 4);
			var strList = new List<string>();
			for (int i = 0; i < 50; i++)
			{
				strList.Add($"s {i} {i} {i}");
			}

		
			listView.makeItem = () =>
								{
									var element = new VisualElement();
									var label = new Label();
									element.Add(label);
									element.userData = label;
									return element;
								};
			listView.bindItem = (ve, index) =>
								{
									var s = strList[index];
									(ve.userData as Label).text = s;
								};
		}
	}
}
