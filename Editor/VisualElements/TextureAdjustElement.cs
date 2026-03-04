namespace MaterialLab.Editor
{
	using System;

	using UnityEditor.UIElements;

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

		// Immutable snapshot of the source at the moment the editor UI is built.
		// Used as the input for full-resolution processing so subsequent asset
		// overwrites don't change the "original" we are adjusting from.
		private Texture2D sourceSnapshotFull;

		private Texture2D _texture;

		private Texture2D sourceTexture_resized;

		private Texture2D _texture_resized;

		private TextureDataWrapper textureData;

		public Action OnTextureChange;

		/// <summary>
		/// Fired when the user changes any control (sliders, invert, curves, etc.)
		/// or when values are reset to defaults.
		/// </summary>
		public Action OnUserChange;

		private TexturePreview resultPreview;

		private Image histogramImage;

		private Toggle invertToggle;

		private Toggle curvesToggle;

		private CurveField curveField;

		private float multiplierValue;

		private float offsetValue;

		private bool curveEnabled;

		private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

		public bool HasUserChanges { get; private set; }

		private bool suppressUserChangeNotification;
		public Texture2D ProcessedTexture
		{
			get
			{
				if (_texture == null)
				{
					_texture = new Texture2D(sourceTexture.width, sourceTexture.height);
				}

				var input = sourceSnapshotFull != null ? sourceSnapshotFull : sourceTexture;
				ApplyModifications(input, _texture);

				return _texture;
			}
		}


		protected virtual void ApplyModifications(Texture2D thisSource, Texture2D thisTarget)
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
		
		/// <summary>
		/// Clamps color. unused but do not remove.
		/// </summary>
		/// <param name="c"></param>
		/// <returns></returns>
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
			float ProcessChannel(float v)
			{
				float value = v * multiplierValue + offsetValue;

				if (curveEnabled && curve != null)
				{
					value = curve.Evaluate(value);
				}

				if (invertToggle != null && invertToggle.value)
				{
					value = 1f - value;
				}

				return Mathf.Clamp01(value);
			}

			return new Color(
				ProcessChannel(c.r),
				ProcessChannel(c.g),
				ProcessChannel(c.b),
				c.a);
		}

		protected VisualElement GetControls(string id)
		{
			var controls = new VisualElement();
			controls.style.minWidth = 200;
			controls.style.marginLeft = 10;
			controls.style.marginRight = 10;
			multiplier = GetSavedSlider(id + "multiplier", 0, 3f, 1);
			offset = GetSavedSlider(id + "offset", -0.5f, 0.5f, 0);
			// Initialize internal values from sliders so first repaint is correct.
			multiplierValue = multiplier.value;
			offsetValue = offset.value;
			multiplier.RegisterValueChangedCallback(x =>
			{
				multiplierValue = x.newValue;
				NotifyUserChange();
				UpdateTexturePreview();
			});
			offset.RegisterValueChangedCallback(x =>
			{
				offsetValue = x.newValue;
				NotifyUserChange();
				UpdateTexturePreview();
			});
			controls.Add(new Label("Controls"));
			controls.Add(new Label("multiplier"));
			controls.Add(multiplier);
			controls.Add(new Label("offset"));
			controls.Add(offset);

			invertToggle = new Toggle("Invert");
			invertToggle.RegisterValueChangedCallback(_ =>
			{
				NotifyUserChange();
				UpdateTexturePreview();
			});
			controls.Add(invertToggle);

			curvesToggle = new Toggle("Enable curves");
			curvesToggle.RegisterValueChangedCallback(evt =>
			{
				curveEnabled = evt.newValue;
				if (curveField != null)
				{
					curveField.style.display = curveEnabled ? DisplayStyle.Flex : DisplayStyle.None;
				}
				NotifyUserChange();
				UpdateTexturePreview();
			});
			controls.Add(curvesToggle);

			curveField = new CurveField("Curve")
			{
				value = curve
			};
			curveField.style.display = DisplayStyle.None;
			curveField.RegisterValueChangedCallback(evt =>
			{
				curve = evt.newValue ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
				NotifyUserChange();
				UpdateTexturePreview();
			});
			controls.Add(curveField);

			var resetButton = new Button(
				() =>
				{
					suppressUserChangeNotification = true;
					multiplier.value = 1f;
					offset.value = 0f;
					if (invertToggle != null) invertToggle.value = false;
					if (curvesToggle != null) curvesToggle.value = false;
					curveEnabled = false;
					curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
					if (curveField != null) curveField.value = curve;
					suppressUserChangeNotification = false;
					HasUserChanges = false;
					OnUserChange?.Invoke();
					UpdateTexturePreview();
				})
			{
				text = "Reset"
			};
			resetButton.style.marginTop = 4;
			controls.Add(resetButton);

			return controls;
		}

	

		private void UpdateTexturePreview()
		{
			ApplyModifications(sourceTexture_resized, _texture_resized);
			resultPreview.Texture = _texture_resized;

			if (textureData != null && histogramImage != null)
			{
				var processedPreview = _texture_resized.GetPixels();
				var previewColors = textureData.ColorsPreview;
				var count = Mathf.Min(processedPreview.Length, previewColors.Length);
				Array.Copy(processedPreview, previewColors, count);
				textureData.RepaintHistogram();
				histogramImage.image = textureData.HistogramTexture;
			}

			OnTextureChange?.Invoke();
		}

		public TextureAdjustElement(Texture2D texture, string name)
		{
			this.AddBorder();
			if (texture == null)
			{
				Add(new Label("No texture selected"));
				return;
			}

			Add(new Label($"Edit {name}"));
			if (!texture.isReadable)
			{
				var readableRow = new Row();
				readableRow.Add(new Label("Error, texture is not readable. Enable Read/Write or click Fix."));

				var fixButton = new Button(
					() =>
					{
						var fixedTexture = texture.MakeReadableInPlace();
						if (fixedTexture == null || !fixedTexture.isReadable)
						{
							Debug.LogWarning($"[{nameof(TextureAdjustElement)}] Failed to make texture readable: {texture?.name}");
							return;
						}

						Clear();
						this.AddBorder();
						BuildUIForTexture(fixedTexture, name);
					})
				{
					text = "Fix"
				};

				readableRow.Add(fixButton);
				Add(readableRow);
				return;
			}

			BuildUIForTexture(texture, name);
		}

		private void BuildUIForTexture(Texture2D texture, string name)
		{
			sourceTexture = texture;
			// Take a decoupled full-resolution snapshot as our immutable "input".
			sourceSnapshotFull = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
			sourceSnapshotFull.SetPixels(sourceTexture.GetPixels());
			sourceSnapshotFull.Apply();

			textureData = new TextureDataWrapper(sourceTexture);
			sourceTexture_resized = textureData.PreviewTexture;
			_texture = new Texture2D(sourceTexture.width, sourceTexture.height);
			_texture_resized = new Texture2D(sourceTexture_resized.width, sourceTexture_resized.height);

			var row = new VisualElement() { style = { flexDirection = FlexDirection.Row } };
			row.style.flexGrow = 0;

			var originalColumn = new VisualElement();
			originalColumn.Add(new TexturePreview(texture));
			row.Add(originalColumn);

			var controlsColumn = GetControls(name);
			row.Add(controlsColumn);

			var previewColumn = new VisualElement();
			resultPreview = new TexturePreview(texture);
			previewColumn.Add(resultPreview);

			histogramImage = new Image();
			histogramImage.style.width = 160;
			histogramImage.style.height = 40;
			histogramImage.scaleMode = ScaleMode.StretchToFill;
			previewColumn.Add(new Label("Histogram"));
			previewColumn.Add(histogramImage);

			row.Add(previewColumn);

			Add(row);

			textureData.RepaintHistogram();
			histogramImage.image = textureData.HistogramTexture;
			UpdateTexturePreview();
		}

		private Slider GetSavedSlider(string id, float lowValue, float highValue, float defaultValue)
		{
			var slider = new Slider() { lowValue = lowValue, highValue = highValue };
			float control1Value = PlayerPrefs.GetFloat(prefPrefix + id, defaultValue);
			slider.value = control1Value;
			slider.RegisterValueChangedCallback(x =>
												{
													PlayerPrefs.SetFloat(prefPrefix + id, x.newValue);
												});
			return slider;
		}

		private void NotifyUserChange()
		{
			if (suppressUserChangeNotification) return;
			HasUserChanges = true;
			OnUserChange?.Invoke();
		}
	}
}
