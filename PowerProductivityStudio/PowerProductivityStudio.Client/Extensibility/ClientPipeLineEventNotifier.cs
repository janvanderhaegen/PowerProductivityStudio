using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.LightSwitch;
using Microsoft.VisualStudio.ExtensibilityHosting;
using Microsoft.LightSwitch.Client; 

namespace PowerProductivityStudio.Extensibility
{
    public static class ClientPipeLineEventNotifier
    {
        static ClientPipeLineEventNotifier() {
            AggregateCatalog catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new AssemblyCatalog(typeof(ClientPipeLineEventNotifier).Assembly));
            CompositionContainer container = new CompositionContainer(catalog);
            VsCompositionContainer.Create(container);
            clientEventHandlers = VsExportProviderService.GetExportedValues<IClientEventHandler>();

        }

        private static IEnumerable<IClientEventHandler> clientEventHandlers;
        //TODO screen can run??
        public static void ApplicationUserLoggedInOccured() {
            foreach (var handler in clientEventHandlers) {
                handler.ApplicationUserLoggedInOccured();
            } 
        }
        public static void ApplicationInitializedOccured() {
            foreach (var handler in clientEventHandlers)
            {
                handler.ApplicationInitializedOccured();
            } 
        }

        public static void EntityCreatedEventOccured(object entity) {
            foreach (var handler in clientEventHandlers)
            {
                handler.EntityCreatedEventOccured(entity as IEntityObject); 
            }
        }


        public static void ScreenSavingEventOccured(object screen, ref bool handled) {
            foreach (var handler in clientEventHandlers) {
                handler.ScreenSavingEventOccured(screen as IScreenObject, ref handled);
            }
        }


        public static void ScreenCanRunEventOccured(string screenName, ref bool handled)
        {
            foreach (var handler in clientEventHandlers)
            {
                handler.ScreenCanRunEventOccured(screenName, ref handled);
            }
        }
    }
}
