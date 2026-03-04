namespace MaterialLab.Tabs
{
	using MaterialLab.Editor;
	using MaterialLab.UIExtensions;

	using UnityEngine;
	using UnityEngine.UI;
	using UnityEngine.UIElements;


	public class TabSelector : VisualElement
	{
		private const float dimOpacity = .8f;

		private float MinHeight = 35;

		private const float PaddingTop = 4;
		private TabHeaderButton[] _buttons;
		private BaseTab[] _tabs;
		private string _playerPrefsKey;
		private Label _currentTitle;
		
		public BaseTab ActiveBaseTab { get; private set; }

		public TabSelector(VisualElement content, string playerPrefsKey, params BaseTab[] tabs)
		{
			style.minHeight = MinHeight;
			_playerPrefsKey = playerPrefsKey;
			_tabs = tabs;

			var tabHeader = new VisualElement()
			{
				style =
				{
					paddingTop = PaddingTop,
					flexDirection = FlexDirection.Row,
					flexGrow = 1,
					minHeight = MinHeight
				}
			};
			_currentTitle = new Label()
			{
				style =
				{
					fontSize = 18,
					position = Position.Absolute,
					right = 5,
					bottom = 3
				}
			};
			tabHeader.Add(_currentTitle.SetVisible(false));
			_buttons = new TabHeaderButton[tabs.Length];

			for (int i = 0; i < tabs.Length; i++)
			{
				var index = i;
				var thisTab = tabs[i];
				_buttons[i] = new TabHeaderButton(thisTab.Name);
				tabHeader.Add(_buttons[i]);
				_buttons[i].clicked += () => SelectTabByIndex(index);
				content.Add(thisTab);
			}
			Add(tabHeader);

			var selected = PlayerPrefs.GetInt($"{nameof(TabSelector)}{_playerPrefsKey}", -1);
			if (selected >= 0 && selected < tabs.Length)
				SelectTabByIndex(selected);
			else if (tabs.Length > 0)
				SelectTabByIndex(0);
		}

		public int GetActiveTabIndex()
		{
			for (int i = 0; i < _tabs.Length; i++)
			{
				if (ActiveBaseTab == _tabs[i]) return i;
			}
			return -1;
		}

		public void SelectTabByIndex(int index)
		{
			if (index < 0 || index >= _tabs.Length) return;
			_currentTitle.text = _tabs[index].Name;
			ActiveBaseTab = null;
			for (int i = 0; i < _tabs.Length; i++)
			{
				if (i == index)
				{
					PlayerPrefs.SetInt($"{nameof(TabSelector)}{_playerPrefsKey}", i);
					_buttons[i].IsActive = true;
					ActiveBaseTab = _tabs[i];
					_tabs[i].OnTabEntered();
					_tabs[i].style.display = DisplayStyle.Flex;
					_tabs[i].style.opacity = 1;
				}
				else
				{
					if (_buttons[i].IsActive) _tabs[i].OnTabLeft();
					_buttons[i].IsActive = false;
					_tabs[i].style.display = DisplayStyle.None;
					_tabs[i].style.opacity = dimOpacity;
				}
			}
		}
	}
}
