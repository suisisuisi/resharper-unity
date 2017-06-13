using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Metadata.Reader.API;
using JetBrains.Metadata.Utils;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.Properties;
using JetBrains.ReSharper.Plugins.Unity.ProjectModel.Properties.Flavours;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.Util;
using JetBrains.Util.Reflection;

namespace JetBrains.ReSharper.Plugins.Unity
{
    public static class ProjectExtensions
    {
        private static readonly AssemblyNameInfo ourUnityEngineReferenceName = AssemblyNameInfoFactory.Create2("UnityEngine", null);
        private static readonly AssemblyNameInfo ourUnityEditorReferenceName = AssemblyNameInfoFactory.Create2("UnityEditor", null);

        public static readonly ICollection<AssemblyNameInfo> UnityReferenceNames = new List<AssemblyNameInfo>()
        {
            ourUnityEditorReferenceName, ourUnityEngineReferenceName
        };

        public static bool IsUnityProject([CanBeNull] this IProject project)
        {
            // Only VSTU adds the Unity project flavour. Unity + Rider don't, so we have to look at references
            // Project may have UnityProjectFlavor, but it is not a Unity-generated project. https://github.com/JetBrains/resharper-unity/issues/150
            Shell.Instance.GetComponent<IProjectPropertiesRequest>();
            return project != null && project.HasSubItems("Assets")
                   && (project.HasFlavour<UnityProjectFlavor>() || ReferencesUnity(project));
        }

        private static bool ReferencesUnity(IProject project)
        {
            return ReferencesAssembly(project, ourUnityEngineReferenceName) ||
                   ReferencesAssembly(project, ourUnityEditorReferenceName);
        }

        private static bool ReferencesAssembly(IProject project, AssemblyNameInfo name)
        {
            AssemblyNameInfo info;
            return ReferencedAssembliesService.IsProjectReferencingAssemblyByName(project,
                project.GetCurrentTargetFrameworkId(), name, out info);
        }
    }
}