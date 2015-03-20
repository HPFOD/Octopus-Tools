using System;
using System.Collections.Generic;
using log4net;
using Octopus.Client;
using Octopus.Platform.Util;
using OctopusTools.Commands;
using OctopusTools.Extensions;
using OctopusTools.Infrastructure;

namespace OctopusTools.Exporters
{
    [Exporter("lifecycle", "Lifecycle", Description = "Exports a lifecycle definition as JSON to a file")]
    public class LifecycleExporter : BaseExporter
    {
        public LifecycleExporter(IOctopusRepository repository, IOctopusFileSystem fileSystem, ILog log) :
            base(repository, fileSystem, log)
        {
        }

        protected override void Export(Dictionary<string, string> parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters["Name"])) throw new CommandException("Please specify the name of the lifecycle to export using the paramater: --name=XYZ");
            var lcName = parameters["Name"];

            Log.Debug("Finding lifecycle: " + lcName);
            var lifecycle = Repository.Lifecycles.FindByName(lcName);
            if (lifecycle == null)
                throw new CommandException("Could not find lifecycle named: " + lcName);

            var export = new LifecycleExport
            {
                Lifecycle = lifecycle
            };

            var metadata = new ExportMetadata
            {
                ExportedAt = DateTime.Now,
                OctopusVersion = Repository.Client.RootDocument.Version,
                Type = typeof(LifecycleExporter).GetAttributeValue((ExporterAttribute ea) => ea.Name),
                ContainerType = typeof(LifecycleExporter).GetAttributeValue((ExporterAttribute ea) => ea.EntityType)
            };
            FileSystemExporter.Export(FilePath, metadata, export);
        }
    }
}