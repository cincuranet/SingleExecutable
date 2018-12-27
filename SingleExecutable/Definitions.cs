
namespace SingleExecutable
{
	static class Definitions
	{
		private const string Separator = "|";
		public const string Prefix = nameof(SingleExecutable) + Separator;
		public const string PrefixDll = Prefix + "DLL" + Separator;
		public const string PreExtractResourceName = Prefix + "PreExtract";
		public const char PreExtractSeparator = ':';
		public const string CompressionResourceName = Prefix + "Compression";
		public const char CompressionSeparator = ':';
		public const string LogFile = nameof(SingleExecutable) + ".log";
		public const string LoggingEnvironmentVariable = "SE_DEBUG";
	}
}
