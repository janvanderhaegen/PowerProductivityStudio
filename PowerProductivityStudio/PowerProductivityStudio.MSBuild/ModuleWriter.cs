using System.ComponentModel.Composition;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using PowerProductivityStudio.MSBuild;

[Export, PartCreationPolicy(CreationPolicy.Shared)]
public class ModuleWriter
{
    ModuleReader moduleReader;
    ProjectKeyReader projectKeyReader;
    NotifyUserCodeWeaverTask config;

    [ImportingConstructor]
    public ModuleWriter(ModuleReader moduleReader, ProjectKeyReader projectKeyReader, NotifyUserCodeWeaverTask config)
    {
        this.moduleReader = moduleReader;
        this.projectKeyReader = projectKeyReader;
        this.config = config;
    }

    static ISymbolWriterProvider GetSymbolWriterProvider(string targetPath)
    {
        var pdbPath = Path.ChangeExtension(targetPath, "pdb");
        if (File.Exists(pdbPath))
        {
            return new PdbWriterProvider();
        }
        var mdbPath = Path.ChangeExtension(targetPath, "mdb");

        if (File.Exists(mdbPath))
        {
            return new MdbWriterProvider();
        }
        return null;
    }

    public void Execute()
    {
        Execute(config.TargetPath);
    }

    public void Execute(string targetPath)
    {
        var parameters = new WriterParameters
        {
            StrongNameKeyPair = projectKeyReader.StrongNameKeyPair,
            WriteSymbols = true,
            SymbolWriterProvider = GetSymbolWriterProvider(config.TargetPath)
        };
        moduleReader.Module.Write(targetPath, parameters);
    }
}