using System;
using Octopus.Client.Model;

namespace Octopus.Cli.Commands
{
    public class LibraryVariableSetExport
    {
        public LibraryVariableSetResource LibraryVariableSet { get; set; }
        public VariableSetResource VariableSet { get; set; }
    }
}
