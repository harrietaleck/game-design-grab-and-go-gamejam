using System.Threading;
using System.Threading.Tasks;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;

namespace Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Project entity persistence.
    /// Projects are typically singleton per Unity project.
    /// </summary>
    internal interface IProjectRepository
    {
        /// <summary>
        /// Retrieves the current project settings.
        /// Creates a new project if one doesn't exist.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The current project</returns>
        Task<Project> GetCurrentAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a project by its unique identifier.
        /// </summary>
        /// <param name="id">The project ID to search for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The project if found, null otherwise</returns>
        Task<Project> GetByIdAsync(ProjectId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves or updates the project settings.
        /// </summary>
        /// <param name="project">The project to save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SaveAsync(Project project, CancellationToken cancellationToken = default);
    }
}

