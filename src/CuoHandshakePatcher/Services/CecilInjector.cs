using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CuoHandshakePatcher.Services;

public static class CecilInjector
{
    public const string MarkerTypeName = "CuoHandshakeFixMarker";
    private const string MarkerNamespace = "Assistant";

    public static bool IsPatched(string exePath)
    {
        using AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(exePath, new ReaderParameters
        {
            ReadWrite = false,
            InMemory = true,
            ReadingMode = ReadingMode.Deferred,
            AssemblyResolver = CreateResolver(Path.GetDirectoryName(exePath)!),
        });
        return asm.MainModule.Types.Any(t => t.Namespace == MarkerNamespace && t.Name == MarkerTypeName);
    }

    public static void InjectBootstrap(string exePath, Action<string>? log = null)
    {
        string tempPath = exePath + ".cuo-tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        string installDir = Path.GetDirectoryName(exePath)!;
        using var resolver = CreateResolver(installDir);

        var readerParams = new ReaderParameters
        {
            ReadWrite = false,
            InMemory = true,
            AssemblyResolver = resolver,
            ReadSymbols = false,
        };

        using (AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(exePath, readerParams))
        {
            ModuleDefinition module = asm.MainModule;

            if (module.Types.Any(t => t.Namespace == MarkerNamespace && t.Name == MarkerTypeName))
            {
                log?.Invoke("Already patched (marker present). Skipping IL injection.");
                return;
            }

            TypeDefinition handlers = module.Types.FirstOrDefault(t =>
                    t.Namespace == "Assistant" && (t.Name == "PacketHandlers" || t.Name == "Handlers"))
                ?? throw new InvalidOperationException("Assistant.PacketHandlers / Handlers not found — RE layout may have changed.");

            MethodDefinition initialize = handlers.Methods.FirstOrDefault(m => m.Name == "Initialize" && m.IsStatic && !m.HasParameters)
                ?? throw new InvalidOperationException($"{handlers.Name}.Initialize() not found — RE layout may have changed.");

            if (initialize.Body == null)
                throw new InvalidOperationException($"{handlers.Name}.Initialize has no method body.");

            InjectCall(module, initialize);
            AddMarkerType(module);

            asm.Write(tempPath, new WriterParameters
            {
                WriteSymbols = false,
            });
            log?.Invoke($"IL bootstrap injected into {handlers.FullName}.Initialize.");
        }

        File.Copy(tempPath, exePath, overwrite: true);
        File.Delete(tempPath);
    }

    private static FrameworkAssemblyResolver CreateResolver(string installDir)
    {
        var resolver = new FrameworkAssemblyResolver();
        resolver.AddSearchDirectory(installDir);

        foreach (string dir in GetNetFrameworkSearchDirs())
        {
            if (Directory.Exists(dir))
                resolver.AddSearchDirectory(dir);
        }

        return resolver;
    }

    private static IEnumerable<string> GetNetFrameworkSearchDirs()
    {
        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Path.Combine(windir, "Microsoft.NET", "Framework64", "v4.0.30319");
        yield return Path.Combine(windir, "Microsoft.NET", "Framework", "v4.0.30319");

        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        foreach (string root in new[] { pf86, pf }.Where(p => !string.IsNullOrEmpty(p)).Distinct())
        {
            string refRoot = Path.Combine(root, "Reference Assemblies", "Microsoft", "Framework", ".NETFramework");
            if (!Directory.Exists(refRoot))
                continue;

            // Prefer newer frameworks first.
            foreach (string dir in Directory.GetDirectories(refRoot, "v4.*").OrderByDescending(d => d))
                yield return dir;
        }

        // GAC_MSIL (where System.Windows.Forms 4.0 often lives)
        string gac = Path.Combine(windir, "Microsoft.NET", "assembly", "GAC_MSIL");
        if (Directory.Exists(gac))
        {
            foreach (string asmDir in new[] { "System.Windows.Forms", "System.Drawing", "System", "System.Core" })
            {
                string path = Path.Combine(gac, asmDir);
                if (!Directory.Exists(path))
                    continue;
                foreach (string verDir in Directory.GetDirectories(path))
                    yield return verDir;
            }
        }
    }

    private static void AddMarkerType(ModuleDefinition module)
    {
        TypeDefinition marker = new(
            MarkerNamespace,
            MarkerTypeName,
            TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit,
            module.TypeSystem.Object);
        module.Types.Add(marker);
    }

