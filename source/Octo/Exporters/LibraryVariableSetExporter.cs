using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octopus.Cli.Commands;
using Octopus.Cli.Extensions;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Octopus.Client;
using Serilog;

namespace Octopus.Cli.Exporters
{
    [Exporter("libraryvariableset", "LibraryVariableSetWithValues", Description = "Exports a library variable set as JSON to a file")]
    public class LibraryVariableSetExporter : BaseExporter
    {
        public LibraryVariableSetExporter(IOctopusAsyncRepository repository, IOctopusFileSystem fileSystem, ILogger log)
            : base(repository, fileSystem, log)
        { }

        protected override async Task Export(Dictionary<string, string> parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters["Name"]))
            {
                throw new CommandException("Please specify the name of the library variable set to export using the parameter: --name=XYZ");
            }

            var lvsName = parameters["Name"];

            Log.Debug("Finding library variable set: {LibraryVariableSet:l}", lvsName);
            var libraryVariableSet = await Repository.LibraryVariableSets.FindByName(lvsName).ConfigureAwait(false);
            if (libraryVariableSet == null)
                throw new CouldNotFindException("a library variable set named", lvsName);

            Log.Debug("Finding variable set for library variable set");
            var variables = await Repository.VariableSets.Get(libraryVariableSet.VariableSetId).ConfigureAwait(false);
            if (variables == null)
                throw new CouldNotFindException("variable set for library variable set", libraryVariableSet.Name);

            var export = new LibraryVariableSetExport
            {
                LibraryVariableSet = libraryVariableSet,
                VariableSet = variables
            };

            var metadata = new ExportMetadata
            {
                ExportedAt = DateTime.Now,
                OctopusVersion = Repository.Client.RootDocument.Version,
                Type = typeof(LibraryVariableSetExporter).GetAttributeValue((ExporterAttribute ea) => ea.Name),
                ContainerType = typeof(LibraryVariableSetExporter).GetAttributeValue((ExporterAttribute ea) => ea.EntityType)
            };
            FileSystemExporter.Export(FilePath, metadata, export);
        }
    }
}
