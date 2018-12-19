using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;
using Mono.Cecil;

namespace SingleExecutable
{
	static class Program
	{
		static readonly System.Reflection.Assembly ExecutingAssembly = System.Reflection.Assembly.GetExecutingAssembly();

		static void Main(string[] args)
		{
			var app = new CommandLineApplication(false)
			{
				FullName = nameof(SingleExecutable),
				Name = Path.GetFileName(ExecutingAssembly.Location)
			};
			var argExecutable = app.Option("-e", "Executable to inject into.", CommandOptionType.SingleValue);
			var argOutput = app.Option("-o", "Output file.", CommandOptionType.SingleValue);
			var argAdd = app.Option("-a", "Another files to add.", CommandOptionType.MultipleValue);
			var argPreExtract = app.Option("-p", "Pre-extract files.", CommandOptionType.MultipleValue);
			var argCompressed = app.Option("-c", "Compress binaries.", CommandOptionType.NoValue);
			app.Execute(args);
			if (!argExecutable.HasValue() || !argOutput.HasValue())
			{
				app.ShowHelp();
				ErrorExit();
			}

			var executable = Path.GetFullPath(argExecutable.Value());
			var output = Path.GetFullPath(argOutput.Value());
			if (!File.Exists(executable))
			{
				ErrorExit($"File '{executable}' does not exists.");
			}

			try
			{
				Console.WriteLine("Loading...");
				var assembly = AssemblyDefinition.ReadAssembly(executable);
				Console.WriteLine("Embedding:");
				var embedDllsResult = EmbedDlls(assembly, Path.GetDirectoryName(executable), argAdd.Values, argCompressed.HasValue());
				Console.WriteLine("Writing pre-extraction...");
				WritePreExtract(assembly, embedDllsResult, argPreExtract.Values);
				Console.WriteLine("Injecting:");
				InjectLoading(assembly);
				Console.WriteLine("Writing...");
				WriteAssembly(assembly, output);
				Console.WriteLine("Done...");
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

		static IEnumerable<Tuple<string, string>> EmbedDlls(AssemblyDefinition assembly, string directory, IEnumerable<string> anotherDlls, bool compress)
		{
			var result = new List<Tuple<string, string>>();
			var standard = Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);
			var another = anotherDlls.Select(Path.GetFullPath).Where(File.Exists);
			foreach (var dll in standard.Concat(another))
			{
				var resourceName = EmbedDll(assembly, dll, compress);
				result.Add(Tuple.Create(dll, resourceName));
			}
			return result;
		}

		static void WritePreExtract(AssemblyDefinition assembly, IEnumerable<Tuple<string, string>> embeddedDlls, IEnumerable<string> preExtractDlls)
		{
			var preExtract = new HashSet<string>(preExtractDlls.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
			var dlls = embeddedDlls
				.Where(x => preExtract.Contains(x.Item1) || !Helpers.IsDotNetDll(x.Item1))
				.Select(x => x.Item2);
			var data = Encoding.UTF8.GetBytes(string.Join(Definitions.PreExtractSeparator.ToString(), dlls));
			var resource = new EmbeddedResource(Definitions.PreExtractResourceName, ManifestResourceAttributes.Public, data);
			assembly.MainModule.Resources.Add(resource);
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

		static byte[] GetDllData(string dll, bool compress)
		{
			var data = File.ReadAllBytes(dll);
			if (!compress)
			{
				return data;
			}

			using (var output = new MemoryStream())
			{
				using (var dataIn = new MemoryStream(data))
				{
					using (var compressor = new GZipStream(output, CompressionMode.Compress))
					{
						dataIn.CopyTo(compressor);
					}
				}

				return output.ToArray();
			}
		}

		static string EmbedDll(AssemblyDefinition assembly, string dll, bool compress)
		{
			Console.WriteLine($"  {dll}");
			var name = Path.GetFileName(dll);
			var resourceName = $"{(compress ? Definitions.PrefixCompressed : Definitions.PrefixNotCompressed)}{name}";

			var resource = new EmbeddedResource(resourceName, ManifestResourceAttributes.Private, GetDllData(dll, compress));
			assembly.MainModule.Resources.Add(resource);
			return resourceName;
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
