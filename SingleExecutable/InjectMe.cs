using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SingleExecutable
{
	static class InjectMe
	{
		static InjectMe()
		{
			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
			PreExtractDlls();
		}

		static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
		{
			Log($"Resolving '{args.Name}'.");
			var assemblyName = new AssemblyName(args.Name);
			return GetLoadedAssembly(assemblyName) ?? GetEmbeddedAssembly(assemblyName);
		}

		static MemoryStream Decompress(Stream stream)
		{
			var output = new MemoryStream();
			using (var compressor = new GZipStream(output, CompressionMode.Decompress))
			{
				stream.CopyTo(compressor);
			}

			return output;
		}

		static byte[] DecompressBytes(Stream aStream)
		{
			using (var output = Decompress(aStream))
			{
				return output.ToArray();
			}
		}

		static bool IsCompressed(string resourceName) =>
			resourceName.IndexOf(Definitions.PrefixCompressed, StringComparison.Ordinal) == 0;

		static void PreExtractDlls()
		{
			var executingAssembly = Assembly.GetExecutingAssembly();
			var executingDirectory = Path.GetDirectoryName(executingAssembly.Location);
			foreach (var name in GetPreExtractNames(executingAssembly))
			{
				var dllName = name.Remove(0, Definitions.PrefixCompressed.Length);
				Log($"Pre-extracting '{dllName}'.");
				using (var s = executingAssembly.GetManifestResourceStream(name))
				{
					using (var data = IsCompressed(name) ? Decompress(s) : default(Stream))
					{
						try
						{
							var path = Path.Combine(executingDirectory, dllName);
							if (File.Exists(path) && OnDiskSameAsInResource(data ?? s, path))
							{
								continue;
							}

							SaveToDisk(data ?? s, path);
						}
						catch (IOException ex)
						{
							throw new ApplicationException($"Unable to pre-extract DLL '{dllName}'.", ex);
						}
					}
				}
			}
		}

		static Assembly GetLoadedAssembly(AssemblyName assemblyName)
		{
			Log($"Searching for '{assemblyName.Name}' in loaded assemblies.");
			foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (a.FullName == assemblyName.FullName || a.GetName().Name == assemblyName.Name)
				{
					Log($"Found '{assemblyName.Name}' in loaded assemblies.");
					return a;
				}
			}
			return null;
		}

		static Assembly GetEmbeddedAssembly(AssemblyName assemblyName, Assembly executingAssembly, bool compressed)
		{
			var prefix = compressed ? Definitions.PrefixCompressed : Definitions.PrefixNotCompressed;
			using (var s = executingAssembly.GetManifestResourceStream($"{prefix}{assemblyName.Name}.dll"))
			{
				if (s != null)
				{
					Log($"Found '{assemblyName.Name}' in embedded assemblies.");
					return Assembly.Load(compressed ? DecompressBytes(s) : ReadAllBytes(s));
				}
			}

			return null;
		}

		static Assembly GetEmbeddedAssembly(AssemblyName assemblyName)
		{
			Log($"Searching for '{assemblyName.Name}' in embedded assemblies.");
			var executingAssembly = Assembly.GetExecutingAssembly();
			return GetEmbeddedAssembly(assemblyName, executingAssembly, true) ??
				   GetEmbeddedAssembly(assemblyName, executingAssembly, false);
		}

		static bool OnDiskSameAsInResource(Stream resource, string path)
		{
			resource.Position = 0;
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
			{
				if (fs.Length != resource.Length)
					return false;
				using (BinaryReader resourceReader = new BinaryReader(resource, Encoding.UTF8, true),
					fileReader = new BinaryReader(fs, Encoding.UTF8, true))
				{
					var fileData = fileReader.ReadBytes((int)fs.Length);
					var resourceData = resourceReader.ReadBytes((int)resource.Length);
					if (!fileData.SequenceEqual(resourceData))
						return false;
				}
			}
			return true;
		}

		static void SaveToDisk(Stream resource, string path)
		{
			resource.Position = 0;
			using (var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
			{
				resource.CopyTo(fs);
				fs.SetLength(resource.Length);
			}
		}

		static byte[] ReadAllBytes(Stream stream)
		{
			using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
			{
				return reader.ReadBytes((int)stream.Length);
			}
		}

		static string[] GetPreExtractNames(Assembly executingAssembly)
		{
			using (var s = executingAssembly.GetManifestResourceStream(Definitions.PreExtractResourceName))
			{
				var data = Encoding.UTF8.GetString(ReadAllBytes(s));
				if (data == string.Empty)
					return Array.Empty<string>();
				return data.Split(Definitions.PreExtractSeparator);
			}
		}

		static void Log(string message)
		{
			if (int.TryParse(Environment.GetEnvironmentVariable(Definitions.LoggingEnvironmentVariable), out var logging) && logging == 1)
			{
				File.AppendAllLines(Definitions.LogFile, new[] { message });
			}
		}
	}
}
