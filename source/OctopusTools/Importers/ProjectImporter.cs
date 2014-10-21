using System;
using System.Collections.Generic;
using log4net;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Platform.Util;
using OctopusTools.Commands;
using OctopusTools.Extensions;
using OctopusTools.Infrastructure;

namespace OctopusTools.Importers
{
    [Importer("project", "ProjectWithDependencies", Description = "Imports a project from an export file")]
    public class ProjectImporter : BaseImporter
    {
        public ProjectImporter(IOctopusRepository repository, IOctopusFileSystem fileSystem, ILog log)
            : base(repository, fileSystem, log)
        {
        }

        protected override void Import(Dictionary<string, string> paramDictionary)
        {
            var filePath = paramDictionary["FilePath"];
            var importedObject = FileSystemImporter.Import<ProjectExport>(filePath, typeof (ProjectImporter).GetAttributeValue((ImporterAttribute ia) => ia.EntityType));

            var project = importedObject.Project;
            var variableSet = importedObject.VariableSet;
            var deploymentProcess = importedObject.DeploymentProcess;
            var nugetFeeds = importedObject.NuGetFeeds;
            var libVariableSets = importedObject.LibraryVariableSets;
            var projectGroup = importedObject.ProjectGroup;

            var scopeValuesMapper = new ScopeValuesMapper(variableSet.ScopeValues, Log);
            scopeValuesMapper.GetVariableScopeValuesUsed(variableSet.Variables);
            scopeValuesMapper.GetActionScopeValuesUsed(deploymentProcess.Steps);

            // Check that all used Environments and Machines exist
            scopeValuesMapper.CheckScopeValuesExist(Repository);

            // Check Roles
            //var roles = CheckRolesExist(variableSet.ScopeValues.Roles);

            // Check NuGet Feeds
            var feeds = CheckNuGetFeedsExist(nugetFeeds);

            // Check Libary Variable Sets
            var libraryVariableSets = CheckLibraryVariableSets(libVariableSets);

            // Check Project Group
            var projectGroupId = CheckProjectGroup(projectGroup);

            Log.DebugFormat("Beginning import of project '{0}'", project.Name);

            var importedProject = ImportProject(project, projectGroupId, libraryVariableSets);

            ImportDeploymentProcess(deploymentProcess, importedProject, scopeValuesMapper, feeds);

            ImportVariableSets(variableSet, importedProject, scopeValuesMapper);

            Log.DebugFormat("Successfully imported project '{0}'", project.Name);
        }

        void ImportVariableSets(VariableSetResource variableSet,
            ProjectResource importedProject,
            ScopeValuesMapper scopeValuesMapper)
        {
            Log.Debug("Importing the Projects Variable Set");
            var existingVariableSet = Repository.VariableSets.Get(importedProject.VariableSetId);

            var variableUpdater = new VariableSetUpdater(Log);
            variableUpdater.UpdateVariableSet(existingVariableSet, variableSet, scopeValuesMapper);

            Repository.VariableSets.Modify(existingVariableSet);
        }

        void ImportDeploymentProcess(DeploymentProcessResource deploymentProcess,
            ProjectResource importedProject,
            ScopeValuesMapper scopeValuesMapper,
            Dictionary<string, FeedResource> nugetFeeds)
        {
            Log.Debug("Importing the Projects Deployment Process");
            var existingDeploymentProcess = Repository.DeploymentProcesses.Get(importedProject.DeploymentProcessId);
            var steps = deploymentProcess.Steps;
            foreach (var step in steps)
            {
                foreach (var action in step.Actions)
                {
                    if (action.Properties.ContainsKey("Octopus.Action.Package.NuGetFeedId"))
                    {
                        Log.Debug("Updating ID of NuGet Feed");
                        var nugetFeedId = action.Properties["Octopus.Action.Package.NuGetFeedId"];
                        action.Properties["Octopus.Action.Package.NuGetFeedId"] = nugetFeeds[nugetFeedId].Id;
                    }
                    Log.Debug("Updating IDs of Environments");
                    scopeValuesMapper.MapEnvironmentIds(action.Environments);
                }
            }
            existingDeploymentProcess.Steps.Clear();
            existingDeploymentProcess.Steps.AddRange(steps);

            Repository.DeploymentProcesses.Modify(existingDeploymentProcess);
        }