    private static void InjectCall(ModuleDefinition module, MethodDefinition initialize)
    {
        ILProcessor il = initialize.Body.GetILProcessor();
        Instruction ret = initialize.Body.Instructions.LastOrDefault(i => i.OpCode == OpCodes.Ret)
            ?? throw new InvalidOperationException("Handlers.Initialize has no ret instruction.");

        IMetadataScope corlib = module.TypeSystem.CoreLibrary;
        TypeReference stringType = module.TypeSystem.String;
        TypeReference objectType = module.TypeSystem.Object;
        TypeReference typeType = new TypeReference("System", "Type", module, corlib);
        TypeReference assemblyType = new TypeReference("System.Reflection", "Assembly", module, corlib);
        TypeReference methodBaseType = new TypeReference("System.Reflection", "MethodBase", module, corlib);
        TypeReference methodInfoType = new TypeReference("System.Reflection", "MethodInfo", module, corlib);
        TypeReference pathType = new TypeReference("System.IO", "Path", module, corlib);
        TypeReference objectArray = new ArrayType(objectType);

        MethodReference getExecutingAssembly = StaticMethod(module, assemblyType, "GetExecutingAssembly", assemblyType);
        MethodReference getLocation = InstanceMethod(module, assemblyType, "get_Location", stringType);
        MethodReference getDirectoryName = StaticMethod(module, pathType, "GetDirectoryName", stringType, stringType);
        MethodReference pathCombine = StaticMethod(module, pathType, "Combine", stringType, stringType, stringType);
        MethodReference loadFrom = StaticMethod(module, assemblyType, "LoadFrom", assemblyType, stringType);
        MethodReference assemblyGetType = InstanceMethod(module, assemblyType, "GetType", typeType, stringType);
        MethodReference typeGetMethod = InstanceMethod(module, typeType, "GetMethod", methodInfoType, stringType);
        MethodReference invoke = InstanceMethod(module, methodBaseType, "Invoke", objectType, objectType, objectArray);

        Instruction[] injected =
        [
            il.Create(OpCodes.Call, getExecutingAssembly),
            il.Create(OpCodes.Callvirt, getLocation),
            il.Create(OpCodes.Call, getDirectoryName),
            il.Create(OpCodes.Ldstr, "CuoHandshakeFix.dll"),
            il.Create(OpCodes.Call, pathCombine),
            il.Create(OpCodes.Call, loadFrom),
            il.Create(OpCodes.Ldstr, "CuoHandshakeFix.Bootstrap"),
            il.Create(OpCodes.Callvirt, assemblyGetType),
            il.Create(OpCodes.Ldstr, "Apply"),
            il.Create(OpCodes.Callvirt, typeGetMethod),
            il.Create(OpCodes.Ldnull),
            il.Create(OpCodes.Ldnull),
            il.Create(OpCodes.Callvirt, invoke),
            il.Create(OpCodes.Pop),
        ];

        foreach (Instruction instruction in injected)
            il.InsertBefore(ret, instruction);
    }

    private static MethodReference StaticMethod(ModuleDefinition module, TypeReference declaring, string name, TypeReference returnType, params TypeReference[] parameters)
    {
        MethodReference mr = new(name, returnType, declaring) { HasThis = false };
        foreach (TypeReference p in parameters)
            mr.Parameters.Add(new ParameterDefinition(p));
        return module.ImportReference(mr);
    }

    private static MethodReference InstanceMethod(ModuleDefinition module, TypeReference declaring, string name, TypeReference returnType, params TypeReference[] parameters)
    {
        MethodReference mr = new(name, returnType, declaring) { HasThis = true };
        foreach (TypeReference p in parameters)
            mr.Parameters.Add(new ParameterDefinition(p));
        return module.ImportReference(mr);
    }

    /// <summary>
    /// Mono.Cecil running on .NET 8 cannot resolve .NET Framework assemblies unless we
    /// explicitly search Framework / reference-assembly / GAC folders.
    /// </summary>
    private sealed class FrameworkAssemblyResolver : DefaultAssemblyResolver
    {
        public override AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            try
            {
                return base.Resolve(name);
            }
            catch (AssemblyResolutionException)
            {
                // Last resort: look for Name.dll under every search directory (GAC layout uses nested folders).
                foreach (string dir in GetSearchDirectories())
                {
                    string direct = Path.Combine(dir, name.Name + ".dll");
                    if (File.Exists(direct))
                        return AssemblyDefinition.ReadAssembly(direct, new ReaderParameters { AssemblyResolver = this });

                    // GAC_MSIL\System.Windows.Forms\v4.0_4.0.0.0__b77a5c561934e089\System.Windows.Forms.dll
                    if (Directory.Exists(dir))
                    {
                        try
                        {
                            foreach (string candidate in Directory.EnumerateFiles(dir, name.Name + ".dll", SearchOption.AllDirectories).Take(3))
                                return AssemblyDefinition.ReadAssembly(candidate, new ReaderParameters { AssemblyResolver = this });
                        }
                        catch
                        {
                            // ignore enumeration issues
                        }
                    }
                }

                throw;
            }
        }
    }
}
