using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace SingleExecutable
{
	static class InjectMe
	{
		static InjectMe()
		{
			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
			ExtractNativeDlls();
		}

		static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
		{
			var assemblyName = new AssemblyName(args.Name);
			return GetLoadedAssembly(assemblyName) ?? GetEmbeddedAssembly(assemblyName);
		}

		static void ExtractNativeDlls()
		{
			var executingAssembly = Assembly.GetExecutingAssembly();
			var executingDirectory = Path.GetDirectoryName(executingAssembly.Location);
			foreach (var name in executingAssembly.GetManifestResourceNames())
			{
				if (!name.StartsWith(Definitions.PrefixNative, StringComparison.Ordinal))
				{
					continue;
				}
				var dllName = name.Remove(0, Definitions.PrefixNative.Length);
				using (var s = executingAssembly.GetManifestResourceStream(name))
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
						throw new ApplicationException($"Unable to extract native DLL '{dllName}'.", ex);
					}
				}
			}
		}

		static Assembly GetLoadedAssembly(AssemblyName assemblyName)
		{
			foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (a.FullName == assemblyName.FullName || a.GetName().Name == assemblyName.Name)
				{
					return a;
				}
			}
			return null;
		}

		static Assembly GetEmbeddedAssembly(AssemblyName assemblyName)
		{
			var executingAssembly = Assembly.GetExecutingAssembly();
			using (var s = executingAssembly.GetManifestResourceStream($"{Definitions.Prefix}{assemblyName.Name}.dll"))
			{
				if (s != null)
				{
					using (var reader = new BinaryReader(s))
					{
						return Assembly.Load(reader.ReadBytes((int)s.Length));
					}
				}
			}
			return null;
		}

		static byte[] ComputeChecksum(Stream stream)
		{
			using (var sha = SHA1.Create())
			{
				return sha.ComputeHash(stream);
			}
		}

		static bool OnDiskSameAsInResource(Stream resource, string path)
		{
			resource.Position = 0;
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
			{
				if (fs.Length != resource.Length)
					return false;
				using (BinaryReader resourceReader = new BinaryReader(resource),
					fileReader = new BinaryReader(fs))
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
	}
}
