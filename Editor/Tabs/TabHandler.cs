namespace MaterialLab.Tabs
{
	using MaterialLab.Editor;

	using UnityEngine;
	using UnityEngine.UI;
	using UnityEngine.UIElements;

	public class TabHandler : VisualElement
	{
		private const float dimOpacity = .8f;

		private TabSection[] tabs;

		private TabHeaderButton[] buttons;

		public TabHandler(string prefsId, params TabSection[] tabs)
		{
			this.tabs = tabs;
			var tabHeader = new VisualElement()
			{
				style =
				{
					paddingTop = 8,
					backgroundColor = Color.black / 3,
					flexDirection = FlexDirection.Row,
					flexGrow = 1,
					minHeight = 40
				}
			};
			Add(tabHeader);
			var currentTitle = new Label()
			{
				style =
				{
					fontSize = 18,
					position = Position.Absolute,
					right = 5,
					bottom = 3
				}
			};
			tabHeader.Add(currentTitle.SetVisible(false));

			// var content = new ScrollView();
			var content =
				new VisualElement(); // cos tu raz bylo dziwnie ale to chyba listview wariowalo po prosut {style={flexGrow=.8f}}; //,backgroundColor = Color.red
			buttons = new TabHeaderButton[tabs.Length];
			Add(content);
			for (int i = 0; i < tabs.Length; i++)
			{
				var thisTab = tabs[i];
				buttons[i] = new TabHeaderButton(thisTab.Name);
				tabHeader.Add(buttons[i]);
				buttons[i].clicked += () => SelectTab(thisTab);
				content.Add(thisTab.content);
			}

			var selected = PlayerPrefs.GetInt($"{nameof(TabHandler)}{prefsId}", -1);
			if (selected != -1 && selected < tabs.Length) SelectTab(tabs[selected]);
			else if (tabs.Length > 0) SelectTab(tabs[0]);

			void SelectTab(TabSection tab)
			{
				currentTitle.text = tab.Name;
				for (int i = 0; i < tabs.Length; i++)
				{
					if (tabs[i] == tab)
					{
						PlayerPrefs.SetInt($"{nameof(TabHandler)}{prefsId}", i);
						buttons[i].IsActive = true;
						tabs[i].content.style.display = DisplayStyle.Flex;
						tabs[i].content.style.opacity = 1;
					}
					else
					{
						buttons[i].IsActive = false;
						tabs[i].content.style.display = DisplayStyle.None;
						tabs[i].content.style.opacity = dimOpacity;
					}
				}
			}
		}

		public void OnTabEntered()
		{
			foreach (var thisTab in tabs) thisTab?.OnTabEntered();
		}

		public void OnTabLeft()
		{
			foreach (var thisTab in tabs) thisTab?.OnTabLeft();
		}
	}
}
