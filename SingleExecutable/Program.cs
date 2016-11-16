using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Mono.Cecil;

namespace SingleExecutable
{
	static class Program
	{
		readonly static System.Reflection.Assembly ExecutingAssembly = System.Reflection.Assembly.GetExecutingAssembly();

		static void Main(string[] args)
		{
			var app = new CommandLineApplication(false);
			app.FullName = nameof(SingleExecutable);
			app.Name = Path.GetFileName(ExecutingAssembly.Location);
			var argExecutable = app.Option("-e", "Executable to inject into.", CommandOptionType.SingleValue);
			var argOutput = app.Option("-o", "Output file.", CommandOptionType.SingleValue);
			var argAdd = app.Option("-a", "Another files to add.", CommandOptionType.MultipleValue);
			app.Execute(args);
			if (!argExecutable.HasValue() || !argOutput.HasValue())
			{
				app.ShowHelp();
				ErrorExit();
			}

			var executable = Path.GetFullPath(argExecutable.Value());
			var output = Path.GetFullPath(argOutput.Value());
			if (!File.Exists(executable))
				ErrorExit($"File '{executable}' does not exists.");

			try
			{
				Console.WriteLine("Loading.");
				var assembly = AssemblyDefinition.ReadAssembly(executable);
				Console.WriteLine("Embedding:");
				EmbeddDlls(assembly, Path.GetDirectoryName(executable), argAdd.Values);
				Console.WriteLine("Injecting:");
				InjectLoading(assembly);
				Console.WriteLine("Writing.");
				WriteAssembly(assembly, output);
				Console.WriteLine("Done.");
			}
			catch (Exception ex)
			{
				ErrorExit(ex.ToString());
			}
		}

		static void ErrorExit(string message = null)
		{
			if (message != null)
				Console.Error.WriteLine(message);
			Environment.Exit(1);
		}

		static void EmbeddDlls(AssemblyDefinition assembly, string directory, IEnumerable<string> anotherFiles)
		{
			var resources = assembly.MainModule.Resources;
			foreach (var dll in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
			{
				var name = dll.Remove(0, directory.Length + 1);
				EmbedDll(assembly, dll, name);
			}
			foreach (var file in anotherFiles.Select(f => Path.GetFullPath(f)).Where(f => File.Exists(f)))
			{
				var name = Path.GetFileName(file);
				EmbedDll(assembly, file, name);
			}
		}

		static void InjectLoading(AssemblyDefinition assembly)
		{
			var entryPointClass = assembly.EntryPoint.DeclaringType;
			var sourceAssembly = AssemblyDefinition.ReadAssembly(ExecutingAssembly.Location);
			Console.WriteLine($"  methods");
			MethodCopier.CopyMethods(sourceAssembly.MainModule.Types.First(t => t.Name == nameof(InjectMe)), entryPointClass, Definitions.Prefix);
			Console.WriteLine($"  .cctor");
			CctorProcessor.ProcessCctor(entryPointClass, Definitions.Prefix);
		}

		static void EmbedDll(AssemblyDefinition assembly, string dll, string name)
		{
			Console.WriteLine($"  {name}");
			var resourceName = Helpers.IsNativeDll(dll)
				? $"{Definitions.Prefix}{name}"
				: $"{Definitions.PrefixNative}{name}";
			var resource = new EmbeddedResource(resourceName, ManifestResourceAttributes.Private, File.ReadAllBytes(dll));
			resources.Add(resource);
		}

		static void WriteAssembly(AssemblyDefinition assembly, string output)
		{
			var dir = Path.GetDirectoryName(output);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			assembly.Write(output);
		}
	}
}
