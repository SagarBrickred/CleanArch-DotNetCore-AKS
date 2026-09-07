namespace CleanArch.Domain.Exceptions;

/// <summary>Thrown when an entity invariant is violated inside the domain model itself.</summary>
public class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}
