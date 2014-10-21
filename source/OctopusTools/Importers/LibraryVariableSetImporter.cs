using System;
using System.Collections.Generic;
using log4net;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Platform.Model;
using Octopus.Platform.Util;
using OctopusTools.Commands;
using OctopusTools.Extensions;
using OctopusTools.Infrastructure;

namespace OctopusTools.Importers
{
    [Importer("libraryvariableset", "LibraryVariableSetWithValues", Description = "Imports a library variable set from an export file")]
    public class LibraryVariableSetImporter : BaseImporter
    {
        public LibraryVariableSetImporter(IOctopusRepository repository, IOctopusFileSystem fileSystem, ILog log)
            : base(repository, fileSystem, log)
        {
        }

        protected override void Import(Dictionary<string, string> paramDictionary)
        {
            var filePath = paramDictionary["FilePath"];
            var importedObject = FileSystemImporter.Import<LibraryVariableSetExport>(filePath, typeof (LibraryVariableSetImporter).GetAttributeValue((ImporterAttribute ia) => ia.EntityType));

            var libraryVariableSet = importedObject.LibraryVariableSet;
            var variableSet = importedObject.VariableSet;

            var scopeValuesMapper = new ScopeValuesMapper(Log);
            scopeValuesMapper.GetVariableScopeValuesUsed(variableSet);

            // Check that all used Environments and Machines exist
            scopeValuesMapper.CheckScopeValuesExist(Repository);

            Log.DebugFormat("Beginning import of library variable set '{0}'", libraryVariableSet.Name);

            var importedLibVariableSet = ImportLibraryVariableSet(libraryVariableSet);

            ImportVariableSets(variableSet, importedLibVariableSet, scopeValuesMapper);

            Log.DebugFormat("Successfully imported library variable set '{0}'", libraryVariableSet.Name);
        }

        void ImportVariableSets(VariableSetResource variableSet,
            LibraryVariableSetResource importedLibraryVariableSet,
            ScopeValuesMapper scopeValuesMapper)
        {
            Log.Debug("Importing the Library Variable Set Variables");
            var existingVariableSet = Repository.VariableSets.Get(importedLibraryVariableSet.VariableSetId);

            var variables = UpdateVariables(variableSet, scopeValuesMapper);
            existingVariableSet.Variables.Clear();
            existingVariableSet.Variables.AddRange(variables);

            var scopeValues = scopeValuesMapper.UpdateScopeValues();
            existingVariableSet.ScopeValues.Actions.Clear();
            existingVariableSet.ScopeValues.Actions.AddRange(scopeValues.Actions);
            existingVariableSet.ScopeValues.Environments.Clear();
            existingVariableSet.ScopeValues.Environments.AddRange(scopeValues.Environments);
            existingVariableSet.ScopeValues.Machines.Clear();
            existingVariableSet.ScopeValues.Machines.AddRange(scopeValues.Machines);
            existingVariableSet.ScopeValues.Roles.Clear();
            existingVariableSet.ScopeValues.Roles.AddRange(scopeValues.Roles);
            existingVariableSet.ScopeValues.Machines.AddRange(scopeValues.Machines);

            Repository.VariableSets.Modify(existingVariableSet);
        }

        IList<VariableResource> UpdateVariables(VariableSetResource variableSet, ScopeValuesMapper scopeValuesMapper)
        {
            var variables = variableSet.Variables;

            foreach (var variable in variables)
            {
                if (variable.IsSensitive)
                {
                    Log.WarnFormat("'{0}' is a sensitive variable and it's value will be reset to a blank string, once the import has completed you will have to update it's value from the UI", variable.Name);
                    variable.Value = String.Empty;
                }
                foreach (var scopeValue in variable.Scope)
                {
                    switch (scopeValue.Key)
                    {
                        case ScopeField.Environment:
                            Log.Debug("Updating the Environment IDs of the Variables scope");
                            var oldEnvironmentIds = scopeValue.Value;
                            var newEnvironmentIds = new List<string>();
                            foreach (var oldEnvironmentId in oldEnvironmentIds)
                            {
                                newEnvironmentIds.Add(scopeValuesMapper.GetMappedEnvironment(oldEnvironmentId).Id);
                            }
                            scopeValue.Value.Clear();
                            scopeValue.Value.AddRange(newEnvironmentIds);
                            break;
                        case ScopeField.Machine:
                            Log.Debug("Updating the Machine IDs of the Variables scope");
                            var oldMachineIds = scopeValue.Value;
                            var newMachineIds = new List<string>();
                            foreach (var oldMachineId in oldMachineIds)
                            {
                                newMachineIds.Add(scopeValuesMapper.GetMappedMachine(oldMachineId).Id);
                            }
                            scopeValue.Value.Clear();
                            scopeValue.Value.AddRange(newMachineIds);
                            break;
                    }
                }
            }
            return variables;
        }

        LibraryVariableSetResource ImportLibraryVariableSet(LibraryVariableSetResource libVariableSet)
        {
            Log.Debug("Importing Library Variable Set");
            var existingLibVariableSet = Repository.LibraryVariableSets.FindByName(libVariableSet.Name);
            if (existingLibVariableSet != null)
            {
                Log.Debug("Library variable set already exists, library variable set will be updated with new settings");
                existingLibVariableSet.Description = libVariableSet.Description;

                return Repository.LibraryVariableSets.Modify(existingLibVariableSet);
            }

            Log.Debug("Library variable set does not exist, a new library variable set will be created");

            return Repository.LibraryVariableSets.Create(libVariableSet);
        }
    }
}