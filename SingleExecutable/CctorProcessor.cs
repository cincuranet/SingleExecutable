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
			type.Attributes = type.Attributes & ~TypeAttributes.BeforeFieldInit;

			var oldCctor = type.Methods.FirstOrDefault(m => m.IsStatic && m.IsConstructor);
			if (oldCctor != null)
			{
				oldCctor.Name = $"{prefix}OldCctor";
				oldCctor.Attributes = oldCctor.Attributes & ~MethodAttributes.SpecialName;
				oldCctor.Attributes = oldCctor.Attributes & ~MethodAttributes.RTSpecialName;
			}

			var newCctor = new MethodDefinition(
					CctorName,
					MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
					type.Module.Import(typeof(void)));
			type.Methods.Add(newCctor);
			newCctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, new MethodReference($"{prefix}.cctor", newCctor.ReturnType, type)));
			if (oldCctor != null)
			{
				newCctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, new MethodReference(oldCctor.Name, newCctor.ReturnType, type)));
			}
			newCctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
		}
	}
}
