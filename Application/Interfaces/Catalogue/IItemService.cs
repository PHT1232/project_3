using Application.DTOs.Catalogue;
using Application.DTOs.Common;

namespace Application.Interfaces.Catalogue;

public interface IItemService
{
    Task<PagedResult<ItemDto>> GetItemsAsync(ItemQueryParameters parameters, int callerRankLevel);

    Task<ItemDto?> GetItemByIdAsync(int itemId, int callerRankLevel);

    Task<ItemDto> CreateItemAsync(CreateItemRequest request);

    Task<ItemDto> UpdateItemAsync(int itemId, UpdateItemRequest request);

    Task DeactivateItemAsync(int itemId);
}
