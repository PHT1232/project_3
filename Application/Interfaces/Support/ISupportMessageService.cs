namespace Application.Interfaces.Support;

using Application.DTOs.Support;

/// <summary>
/// Write side of the in-app support inbox (Help page "message the team" → a stored row a
/// Manager+ triages; no email is sent — SMTP is [CUT]).
/// </summary>
public interface ISupportMessageService
{
    /// <summary>Store a new message from <paramref name="senderEmployeeNumber"/>.</summary>
    Task<SupportMessageDto> CreateAsync(CreateSupportMessageCommand command, int senderEmployeeNumber);

    /// <summary>
    /// Flip a message between New and Resolved. <paramref name="actorEmployeeNumber"/> is
    /// recorded as the resolver. Throws if the id does not exist.
    /// </summary>
    Task<SupportMessageDto> SetResolvedAsync(int id, bool resolved, int actorEmployeeNumber);
}
