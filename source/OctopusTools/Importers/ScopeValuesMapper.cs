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
    ///     Handles mapping the original ids of imported scope values to the
    ///     corresponding id values on the target Octopus server.
    /// </summary>
    public class ScopeValuesMapper
    {
        readonly ILog log;
        readonly Dictionary<ScopeField, List<ReferenceDataItem>> usedScopeValues =
            new Dictionary<ScopeField, List<ReferenceDataItem>>
            {
                {ScopeField.Environment, new List<ReferenceDataItem>()},
                {ScopeField.Machine, new List<ReferenceDataItem>()}
            };
        readonly Dictionary<string, EnvironmentResource> environments = new Dictionary<string, EnvironmentResource>();
        readonly Dictionary<string, MachineResource> machines = new Dictionary<string, MachineResource>();

        public ScopeValuesMapper(ILog log)
        {
            this.log = log;
        }

        private ILog Log
        {
            get { return log; }
        }

        public void GetVariableScopeValuesUsed(VariableSetResource variableSet)
        {
            var variableScopeValues = variableSet.ScopeValues;

            foreach (var variable in variableSet.Variables)
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
