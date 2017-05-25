using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Commands;
using Octopus.Cli.Extensions;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Serilog;

namespace Octopus.Cli.Importers
{
    [Importer("libraryvariableset", "LibraryVariableSetWithValues", Description = "Imports a library variable set from an export file")]
    public class LibraryVariableSetImporter : BaseImporter
    {
        private ValidatedImportSettings validatedImportSettings;

        private bool ReadyToImport => validatedImportSettings != null && !validatedImportSettings.ErrorList.Any();

        public LibraryVariableSetImporter(IOctopusAsyncRepository repository, IOctopusFileSystem fileSystem, ILogger log)
            : base(repository, fileSystem, log)
        { }

        private class ValidatedImportSettings : BaseValidatedImportSettings
        {
            public LibraryVariableSetResource LibraryVariableSet { get; set; }
            public VariableSetResource VariableSet { get; set; }
            public IDictionary<string, EnvironmentResource> Environments { get; set; }
            public IDictionary<string, MachineResource> Machines { get; set; }
        }

        protected override async Task<bool> Validate(Dictionary<string, string> paramDictionary)
        {
            var errorList = new List<string>();

            var importedObject = FileSystemImporter.Import<LibraryVariableSetExport>(FilePath, typeof(LibraryVariableSetImporter).GetAttributeValue((ImporterAttribute ia) => ia.EntityType));
            if (importedObject == null)
                errorList.Add("Unable to deserialize the specified export file");

            var libVariableSet = importedObject.LibraryVariableSet;
            var variableSet = importedObject.VariableSet;

            var scopeValuesUsed = GetScopeValuesUsed(variableSet.Variables, variableSet.ScopeValues);

            var environmentChecksTask = CheckEnvironmentsExist(scopeValuesUsed[ScopeField.Environment]).ConfigureAwait(false);
            var machineChecksTask = CheckMachinesExist(scopeValuesUsed[ScopeField.Machine]).ConfigureAwait(false);

            var environmentChecks = await environmentChecksTask;
            var machineChecks = await machineChecksTask;

            errorList.AddRange(
                environmentChecks.MissingDependencyErrors
                    .Concat(machineChecks.MissingDependencyErrors)
            );

            validatedImportSettings = new ValidatedImportSettings
            {
                LibraryVariableSet = libVariableSet,
                VariableSet = variableSet,
                Environments = environmentChecks.FoundDependencies,
                Machines = machineChecks.FoundDependencies,
                ErrorList = errorList
            };

            if (validatedImportSettings.HasErrors)
            {
                var errorMessagesCsvString = string.Join(Environment.NewLine, validatedImportSettings.ErrorList);
                var errorMessage = string.Format($"The following issues were found with the provided import file: {Environment.NewLine}{errorMessagesCsvString}");
                throw new CommandException(errorMessage);
            }

            Log.Information("No validation errors found. Library variable set is ready to import.");
            return !validatedImportSettings.HasErrors;
        }

