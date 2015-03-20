using System;
using Octopus.Client.Model;
using Octopus.Client.Repositories;

namespace OctopusTools.Extensions
{
    public static class LifecycleRepositoryExtensions
    {
        public static LifecycleResource FindByName(this ILifecyclesRepository repo, string name)
        {
            name = (name ?? string.Empty).Trim();
            return repo.FindOne(r => string.Equals((r.Name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
