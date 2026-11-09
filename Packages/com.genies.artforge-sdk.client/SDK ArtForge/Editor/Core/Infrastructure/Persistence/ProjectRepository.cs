using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using IProjectRepository = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.IProjectRepository;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Persistence
{
    /// <summary>
    /// File-based repository implementation for Project persistence.
    /// Stores project settings as a singleton JSON file.
    /// </summary>
    internal class ProjectRepository : IProjectRepository
    {
        private readonly ILogger _logger;
        private const string ProjectFileName = "project.json";
        private Project _cachedProject;

        public ProjectRepository(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Project> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            if (_cachedProject != null)
            {
                return _cachedProject;
            }

            var projectPath = GetProjectFilePath();

            if (!File.Exists(projectPath))
            {
                _cachedProject = Project.Create();
                await SaveAsync(_cachedProject, cancellationToken);
                return _cachedProject;
            }

            try
            {
                var json = await File.ReadAllTextAsync(projectPath, cancellationToken);
                _cachedProject = JsonConvert.DeserializeObject<Project>(json);
                return _cachedProject;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load project settings, creating new project", ex);
                _cachedProject = Project.Create();
                await SaveAsync(_cachedProject, cancellationToken);
                return _cachedProject;
            }
        }

        public async Task<Project> GetByIdAsync(ProjectId id, CancellationToken cancellationToken = default)
        {
            var project = await GetCurrentAsync(cancellationToken);
            return project?.Id == id ? project : null;
        }

        public async Task SaveAsync(Project project, CancellationToken cancellationToken = default)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            var projectPath = GetProjectFilePath();
            var directory = Path.GetDirectoryName(projectPath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(project, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            });

            await File.WriteAllTextAsync(projectPath, json, cancellationToken);

            _cachedProject = project;

            _logger.Info($"Project settings saved (ID: {project.Id})");
        }

        private string GetProjectFilePath()
        {
            // Store in Assets/Genies/{ShortProductName}/project.json
            var projectPath = UnityEngine.Application.dataPath;
            var gafDirectory = Path.Combine(projectPath, "Genies", BrandingConstants.ShortProductName);
            return Path.Combine(gafDirectory, ProjectFileName);
        }
    }
}
