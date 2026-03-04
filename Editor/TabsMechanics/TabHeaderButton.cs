namespace MaterialLab.Tabs
{
	using System;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class TabHeaderButton : Button
	{

		private readonly float borderWidth = 2;

		private readonly float borderRadius = 5;

		private readonly float fontsize = 12;

		private readonly float paddingHorizontal = 10;

		private readonly Color borderColor = Color.white / 3;

		private readonly Color borderColorActive = Color.black * 0.2f;
		private readonly Color borderColorActiveBottom =Color.orange;
		private readonly Color borderColorInactive=	new Color(0, 0, 0,0);
		private readonly Color bgInactive = Color.black /2;

		private readonly Color bgActive = Color.clear;

		private readonly float nonActiveTransparency =.5f;

		private bool isActive;

		public bool IsActive
		{
			get { return isActive; }
			set
			{
				isActive = value;
				if (isActive)
				{
					style.backgroundColor = bgActive;
					style.marginTop = -3;
					style.opacity = 1;
					SetBorderColor(borderColorActive);
					style.borderBottomColor = borderColorActiveBottom;
				}
				else
				{
					style.backgroundColor = bgInactive;
					style.marginTop = 2;
					style.opacity = nonActiveTransparency;
					SetBorderColor(borderColor);
					style.borderBottomColor = borderColorInactive;
				}
			}
		}

		private void SetBorderColor(Color color)
		{
			style.borderLeftColor = color;
			style.borderRightColor = color;
			style.borderTopColor = color;
		}

		public TabHeaderButton(string label)
		{
			text = label;

			SetBorderColor(borderColor);
			style.paddingLeft = paddingHorizontal;
			style.paddingRight = paddingHorizontal;
			style.borderTopLeftRadius = borderRadius;
			style.borderTopRightRadius = borderRadius;
			style.borderTopWidth = borderWidth;
			style.borderLeftWidth = borderWidth;
			style.borderRightWidth = borderWidth;
			style.fontSize = fontsize;
			style.backgroundColor = bgInactive;
			style.marginBottom = 0;
		}
	}
}
