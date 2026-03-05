namespace MaterialLab.Editor
{
	using UnityEngine;
	using UnityEngine.UIElements;

	/// <summary>
	/// Shows a message when texture is null (No Texture) or not readable (error + Fix button).
	/// When Fix succeeds, invokes <see cref="OnTextureFixed"/> with the readable texture; parent should rebuild UI.
	/// When texture is readable, the element is left empty.
	/// </summary>
	public class TextureReadableGateElement : VisualElement
	{
		private Texture2D _texture;

		/// <summary>
		/// Current texture. Setting this redraws the element (No Texture / not readable message / empty).
		/// </summary>
		public Texture2D Texture
		{
			get => _texture;
			set
			{
				_texture = value;
				Redraw();
			}
		}

		/// <summary>
		/// Fired when the user clicks Fix and the texture was successfully made readable. Pass the fixed texture.
		/// </summary>
		public System.Action<Texture2D> OnTextureFixed;

		public TextureReadableGateElement(Texture2D texture = null)
		{
			_texture = texture;
			Redraw();
		}

		private void Redraw()
		{
			Clear();

			if (_texture == null)
			{
				Add(new Label("No Texture"));
				return;
			}

			if (!_texture.isReadable)
			{
				var readableRow = new Row();
				readableRow.Add(new Label("Error, texture is not readable. Enable Read/Write or click Fix."));

				var fixButton = new Button(ApplyFix) { text = "Fix" };
				readableRow.Add(fixButton);
				Add(readableRow);
			}
		}

		private void ApplyFix()
		{
			var fixedTexture = _texture.MakeReadableInPlace();
			if (fixedTexture == null || !fixedTexture.isReadable)
			{
				Debug.LogWarning(
					$"[{nameof(TextureReadableGateElement)}] Failed to make texture readable: {_texture?.name}");
				return;
			}

			_texture = fixedTexture;
			Redraw();
			OnTextureFixed?.Invoke(fixedTexture);
		}
	}
}
