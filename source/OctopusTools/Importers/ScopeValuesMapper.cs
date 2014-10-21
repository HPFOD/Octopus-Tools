using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Platform.Model;
using Octopus.Platform.Util;
using OctopusTools.Infrastructure;

namespace OctopusTools.Importers
{
    /// <summary>
    ///     Handles mapping the original ids of imported scope values to the
    ///     corresponding id values on the target Octopus server.
    /// </summary>
    public class ScopeValuesMapper
    {
        readonly ILog log;
        readonly VariableScopeValues allScopeValues;
        readonly Dictionary<ScopeField, List<ReferenceDataItem>> usedScopeValues =
            new Dictionary<ScopeField, List<ReferenceDataItem>>
            {
                {ScopeField.Environment, new List<ReferenceDataItem>()},
                {ScopeField.Machine, new List<ReferenceDataItem>()}
            };
        readonly Dictionary<string, EnvironmentResource> environments = new Dictionary<string, EnvironmentResource>();
        readonly Dictionary<string, MachineResource> machines = new Dictionary<string, MachineResource>();

        public ScopeValuesMapper(VariableScopeValues scopeValues, ILog log)
        {
            allScopeValues = scopeValues;
            this.log = log;
        }

        private ILog Log
        {
            get { return log; }
        }

        public void GetVariableScopeValuesUsed(IList<VariableResource> variables)
        {
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
                                AddUsedEnvironment(usedEnvironment);
                            }
                            break;
                        case ScopeField.Machine:
                            var usedMachines = variableScope.Value;
                            foreach (var usedMachine in usedMachines)
                            {
                                AddUsedMachine(usedMachine);
                            }
                            break;
                    }
                }
            }
        }

        public void GetActionScopeValuesUsed(IList<DeploymentStepResource> steps)
        {
            foreach (var step in steps)
            {
                foreach (var action in step.Actions)
                {
                    foreach (var usedEnvironment in action.Environments)
                    {
                        AddUsedEnvironment(usedEnvironment);
                    }
                }
            }
        }

        void AddUsedEnvironment(string usedEnvironment)
        {
            var environment = allScopeValues.Environments.Find(e => e.Id == usedEnvironment);
            if (environment != null && !usedScopeValues[ScopeField.Environment].Exists(env => env.Id == usedEnvironment))
            {
                usedScopeValues[ScopeField.Environment].Add(environment);
            }
        }

        void AddUsedMachine(string usedMachine)
        {
            var machine = allScopeValues.Machines.Find(m => m.Id == usedMachine);
            if (machine != null && !usedScopeValues[ScopeField.Machine].Exists(env => env.Id == usedMachine))
            {
                usedScopeValues[ScopeField.Machine].Add(machine);
            }
        }

        public void CheckScopeValuesExist(IOctopusRepository repository)
        {
            // Check Environments
            CheckEnvironmentsExist(repository);

            // Check Machines
            CheckMachinesExist(repository);
        }

        void CheckEnvironmentsExist(IOctopusRepository repository)
        {
            Log.Debug("Checking that all environments exist");
            environments.Clear();
            foreach (var env in usedScopeValues[ScopeField.Environment])
            {
                var environment = repository.Environments.FindByName(env.Name);
                if (environment == null)
                {
                    throw new CommandException("Environment " + env.Name + " does not exist");
                }
                if (!environments.ContainsKey(env.Id))
                    environments.Add(env.Id, environment);
            }
        }

        void CheckMachinesExist(IOctopusRepository repository)
        {
            Log.Debug("Checking that all machines exist");
            machines.Clear();
            foreach (var m in usedScopeValues[ScopeField.Machine])
            {
                var machine = repository.Machines.FindByName(m.Name);
                if (machine == null)
                {
                    throw new CommandException("Machine " + m.Name + " does not exist");
                }
                if (!machines.ContainsKey(m.Id))
                    machines.Add(m.Id, machine);
            }
        }

        public VariableScopeValues UpdateScopeValues()
        {
            var scopeValues = new VariableScopeValues();
            Log.Debug("Updating the Environments of the Variable Sets Scope Values");
            scopeValues.Environments = new List<ReferenceDataItem>();
            foreach (var environment in usedScopeValues[ScopeField.Environment])
            {
                var newEnvironment = GetMappedEnvironment(environment.Id);
                scopeValues.Environments.Add(new ReferenceDataItem(newEnvironment.Id, newEnvironment.Name));
            }
            Log.Debug("Updating the Machines of the Variable Sets Scope Values");
            scopeValues.Machines = new List<ReferenceDataItem>();
            foreach (var machine in usedScopeValues[ScopeField.Machine])
            {
                var newMachine = GetMappedMachine(machine.Id);
                scopeValues.Machines.Add(new ReferenceDataItem(newMachine.Id, newMachine.Name));
            }
            return scopeValues;
        }

        EnvironmentResource GetMappedEnvironment(string originalId)
        {
            return environments[originalId];
        }

        MachineResource GetMappedMachine(string originalId)
        {
            return machines[originalId];
        }

        public void MapEnvironmentIds(ReferenceCollection environmentIds)
        {
            var oldEnvironmentIds = environmentIds.Clone();
            var newEnvironmentIds = oldEnvironmentIds.Select(oldId => GetMappedEnvironment(oldId).Id);
            environmentIds.Clear();
            environmentIds.AddRange(newEnvironmentIds);
        }

        public void MapMachineIds(ReferenceCollection machineIds)
        {
            var oldMachineIds = machineIds.Clone();
            var newMachineIds = oldMachineIds.Select(oldId => GetMappedMachine(oldId).Id);
            machineIds.Clear();
            machineIds.AddRange(newMachineIds);
        }
    }
}
