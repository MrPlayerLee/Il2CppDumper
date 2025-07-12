﻿using System;
using System.IO;

namespace Il2CppDumper
{
    public static class DummyAssemblyExporter
    {
        public static void Export(Il2CppExecutor il2CppExecutor, string outputDir, bool addToken)
        {
            Directory.SetCurrentDirectory(outputDir);
            if (Directory.Exists("DummyDll"))
                Directory.Delete("DummyDll", true);
            Directory.CreateDirectory("DummyDll");
            Directory.SetCurrentDirectory("DummyDll");
            var dummy = new DummyAssemblyGenerator(il2CppExecutor, addToken);
            foreach (var assembly in dummy.Assemblies)
            {
                if (assembly.MainModule.Name.Equals("Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = new MemoryStream();
                    assembly.Write(stream);
                    File.WriteAllBytes(assembly.MainModule.Name, stream.ToArray());
                }
            }
        }
    }
}
