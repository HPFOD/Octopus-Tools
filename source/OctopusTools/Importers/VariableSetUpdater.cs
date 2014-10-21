using System;
using System.Collections.Generic;
using log4net;
using Octopus.Client.Model;
using Octopus.Platform.Model;
using Octopus.Platform.Util;

namespace OctopusTools.Importers
{
    /// <summary>
    ///     Handles updating an existing variable set from the values and scope
    ///     ids in an imported variable set.
    /// </summary>
    public class VariableSetUpdater
    {
        readonly ILog log;

        public VariableSetUpdater(ILog log)
        {
            this.log = log;
        }

        private ILog Log
        {
            get { return log; }
        }

        public void UpdateVariableSet(VariableSetResource existingVariableSet,
            VariableSetResource importedVariableSet,
            ScopeValuesMapper scopeValuesMapper)
        {
            var variables = UpdateVariables(importedVariableSet, scopeValuesMapper);
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
                            scopeValuesMapper.MapEnvironmentIds(scopeValue.Value);
                            break;
                        case ScopeField.Machine:
                            Log.Debug("Updating the Machine IDs of the Variables scope");
                            scopeValuesMapper.MapMachineIds(scopeValue.Value);
                            break;
                    }
                }
            }
            return variables;
        }
    }
}
