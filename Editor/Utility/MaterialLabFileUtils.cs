namespace MaterialLab.Editor
{
	using System.IO;

	/// <summary>
	/// Helper methods for generating unique asset paths, timestamp suffixes, and safe file writes.
	/// All file writes should go through <see cref="WriteBytes"/> to avoid accidental overwrites.
	/// </summary>
	internal static class MaterialLabFileUtils
	{
		public static string GetTimestampSuffix()
		{
			return System.DateTime.Now.ToString("yyyyMMddHHmmss");
		}

		/// <summary>
		/// Returns a path that does not overwrite an existing file.
		/// Uses Unity-style naming: name.ext, name (1).ext, name (2).ext, ...
		/// </summary>
		public static string GetUniquePath(string path)
		{
			if (string.IsNullOrEmpty(path)) return path;
			var dir = Path.GetDirectoryName(path);
			var ext = Path.GetExtension(path);
			var baseName = Path.GetFileNameWithoutExtension(path);
			for (int attempt = 0; ; attempt++)
			{
				var name = attempt == 0 ? baseName + ext : $"{baseName} ({attempt}){ext}";
				var candidate = Path.Combine(dir, name);
				if (!File.Exists(candidate)) return candidate;
			}
		}

		/// <summary>
		/// Given an existing asset path, returns a unique path in the same directory
		/// with an extra suffix and optional attempt index.
		/// Example: MyTex.png -> MyTex_suffix.png, MyTex_suffix_1.png, ...
		/// </summary>
		public static string GetUniquePathWithSuffix(string originalPath, string suffix)
		{
			var dir = Path.GetDirectoryName(originalPath);
			var ext = Path.GetExtension(originalPath);
			var baseName = Path.GetFileNameWithoutExtension(originalPath);

			int attempt = 0;
			while (true)
			{
				var attemptSuffix = attempt == 0 ? suffix : $"{suffix}_{attempt}";
				var candidate = Path.Combine(dir, $"{baseName}_{attemptSuffix}{ext}");
				if (!File.Exists(candidate)) return candidate;
				attempt++;
			}
		}

		/// <summary>
		/// Writes bytes to disk. Use for all texture/asset file writes.
		/// If <paramref name="allowOverwrite"/> is false, resolves to a unique path first (no overwrite).
		/// Returns the path actually written to.
		/// </summary>
		public static string WriteBytes(string path, byte[] bytes, bool allowOverwrite = false)
		{
			if (string.IsNullOrEmpty(path) || bytes == null) return path;
			var targetPath = allowOverwrite ? path : GetUniquePath(path);
			File.WriteAllBytes(targetPath, bytes);
			return targetPath;
		}
	}
}