        protected override async Task Import(Dictionary<string, string> paramDictionary)
        {
            if (ReadyToImport)
            {
                Log.Debug("Beginning import of library variable set '{LibraryVariableSet:l}'", validatedImportSettings.LibraryVariableSet.Name);

                var importedLibVariableSet = await ImportLibraryVariableSet(validatedImportSettings.LibraryVariableSet).ConfigureAwait(false);

                await ImportVariableSets(validatedImportSettings.VariableSet, importedLibVariableSet, validatedImportSettings.Environments, validatedImportSettings.Machines).ConfigureAwait(false);

                Log.Debug("Successfully imported library variable set '{LibraryVariableSet:l}'", validatedImportSettings.LibraryVariableSet.Name);
            }
            else
            {
                Log.Error("Library variable set is not ready to be imported.");
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

        private async Task<LibraryVariableSetResource> ImportLibraryVariableSet(LibraryVariableSetResource libVariableSet)
        {
            Log.Debug("Importing Library Variable Set");
            var existingLibVariableSet = await Repository.LibraryVariableSets.FindByName(libVariableSet.Name).ConfigureAwait(false);
            if (existingLibVariableSet != null)
            {
                Log.Debug("Library variable set already exists, library variable set will be updated with new settings");
                existingLibVariableSet.Description = libVariableSet.Description;

                return await Repository.LibraryVariableSets.Modify(existingLibVariableSet).ConfigureAwait(false);
            }

            Log.Debug("Library variable set does not exist, a new library variable set will be created");
            return await Repository.LibraryVariableSets.Create(libVariableSet).ConfigureAwait(false);
        }

        protected Dictionary<ScopeField, List<ReferenceDataItem>> GetScopeValuesUsed(IList<VariableResource> variables, VariableScopeValues variableScopeValues)
        {
            var usedScopeValues = new Dictionary<ScopeField, List<ReferenceDataItem>>
            {
                {ScopeField.Environment, new List<ReferenceDataItem>()},
                {ScopeField.Machine, new List<ReferenceDataItem>()},
            };

            foreach (var variable in variables)
            {
                foreach (var variableScope in variable.Scope)
                {
                    switch (variableScope.Key)
                    {
                        case ScopeField.Environment:
                            var usedEnvironments = variableScope.Value;
                            foreach (var usedEnvironment in usedEnvironments)
                            {
                                var environment = variableScopeValues.Environments.Find(e => e.Id == usedEnvironment);
                                if (environment != null && !usedScopeValues[ScopeField.Environment].Exists(env => env.Id == usedEnvironment))
                                {
                                    usedScopeValues[ScopeField.Environment].Add(environment);
                                }
                            }
                            break;
                        case ScopeField.Machine:
                            var usedMachines = variableScope.Value;
                            foreach (var usedMachine in usedMachines)
                            {
                                var machine = variableScopeValues.Machines.Find(m => m.Id == usedMachine);
                                if (machine != null && !usedScopeValues[ScopeField.Machine].Exists(m => m.Id == usedMachine))
                                {
                                    usedScopeValues[ScopeField.Machine].Add(machine);
                                }
                            }
                            break;
                    }
                }
            }

            return usedScopeValues;
        }

        async Task ImportVariableSets(VariableSetResource variableSet,
            LibraryVariableSetResource importedLibVariableSet,
            IDictionary<string, EnvironmentResource> environments,
            IDictionary<string, MachineResource> machines)
        {
            Log.Debug("Importing the Library Variable Set's Variable Set");
            var existingVariableSet = await Repository.VariableSets.Get(importedLibVariableSet.VariableSetId).ConfigureAwait(false);

            var variables = UpdateVariables(variableSet, environments, machines);
            existingVariableSet.Variables.Clear();
            existingVariableSet.Variables.AddRange(variables);

            await Repository.VariableSets.Modify(existingVariableSet).ConfigureAwait(false);
        }

        private IList<VariableResource> UpdateVariables(VariableSetResource variableSet, IDictionary<string, EnvironmentResource> environments, IDictionary<string, MachineResource> machines)
        {
            var variables = variableSet.Variables;

            foreach (var variable in variables)
            {
                if (variable.IsSensitive)
                {
                    Log.Warning("{Variable} is a sensitive variable and its value will be reset to a blank string, once the import has completed you will have to update its value from the UI", variable.Name);
                    variable.Value = string.Empty;
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
                                newEnvironmentIds.Add(environments[oldEnvironmentId].Id);
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
                                newMachineIds.Add(machines[oldMachineId].Id);
                            }
                            scopeValue.Value.Clear();
                            scopeValue.Value.AddRange(newMachineIds);
                            break;
                    }
                }
            }
            return variables;
        }

        protected async Task<CheckedReferences<MachineResource>> CheckMachinesExist(List<ReferenceDataItem> machineList)
        {
            Log.Debug("Checking that all machines exist");
            var dependencies = new CheckedReferences<MachineResource>();
            foreach (var m in machineList)
            {
                var machine = await Repository.Machines.FindByName(m.Name).ConfigureAwait(false);
                dependencies.Register(m.Name, m.Id, machine);
            }
            return dependencies;
        }

        protected async Task<CheckedReferences<EnvironmentResource>> CheckEnvironmentsExist(List<ReferenceDataItem> environmentList)
        {
            Log.Debug("Checking that all environments exist");
            var dependencies = new CheckedReferences<EnvironmentResource>();
            foreach (var env in environmentList)
            {
                var environment = await Repository.Environments.FindByName(env.Name).ConfigureAwait(false);
                dependencies.Register(env.Name, env.Id, environment);
            }
            return dependencies;
        }
    }
}