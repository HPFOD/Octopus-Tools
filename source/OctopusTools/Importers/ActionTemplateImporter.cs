using System;
using System.Collections.Generic;
using log4net;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Platform.Util;
using OctopusTools.Commands;
using OctopusTools.Extensions;
using OctopusTools.Repositories;

namespace OctopusTools.Importers
{
    [Importer("actiontemplate", "ActionTemplate", Description = "Imports an action template from an export file")]
    public class ActionTemplateImporter : BaseImporter
    {
        readonly IActionTemplateRepository actionTemplateRepository;

        public ActionTemplateImporter(IOctopusRepository repository, IOctopusFileSystem fileSystem, ILog log)
            : base(repository, fileSystem, log)
        {
            actionTemplateRepository = new ActionTemplateRepository(repository.Client);
        }

        protected override void Import(Dictionary<string, string> paramDictionary)
        {
            var filePath = paramDictionary["FilePath"];
            var importedObject = FileSystemImporter.Import<ActionTemplateExport>(filePath, typeof(ActionTemplateImporter).GetAttributeValue((ImporterAttribute ia) => ia.EntityType));

            var actionTemplate = importedObject.ActionTemplate;

            Log.DebugFormat("Beginning import of action template '{0}'", actionTemplate.Name);

            ImportActionTemplate(actionTemplate);

            Log.DebugFormat("Successfully imported action template '{0}'", actionTemplate.Name);
        }

        void ImportActionTemplate(ActionTemplateResource actionTemplate)
        {
            Log.Debug("Importing Action Template");

            // The SensitiveProperties collection doesn't really seemed to be used right now,
            // but handle them like sensitive variables just in case.
            foreach (var propertyKey in actionTemplate.SensitiveProperties.Keys)
            {
                Log.WarnFormat("'{0}' is a sensitive property and it's value will be reset to a blank string, once the import has completed you will have to update its value from the UI", propertyKey);
                actionTemplate.SensitiveProperties[propertyKey] = String.Empty;
            }

            var existingActionTemplate = actionTemplateRepository.FindByName(actionTemplate.Name);
            if (existingActionTemplate != null)
            {
                Log.Debug("Action template already exists, action template will be updated with new settings");
                existingActionTemplate.Description = actionTemplate.Description;
                existingActionTemplate.ActionType = actionTemplate.ActionType;
                existingActionTemplate.Properties.Clear();
                existingActionTemplate.Properties.AddRange(actionTemplate.Properties);
                existingActionTemplate.SensitiveProperties.Clear();
                existingActionTemplate.SensitiveProperties.AddRange(actionTemplate.SensitiveProperties);
                existingActionTemplate.Parameters.Clear();
                existingActionTemplate.Parameters.AddRange(actionTemplate.Parameters);

                actionTemplateRepository.Modify(existingActionTemplate);
            }
            else
            {
                Log.Debug("Action template does not exist, a new action template will be created");
                actionTemplateRepository.Create(actionTemplate);
            }
        }
    }
}
