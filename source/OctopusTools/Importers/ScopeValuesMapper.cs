using System;
using System.Collections.Generic;
using log4net;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Platform.Model;
using OctopusTools.Infrastructure;

namespace OctopusTools.Importers
{
    /// <summary>
    ///     This class handles mapping the original ids of imported scope values
    ///     to the corresponding id values on the target Octopus server.
    /// </summary>
    public class ScopeValuesMapper
    {
        readonly ILog log;
        readonly IOctopusRepository repository;
        readonly Dictionary<ScopeField, List<ReferenceDataItem>> usedScopeValues =
            new Dictionary<ScopeField, List<ReferenceDataItem>>
            {
                {ScopeField.Environment, new List<ReferenceDataItem>()},
                {ScopeField.Machine, new List<ReferenceDataItem>()}
            };
        readonly Dictionary<string, EnvironmentResource> environments = new Dictionary<string, EnvironmentResource>();
        readonly Dictionary<string, MachineResource> machines = new Dictionary<string, MachineResource>();

        public ScopeValuesMapper(IOctopusRepository repository, ILog log)
        {
            this.log = log;
            this.repository = repository;
        }

        private ILog Log
        {
            get { return log; }
        }

        private IOctopusRepository Repository
        {
            get { return repository; }
        }

        public void GetVariableScopeValuesUsed(VariableSetResource variableSet)
        {
            var variables = variableSet.Variables;
            var variableScopeValues = variableSet.ScopeValues;

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
        }

        public void CheckScopeValuesExist()
        {
            // Check Environments
            CheckEnvironmentsExist(usedScopeValues[ScopeField.Environment]);

            // Check Machines
            CheckMachinesExist(usedScopeValues[ScopeField.Machine]);
        }

        private void CheckEnvironmentsExist(List<ReferenceDataItem> environmentList)
        {
            Log.Debug("Checking that all environments exist");
            environments.Clear();
            foreach (var env in environmentList)
            {
                var environment = Repository.Environments.FindByName(env.Name);
                if (environment == null)
                {
                    throw new CommandException("Environment " + env.Name + " does not exist");
                }
                if (!environments.ContainsKey(env.Id))
                    environments.Add(env.Id, environment);
            }
        }

        private void CheckMachinesExist(List<ReferenceDataItem> machineList)
        {
            Log.Debug("Checking that all machines exist");
            machines.Clear();
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

        public EnvironmentResource GetMappedEnvironment(string originalId)
        {
            return environments[originalId];
        }

        public MachineResource GetMappedMachine(string originalId)
        {
            return machines[originalId];
        }
    }
}
