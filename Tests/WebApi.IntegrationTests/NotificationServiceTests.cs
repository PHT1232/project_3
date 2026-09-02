using Core.Entities;
using Core.Enums;
using FluentAssertions;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests;

/// <summary>
/// Plan §4.2 acceptance criteria, verbatim: "notification service emits 2 rows for each of
/// the 6 event types (a 6-case [Theory] — this is the acceptance evidence for a
/// heavily-specified requirement)." Exercises NotificationService directly against the real
/// SQLite-backed DataContext (not through HTTP) since this is testing the service in
/// isolation, not the request-lifecycle endpoints that call it.
/// </summary>
public class NotificationServiceTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();
    private const int Approver = 700;
    private const int Requestor = 701;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, Approver, "Ada Approver", "ada.notif@hmt.test", "Manager", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, Requestor, "Rio Requestor", "rio.notif@hmt.test", "Engineer", "Password1!",
            superiorEmployeeNumber: Approver);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(NotificationEventType.RequestSubmitted)]
    [InlineData(NotificationEventType.RequestApproved)]
    [InlineData(NotificationEventType.RequestRejected)]
    [InlineData(NotificationEventType.RequestWithdrawn)]
    [InlineData(NotificationEventType.RequestCancelled)]
    [InlineData(NotificationEventType.PasswordChanged)]
    public async Task NotifyEvent_EachOfTheSixTriggers_InsertsExactlyTwoRows(NotificationEventType eventType)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var sut = new NotificationService(db);

        if (eventType == NotificationEventType.PasswordChanged)
        {
            // Requestor is the actor here; their superior is Approver — same {700, 701} pair
            // as the request-event cases below, so every InlineData case shares one assertion.
            await sut.NotifyPasswordChangedAsync(Requestor);
        }
        else
        {
            var request = new Request
            {
                RequestorEmployeeNumber = Requestor,
                ApproverEmployeeNumber = Approver,
                Status = "Pending",
                TotalEstimatedCost = 0,
                CreatedAtUtc = DateTime.UtcNow,
                RowVersion = Guid.NewGuid(),
            };
            db.Requests.Add(request);
            await db.SaveChangesAsync();

            await sut.NotifyRequestEventAsync(eventType, request, actorEmployeeNumber: Requestor);
            await db.SaveChangesAsync();
        }

        var rows = await db.Notifications
            .Where(n => n.EventType == eventType)
            .ToListAsync();

        rows.Should().HaveCount(2, "every one of the 6 triggers must fire to exactly two recipients");
        rows.Select(r => r.RecipientEmployeeNumber).Should().BeEquivalentTo([Approver, Requestor]);
        rows.Should().OnlyContain(r => !r.IsRead);
        rows.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Title) && !string.IsNullOrWhiteSpace(r.Message));
    }

    [Fact]
    public async Task NotifyPasswordChangedAsync_ActorHasNoSuperior_InsertsOnlyOneRow()
    {
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 702, "Mo MD", "mo.notif@hmt.test", "Managing Director", "Password1!");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var sut = new NotificationService(db);

        await sut.NotifyPasswordChangedAsync(702);

        var rows = await db.Notifications
            .Where(n => n.EventType == NotificationEventType.PasswordChanged && n.RecipientEmployeeNumber == 702)
            .ToListAsync();
        rows.Should().HaveCount(1, "there is nobody to notify as 'the superior' when the actor has none");
    }
}
