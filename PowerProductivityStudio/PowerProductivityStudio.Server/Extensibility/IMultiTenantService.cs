using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace PowerProductivityStudio.Extensibility
{
    [InheritedExport(typeof(IMultiTenantService))]
    public interface IMultiTenantService
    {
        int GetCurrentTenantId();
    }

}
