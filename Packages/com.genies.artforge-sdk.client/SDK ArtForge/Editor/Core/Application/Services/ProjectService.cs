using System.Threading;
using System.Threading.Tasks;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;

namespace Genies.Sdk.ArtForge.Editor.Core.Application.Services
{
    /// <summary>
    /// Service for managing project settings.
    /// Provides simple, direct methods for project operations.
    /// </summary>
    internal class ProjectService
    {
        /// <summary>
        /// Gets the current project settings.
        /// </summary>
        public async Task<Project> GetCurrentProjectAsync(CancellationToken cancellationToken = default)
        {
            return await GeniesArtForgeService.Projects.GetCurrentAsync(cancellationToken);
        }

        /// <summary>
        /// Auto-saves project settings WITHOUT validation.
        /// Allows partial/in-progress data to be saved while user is typing.
        /// </summary>
        public async Task AutoSaveProjectSettingsAsync(
            string sceneDescription,
            string artStyle,
            CancellationToken cancellationToken = default)
        {
            var project = await GeniesArtForgeService.Projects.GetCurrentAsync(cancellationToken);
            project.UpdateSettingsAutoSave(sceneDescription, artStyle);
            await GeniesArtForgeService.Projects.SaveAsync(project, cancellationToken);
        }

        /// <summary>
        /// Sets the style reference image.
        /// </summary>
        public async Task<Result> SetStyleReferenceAsync(
            string imagePath,
            CancellationToken cancellationToken = default)
        {
            var project = await GeniesArtForgeService.Projects.GetCurrentAsync(cancellationToken);

            var result = project.SetStyleReference(imagePath);
            if (!result.IsSuccess)
            {
                return result;
            }

            await GeniesArtForgeService.Projects.SaveAsync(project, cancellationToken);

            GeniesArtForgeService.Logger.Info("Style reference updated");

            return Result.Success();
        }
    }
}

