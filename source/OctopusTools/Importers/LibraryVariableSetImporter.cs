﻿using System;
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

            var scopeValuesUsed = GetScopeValuesUsed(variableSet.Variables, variableSet.ScopeValues);

            // Check Environments
            var environments = CheckEnvironmentsExist(scopeValuesUsed[ScopeField.Environment]);

            // Check Machines
            var machines = CheckMachinesExist(scopeValuesUsed[ScopeField.Machine]);

            Log.DebugFormat("Beginning import of library variable set '{0}'", libraryVariableSet.Name);

            var importedLibVariableSet = ImportLibraryVariableSet(libraryVariableSet);

            ImportVariableSets(variableSet, importedLibVariableSet, environments, machines, scopeValuesUsed);

            Log.DebugFormat("Successfully imported library variable set '{0}'", libraryVariableSet.Name);
        }

        Dictionary<ScopeField, List<ReferenceDataItem>> GetScopeValuesUsed(IList<VariableResource> variables, VariableScopeValues variableScopeValues)
        {
            var usedScopeValues = new Dictionary<ScopeField, List<ReferenceDataItem>>
            {
                {ScopeField.Environment, new List<ReferenceDataItem>()},
                {ScopeField.Machine, new List<ReferenceDataItem>()}
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
                                if (environment != null)
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
                                if (machine != null)
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

        void ImportVariableSets(VariableSetResource variableSet,
            LibraryVariableSetResource importedLibraryVariableSet,
            Dictionary<string, EnvironmentResource> environments,
            Dictionary<string, MachineResource> machines,
            Dictionary<ScopeField, List<ReferenceDataItem>> scopeValuesUsed)
        {
            Log.Debug("Importing the Library Variable Set Variables");
            var existingVariableSet = Repository.VariableSets.Get(importedLibraryVariableSet.VariableSetId);

            var variables = UpdateVariables(variableSet, environments, machines);
            existingVariableSet.Variables.Clear();
            existingVariableSet.Variables.AddRange(variables);

            var scopeValues = UpdateScopeValues(environments, machines, scopeValuesUsed);
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

        VariableScopeValues UpdateScopeValues(Dictionary<string, EnvironmentResource> environments, Dictionary<string, MachineResource> machines, Dictionary<ScopeField, List<ReferenceDataItem>> scopeValuesUsed)
        {
            var scopeValues = new VariableScopeValues();
            Log.Debug("Updating the Environments of the Variable Sets Scope Values");
            scopeValues.Environments = new List<ReferenceDataItem>();
            foreach (var environment in scopeValuesUsed[ScopeField.Environment])
            {
                var newEnvironment = environments[environment.Id];
                scopeValues.Environments.Add(new ReferenceDataItem(newEnvironment.Id, newEnvironment.Name));
            }
            Log.Debug("Updating the Machines of the Variable Sets Scope Values");
            scopeValues.Machines = new List<ReferenceDataItem>();
            foreach (var machine in scopeValuesUsed[ScopeField.Machine])
            {
                var newMachine = machines[machine.Id];
                scopeValues.Machines.Add(new ReferenceDataItem(newMachine.Id, newMachine.Name));
            }
            return scopeValues;
        }

        IList<VariableResource> UpdateVariables(VariableSetResource variableSet, Dictionary<string, EnvironmentResource> environments, Dictionary<string, MachineResource> machines)
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

        Dictionary<string, MachineResource> CheckMachinesExist(List<ReferenceDataItem> machineList)
        {
            Log.Debug("Checking that all machines exist");
            var machines = new Dictionary<string, MachineResource>();
            foreach (var m in machineList)
            {
                var machine = Repository.Machines.FindByName(m.Name);
                if (machine == null)
                {
                    throw new CommandException("Machine " + m.Name + " does not exist");
                }
                if (!machines.ContainsKey(m.Id))
                    machines.Add(m.Id, machine);
            }
            return machines;
        }

        Dictionary<string, EnvironmentResource> CheckEnvironmentsExist(List<ReferenceDataItem> environmentList)
        {
            Log.Debug("Checking that all environments exist");
            var usedEnvironments = new Dictionary<string, EnvironmentResource>();
            foreach (var env in environmentList)
            {
                var environment = Repository.Environments.FindByName(env.Name);
                if (environment == null)
                {
                    throw new CommandException("Environment " + env.Name + " does not exist");
                }
                if (!usedEnvironments.ContainsKey(env.Id))
                    usedEnvironments.Add(env.Id, environment);
            }
            return usedEnvironments;
        }
    }
}