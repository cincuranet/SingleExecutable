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
		static readonly Assembly ExecutingAssembly;
		static readonly string[] CompressedResources;

		static InjectMe()
		{
			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
			ExecutingAssembly = Assembly.GetExecutingAssembly();
			CompressedResources = GetCompressionNames(ExecutingAssembly);
			PreExtractDlls();
		}

		static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
		{
			Log($"Resolving '{args.Name}'.");
			var assemblyName = new AssemblyName(args.Name);
			return GetLoadedAssembly(assemblyName) ?? GetEmbeddedAssembly(assemblyName);
		}

		static void PreExtractDlls()
		{
			var executingDirectory = Path.GetDirectoryName(ExecutingAssembly.Location);
			foreach (var name in GetPreExtractNames(ExecutingAssembly))
			{
				var dllName = name.Remove(0, Definitions.PrefixDll.Length);
				Log($"Pre-extracting '{dllName}'.");
				using (var s = GetResourceDllStream(name))
				{
					var path = Path.Combine(executingDirectory, dllName);
					try
					{
						if (File.Exists(path) && OnDiskSameAsInResource(s, path))
						{
							continue;
						}
						SaveToDisk(s, path);
					}
					catch (IOException ex)
					{
						throw new ApplicationException($"Unable to pre-extract DLL '{dllName}'.", ex);
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

		static Assembly GetEmbeddedAssembly(AssemblyName assemblyName)
		{
			Log($"Searching for '{assemblyName.Name}' in embedded assemblies.");
			var name = $"{Definitions.PrefixDll}{assemblyName.Name}.dll";
			using (var s = GetResourceDllStream(name))
			{
				if (s != null)
				{
					Log($"Found '{assemblyName.Name}' in embedded assemblies.");
					return Assembly.Load(ReadAllBytes(s));
				}
			}
			return null;
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

		static byte[] ReadAllBytes(Stream resource)
		{
			resource.Position = 0;
			using (var reader = new BinaryReader(resource, Encoding.UTF8, true))
			{
				return reader.ReadBytes((int)resource.Length);
			}
		}

		static Stream GetResourceDllStream(string name)
		{
			var manifest = ExecutingAssembly.GetManifestResourceStream(name);
			if (manifest == null)
				return null;
			if (!CompressedResources.Contains(name))
				return manifest;
			var memory = new MemoryStream();
			using (var deflate = new DeflateStream(manifest, CompressionMode.Decompress, true))
			{
				deflate.CopyTo(memory);
			}
			return memory;
		}

		static string[] GetCompressionNames(Assembly executingAssembly)
		{
			using (var s = executingAssembly.GetManifestResourceStream(Definitions.CompressionResourceName))
			{
				var data = Encoding.UTF8.GetString(ReadAllBytes(s));
				if (data == string.Empty)
					return Array.Empty<string>();
				return data.Split(Definitions.PreExtractSeparator);
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
