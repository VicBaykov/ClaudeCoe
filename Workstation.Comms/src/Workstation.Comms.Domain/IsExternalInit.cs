// Полифил для поддержки C# 9.0 record в .NET Standard 2.1
// Этот файл решает проблему с IsExternalInit
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Полифил для поддержки init-only setters в .NET Standard 2.1
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
