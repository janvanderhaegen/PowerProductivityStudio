using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PowerProductivityStudio.MSBuild;

[Export, PartCreationPolicy(CreationPolicy.Shared)]
public class TargetPathFinder
{
    NotifyUserCodeWeaverTask config;
    BuildEnginePropertyExtractor buildEnginePropertyExtractor;

    //For Testing
    public TargetPathFinder()
    {
    }

    [ImportingConstructor]
    public TargetPathFinder(NotifyUserCodeWeaverTask config, BuildEnginePropertyExtractor buildEnginePropertyExtractor)
    {
        this.config = config;
        this.buildEnginePropertyExtractor = buildEnginePropertyExtractor;
    }

    public string GetBuildEngineKey()
    {
        var projectFilePath = buildEnginePropertyExtractor.GetProjectPath();
        var xDocument = XDocument.Load(projectFilePath);
        var weavingTaskName = config.GetType().Assembly.GetName().Name + "." + config.GetType().Name;
        var weavingTaskNode = xDocument.BuildDescendants(weavingTaskName).First();
        var xAttribute = weavingTaskNode.Parent.Attribute("Name");
        if (xAttribute == null)
        {
            throw new Exception("Target node contains no 'Name' attribute.");
        }
        ;
        var targetNodeName = xAttribute.Value.ToUpperInvariant();
        switch (targetNodeName)
        {
            case ("AFTERCOMPILE"):
                {
                    return "IntermediateAssembly";
                }
            case ("AFTERBUILD"):
                {
                    return "TargetPath";
                }
        }
        throw new Exception(
            string.Format(
                @"Failed to derive TargetPath from target node. WeavingTask is located in '{0}'. 
Target path can only be derived when WeavingTask is located in 'AfterCompile' or 'AfterBuild'.
Please define 'TargetPath' as follows: 
<WeavingTask ... TargetPath=""PathToYourAssembly"" />", targetNodeName));
    }


    public void Execute()
    {
        if (string.IsNullOrWhiteSpace(config.TargetPath))
        {
            var buildEngineKey = GetBuildEngineKey();
            try
            {
                config.TargetPath = buildEnginePropertyExtractor.GetEnvironmentVariable(buildEngineKey, true).First();
            }
            catch (Exception exception)
            {
                throw new Exception(string.Format(
                    @"Failed to extract target assembly path from the BuildEngine. 
Please raise a bug with the below exception text.
The temporary work-around is to change the weaving task as follows 
<WeavingTask ... TargetPath=""PathToYourAssembly"" />
Exception details: {0}", exception));
            }
        }
        if (!File.Exists(config.TargetPath))
        {
            throw new Exception(string.Format("TargetPath \"{0}\" does not exists. If you have not done a build you can ignore this error.", config.TargetPath));
        }
    }
}