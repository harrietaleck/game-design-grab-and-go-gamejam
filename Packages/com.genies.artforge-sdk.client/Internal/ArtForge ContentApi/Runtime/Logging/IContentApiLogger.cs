namespace Genies.ArtForge.ContentAPI.Logging
{
    internal interface IContentApiLogger
    {
        bool IsEnabled { get; set; }
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }
}
