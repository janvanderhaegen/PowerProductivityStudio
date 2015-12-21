using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Microsoft.LightSwitch;

namespace PowerProductivityStudio.Extensibility
{
        [InheritedExport(typeof(IServerEventHandler))]
    public interface IServerEventHandler
    {

            void EntityValidatedEventOccured(IDataService dataService, IEntityObject entity, IValidationResultsBuilder validationResultsBuilder);

        void ServerEventOccured(string action, IEntityObject entity, IDataService dws);


        void EntityCreatedEventOccured(IEntityObject entityObject);

        void EntityPermissionRequestOccured(string action, string entityPluralName, ref bool result);

        void FilterRequestOccured<T>(ref T originalFilter) where T : class;



    }
}
