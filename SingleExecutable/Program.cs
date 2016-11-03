using System;
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
				EmbeddDlls(assembly, Path.GetDirectoryName(executable));
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

		static void EmbeddDlls(AssemblyDefinition assembly, string directory)
		{
			var resources = assembly.MainModule.Resources;
			foreach (var dll in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
			{
				var name = dll.Remove(0, directory.Length + 1);
				Console.WriteLine($"  {name}");
				var resource = new EmbeddedResource($"{Definitions.Prefix}{name}", ManifestResourceAttributes.Private, File.ReadAllBytes(dll));
				resources.Add(resource);
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

		static void WriteAssembly(AssemblyDefinition assembly, string output)
		{
			var dir = Path.GetDirectoryName(output);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			assembly.Write(output);
		}
	}
}
