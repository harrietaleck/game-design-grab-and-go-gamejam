namespace Genies.Convex.Logging
{
    internal interface IConvexLogger
    {
        bool IsEnabled { get; set; }
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }
}
