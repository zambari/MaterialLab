namespace MaterialLab.Editor
{
	using System;

	using UnityEngine;

	public enum HistogramValueType
	{
		AverageRgb,
		Alpha
	}

	/// <summary>
	/// Wraps a source texture with:
	/// - a low resolution preview (averaged blocks of pixels)
	/// - a histogram texture built from that preview.
	/// Used by editor elements (e.g. TextureAdjustElement) to preview and
	/// analyze textures without touching full resolution data every frame.
	/// </summary>
	public class TextureDataWrapper
	{
		private const int PreferredMinPreviewSize = 128;
		private const int PreferredMaxPreviewSize = 256;
		private const int RelaxedMinPreviewSize = 112;
		private const int RelaxedMaxPreviewSize = 496;
		private const int PreviewMultiple = 16;

		private readonly Texture2D source;
		private readonly int previewWidth;
		private readonly int previewHeight;
		private readonly int blockWidth;
		private readonly int blockHeight;

		private Color[] colorsSource;
		private Color[] colorsPreview;
		private Texture2D previewTexture;
		private int[] histogramCounts;
		private Texture2D histogramTexture;
		private int histogramBucketSize = 256;
		private int histogramHeight = 64;
		private float histogramMultiplier = 1f;
		private bool useNonLinearHistogramScaling = true;
		private float histogramNonLinearStrength = 1f;
		private Color histogramSampleColor = Color.white;
		private Color histogramBackgroundColor = Color.black;
		private HistogramValueType histogramValueType = HistogramValueType.AverageRgb;

		public Texture2D Source => source;

		public int Width => source.width;

		public int Height => source.height;

		public int PreviewWidth => previewWidth;

		public int PreviewHeight => previewHeight;

		public TextureDataWrapper(Texture2D source)
		{
			this.source = source ?? throw new ArgumentNullException(nameof(source));
			(previewWidth, blockWidth) = CalculatePreviewDimension(this.source.width);
			(previewHeight, blockHeight) = CalculatePreviewDimension(this.source.height);
		}

		/// <summary>
		/// Full resolution source colors (lazy loaded).
		/// </summary>
		public Color[] ColorsSource => colorsSource ??= source.GetPixels();

		/// <summary>
		/// Downsampled preview colors.
		/// External code may overwrite this array (and then call RepaintHistogram)
		/// to visualize histograms of processed preview data.
		/// </summary>
		public Color[] ColorsPreview
		{
			get
			{
				if (colorsPreview == null)
				{
					colorsPreview = BuildPreviewColors();
				}

				return colorsPreview;
			}
		}

		/// <summary>
		/// Low resolution preview texture built from ColorsPreview.
		/// </summary>
		public Texture2D PreviewTexture
		{
			get
			{
				if (previewTexture == null)
				{
					previewTexture = new Texture2D(previewWidth, previewHeight, TextureFormat.RGBA32, false);
					previewTexture.SetPixels(ColorsPreview);
					previewTexture.Apply();
				}

				return previewTexture;
			}
		}

		/// <summary>
		/// Texture reused for histogram drawing. Call RepaintHistogram
		/// after modifying ColorsPreview to update its contents.
		/// </summary>
		public Texture2D HistogramTexture
		{
			get
			{
				if (histogramTexture == null)
				{
					histogramTexture = CreateHistogramTexture();
				}

				return histogramTexture;
			}
		}

		/// <summary>
		/// Computes histogram bucket counts for the current preview colors.
		/// </summary>
		public int[] GetHistogram(int bucketSize, HistogramValueType valueType)
		{
			if (bucketSize <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(bucketSize));
			}

			if (histogramCounts == null || histogramCounts.Length != bucketSize)
			{
				histogramCounts = new int[bucketSize];
			}
			else
			{
				Array.Clear(histogramCounts, 0, histogramCounts.Length);
			}

			histogramBucketSize = bucketSize;
			histogramValueType = valueType;

			var counts = histogramCounts;
			var previewColors = ColorsPreview;

			for (int i = 0; i < previewColors.Length; i++)
			{
				float value = valueType == HistogramValueType.Alpha
					? previewColors[i].a
					: (previewColors[i].r + previewColors[i].g + previewColors[i].b) / 3f;
				value = Mathf.Clamp01(value);
				int bucket = Mathf.Min(bucketSize - 1, Mathf.FloorToInt(value * bucketSize));
				counts[bucket]++;
			}

			return counts;
		}

		/// <summary>
		/// Rebuilds the histogram texture from current ColorsPreview data and settings.
		/// </summary>
		public void RepaintHistogram()
		{
			var counts = GetHistogram(histogramBucketSize, histogramValueType);
			int width = Mathf.Max(1, counts.Length);
			int maxCount = 0;
			for (int i = 0; i < width; i++)
			{
				if (counts[i] > maxCount)
				{
					maxCount = counts[i];
				}
			}

			if (histogramTexture == null
				|| histogramTexture.width != width
				|| histogramTexture.height != histogramHeight)
			{
				histogramTexture = new Texture2D(width, histogramHeight, TextureFormat.RGBA32, false);
			}

			var pixels = new Color[width * histogramHeight];
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = histogramBackgroundColor;
			}

			for (int x = 0; x < width; x++)
			{
				int barHeight;
				if (!useNonLinearHistogramScaling)
				{
					barHeight = Mathf.Clamp(Mathf.RoundToInt(counts[x] * histogramMultiplier), 0, histogramHeight);
				}
				else
				{
					float normalized = maxCount > 0 ? (float)counts[x] / maxCount : 0f;
					float curved = ApplyNonLinearHistogramCurve(normalized);
					barHeight = Mathf.Clamp(Mathf.RoundToInt(curved * histogramMultiplier * histogramHeight), 0, histogramHeight);
				}

				for (int y = 0; y < barHeight; y++)
				{
					pixels[y * width + x] = histogramSampleColor;
				}
			}

			histogramTexture.SetPixels(pixels);
			histogramTexture.Apply();
		}

		private Texture2D CreateHistogramTexture()
		{
			int width = Mathf.Max(1, histogramBucketSize);
			return new Texture2D(width, histogramHeight, TextureFormat.RGBA32, false);
		}

		/// <summary>
		/// Applies a non-linear curve to a normalized histogram value in [0,1].
		/// Low values are preserved while higher values are progressively
		/// compressed, so large peaks do not dominate the display.
		/// </summary>
		/// <param name="normalizedValue">Histogram bucket value normalized to [0,1].</param>
		/// <returns>Curved value in [0,1].</returns>
		private float ApplyNonLinearHistogramCurve(float normalizedValue)
		{
			normalizedValue = Mathf.Clamp01(normalizedValue);

			// First gently "lift" low values with a gamma-like curve so that
			// near-zero buckets become more visible, while keeping 0 mapped to 0.
			const float lowValueBoost = 2f; // >1 boosts shadows
			if (normalizedValue > 0f)
			{
				normalizedValue = Mathf.Pow(normalizedValue, 1f / lowValueBoost);
			}

			// Then compress high values so large peaks do not dominate:
			// f(v) = log(1 + k * v) / log(1 + k)
			// k controls how aggressively we compress high values.
			// k -> 0 approximates linear; larger k increases compression.
			float k = Mathf.Max(0.01f, histogramNonLinearStrength);
			float denom = Mathf.Log(1f + k);
			if (denom <= 0f)
			{
				return normalizedValue;
			}

			return Mathf.Log(1f + k * normalizedValue) / denom;
		}


		private Color[] BuildPreviewColors()
		{
			if (previewWidth == Width && previewHeight == Height)
			{
				return ColorsSource;
			}

			var colors = new Color[previewWidth * previewHeight];
			var sourceColors = ColorsSource;
			int sourceWidth = Width;
			int blockPixelCount = blockWidth * blockHeight;

			for (int py = 0; py < previewHeight; py++)
			{
				int srcY = py * blockHeight;
				for (int px = 0; px < previewWidth; px++)
				{
					int srcX = px * blockWidth;
					Color sum = Color.clear;
					for (int by = 0; by < blockHeight; by++)
					{
						int rowIndex = (srcY + by) * sourceWidth + srcX;
						for (int bx = 0; bx < blockWidth; bx++)
						{
							sum += sourceColors[rowIndex + bx];
						}
					}

					colors[py * previewWidth + px] = sum / blockPixelCount;
				}
			}

			return colors;
		}

		private static (int preview, int blockSize) CalculatePreviewDimension(int sourceDimension)
		{
			if (sourceDimension <= PreferredMaxPreviewSize)
			{
				return (sourceDimension, 1);
			}

			int preview = FindPreviewDivisor(sourceDimension, PreferredMaxPreviewSize, PreferredMinPreviewSize);
			if (preview == 0)
			{
				int relaxedMax = Mathf.Min(RelaxedMaxPreviewSize, sourceDimension - (sourceDimension % PreviewMultiple));
				preview = FindPreviewDivisor(sourceDimension, relaxedMax, RelaxedMinPreviewSize);
			}

			if (preview == 0)
			{
				return (sourceDimension, 1);
			}

			return (preview, sourceDimension / preview);
		}

		private static int FindPreviewDivisor(int sourceDimension, int maxCandidate, int minCandidate)
		{
			int start = Mathf.Min(maxCandidate, sourceDimension - (sourceDimension % PreviewMultiple));
			for (int candidate = start; candidate >= minCandidate; candidate -= PreviewMultiple)
			{
				if (candidate > 0 && sourceDimension % candidate == 0)
				{
					return candidate;
				}
			}

			return 0;
		}
	}
}

