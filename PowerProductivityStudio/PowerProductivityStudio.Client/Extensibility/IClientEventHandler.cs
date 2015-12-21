using System;
using System.ComponentModel.Composition;
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
using Microsoft.LightSwitch.Client;

namespace PowerProductivityStudio.Extensibility
{
    [InheritedExport(typeof(IClientEventHandler))]
    public interface IClientEventHandler
    {
        void ApplicationUserLoggedInOccured();
        void ApplicationInitializedOccured();

        void EntityCreatedEventOccured(IEntityObject entityObject);

        void ScreenSavingEventOccured(IScreenObject screen, ref bool handled);

        void ScreenCanRunEventOccured(string screenName, ref bool canRun);
    }
}
