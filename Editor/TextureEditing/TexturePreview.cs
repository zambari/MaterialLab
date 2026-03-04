namespace MaterialLab.Editor
{
	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class TexturePreview : VisualElement
	{
		private Texture2D _texture;

		private VisualElement preview;

		public Texture2D Texture
		{
			get { return _texture; }
			set
			{
				var (sizex, sizey) = GetScaledTextureSize(value);
				preview.style.width = sizex;
				preview.style.height = sizey;

				preview.style.backgroundImage = value;
				preview.style.backgroundSize = new BackgroundSize(Length.Pixels(sizex), Length.Pixels(sizey));
				preview.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
			}
		}

		internal static (int, int ) GetScaledTextureSize(Texture2D texture)
		{
			if (texture == null) return (0, 0);
			float ratio = texture.width / (float)texture.height;
			int targetSize = 100;
			int sizex = targetSize;
			int sizey = targetSize;
			if (ratio < 0.5f) targetSize = (int)(targetSize * 1.5f);
			if (ratio > 2f) targetSize = (int)(targetSize / 1.5f);
			if (ratio > 1)
			{
				sizey = targetSize;
				sizex = (int)(targetSize * ratio);
			}
			else
			{
				sizex = (int)(targetSize * ratio);
				sizey = (int)(targetSize);
			}

			return (sizex, sizey);
		}

		public TexturePreview(Texture2D texture)
		{
			preview = new VisualElement();
			Add(preview);
			Texture = texture;
		}
	}
}
