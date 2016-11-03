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
			foreach (var resourceName in executingAssembly.GetManifestResourceNames())
			{
				using (var s = executingAssembly.GetManifestResourceStream($"{Definitions.Prefix}{assemblyName.Name}.dll"))
				{
					if (s != null)
					{
						using (BinaryReader reader = new BinaryReader(s))
						{
							return Assembly.Load(reader.ReadBytes((int)s.Length));
						}
					}
				}
			}
			return null;
		}
	}
}
