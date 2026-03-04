namespace MaterialLab.Editor
{
	using System.IO;

	/// <summary>
	/// Helper methods for generating unique asset paths and timestamp suffixes.
	/// </summary>
	internal static class MaterialLabFileUtils
	{
		public static string GetTimestampSuffix()
		{
			return System.DateTime.Now.ToString("yyyyMMddHHmmss");
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
	}
}

