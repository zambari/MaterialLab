namespace MaterialLab.Editor
{
	using System;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class TextureAdjustElement : VisualElement
	{
		private const string prefPrefix = nameof(TextureAdjustElement);

		private const int previewWidth = 256;

		private const int previewHeight = 256;

		private Slider multiplier;

		private Slider offset;

		private Texture2D sourceTexture;

		private Texture2D _texture;

		private Texture2D sourceTexture_resized;

		private Texture2D _texture_resized;

		private TextureDataWrapper textureData;

		public Action OnTextureChange;

		private TexturePreview resultPreview;

		public Texture2D ProcessedTexture
		{
			get
			{
				if (_texture == null)
				{
					_texture = new Texture2D(sourceTexture.width, sourceTexture.height);
				}

				ApplyModifcations(sourceTexture, _texture);

				return _texture;
			}
		}


		protected virtual void ApplyModifcations(Texture2D thisSource, Texture2D thisTarget)
		{
			var pixels = thisSource.GetPixels();
			for (int i = 0; i < pixels.Length; i++)
			{
				var c = pixels[i];
				pixels[i] = ProcessColor(c); //ClampColor
			}

			thisTarget.SetPixels(pixels);
			thisTarget.Apply();
		}

		private Color ClampColor(Color c)
		{
			c.r = Mathf.Clamp(c.r, .2f, .6f);
			c.g = Mathf.Clamp(c.g, .1f, .3f);
			c.b = Mathf.Clamp(c.b, .4f, .9f);
			c.a = Mathf.Clamp(c.a, 0, 1);

		
			return c;
		}

		protected virtual Color ProcessColor(Color c)
		{
			return new Color(
				c.r * multiplierValue + offsetValue,
				c.g * multiplierValue + offsetValue,
				c.b * multiplierValue + offsetValue,
				c.a);
		}

		protected VisualElement GetControls(string id)
		{
			var controls = new VisualElement();
			controls.style.minWidth = 100;
			controls.style.marginLeft = 10;
			controls.style.marginRight = 10;
			multiplier = GetSavedSlider(id + "multiplier", -1.5f, 1.5f, 1);
			offset = GetSavedSlider(id + "offset", -1.5f, 1.5f, 1);
			multiplier.RegisterValueChangedCallback((x) => { multiplierValue = x.newValue; });
			offset.RegisterValueChangedCallback((x) => { offsetValue = x.newValue; });
			controls.Add(new Label("Controls"));
			controls.Add(new Label("multiplier"));
			controls.Add(multiplier);
			controls.Add(new Label("offset"));
			controls.Add(offset);

			return controls;
		}

		private float multiplierValue;

		private float offsetValue;

		private void UpdateTexturePreview()
		{
			ApplyModifcations(sourceTexture_resized, _texture_resized);
			resultPreview.Texture = _texture_resized;
			OnTextureChange?.Invoke();
		}

		public TextureAdjustElement(Texture2D texture, string name)
		{
			this.AddBorder();
			if (texture == null)
			{
				Add(new Label($"No texture selected"));
			}
			else
			{
				Add(new Label($"Edit {name}"));
				if (!texture.isReadable)
				{
					Add(new Label($"Error, texture is not readable, please mark"));
					return;
				}

				sourceTexture = texture; //.EnsureReadableWarningModifiesAsset();
				textureData = new TextureDataWrapper(sourceTexture);
				sourceTexture_resized = textureData.PreviewTexture;
				_texture = new Texture2D(sourceTexture.width, sourceTexture.height);
				_texture_resized = new Texture2D(sourceTexture_resized.width, sourceTexture_resized.height);

				var row = new VisualElement() { style = { flexDirection = FlexDirection.Row } };
				row.style.flexGrow = 0;
				row.Add(new TexturePreview(texture));
				row.Add(GetControls(name));
				resultPreview = new TexturePreview(texture);
				row.Add(resultPreview);
				Add(row);
				UpdateTexturePreview();
			}
		}

		private Slider GetSavedSlider(string id, float lowValue, float highValue, float defaultValue)
		{
			var slider = new Slider() { lowValue = lowValue, highValue = highValue };
			float control1Value = PlayerPrefs.GetFloat(prefPrefix + id, defaultValue);
			slider.value = control1Value;
			slider.RegisterValueChangedCallback(x =>
												{
													PlayerPrefs.SetFloat(prefPrefix + id, x.newValue);
													UpdateTexturePreview();
												});
			return slider;
		}
	}
}
