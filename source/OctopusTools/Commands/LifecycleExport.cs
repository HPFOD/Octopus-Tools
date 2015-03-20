using System.Collections.Generic;
using Octopus.Client.Model;

namespace OctopusTools.Commands
{
    public class LifecycleExport
    {
        public LifecycleResource Lifecycle { get; set; }
        public List<ReferenceDataItem> Environments { get; set; }
    }
}