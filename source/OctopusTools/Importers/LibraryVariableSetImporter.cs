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

            var variableUpdater = new VariableSetUpdater(Log);
            variableUpdater.UpdateVariableSet(existingVariableSet, variableSet, scopeValuesMapper);

            Repository.VariableSets.Modify(existingVariableSet);
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