using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SingleExecutable
{
	static class MethodCopier
	{
		public static void CopyMethods(TypeDefinition type, TypeDefinition destination, string prefix)
		{
			foreach (var m in type.Methods)
			{
				CopyMethod(m, destination, prefix);
			}
		}

		static MethodDefinition CopyMethod(MethodDefinition source, TypeDefinition destination, string prefix)
		{
			var targetModule = destination.Module;
			var newMethod = new MethodDefinition($"{prefix}{source.Name}", FixAttributes(source.Attributes), targetModule.Import(source.ReturnType));

			foreach (var p in source.Parameters)
			{
				newMethod.Parameters.Add(DuplicateParameter(p, targetModule));
			}
			newMethod.Body.InitLocals = source.Body.InitLocals;
			foreach (var v in source.Body.Variables)
			{
				newMethod.Body.Variables.Add(DuplicateVariable(v, targetModule));
			}
			foreach (var i in source.Body.Instructions)
			{
				var operand = i.Operand;

				if (operand is MethodReference)
				{
					var methodReference = operand as MethodReference;
					if (methodReference.DeclaringType == source.DeclaringType)
					{
						methodReference = FixLocalMethodReference(methodReference, destination, prefix, targetModule);
					}
					newMethod.Body.Instructions.Add(Instruction.Create(i.OpCode, targetModule.Import(methodReference)));
					continue;
				}
				if (operand is FieldReference)
				{
					newMethod.Body.Instructions.Add(Instruction.Create(i.OpCode, targetModule.Import(operand as FieldReference)));
					continue;
				}
				if (operand is TypeReference)
				{
					newMethod.Body.Instructions.Add(Instruction.Create(i.OpCode, targetModule.Import(operand as TypeReference)));
					continue;
				}

				newMethod.Body.Instructions.Add(i);
			}
			foreach (var eh in source.Body.ExceptionHandlers)
			{
				newMethod.Body.ExceptionHandlers.Add(DuplicateExceptionHandler(eh, source, newMethod, targetModule));
			}

			destination.Methods.Add(newMethod);
			return newMethod;
		}

		static MethodAttributes FixAttributes(MethodAttributes attributes)
		{
			return attributes & ~MethodAttributes.RTSpecialName & ~MethodAttributes.SpecialName;
		}

		static MethodReference FixLocalMethodReference(MethodReference m, TypeDefinition destination, string prefix, ModuleDefinition targetModule)
		{
			var method = new MethodReference($"{prefix}{m.Name}", targetModule.Import(m.ReturnType), destination);
			foreach (var p in m.Parameters)
			{
				method.Parameters.Add(DuplicateParameter(p, targetModule));
			}
			return method;
		}

		static VariableDefinition DuplicateVariable(VariableDefinition v, ModuleDefinition targetModule)
		{
			return new VariableDefinition(v.Name, targetModule.Import(v.VariableType));
		}

		static ParameterDefinition DuplicateParameter(ParameterDefinition p, ModuleDefinition targetModule)
		{
			return new ParameterDefinition(p.Name, p.Attributes, targetModule.Import(p.ParameterType));
		}

		static ExceptionHandler DuplicateExceptionHandler(ExceptionHandler eh, MethodDefinition source, MethodDefinition newMethod, ModuleDefinition targetModule)
		{
			return new ExceptionHandler(eh.HandlerType)
			{
				CatchType = eh.CatchType != null ? targetModule.Import(eh.CatchType) : null,
				TryStart = newMethod.Body.Instructions[source.Body.Instructions.IndexOf(eh.TryStart)],
				TryEnd = newMethod.Body.Instructions[source.Body.Instructions.IndexOf(eh.TryEnd)],
				HandlerType = eh.HandlerType,
				HandlerStart = newMethod.Body.Instructions[source.Body.Instructions.IndexOf(eh.HandlerStart)],
				HandlerEnd = newMethod.Body.Instructions[source.Body.Instructions.IndexOf(eh.HandlerEnd)],
				FilterStart = eh.FilterStart,
			};
		}
	}
}
