namespace MaterialLab.UIExtensions
{

	using System;

	using UnityEngine;
	using UnityEngine.UIElements;

	/// <summary>
	/// UI Extensions
	/// v.2. Made generic and added some extra methods
	/// This class gets copied around A LOT, sorry.
	/// </summary>
	public static class UIExtensions
	{
		/// <summary>
		/// Sets border width.
		/// </summary>
		public static T SetBorderWidth<T>(this T element, float size) where T : VisualElement
		{
			if (element == null) return null;

			element.style.borderTopWidth = size;
			element.style.borderBottomWidth = size;
			element.style.borderLeftWidth = size;
			element.style.borderRightWidth = size;
			return element;
		}

		/// <summary>
		/// Sets border color.
		/// </summary>
		public static T SetBorderColor<T>(this T element, Color color) where T : VisualElement
		{
			if (element == null) return null;

			element.style.borderTopColor = color;
			element.style.borderBottomColor = color;
			element.style.borderLeftColor = color;
			element.style.borderRightColor = color;
			return element;
		}

		/// <summary>
		/// Sets border radius.
		/// </summary>
		public static T SetBorderRadius<T>(this T element, int radius) where T : VisualElement
		{
			if (element == null) return null;

			element.style.borderTopRightRadius = radius;
			element.style.borderTopLeftRadius = radius;
			element.style.borderBottomLeftRadius = radius;
			element.style.borderBottomRightRadius = radius;
			return element;
		}

		/// <summary>
		/// Sets all margins.
		/// </summary>
		public static VisualElement SetMargin<T>(this T element, int margin) where T : VisualElement
		{
			if (element == null) return null;

			element.style.marginTop = margin;
			element.style.marginRight = margin;
			element.style.marginBottom = margin;
			element.style.marginLeft = margin;
			return element;
		}

		/// <summary>
		/// Sets all paddings.
		/// </summary>
		public static VisualElement SetPadding<T>(this T element, int padding) where T : VisualElement
		{
			if (element == null) return null;

			element.style.paddingTop = padding;
			element.style.paddingRight = padding;
			element.style.paddingBottom = padding;
			element.style.paddingLeft = padding;
			return element;
		}

		/// <summary>
		/// Sets background color.
		/// </summary>
		public static T SetBackgroundColor<T>(this T element, Color backgroundColor) where T : VisualElement
		{
			if (element == null) return null;

			element.style.backgroundColor = backgroundColor;
			return element;
		}

		/// <summary>
		/// Sets background color.
		/// </summary>
		public static T SetVisible<T>(this T element, bool visible) where T : VisualElement
		{
			if (element == null) return null;

			element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
			return element;
		}

		/// <summary>
		/// Sets background color.
		/// </summary>
		public static T SetOpacity<T>(this T element, float opacity) where T : VisualElement
		{
			if (element == null) return null;

			element.style.opacity = opacity;
			return element;
		}

		/// <summary>
		/// Adds a child label
		/// </summary>
		public static Label AddLabel(this VisualElement element, string text, int fontSize = -1)
		{
			var label = new Label(text);
			if (fontSize != -1) label.style.fontSize = -1;
			element.Add(label);
			return label;
		}

		/// <summary>
		/// Adds a child button using delegate method as label
		/// </summary>
		public static Button AddButton(this VisualElement element, Action method, string text = null)
		{
			var button = GetMethodButton(method, text);
			element.Add(button);
			return button;
		}

		/// <summary>
		/// Adds a child button using delegate method as label
		/// </summary>
		public static Button AddButtonSmall(this VisualElement element, Action method, string text = null)
		{
			var button = GetMethodButton(method, text);
			button.style.fontSize = 9;
			element.Add(button);
			return button;
		}

		/// <summary>
		/// Creates a button using delegate name as label
		/// </summary>
		public static Button GetMethodButton(Action method, string text = null)
		{
			if (string.IsNullOrEmpty(text))
			{
				text = method.Method.Name;

				// sometimes the format will be "<Class>__Method|1", we strip it here

				int index = text.IndexOf("__", StringComparison.InvariantCultureIgnoreCase);
				if (index >= 0) text = text.Substring(index + 2);
				index = text.IndexOf("|", StringComparison.InvariantCultureIgnoreCase);
				if (index >= 0) text = text.Substring(0, index);
			}

			return new Button(method) { text = text };
		}

		/// <summary>
		/// Adds a child label, using relative font size adjustment (+/-)
		/// </summary>
		public static T AdjustFontSize<T>(this T label, int relativeFontSize) where T : VisualElement
		{
			label.style.fontSize = 12 + relativeFontSize;
			return label;
		}
	}
}
