using System.ComponentModel;

// ReSharper disable once CheckNamespace - this type must live in this exact namespace to work.
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Compiler-required marker that enables init-only setters and positional records.
    /// </summary>
    /// <remarks>
    /// Unity's C# 9 support ships without this type in its reference assemblies, so any use of
    /// <c>init</c> or <c>record</c> fails to compile with "predefined type
    /// 'System.Runtime.CompilerServices.IsExternalInit' is not defined or imported". Declaring it
    /// ourselves is the sanctioned workaround and lets immutable data types stay idiomatic across
    /// the project. It emits no code and has no runtime cost.
    /// </remarks>
    /// <remarks>
    /// Declared public, and exactly once, in the lowest assembly in the dependency graph. An
    /// internal declaration would only enable <c>init</c> inside Utilities itself, and a second
    /// declaration in another assembly would make the type ambiguous everywhere.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class IsExternalInit
    {
    }
}
