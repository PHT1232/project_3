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
    /// recorded as the resolver. The endpoint is Managing-Director-only; this method
    /// additionally throws <see cref="FluentValidation.ValidationException"/> if the actor is
    /// the message's own sender, and <see cref="Application.Exceptions.NotFoundException"/> if
    /// the id does not exist.
    /// </summary>
    Task<SupportMessageDto> SetResolvedAsync(int id, bool resolved, int actorEmployeeNumber);
}
