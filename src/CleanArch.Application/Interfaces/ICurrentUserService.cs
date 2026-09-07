namespace CleanArch.Application.Interfaces;

/// <summary>Provides the identity of the caller for audit fields, populated from the JWT/AAD principal.</summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
}
