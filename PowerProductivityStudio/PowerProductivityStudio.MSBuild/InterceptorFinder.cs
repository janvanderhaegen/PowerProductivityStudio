using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PowerProductivityStudio.MSBuild;

//This should work in release mode...
[Export, PartCreationPolicy(CreationPolicy.Shared)]
public class InterceptorFinder
{
    ModuleReader moduleReader;
    public bool Found;
    public MethodDefinition InterceptMethod;
    public bool IsBeforeAfter;
    private NotifyUserCodeWeaverTask config;
    private BuildEnginePropertyExtractor buildEnginePropertyExtractor;
    [ImportingConstructor]
    public InterceptorFinder(ModuleReader moduleReader, NotifyUserCodeWeaverTask config, BuildEnginePropertyExtractor buildEnginePropertyExtractor)
    {
        this.moduleReader = moduleReader;
        this.config = config;
        this.buildEnginePropertyExtractor = buildEnginePropertyExtractor;
    }


    void SearchForMethod(TypeDefinition typeDefinition)
    {
        var methodDefinition = typeDefinition.Methods.FirstOrDefault(x => x.Name == "Intercept");
        if (methodDefinition == null)
        {
            throw new Exception(string.Format("Found Type '{0}' but could not find a method named 'Intercept'.", typeDefinition.FullName));
        }
        if (!methodDefinition.IsStatic)
        {
            throw new Exception(string.Format("Found Type '{0}.Intercept' but it is not static.", typeDefinition.FullName));
        }
        if (!methodDefinition.IsPublic)
        {
            throw new Exception(string.Format("Found Type '{0}.Intercept' but it is not public.", typeDefinition.FullName));
        }

        if (IsSingleStringMethod(methodDefinition))
        {
            Found = true;
            InterceptMethod = methodDefinition;
            return;
        }
        if (IsBeforeAfterMethod(methodDefinition))
        {
            Found = true;
            InterceptMethod = methodDefinition;
            IsBeforeAfter = true;
            return;
        }
        var message = string.Format(
            @"Found '{0}.Intercept' But the signature is not correct. It needs to be either.
Intercept(object target, Action firePropertyChanged, string propertyName)
or
Intercept(object target, Action firePropertyChanged, string propertyName, object before, object after)", typeDefinition.FullName);
        throw new Exception(message);
    }


    public bool IsSingleStringMethod(MethodDefinition method)
    {
        var parameters = method.Parameters;
        return parameters.Count == 3
               && parameters[0].ParameterType.FullName == "System.Object"
               && parameters[1].ParameterType.FullName == "System.Action"
               && parameters[2].ParameterType.FullName == "System.String";
    }

    public bool IsBeforeAfterMethod(MethodDefinition method)
    {
        var parameters = method.Parameters;
        return parameters.Count == 5
               && parameters[0].ParameterType.FullName == "System.Object"
               && parameters[1].ParameterType.FullName == "System.Action"
               && parameters[2].ParameterType.FullName == "System.String"
               && parameters[3].ParameterType.FullName == "System.Object"
               && parameters[4].ParameterType.FullName == "System.Object";
    }

    private bool isValidPermissionRequestEvent(string methodName)
    {
        bool isValid = methodName.StartsWith("__");
        isValid = isValid &&
            (
                methodName.EndsWith("_CanDelete") ||
                methodName.EndsWith("_CanInsert") ||
                methodName.EndsWith("_CanRead") ||
                methodName.EndsWith("_CanUpdate") 
            );
        return isValid;
    }

    private bool isValidFilterEvent(string methodName)
    {
        bool isValid = methodName.StartsWith("__");
        isValid = isValid && methodName.EndsWith("_Filter");
        return isValid;
    }


    private bool isValidScreenSavingEvent(string methodName)
    {
        bool isValid = methodName.StartsWith("__");
        isValid = isValid && methodName.EndsWith("_InvokeSavingEvent");
        return isValid;
    }

    private bool isValidValidationEventOccured(string methodName) {
        bool isValid = methodName.StartsWith("__");
        isValid = isValid &&
            (
                methodName.EndsWith("_Validate")  
            ); 
        return isValid;
    }

    private bool isValidServerEventOccured(string methodName)
    {
        bool isValid = methodName.StartsWith("__");
        isValid = isValid &&
            (
                methodName.EndsWith("_Inserting") ||
                methodName.EndsWith("_Inserted") ||
                methodName.EndsWith("_Updating") ||
                methodName.EndsWith("_Updated") ||
                methodName.EndsWith("_Deleting") ||
                methodName.EndsWith("_Deleted")
            );
        return isValid;
    }

    private bool isValidEntityCreatedEventOccured(string methodName) {
        bool isValid = methodName.StartsWith("__") && methodName.EndsWith("_Created");
        return isValid;
    }

    private bool isValidApplicationUserLoggedInEventOccured(string methodName) {
        return methodName.Equals("__Application_LoggedIn");
    }

    private bool isValidScreenCanRun(string methodName)
    {
        bool isValid = methodName.StartsWith("_") && methodName.EndsWith("_CanInvoke");
        return isValid;
    }

    private bool isValidApplicationInitializedOccured(string methodName) {
        return methodName.Equals("__Application_Initialized");
    }

    public void Execute()
    { 
        addServerStuff();
        addClientStuff(); 
    }

    private void addCommonStuff(Type commonStuffType) {
        var entityClasses = moduleReader.Module.Types.Where(t => t.BaseType != null && t.BaseType.Name.StartsWith("EntityObject"));
        foreach(var entityClass in entityClasses){
            var detailsClass = entityClass.NestedTypes.Where(t => t.Name.Equals("DetailsClass")).FirstOrDefault();
            if(detailsClass != null){


                //__Created
                var createdMethod = detailsClass.Methods.Where(m => isValidEntityCreatedEventOccured(m.Name)).FirstOrDefault();

                MethodReference writeline = moduleReader.Module.Import(commonStuffType.GetMethod("EntityCreatedEventOccured"));

                Instruction pushEntityOnStack = createdMethod.Body.GetILProcessor().Create(OpCodes.Ldarg_0);
                Instruction call_methodWriteline = detailsClass.Methods.FirstOrDefault().Body.GetILProcessor()
                    .Create(OpCodes.Call, writeline);

                createdMethod.Body.GetILProcessor().InsertBefore(createdMethod.Body.Instructions[0], pushEntityOnStack);
                createdMethod.Body.GetILProcessor().InsertAfter(pushEntityOnStack, call_methodWriteline);
            }
            
        }
    }

    private void addClientStuff() { 
        var applicationClass = moduleReader.Module.Types.Where( t => t.Name.Equals("Application")).FirstOrDefault();
        if(applicationClass == null)
            return; //We're not on the client, LOL

        var detailsClass = applicationClass.NestedTypes.Where(t => t.Name.Equals("DetailsClass")).FirstOrDefault();

        if(
            detailsClass.Methods.Where(m => isValidApplicationUserLoggedInEventOccured(m.Name)).FirstOrDefault()
            == null)
            return; //We're not on the client, LOL

        addUserLoggedInEventOccured(detailsClass);
        addApplicationInitializedEventOccured(detailsClass);
        addScreenCanRunOccured(detailsClass);


        addScreenStuff();
        addCommonStuff(typeof(PowerProductivityStudio.Extensibility.ClientPipeLineEventNotifier));

    }

  
    private void addUserLoggedInEventOccured(TypeDefinition detailsClass)
    {
        var methods = detailsClass.Methods.Where(m => isValidApplicationUserLoggedInEventOccured(m.Name));

        MethodReference writeline = moduleReader.Module.Import(typeof(
            PowerProductivityStudio.Extensibility.ClientPipeLineEventNotifier)
            .GetMethod("ApplicationUserLoggedInOccured"));

        Instruction call_methodWriteline = detailsClass.Methods.FirstOrDefault().Body.GetILProcessor()
            .Create(OpCodes.Call, writeline);

        if (methods != null)
        {
            foreach (var method in methods)
            {
                method.Body.GetILProcessor().InsertBefore(method.Body.Instructions[0], call_methodWriteline);
            }
        }
    }
    private void addApplicationInitializedEventOccured(TypeDefinition detailsClass) {
        var methods = detailsClass.Methods.Where(m => isValidApplicationInitializedOccured(m.Name));

        MethodReference writeline = moduleReader.Module.Import(typeof(
            PowerProductivityStudio.Extensibility.ClientPipeLineEventNotifier)
            .GetMethod("ApplicationInitializedOccured"));

        Instruction call_methodWriteline = detailsClass.Methods.FirstOrDefault().Body.GetILProcessor()
            .Create(OpCodes.Call, writeline);

        if (methods != null)
        {
            foreach (var method in methods)
            {
                method.Body.GetILProcessor().InsertBefore(method.Body.Instructions[0], call_methodWriteline);
            }
        }
    }

    private void addScreenCanRunOccured(TypeDefinition detailsClass)
    {
        var methodSetPropertiesClass = detailsClass.NestedTypes.Where(f => f.Name.Equals("MethodSetProperties")).FirstOrDefault();
        if (methodSetPropertiesClass == null)
            return;



        var methods = methodSetPropertiesClass.Methods.Where(m => isValidScreenCanRun(m.Name));
        MethodReference writeline = moduleReader.Module.Import(typeof(
           PowerProductivityStudio.Extensibility.ClientPipeLineEventNotifier)
           .GetMethod("ScreenCanRunEventOccured"));
         

        if (methods != null)
        {
            foreach (var method in methods)
            {
                var methodName = method.Name.Replace("_", "").Replace("CanInvoke", "").Substring(4);


                var ilProcessor = method.Body.GetILProcessor();

                var body = method.Body;
                Instruction justFiltered;

                justFiltered = 
                    body.Variables.Count == 1 ?
                        body.Instructions.First(f => f.OpCode == OpCodes.Stloc_0) :
                        body.Instructions.First(f => f.OpCode == OpCodes.Stloc_1);

                //if (body.Instructions.Count > 8)
                //{ //User code in place
                //    justFiltered = body.Instructions[3];




                //}
                //else
                //{ //No user code in place
                //    justFiltered = body.Instructions[2];
                //}

                VariableDefinition filter = method.Body.Variables.FirstOrDefault();
                Instruction popFilterOnStack = ilProcessor.Create(OpCodes.Ldloca_S, filter);
                Instruction popScreenOnStack = method.Body.GetILProcessor().Create(OpCodes.Ldstr, methodName);
                Instruction makeCall = ilProcessor.Create(OpCodes.Call, writeline);

                ilProcessor.InsertAfter(justFiltered, popScreenOnStack);
                ilProcessor.InsertAfter(popScreenOnStack, popFilterOnStack);
                ilProcessor.InsertAfter(popFilterOnStack, makeCall);   
            }
        }
    }


    private void addServerStuff() 
    {
        var entities = moduleReader.Module.Types.Where(t => t.BaseType != null && t.BaseType.Name.StartsWith("EntityObject")).ToList();

        foreach (var applicationDataService in moduleReader.Module.Types.Where(t => t.Name.EndsWith("DataService")))
        {

            if (applicationDataService == null)
                continue; //We're not on the server, LOL

            var detailsClass = applicationDataService.NestedTypes.Where(t => t.Name.Equals("DetailsClass")).FirstOrDefault();

            if (detailsClass == null)
                continue; //Mysterious ApplicationDataDataDataDataService class... wooow...

            addFilterRequests(applicationDataService, detailsClass);
            addPermissionRequests(applicationDataService, detailsClass);

            addServerEventOccured(applicationDataService, detailsClass);
            addServerValidationEventOccured(applicationDataService, detailsClass);
            addCommonStuff(typeof(PowerProductivityStudio.Extensibility.PipeLineEventNotifier));


        }
       // verifyKey();
    }

    private void addPermissionRequests(TypeDefinition applicationDataService, TypeDefinition detailsClass)
    {
        var methods = detailsClass.Methods.Where(m => isValidPermissionRequestEvent(m.Name));

        MethodReference writeline = moduleReader.Module.Import(typeof(
            PowerProductivityStudio.Extensibility.PipeLineEventNotifier)
            .GetMethod("EntityPermissionRequestOccured"));



        if (methods != null)
        {
            foreach (var method in methods)
            {
                try
                {
                    //__Maintenance_vw_Instruments_CanRead
                    var methodName = method.Name.Substring(2);  //Maintenance_vw_Instruments_CanRead

                    var split =  methodName.Split('_');
                    var permissionName = split[split.Length -1].Substring(3); //Read

                    var entityName = methodName.Replace("_Can" + permissionName, "");//Maintenance_vw_Instruments

                    var ilProcessor = method.Body.GetILProcessor();

                    var body = method.Body;
                    var variables = body.Variables;

                    Instruction justFiltered;

                    justFiltered =
                        body.Variables.Count == 1 ?
                            body.Instructions.First(f => f.OpCode == OpCodes.Stloc_0) :
                            body.Instructions.First(f => f.OpCode == OpCodes.Stloc_1);


                    VariableDefinition filter = method.Body.Variables.FirstOrDefault();
                    Instruction popFilterOnStack = ilProcessor.Create(OpCodes.Ldloca_S, filter);
                    Instruction popEntityNameOnStack = method.Body.GetILProcessor().Create(OpCodes.Ldstr, entityName);
                    Instruction popPermissionNameOnStack = method.Body.GetILProcessor().Create(OpCodes.Ldstr, permissionName);

                    Instruction makeCall = ilProcessor.Create(OpCodes.Call, writeline);

                    ilProcessor.InsertAfter(justFiltered, popPermissionNameOnStack);
                    ilProcessor.InsertAfter(popPermissionNameOnStack, popEntityNameOnStack);
                    ilProcessor.InsertAfter(popEntityNameOnStack, popFilterOnStack);
                    ilProcessor.InsertAfter(popFilterOnStack, makeCall);
                }
                catch (Exception x)
                {
                    throw new Exception("An error occured when trying to evaluate method " + method.Name, x);
                }
            }
        }
    }


    private void addFilterRequests(TypeDefinition applicationDataService, TypeDefinition detailsClass)
    {
        var methods = detailsClass.Methods.Where(m => isValidFilterEvent(m.Name));

        MethodReference writelinenNotGeneric = moduleReader.Module.Import(typeof(
            PowerProductivityStudio.Extensibility.PipeLineEventNotifier)
            .GetMethod("FilterRequestOccured"));



        if (methods != null)
        {
            foreach (var method in methods)
            {
                var ilProcessor = method.Body.GetILProcessor();

                var returnType = method.ReturnType;

                MethodReference writeLineGeneric = MakeGeneric(writelinenNotGeneric, returnType);

                var body = method.Body;
                Instruction justFiltered;
                if (body.Instructions.Count > 8)
                { //User code in place
                    justFiltered = body.Instructions[3];
                    VariableDefinition filter = method.Body.Variables.FirstOrDefault();
                    Instruction popFilterOnStack = ilProcessor.Create(OpCodes.Ldloca_S, filter);
                    Instruction makeCall = ilProcessor.Create(OpCodes.Call, writeLineGeneric);

                    ilProcessor.InsertAfter(justFiltered, popFilterOnStack);
                    ilProcessor.InsertAfter(popFilterOnStack, makeCall);




                }
                //Release
                else if (body.Instructions.Count == 4) { 

                    justFiltered = body.Instructions[1];
                    VariableDefinition filter = method.Body.Variables.FirstOrDefault();


                    //ldnull
                    //stloc 0 or 1 for VB
                    ilProcessor.InsertBefore(body.Instructions[2], ilProcessor.Create(OpCodes.Ldloca_S, filter));
                    ilProcessor.InsertBefore(body.Instructions[3], ilProcessor.Create(OpCodes.Call, writeLineGeneric));
                    //ldloc0
                    //ret

                    for (int i = 0; i < body.Instructions.Count; i++)
                    {
                        body.Instructions[i].Offset = i;
                    }
                }
                else
                { //No user code in place
                    justFiltered = body.Instructions[2];
                    VariableDefinition filter = method.Body.Variables.FirstOrDefault();
                    Instruction popFilterOnStack = ilProcessor.Create(OpCodes.Ldloca_S, filter);
                    Instruction makeCall = ilProcessor.Create(OpCodes.Call, writeLineGeneric);

                    ilProcessor.InsertAfter(justFiltered, popFilterOnStack);
                    ilProcessor.InsertAfter(popFilterOnStack, makeCall);
                    for (int i = 0; i < body.Instructions.Count; i++)
                    {
                        body.Instructions[i].Offset = i;
                    }
                }


            }
        }
    }


    private void addScreenStuff()
    {
        foreach (var screenClass in moduleReader.Module.Types.Where(t => t.BaseType != null && t.BaseType.FullName.StartsWith("Microsoft.LightSwitch.Framework.Client.ScreenObject`")))
        {
            var screenDetailsClass = screenClass.NestedTypes.Where(t => t.Name.Equals("DetailsClass")).First();
            var method = screenDetailsClass.Methods.Where(m => isValidScreenSavingEvent(m.Name)).Single();

            MethodReference writelinenNotGeneric = moduleReader.Module.Import(typeof(
            PowerProductivityStudio.Extensibility.ClientPipeLineEventNotifier)
            .GetMethod("ScreenSavingEventOccured"));
             
            var ilProcessor = method.Body.GetILProcessor();
             
            var body = method.Body;
            Instruction justFiltered;
            if (body.Instructions.Count > 8)
            { //User code in place
                justFiltered = body.Instructions[3];




            }
            else
            { //No user code in place
                justFiltered = body.Instructions[2];
            }

            VariableDefinition filter = method.Body.Variables.FirstOrDefault();
            Instruction popFilterOnStack = ilProcessor.Create(OpCodes.Ldloca_S, filter);
            Instruction popScreenOnStack = ilProcessor.Create(OpCodes.Ldarg_0);
            Instruction makeCall = ilProcessor.Create(OpCodes.Call, writelinenNotGeneric);

            ilProcessor.InsertAfter(justFiltered, popScreenOnStack);
            ilProcessor.InsertAfter(popScreenOnStack, popFilterOnStack);
            ilProcessor.InsertAfter(popFilterOnStack, makeCall);  



        }

    }


    private void addServerValidationEventOccured(TypeDefinition applicationDataService, TypeDefinition detailsClass)
    {
        var methods = detailsClass.Methods.Where(m => isValidValidationEventOccured(m.Name));

        MethodReference writeline = moduleReader.Module.Import(typeof(
        PowerProductivityStudio.Extensibility.PipeLineEventNotifier)
        .GetMethod("EntityValidatedEventOccured"));

        Instruction call_methodWriteline = applicationDataService.Methods.FirstOrDefault().Body.GetILProcessor()
            .Create(OpCodes.Call, writeline);
        if (methods != null)
        {
            foreach (var methodName in methods.Select(x => x.Name))
            {
                var target = detailsClass;
                var instruction = call_methodWriteline;
                MethodDefinition method;
                method = target.Methods.Where(m => m.Name.Equals(methodName)).FirstOrDefault();
                if (method != null)
                {
                    Instruction pushValidationResultBuilderOnStack = method.Body.GetILProcessor().Create(OpCodes.Ldarg_2);
                    Instruction pushEntityOnStack = method.Body.GetILProcessor().Create(OpCodes.Ldarg_1);
                    Instruction pushThisOnStack = method.Body.GetILProcessor().Create(OpCodes.Ldarg_0);

                    method.Body.GetILProcessor().InsertBefore(method.Body.Instructions[0], pushValidationResultBuilderOnStack);
                    method.Body.GetILProcessor().InsertAfter(pushValidationResultBuilderOnStack, pushEntityOnStack);
                    method.Body.GetILProcessor().InsertAfter(pushEntityOnStack, pushThisOnStack);
                    method.Body.GetILProcessor().InsertAfter(pushThisOnStack, instruction);
                }
            }
        }

    }



    private void addServerEventOccured(TypeDefinition applicationDataService, TypeDefinition detailsClass)
    {
        var methods = detailsClass.Methods.Where(m => isValidServerEventOccured(m.Name));

        MethodReference writeline = moduleReader.Module.Import(typeof(
        PowerProductivityStudio.Extensibility.PipeLineEventNotifier)
        .GetMethod("ServerEventOccured"));

        Instruction call_methodWriteline = applicationDataService.Methods.FirstOrDefault().Body.GetILProcessor()
            .Create(OpCodes.Call, writeline);
        if (methods != null)
        {
            foreach (var methodName in methods.Select(x => x.Name))
            {
                var target = detailsClass;
                var instruction = call_methodWriteline;
                MethodDefinition method;
                method = target.Methods.Where(m => m.Name.Equals(methodName)).FirstOrDefault();
                if (method != null)
                {

                    Instruction pushMethodNameOnStack = method.Body.GetILProcessor().Create(OpCodes.Ldstr, methodName);
                    Instruction pushEntityOnStack = method.Body.GetILProcessor().Create(OpCodes.Ldarg_1);
                    Instruction pushThisOnStack = method.Body.GetILProcessor().Create(OpCodes.Ldarg_0);

                    method.Body.GetILProcessor().InsertBefore(method.Body.Instructions.Last(), pushMethodNameOnStack);
                    method.Body.GetILProcessor().InsertAfter(pushMethodNameOnStack, pushEntityOnStack);
                    method.Body.GetILProcessor().InsertAfter(pushEntityOnStack, pushThisOnStack);
                    method.Body.GetILProcessor().InsertAfter(pushThisOnStack, instruction);
                }
            }
        }

    }


    public static MethodReference MakeGeneric(MethodReference method, params TypeReference[] args)
    {
        if (args.Length == 0)
            return method;

        if (method.GenericParameters.Count != args.Length)
            throw new ArgumentException("Invalid number of generic typearguments supplied");

        var genericTypeRef = new GenericInstanceMethod(method);
        foreach (var arg in args)
            genericTypeRef.GenericArguments.Add(arg);

        return genericTypeRef;
    }
     
}

 