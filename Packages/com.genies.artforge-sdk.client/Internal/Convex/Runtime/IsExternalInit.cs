// Polyfill for C# 9.0 init-only properties in older Unity versions
// This allows the use of 'init' accessors in C# 8.0 and earlier

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Reserved for use by the compiler for tracking init-only members.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
