namespace Application.DTOs.Requests;

/// <summary>
/// Input: requestor submits items and delivery deadline to create a Stationery Request.
/// The approver is resolved server-side from the requestor's superior (Core.Entities.User.SuperiorEmployeeNumber).
/// </summary>
public sealed record CreateRequestCommand(
    IReadOnlyList<CreateRequestItemInput> Items,
    DateTime? RequiredByDate
);

/// <summary>
/// Line item in the new request. Unit cost is snapshotted server-side from StationeryItem.UnitCost
/// at submission time, so price changes do not rewrite history.
/// </summary>
public sealed record CreateRequestItemInput(
    int ItemId,
    int Quantity
);
