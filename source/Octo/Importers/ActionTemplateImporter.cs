using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Commands;
using Octopus.Cli.Extensions;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Serilog;

namespace Octopus.Cli.Importers
{
    [Importer("actiontemplate", "ActionTemplate", Description = "Imports an action template from an export file")]
    public class ActionTemplateImporter : BaseImporter
    {
        private ValidatedImportSettings validatedImportSettings;

        private bool ReadyToImport => validatedImportSettings != null && !validatedImportSettings.ErrorList.Any();

        public ActionTemplateImporter(IOctopusAsyncRepository repository, IOctopusFileSystem fileSystem, ILogger log)
            : base(repository, fileSystem, log)
        { }

        private class ValidatedImportSettings : BaseValidatedImportSettings
        {
            public ActionTemplateResource ActionTemplate { get; set; }
        }

        protected override Task<bool> Validate(Dictionary<string, string> paramDictionary)
        {
            var errorList = new List<string>();

            var importedObject = FileSystemImporter.Import<ActionTemplateExport>(FilePath, typeof(ActionTemplateImporter).GetAttributeValue((ImporterAttribute ia) => ia.EntityType));
            if (importedObject == null)
                errorList.Add("Unable to deserialize the specified export file");

            validatedImportSettings = new ValidatedImportSettings
            {
                ActionTemplate = importedObject.ActionTemplate,
                ErrorList = errorList
            };

            if (validatedImportSettings.HasErrors)
            {
                Log.Error("The following issues were found with the provided input:");
                foreach (var error in validatedImportSettings.ErrorList)
                {
                    Log.Error(" {Error:l}", error);
                }
            }
            else
            {
                Log.Information("No validation errors found. Action template is ready to import.");
            }

            return Task.FromResult(!validatedImportSettings.HasErrors);
        }

        protected override async Task Import(Dictionary<string, string> paramDictionary)
        {
            if (ReadyToImport)
            {
                Log.Debug("Beginning import of action template '{ActionTemplate:l}'", validatedImportSettings.ActionTemplate.Name);

                await ImportActionTemplate(validatedImportSettings.ActionTemplate).ConfigureAwait(false);

                Log.Debug("Successfully imported action template '{ActionTemplate:l}'", validatedImportSettings.ActionTemplate.Name);
            }
            else
            {
                Log.Error("Action template is not ready to be imported.");
                if (validatedImportSettings.HasErrors)
                {
                    Log.Error("The following issues were found with the provided input:");
                    foreach (var error in validatedImportSettings.ErrorList)
                    {
                        Log.Error(" {Error:l}", error);
                    }
                }
            }
        }

        private async Task ImportActionTemplate(ActionTemplateResource actionTemplate)
        {
            Log.Debug("Importing Action Template");
            var existingTemplate = await Repository.ActionTemplates.FindByName(actionTemplate.Name).ConfigureAwait(false);
            if (existingTemplate != null)
            {
                Log.Debug("Action template already exists, action template will be updated with new settings");
                existingTemplate.Description = actionTemplate.Description;
                existingTemplate.ActionType = actionTemplate.ActionType;
                existingTemplate.Properties.Clear();
                existingTemplate.Properties.AddRange(actionTemplate.Properties);
                existingTemplate.Parameters.Clear();
                existingTemplate.Parameters.AddRange(actionTemplate.Parameters);

                await Repository.ActionTemplates.Modify(existingTemplate).ConfigureAwait(false);
            }
            else
            {
                Log.Debug("Action template does not exist, a new action template will be created");
                await Repository.ActionTemplates.Create(actionTemplate).ConfigureAwait(false);
            }
        }
    }
}
