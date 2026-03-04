namespace MaterialLab.Editor
{
	using System;
	using UnityEngine;
	using UnityEngine.UIElements;

	/// <summary>
	/// Visual sugar changing default toggle into a flick toggle.
	/// </summary>
	public class FlickToggle : VisualElement
	{
		private const int ToggleWidth = 70;

		private const int MinWidth = 10;

		private const int SideMargin = 2;

		private const int TopBottomMargin = 1;

		private Button subButton;

		private Button bgButton;

		//=new Color(0.2f, .7f, .2f);//
		//new Color(0.2f,0.5f,0.5f);
		private Color onBGColor = new Color(0.1f, .4f, .1f, 0.8f);

		private Color offBGColor = new Color(0.3f, 0.3f, 0.3f);

		/// <summary>
		/// Raised on value change;
		/// </summary>
		public Action<bool> valueChanged;

		private bool _value;

		private string prefsKey;

		/// <summary>
		/// Value Getter/Setter
		/// </summary>
		public bool Value
		{
			get { return _value; }
			set
			{
				_value = value;
				if (_value)
				{
					subButton.style.left = StyleKeyword.Auto;
					subButton.style.right = 0;
					subButton.text = "On";
					bgButton.style.backgroundColor = onBGColor;
				}
				else
				{
					subButton.style.left = 0;
					subButton.style.right = StyleKeyword.Auto;;
					subButton.style.color = Color.white;
					bgButton.style.backgroundColor = offBGColor;
					subButton.text = "Off";
				}

				if (!string.IsNullOrEmpty(prefsKey)) PlayerPrefs.SetInt(prefsKey, value ? 1 : 0);
				valueChanged?.Invoke(_value);
			}
		}

		private void Flick()
		{
			Value = !Value;
		}

		/// <inheritdoc />
		public FlickToggle(string name, bool initialValue = false, string prefsKey = null)
		{
			this.prefsKey = prefsKey;
			style.flexDirection = FlexDirection.Row;
			var label = new Label(name)
			{
				style = { unityTextAlign = TextAnchor.MiddleLeft, minWidth = MinWidth, width = Length.Percent(50) }
			};
			Add(label);
			var buttonBack = new VisualElement() { style = { flexGrow = 1 } };

			bgButton = new Button(Flick);
			bgButton.SetBorderWidth(.2f);
			bgButton.SetBorderRadius(6);
			bgButton.style.height = 20;
			bgButton.style.width = ToggleWidth;
			
			buttonBack.Add(bgButton);
			Add(buttonBack);
			subButton = new Button(Flick);
			subButton.pickingMode = PickingMode.Ignore;
			subButton.style.position = Position.Absolute;
			subButton.style.top = TopBottomMargin;
			subButton.style.bottom = TopBottomMargin;
			subButton.style.left = 0;
			subButton.style.right = 0;
			subButton.SetBorderRadius(4);
			subButton.SetBorderWidth(1.2f);
			subButton.SetBorderColor(Color.black);
			subButton.SetBorderWidth(0);
			subButton.style.width = Length.Percent(50);
			
			// bgButton.style.width = ToggleWidth;
			bgButton.Add(subButton);
			this.style.marginBottom = 1;
			this.style.marginTop = 1;
			if (!string.IsNullOrEmpty(prefsKey) && PlayerPrefs.HasKey(prefsKey))
				Value = PlayerPrefs.GetInt(prefsKey) == 1;
			else Value = initialValue;
		}
	}
}
