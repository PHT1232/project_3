using Application.DTOs.Common;

namespace Application.DTOs.Inventory;

public sealed record InventoryPageResult(PagedResult<InventoryRowDto> Page, InventorySummaryDto Summary);
