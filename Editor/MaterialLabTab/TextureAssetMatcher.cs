namespace MaterialLab.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using UnityEngine;

	/// <summary>
	/// Utility for matching a set of textures to common PBR texture roles based on name patterns.
	/// Designed for "in the wild" naming from DCCs, scan libraries, etc.
	/// </summary>
	internal readonly struct TextureAssetMatcher
	{
		public Texture2D Main { get; }

		public Texture2D Albedo { get; }

		public Texture2D Metallic { get; }

		public Texture2D Roughness { get; }

		public Texture2D Smoothness { get; }

		public Texture2D Gloss { get; }

		public Texture2D Specular { get; }

		public Texture2D Normal { get; }

		public Texture2D Height { get; }

		public Texture2D Occlusion { get; }

		public Texture2D Emission { get; }

		public TextureAssetMatcher(IEnumerable<Texture2D> textures)
		{
			var list = textures?
					   .Where(t => t != null)
					   .Distinct()
					   .ToList()
					   ?? new List<Texture2D>();

			static string GetName(Texture2D t) => t?.name?.ToLowerInvariant() ?? string.Empty;

			static bool ContainsAny(string name, params string[] patterns)
			{
				foreach (var p in patterns)
				{
					if (name.Contains(p)) return true;
				}

				return false;
			}

			Texture2D FindAndRemove(Func<string, bool> predicate)
			{
				for (int i = 0; i < list.Count; i++)
				{
					var tex = list[i];
					if (predicate(GetName(tex)))
					{
						list.RemoveAt(i);
						return tex;
					}
				}

				return null;
			}

			Texture2D Find(Func<string, bool> predicate)
			{
				foreach (var tex in list)
				{
					if (predicate(GetName(tex))) return tex;
				}

				return null;
			}

			// Typical wild naming heuristics.

			var metallic = FindAndRemove(n =>
											 ContainsAny(
												 n,
												 "metallic",
												 "metalness",
												 "metalness",
												 "metal",
												 "_m",
												 "-m",
												 " mt",
												 "mtl"));

			var roughness = FindAndRemove(
				n =>
					n.Contains("roughness") ||
					n.Contains("rough") ||
					n.EndsWith("_r") ||
					n.EndsWith("-r"));

			var smoothness = FindAndRemove(
				n =>
					n.Contains("smoothness") ||
					n.Contains("smooth") ||
					n.Contains("smo") ||
					n.EndsWith("_s") ||
					n.EndsWith("-s"));

			// Prefer explicit "gloss" / "glossiness"; specular is its own role.
			var gloss = FindAndRemove(n =>
										 ContainsAny(
											 n,
											 "glossiness",
											 "gloss",
											 "gls"));

			var specular = FindAndRemove(n =>
											 ContainsAny(
												 n,
												 "specular",
												 "spec",
												 "spc",
												 "_spec"));

			var normal = FindAndRemove(n =>
								  ContainsAny(
									  n,
									  "normal",
									  "norm",
									  "_nrm",
									  "_n",
									  "-n"));

			var height = FindAndRemove(n =>
								  ContainsAny(
									  n,
									  "height",
									  "hgt",
									  "displacement",
									  "displace",
									  "disp",
									  "parallax",
									  "bump"));

			var occlusion = FindAndRemove(n =>
									ContainsAny(
										n,
										"ambientocclusion",
										" occlusion",
										"_ao",
										"-ao",
										"ao_",
										"ao-",
										"_occ",
										" occ"));

			var emission = FindAndRemove(n =>
								   ContainsAny(
									   n,
									   "emission",
									   "emissive",
									   "emiss",
									   "emit",
									   "glow",
									   "_e",
									   "_em",
									   "selfillum"));

			var albedo = FindAndRemove(n =>
								  ContainsAny(
									  n,
									  "albedo",
									  "basecolor",
									  "base_color",
									  "base-color",
									  "base color",
									  "diffuse",
									  "diff",
									  "_d",
									  "-d",
									  " col",
									  "color"));

			// Fallback: pick something that isn't obviously a non-color data map.
			var main = albedo
					   ?? list.FirstOrDefault(
						   t =>
						   {
							   var n = GetName(t);
							   return t != metallic &&
									  t != roughness &&
									  t != smoothness &&
									  t != gloss &&
									  t != specular &&
									  t != normal &&
									  t != height &&
									  t != occlusion &&
									  t != emission &&
									  !ContainsAny(n, "normal", "nrm", "_n", "height", "bump", "ao", "occlusion");
						   })
					   ?? list.FirstOrDefault();

			Main = main;
			Albedo = albedo;
			Metallic = metallic;
			Roughness = roughness;
			Smoothness = smoothness;
			Gloss = gloss;
			Specular = specular;
			Normal = normal;
			Height = height;
			Occlusion = occlusion;
			Emission = emission;
		}

		public Texture2D GetTextureByRole(TextureRole role)
		{
			return role switch
			{
				TextureRole.Main => Main,
				TextureRole.Albedo => Albedo,
				TextureRole.Metallic => Metallic,
				TextureRole.Roughness => Roughness,
				TextureRole.Smoothness => Smoothness,
				TextureRole.Gloss => Gloss,
				TextureRole.Specular => Specular,
				TextureRole.Normal => Normal,
				TextureRole.Height => Height,
				TextureRole.Occlusion => Occlusion,
				TextureRole.Emission => Emission,
				_ => null
			};
		}

		public List<TextureRole> GetRecognizedTextures()
		{
			var result = new List<TextureRole>(11);

			void AddIfNotNull(TextureRole role, Texture2D tex)
			{
				if (tex != null) result.Add(role);
			}

			AddIfNotNull(TextureRole.Main, Main);
			AddIfNotNull(TextureRole.Albedo, Albedo);
			AddIfNotNull(TextureRole.Metallic, Metallic);
			AddIfNotNull(TextureRole.Roughness, Roughness);
			AddIfNotNull(TextureRole.Smoothness, Smoothness);
			AddIfNotNull(TextureRole.Gloss, Gloss);
			AddIfNotNull(TextureRole.Specular, Specular);
			AddIfNotNull(TextureRole.Normal, Normal);
			AddIfNotNull(TextureRole.Height, Height);
			AddIfNotNull(TextureRole.Occlusion, Occlusion);
			AddIfNotNull(TextureRole.Emission, Emission);

			return result;
		}
	}
}
