namespace Application.DTOs.Support;

/// <summary>What a user submits from the Help page's "message the team" dialog.</summary>
public sealed record CreateSupportMessageCommand(
    string Area,
    string Subject,
    string Body,
    string? Diagnostics);

/// <summary>Manager+ marks a message resolved (true) or reopens it (false).</summary>
public sealed record SetSupportMessageStatusRequest(bool Resolved);

/// <summary>A support message as shown in the Manager+ Support Inbox.</summary>
public sealed record SupportMessageDto(
    int Id,
    int SenderEmployeeNumber,
    string SenderName,
    string Area,
    string Subject,
    string Body,
    string? Diagnostics,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolvedByName);
