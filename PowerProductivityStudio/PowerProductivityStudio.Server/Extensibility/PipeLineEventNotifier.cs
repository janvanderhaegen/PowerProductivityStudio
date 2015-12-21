using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.LightSwitch;
using Microsoft.VisualStudio.ExtensibilityHosting;

namespace PowerProductivityStudio.Extensibility
{
    public static class PipeLineEventNotifier
    {
        private static IEnumerable<IServerEventHandler> serverEventHandlers;
        private static IMultiTenantService multiTenantService = null;

        static PipeLineEventNotifier()
        {
            VsExportProviderService.TryGetExportedValue(out multiTenantService);
            AggregateCatalog catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new AssemblyCatalog(typeof(PipeLineEventNotifier).Assembly));
            CompositionContainer container = new CompositionContainer(catalog);
            VsCompositionContainer.Create(container);
            serverEventHandlers = VsExportProviderService.GetExportedValues<IServerEventHandler>();

        }
        public static void ServerEventOccured(string action, IEntityObject entity, IDataService dataService)
        {
            GuardMultiTenantEvents(action, entity, dataService);
            foreach (var handler in serverEventHandlers)
            {
                handler.ServerEventOccured(action, entity, dataService);
            }
        }

        public static void EntityValidatedEventOccured(IValidationResultsBuilder validationResultsBuilder, IEntityObject entity, IDataService dataService)
        {
            foreach (var handler in serverEventHandlers)
            {
                handler.EntityValidatedEventOccured(dataService, entity, validationResultsBuilder);
            }
        }

        private static void GuardMultiTenantEvents(string action, IEntityObject entity, object dws)
        {
            if (multiTenantService != null)
            {
                var tenantIdProperty = entity.Details.Properties.All().FirstOrDefault(t => t.Name.ToLower() == "tenantid");
                if (tenantIdProperty != null)
                {
                    if (tenantIdProperty.PropertyType.Equals(typeof(Int32)) || tenantIdProperty.PropertyType.Equals(typeof(Nullable<Int32>)))
                    {
                        int currentTenantId = multiTenantService.GetCurrentTenantId();
                        if (currentTenantId != 0)
                        {
                            if (action.EndsWith("Inserting"))
                            {
                                if (tenantIdProperty.Value == null || (int)tenantIdProperty.Value == 0)
                                {
                                    tenantIdProperty.Value = currentTenantId;
                                }
                                else if ((int)tenantIdProperty.Value != currentTenantId)
                                {
                                    throw new UnauthorizedAccessException("You are unauthorized to insert data for a different tenant.");
                                }
                            }
                            if (action.EndsWith("Updating") || action.EndsWith("Deleting"))
                            {
                                if (tenantIdProperty.Value == null || (int)tenantIdProperty.Value != currentTenantId)
                                {
                                    throw new UnauthorizedAccessException("You are unauthorized to alter data belonging to a different tenant.");
                                }
                            }
                        }
                        else
                        {
                            if (tenantIdProperty.Value == null)
                            {
                                tenantIdProperty.Value = 0;
                            }
                        }
                    }
                }
            }
        }

        public static void FilterRequestOccured<T>(ref T originalFilter) where T : class
        {
            //typeof(T) is Expression<Func<Company, bool>> where Company : IEntityObject

            CreateMultiTenantFilter<T>(ref originalFilter);

            foreach (var handler in serverEventHandlers)
            {
                handler.FilterRequestOccured(ref originalFilter);
            }
        }

        private static void CreateMultiTenantFilter<T>(ref T originalFilter) where T : class
        {
            if (multiTenantService != null)
            {
                Type entityType = typeof(T).GetGenericArguments()[0].GetGenericArguments()[0];
                PropertyInfo tenantIdProperty = entityType.GetProperties().FirstOrDefault(t => t.Name.ToLower().Equals("tenantid"));
                if (tenantIdProperty != null && (tenantIdProperty.PropertyType.Equals(typeof(Int32)) || tenantIdProperty.PropertyType.Equals(typeof(Nullable<Int32>))))
                {
                    int currentTenantId = multiTenantService.GetCurrentTenantId();

                    if (currentTenantId != 0)
                    {
                            object newFilter = null;
                    
                            //Create: (entity) => entity.TenantId == 0 || entity.TenantId == 1

                            // (entity)
                            ParameterExpression pe = Expression.Parameter(entityType, "entity");

                            //entity.TenantId == 0
                            Expression left = Expression.Property(pe, tenantIdProperty);
                            Expression right = Expression.Constant(0, tenantIdProperty.PropertyType);
                            Expression equals1 = Expression.Equal(left, right);

                            //entity.TenantId == 1
                            left = Expression.Property(pe, tenantIdProperty);
                            right = Expression.Constant(currentTenantId, tenantIdProperty.PropertyType);
                            Expression equals2 = Expression.Equal(left, right);

                            //entity.TenantId == 0 || entity.TenantId == 1
                            Expression predicateBody = Expression.OrElse(equals1, equals2);
                            Type funcType = typeof(Func<,>).MakeGenericType(entityType, typeof(bool));
                            newFilter = Expression.Lambda(funcType, predicateBody, pe);

                            originalFilter = newFilter as T;
                    }
                }
            }
        }

        public static void EntityCreatedEventOccured(object e)
        {
            var entity = e as IEntityObject;
            foreach (var handler in serverEventHandlers)
            {
                handler.EntityCreatedEventOccured(entity);
            }
        }

        public static void EntityPermissionRequestOccured(string action, string entityPluralName, ref bool result)
        {
            foreach (var handler in serverEventHandlers)
            {
                handler.EntityPermissionRequestOccured(action, entityPluralName, ref result);
            }
        }
    }
}
