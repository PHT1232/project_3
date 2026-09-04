using Application.DTOs.Support;
using Application.Exceptions;
using Application.Interfaces.Support;
using Core.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Write side of the in-app support inbox. Lives in Infrastructure because it needs
/// DataContext directly; there is nothing transactionally complex here (one row per call).
/// </summary>
public class SupportMessageService(
    DataContext db,
    IValidator<CreateSupportMessageCommand> createValidator) : ISupportMessageService
{
    public async Task<SupportMessageDto> CreateAsync(CreateSupportMessageCommand command, int senderEmployeeNumber)
    {
        await createValidator.ValidateAndThrowAsync(command);

        var sender = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == senderEmployeeNumber)
            ?? throw new NotFoundException($"User {senderEmployeeNumber} not found.");

        var message = new SupportMessage
        {
            SenderEmployeeNumber = senderEmployeeNumber,
            Area = command.Area.Trim(),
            Subject = command.Subject.Trim(),
            Body = command.Body.Trim(),
            Diagnostics = string.IsNullOrWhiteSpace(command.Diagnostics) ? null : command.Diagnostics.Trim(),
            Status = SupportMessageStatus.New,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.SupportMessages.Add(message);
        await db.SaveChangesAsync();

        return ToDto(message, sender.Name, resolvedByName: null);
    }

    public async Task<SupportMessageDto> SetResolvedAsync(int id, bool resolved, int actorEmployeeNumber)
    {
        var message = await db.SupportMessages.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new NotFoundException($"Support message {id} not found.");

        // The reporter doesn't triage their own ticket.
        if (message.SenderEmployeeNumber == actorEmployeeNumber)
        {
            throw new ValidationException("You can't change the status of a message you sent.");
        }

        if (resolved)
        {
            message.Status = SupportMessageStatus.Resolved;
            message.ResolvedAtUtc = DateTime.UtcNow;
            message.ResolvedByEmployeeNumber = actorEmployeeNumber;
        }
        else
        {
            message.Status = SupportMessageStatus.New;
            message.ResolvedAtUtc = null;
            message.ResolvedByEmployeeNumber = null;
        }

        await db.SaveChangesAsync();

        var senderName = await NameOf(message.SenderEmployeeNumber);
        var resolvedByName = message.ResolvedByEmployeeNumber is { } r ? await NameOf(r) : null;
        return ToDto(message, senderName, resolvedByName);
    }

    private async Task<string> NameOf(int employeeNumber) =>
        await db.Users.Where(u => u.Id == employeeNumber).Select(u => u.Name).FirstOrDefaultAsync() ?? "";

    private static SupportMessageDto ToDto(SupportMessage m, string senderName, string? resolvedByName) =>
        new(
            m.Id,
            m.SenderEmployeeNumber,
            senderName,
            m.Area,
            m.Subject,
            m.Body,
            m.Diagnostics,
            m.Status,
            m.CreatedAtUtc,
            m.ResolvedAtUtc,
            resolvedByName);
}
