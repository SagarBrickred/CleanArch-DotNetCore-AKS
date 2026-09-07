namespace CleanArch.Application.Common.Exceptions;

/// <summary>Thrown for business-rule conflicts, e.g. duplicate SKU, or EF concurrency conflicts surfaced to callers.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
