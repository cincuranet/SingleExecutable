using System;
using Mono.Cecil;

namespace SingleExecutable
{
    static class Helpers
    {
        public static bool IsNativeDll(string path)
        {
            try
            {
                AssemblyDefinition.ReadAssembly(path);
                return true;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }
    }
}
