namespace MaterialLab.Editor
{
	using UnityEngine;
	using UnityEngine.UIElements;

	public class CheckerElement : VisualElement
	{
		private static Color BgCol1 = new Color(0.4f, 0.4f, 0.4f);

		private static Color BgCol2 = new Color(0.7f, 0.7f, 0.7f);

		private const int Size = 15;

		private static Texture2D CheckerTexture
		{
			get
			{
				checkerTexture = MakeChecker();
				return checkerTexture;
			}
		}

		private static Texture2D checkerTexture;

		private static Texture2D MakeChecker()
		{
			var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);

			for (int y = 0; y < Size; y++)
				for (int x = 0; x < Size; x++)
				{
					bool whichColor = (x > Size / 2) ^ (y > Size / 2);
					tex.SetPixel(x, y, whichColor ? BgCol1 : BgCol2);
				}

			tex.Apply();
			tex.wrapMode = TextureWrapMode.Repeat;
			return tex;
		}

		public CheckerElement(int width, int height)
		{
			style.width = width;
			style.height = height;

			style.backgroundImage = CheckerTexture;
			style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
			style.backgroundSize = new BackgroundSize(Size, Size);
		}
	}
}
