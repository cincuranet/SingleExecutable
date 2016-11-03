using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SingleExecutable
{
	static class CctorProcessor
	{
		const string CctorName = ".cctor";

		public static void ProcessCctor(TypeDefinition type, string prefix)
		{
			var cctor = type.Methods.FirstOrDefault(m => m.IsStatic && m.IsConstructor);
			if (cctor == null)
			{
				cctor = new MethodDefinition(
					CctorName,
					MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
					type.Module.Import(typeof(void)));
				cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
				type.Methods.Add(cctor);
			}
			type.Attributes = type.Attributes & ~TypeAttributes.BeforeFieldInit;
			cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, new MethodReference($"{prefix}.cctor", cctor.ReturnType, type)));
		}
	}
}
