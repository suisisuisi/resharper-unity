﻿#if RIDER
using JetBrains.Application.Threading;
#endif
#if WAVE08
using JetBrains.Application;
#endif

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.changes;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.Assemblies.Impl;
using JetBrains.ProjectModel.Tasks;
using JetBrains.Util;

namespace JetBrains.ReSharper.Plugins.Unity.ProjectModel
{
    [SolutionComponent]
    public class ProjectReferenceChangeTracker : IChangeProvider
    {
        private readonly Lifetime myLifetime;
        private readonly ISolution mySolution;
        private readonly IShellLocks myShellLocks;
        private readonly ModuleReferenceResolveSync myModuleReferenceResolveSync;
        private readonly ChangeManager myChangeManager;
        private readonly IViewableProjectsCollection myProjects;
        private readonly ICollection<IProjectChangeHandler> myHandlers;
        private readonly Dictionary<IProject, Lifetime> myProjectLifetimes;
        
        public ProjectReferenceChangeTracker(
            Lifetime lifetime,
            
            IEnumerable<IProjectChangeHandler> handlers,
            ISolution solution,
            
            ISolutionLoadTasksScheduler scheduler,
            IShellLocks shellLocks,
            
            ModuleReferenceResolveSync moduleReferenceResolveSync,
            ChangeManager changeManager,
            IViewableProjectsCollection projects)
        {
            myProjectLifetimes = new Dictionary<IProject, Lifetime>();

            myHandlers = handlers.ToList();
            myLifetime = lifetime;
            mySolution = solution;
            myShellLocks = shellLocks;
            myModuleReferenceResolveSync = moduleReferenceResolveSync;
            myChangeManager = changeManager;
            myProjects = projects;
            
            scheduler.EnqueueTask(new SolutionLoadTask("Checking for Unity projects", SolutionLoadTaskKinds.Done, Register));
        }

        private void Register()
        {
            using (myShellLocks.UsingReadLock())
            {
                var unityProjectLifetimes = new Dictionary<IProject, Lifetime>();
                
                myProjects.Projects.View(myLifetime, (projectLifetime, project) =>
                {
                    if (project.IsUnityProject())
                    {
                        unityProjectLifetimes.Add(project, projectLifetime);
                    }
                    
                    myProjectLifetimes.Add(project, projectLifetime);
                });
                
                var unityProjects = new UnityProjectsCollection(unityProjectLifetimes, mySolution.SolutionFilePath);
                foreach (var handler in myHandlers)
                {
                    handler.OnSolutionLoaded(unityProjects);
                }
            }
            
            myChangeManager.RegisterChangeProvider(myLifetime, this);
            myChangeManager.AddDependency(myLifetime, this, myModuleReferenceResolveSync);
        }

        private void Handle(IProject project)
        {
            Lifetime projectLifetime;
            if (!myProjectLifetimes.TryGetValue(project, out projectLifetime))
                return;

            var exceptions = new LocalList<Exception>();
            foreach (var handler in myHandlers)
            {
                try
                {
                    handler.OnProjectChanged(project, projectLifetime);
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException("Failed to handle project changes", exceptions.ToArray());
            }
        }

        object IChangeProvider.Execute(IChangeMap changeMap)
        {
            var projectModelChange = changeMap.GetChange<ProjectModelChange>(mySolution);
            if (projectModelChange == null)
                return null;

            // ReSharper hasn't necessarily processed all references when it adds the IProject
            // to the IViewableProjectsCollection. Keep an eye on reference changes, add the
            // project settings if/when the project becomes a unity project
            var projects = new JetHashSet<IProject>();
            var changes = ReferencedAssembliesService.TryGetAssemblyReferenceChanges(projectModelChange, ProjectExtensions.UnityReferenceNames);
            foreach (var change in changes)
                projects.Add(change.GetNewProject());

            foreach (var project in projects)
            {
                myChangeManager.ExecuteAfterChange(() =>
                {
                    if (project.IsUnityProject())
                        Handle(project);
                });
            }

            return null;
        }
    }
}