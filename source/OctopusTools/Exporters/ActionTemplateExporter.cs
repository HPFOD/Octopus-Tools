using System;
using System.Collections.Generic;
using log4net;
using Octopus.Client;
using Octopus.Platform.Util;
using OctopusTools.Commands;
using OctopusTools.Extensions;
using OctopusTools.Infrastructure;
using OctopusTools.Repositories;

namespace OctopusTools.Exporters
{
    [Exporter("actiontemplate", "ActionTemplate", Description = "Exports an action (step) template as JSON to a file")]
    public class ActionTemplateExporter : BaseExporter
    {
        readonly IActionTemplateRepository actionTemplateRepository;

        public ActionTemplateExporter(IOctopusRepository repository, IOctopusFileSystem fileSystem, ILog log) :
            base(repository, fileSystem, log)
        {
            actionTemplateRepository = new ActionTemplateRepository(repository.Client);
        }

        protected override void Export(Dictionary<string, string> parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters["Name"])) throw new CommandException("Please specify the name of the action template to export using the paramater: --name=XYZ");
            var templateName = parameters["Name"];

            Log.Debug("Finding action template: " + templateName);
            var template = actionTemplateRepository.FindByName(templateName);
            if (template == null)
                throw new CommandException("Could not find action template named: " + templateName);

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
