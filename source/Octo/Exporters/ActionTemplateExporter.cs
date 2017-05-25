using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octopus.Cli.Commands;
using Octopus.Cli.Extensions;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Octopus.Client;
using Serilog;

namespace Octopus.Cli.Exporters
{
    [Exporter("actiontemplate", "ActionTemplate", Description = "Exports an action (step) template as JSON to a file")]
    public class ActionTemplateExporter : BaseExporter
    {
        public ActionTemplateExporter(IOctopusAsyncRepository repository, IOctopusFileSystem fileSystem, ILogger log)
            : base(repository, fileSystem, log)
        { }

        protected override async Task Export(Dictionary<string, string> parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters["Name"]))
            {
                throw new CommandException("Please specify the name of the action template to export using the parameter: --name=XYZ");
            }

            var templateName = parameters["Name"];

            Log.Debug("Finding action template: {ActionTemplate:l}", templateName);
            var template = await Repository.ActionTemplates.FindByName(templateName).ConfigureAwait(false);
            if (template == null)
                throw new CouldNotFindException("an action template named", templateName);

            var export = new ActionTemplateExport
            {
                ActionTemplate = template
            };

            var metadata = new ExportMetadata
            {
                ExportedAt = DateTime.Now,
                OctopusVersion = Repository.Client.RootDocument.Version,
                Type = typeof(ActionTemplateExporter).GetAttributeValue((ExporterAttribute ea) => ea.Name),
                ContainerType = typeof(ActionTemplateExporter).GetAttributeValue((ExporterAttribute ea) => ea.EntityType)
            };
            FileSystemExporter.Export(FilePath, metadata, export);
        }
    }
}