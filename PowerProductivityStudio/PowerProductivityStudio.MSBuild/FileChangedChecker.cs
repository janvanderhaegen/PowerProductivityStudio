
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Serialization;
using Mono.Cecil;
using PowerProductivityStudio.MSBuild;


    [Export, PartCreationPolicy(CreationPolicy.Shared)]
    public class FileChangedChecker
    {
        ModuleReader moduleReader;
        string namespaceKey;

        [ImportingConstructor]
        public FileChangedChecker(NotifyUserCodeWeaverTask config, ModuleReader moduleReader)
        {
            namespaceKey = config.GetType().Namespace.Replace(".", string.Empty);
            this.moduleReader = moduleReader;
        }

        public bool ShouldStart()
        {
            if (moduleReader.Module.Types.Any(x => x.Name == namespaceKey))
            {
                return false;
            }
            moduleReader.Module.Types.Add(new TypeDefinition(null, namespaceKey, Mono.Cecil.TypeAttributes.NotPublic | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Interface));
            return true;
        }

        
    }

