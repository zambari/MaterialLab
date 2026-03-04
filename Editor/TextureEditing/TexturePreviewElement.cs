namespace MaterialLab.Editor
{
	using UnityEngine;
	using UnityEngine.UIElements;

	public class TexturePreviewElement:VisualElement
	{
		public TexturePreviewElement(Texture2D texture)
		{
			int padding = 5;
			int margin = 5;
			var (sizex, sizey) = TexturePreview.GetScaledTextureSize(texture);
			var checker=new CheckerElement(sizex, sizey);
			var preview = new TexturePreview(texture);
			Add(checker);
			checker.Add(preview);
			this.SetPadding(padding);
			this.SetMargin(margin);
			this.SetBackgroundColor(Color.black * 0.3f);
			this.style.width = Length.Pixels(sizex+2*padding);
			this.style.height = Length.Pixels(sizey+2*padding);

		}
	}
}
