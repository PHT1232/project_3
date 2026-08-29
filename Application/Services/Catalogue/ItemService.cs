using Application.DTOs.Catalogue;
using Application.DTOs.Common;
using Application.Exceptions;
using Application.Interfaces.Catalogue;
using Core.Entities;
using Core.Interfaces;
using FluentValidation;

namespace Application.Services.Catalogue;

public class ItemService(
    IRepository<StationeryItem> itemRepository,
    IItemQueries itemQueries,
    IValidator<CreateItemRequest> createValidator,
    IValidator<UpdateItemRequest> updateValidator) : IItemService
{
    public Task<PagedResult<ItemDto>> GetItemsAsync(ItemQueryParameters parameters, int callerRankLevel) =>
        itemQueries.GetPagedAsync(parameters, callerRankLevel);

    public Task<ItemDto?> GetItemByIdAsync(int itemId, int callerRankLevel) =>
        itemQueries.GetByIdAsync(itemId, callerRankLevel);

    public async Task<ItemDto> CreateItemAsync(CreateItemRequest request)
    {
        await createValidator.ValidateAndThrowAsync(request);

        if (!await itemQueries.CategoryExistsAsync(request.CategoryId))
        {
            throw new NotFoundException($"Category {request.CategoryId} not found.");
        }

        var item = new StationeryItem
        {
            ItemName = request.ItemName,
            CategoryId = request.CategoryId,
            UnitOfMeasure = request.UnitOfMeasure,
            UnitCost = request.UnitCost,
            ReorderLevel = request.ReorderLevel,
            MinRankLevelToRequest = request.MinRankLevelToRequest,
            SupplierId = request.SupplierId,
            QuantityAvailable = 0,
            IsActive = true,
        };

        var created = await itemRepository.AddAsync(item);
        return await itemQueries.GetByIdUnfilteredAsync(created.Id)
            ?? throw new InvalidOperationException("Created item could not be reloaded.");
    }

    public async Task<ItemDto> UpdateItemAsync(int itemId, UpdateItemRequest request)
    {
        await updateValidator.ValidateAndThrowAsync(request);

        var item = await itemRepository.GetByIdAsync(itemId)
            ?? throw new NotFoundException($"Item {itemId} not found.");

        if (!await itemQueries.CategoryExistsAsync(request.CategoryId))
        {
            throw new NotFoundException($"Category {request.CategoryId} not found.");
        }

        if (item.RowVersion != request.RowVersion)
        {
            throw new ConflictException("This item was modified by someone else. Reload and try again.");
        }

        item.ItemName = request.ItemName;
        item.CategoryId = request.CategoryId;
        item.UnitOfMeasure = request.UnitOfMeasure;
        item.UnitCost = request.UnitCost;
        item.ReorderLevel = request.ReorderLevel;
        item.MinRankLevelToRequest = request.MinRankLevelToRequest;
        item.SupplierId = request.SupplierId;
        item.RowVersion = Guid.NewGuid();

        await itemRepository.UpdateAsync(item);
        return await itemQueries.GetByIdUnfilteredAsync(itemId)
            ?? throw new InvalidOperationException("Updated item could not be reloaded.");
    }

    public async Task DeactivateItemAsync(int itemId)
    {
        var item = await itemRepository.GetByIdAsync(itemId)
            ?? throw new NotFoundException($"Item {itemId} not found.");

        item.IsActive = false;
        item.RowVersion = Guid.NewGuid();
        await itemRepository.UpdateAsync(item);
    }
}
