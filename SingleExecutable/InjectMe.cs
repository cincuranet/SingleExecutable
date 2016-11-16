using System;
using System.IO;
using System.Reflection;

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

		static void ExtractNativeDlls()
		{
			var executingAssembly = Assembly.GetExecutingAssembly();
			var executingDirectory = Path.GetDirectoryName(executingAssembly.Location);
			foreach (var name in executingAssembly.GetManifestResourceNames())
			{
				if (!name.StartsWith(Definitions.PrefixNative, StringComparison.Ordinal))
					continue;
				var dllName = name.Remove(0, Definitions.PrefixNative.Length);
				using (var s = executingAssembly.GetManifestResourceStream(name))
				{
					using (var fs = new FileStream(Path.Combine(executingDirectory, dllName), FileMode.OpenOrCreate, FileAccess.Write))
					{
						fs.Position = 0;
						s.CopyTo(fs);
						fs.SetLength(s.Length);
					}
				}
			}
		}
	}
}