        ProjectResource ImportProject(ProjectResource project, string projectGroupId, List<string> libraryVariableSets)
        {
            Log.Debug("Importing Project");
            var existingProject = Repository.Projects.FindByName(project.Name);
            if (existingProject != null)
            {
                Log.Debug("Project already exist, project will be updated with new settings");
                existingProject.ProjectGroupId = projectGroupId;
                existingProject.DefaultToSkipIfAlreadyInstalled = project.DefaultToSkipIfAlreadyInstalled;
                existingProject.Description = project.Description;
                existingProject.IsDisabled = project.IsDisabled;
                existingProject.IncludedLibraryVariableSetIds.Clear();
                existingProject.IncludedLibraryVariableSetIds.AddRange(libraryVariableSets);
                existingProject.Slug = project.Slug;
                existingProject.VersioningStrategy.DonorPackageStepId = project.VersioningStrategy.DonorPackageStepId;
                existingProject.VersioningStrategy.Template = project.VersioningStrategy.Template;

                return Repository.Projects.Modify(existingProject);
            }
            Log.Debug("Project does not exist, a new project will be created");
            project.ProjectGroupId = projectGroupId;
            project.IncludedLibraryVariableSetIds.Clear();
            project.IncludedLibraryVariableSetIds.AddRange(libraryVariableSets);

            return Repository.Projects.Create(project);
        }

        string CheckProjectGroup(ReferenceDataItem projectGroup)
        {
            Log.Debug("Checking that the Project Group exist");
            var group = Repository.ProjectGroups.FindByName(projectGroup.Name);
            if (group == null)
            {
                throw new CommandException("Project Group " + projectGroup.Name + " does not exist");
            }
            return group.Id;
        }

        List<string> CheckLibraryVariableSets(List<ReferenceDataItem> libraryVariableSets)
        {
            Log.Debug("Checking that all Library Variable Sets exist");
            var allVariableSets = Repository.LibraryVariableSets.FindAll();
            var usedLibraryVariableSets = new List<string>();
            foreach (var libraryVariableSet in libraryVariableSets)
            {
                var variableSet = allVariableSets.Find(avs => avs.Name == libraryVariableSet.Name);
                if (variableSet == null)
                {
                    throw new CommandException("Library Variable Set " + libraryVariableSet.Name + " does not exist");
                }
                if (!usedLibraryVariableSets.Contains(variableSet.Id))
                    usedLibraryVariableSets.Add(variableSet.Id);
            }
            return usedLibraryVariableSets;
        }

        Dictionary<string, FeedResource> CheckNuGetFeedsExist(List<ReferenceDataItem> nugetFeeds)
        {
            Log.Debug("Checking that all NuGet Feeds exist");
            var feeds = new Dictionary<string, FeedResource>();
            foreach (var nugetFeed in nugetFeeds)
            {
                var feed = Repository.Feeds.FindByName(nugetFeed.Name);
                if (feed == null)
                {
                    throw new CommandException("NuGet Feed " + nugetFeed.Name + " does not exist");
                }
                if (!feeds.ContainsKey(nugetFeed.Id))
                    feeds.Add(nugetFeed.Id, feed);
            }
            return feeds;
        }

        Dictionary<string, ReferenceDataItem> CheckRolesExist(List<ReferenceDataItem> rolesList)
        {
            Log.Debug("Checking that all roles exist");
            var allRoleNames = Repository.MachineRoles.GetAllRoleNames();
            var usedRoles = new Dictionary<string, ReferenceDataItem>();
            foreach (var role in rolesList)
            {
                if (!allRoleNames.Exists(arn => arn == role.Name))
                {
                    throw new CommandException("Role " + role.Name + " does not exist");
                }
                if (!usedRoles.ContainsKey(role.Id))
                    usedRoles.Add(role.Id, role);
            }
            return usedRoles;
        }
    }
}