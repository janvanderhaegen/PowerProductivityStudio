using System.ComponentModel.Composition;
using System.IO;
using Mono.Cecil;
using PowerProductivityStudio.MSBuild;

[Export, PartCreationPolicy(CreationPolicy.Shared)]
public class ModuleReader
{
    NotifyUserCodeWeaverTask config;
    IAssemblyResolver assemblyResolver;
    public ModuleDefinition Module { get; set; }

    [ImportingConstructor]
    public ModuleReader(
            NotifyUserCodeWeaverTask config, IAssemblyResolver assemblyResolver)
    {
        this.config = config;
        this.assemblyResolver = assemblyResolver;
    }

    FileStream GetSymbolReaderProvider(string targetPath)
    {
        //if (targetPathFinder.TargetPathDerivedFromBuildEngine)
        //{
        //    var debugSymbolsIntermediatePath = buildEnginePropertyExtractor.GetEnvironmentVariable("_DebugSymbolsIntermediatePath", false).FirstOrDefault();
        //    if (debugSymbolsIntermediatePath != null && File.Exists(debugSymbolsIntermediatePath))
        //    {
        //        logger.LogMessage(string.Format("\tFound debug symbols (using build engine) at '{0}'", debugSymbolsIntermediatePath));
        //        return File.OpenRead(debugSymbolsIntermediatePath);
        //    }
        //}


        var pdbPath = Path.ChangeExtension(targetPath, "pdb");
        if (File.Exists(pdbPath))
        {
            return File.OpenRead(pdbPath);
        }
        var mdbPath = Path.ChangeExtension(targetPath, "mdb");

        if (File.Exists(mdbPath))
        {
            return File.OpenRead(mdbPath);
        }
        return null;
    }

    public void Execute()
    {
        using (var symbolStream = GetSymbolReaderProvider(config.TargetPath))
        {
            var readSymbols = symbolStream != null;
            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = assemblyResolver,
                ReadSymbols = readSymbols,
                SymbolStream = symbolStream,
            };
            Module = ModuleDefinition.ReadModule(config.TargetPath, readerParameters);
        }
    }
}