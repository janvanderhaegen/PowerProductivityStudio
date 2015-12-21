using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.Utilities;

namespace PowerProductivityStudio.MSBuild
{
    public sealed class NotifyUserCodeWeaverTask : Task
    {

        public string References { get; set; }
        public string TargetPath { get; set; }
        public string KeyFilePath { get; set; }


        static AssemblyCatalog assemblyCatalog;

        static NotifyUserCodeWeaverTask(){
            assemblyCatalog = new AssemblyCatalog(typeof(NotifyUserCodeWeaverTask).Assembly);
        }


        public override bool Execute()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            var me = this;
            //if (!
            //    (me.BuildEngine.ProjectFileOfTaskNode.EndsWith("erver.csproj")
            //     ||
            //     me.BuildEngine.ProjectFileOfTaskNode.EndsWith("erver.vbproj")
            //    ))
                //return true;

            using (var container = new CompositionContainer(assemblyCatalog)) {
                try
                {
                container.ComposeExportedValue(this);
                container.ComposeExportedValue(BuildEngine);
                container.GetExportedValue<TargetPathFinder>().Execute();
                container.GetExportedValue<AssemblyResolver>().Execute();
                container.GetExportedValue<ModuleReader>().Execute();
                //TODO: check if file changed.
                if (!container.GetExportedValue<FileChangedChecker>().ShouldStart())
                    return true;

                container.GetExportedValue<MsCoreReferenceFinder>().Execute();
                
                    container.GetExportedValue<InterceptorFinder>().Execute();
                
                //Saving back to disk
                container.GetExportedValue<ProjectKeyReader>().Execute();
                container.GetExportedValue<ModuleWriter>().Execute();
                }
                catch (InvalidProgramException x)
                {//Dump stack trace, we know where the exception happened...
                    throw new InvalidProgramException(x.Message);
                }

            }




            return true;
        }

        System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if(args.Name.Contains("PowerProductivityStudio.Client")){
                var url = new Uri(
                         new Uri(this.BuildEngine.ProjectFileOfTaskNode),
                         @"..\_Pvt_Extensions\PowerProductivityStudio\Client\Reference\PowerProductivityStudio.Client.dll").LocalPath;

                return Assembly.LoadFile(url);    
            } 
            return null;
        }

    }
}
