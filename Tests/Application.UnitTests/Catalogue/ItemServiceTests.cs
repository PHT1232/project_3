using Application.DTOs.Catalogue;
using Application.Exceptions;
using Application.Interfaces.Catalogue;
using Application.Services.Catalogue;
using Application.Validators.Catalogue;
using Core.Entities;
using Core.Interfaces;
using FluentAssertions;
using Moq;

namespace Application.UnitTests.Catalogue;

public class ItemServiceTests
{
    private static ItemService CreateSut(Mock<IRepository<StationeryItem>> repository, Mock<IItemQueries> queries) => new(
        repository.Object,
        queries.Object,
        new CreateItemRequestValidator(),
        new UpdateItemRequestValidator());

    private static CreateItemRequest ValidCreateRequest() => new(
        ItemName: "Stapler",
        CategoryId: 1,
        UnitOfMeasure: "Each",
        UnitCost: 4.50m,
        ReorderLevel: 10,
        MinRankLevelToRequest: 1,
        SupplierId: null);

    [Fact]
    public async Task CreateItemAsync_UnknownCategory_ThrowsNotFoundException()
    {
        var repository = new Mock<IRepository<StationeryItem>>();
        var queries = new Mock<IItemQueries>();
        queries.Setup(q => q.CategoryExistsAsync(1)).ReturnsAsync(false);

        var sut = CreateSut(repository, queries);

        var act = () => sut.CreateItemAsync(ValidCreateRequest());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateItemAsync_ValidRequest_PersistsAndReturnsReloadedDto()
    {
        var repository = new Mock<IRepository<StationeryItem>>();
        var queries = new Mock<IItemQueries>();
        queries.Setup(q => q.CategoryExistsAsync(1)).ReturnsAsync(true);

        repository
            .Setup(r => r.AddAsync(It.IsAny<StationeryItem>()))
            .ReturnsAsync((StationeryItem item) => { item.Id = 42; return item; });

        var expectedDto = new ItemDto(42, "Stapler", 1, "Office", "Each", 4.50m, 0, 10, 1, true, null, Guid.NewGuid());
        queries.Setup(q => q.GetByIdUnfilteredAsync(42)).ReturnsAsync(expectedDto);

        var sut = CreateSut(repository, queries);

        var result = await sut.CreateItemAsync(ValidCreateRequest());

        result.Should().Be(expectedDto);
        repository.Verify(r => r.AddAsync(It.Is<StationeryItem>(i => i.ItemName == "Stapler" && i.QuantityAvailable == 0)), Times.Once);
    }

    [Fact]
    public async Task UpdateItemAsync_UnknownItem_ThrowsNotFoundException()
    {
        var repository = new Mock<IRepository<StationeryItem>>();
        repository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((StationeryItem?)null);
        var queries = new Mock<IItemQueries>();

        var sut = CreateSut(repository, queries);
        var request = new UpdateItemRequest("Stapler", 1, "Each", 4.50m, 10, 1, null, Guid.NewGuid());

        var act = () => sut.UpdateItemAsync(99, request);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateItemAsync_StaleRowVersion_ThrowsConflictException()
    {
        var existing = new StationeryItem
        {
            Id = 1,
            ItemName = "Stapler",
            UnitOfMeasure = "Each",
            RowVersion = Guid.NewGuid(),
        };

        var repository = new Mock<IRepository<StationeryItem>>();
        repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);

        var queries = new Mock<IItemQueries>();
        queries.Setup(q => q.CategoryExistsAsync(1)).ReturnsAsync(true);

        var sut = CreateSut(repository, queries);
        var request = new UpdateItemRequest("Stapler", 1, "Each", 4.50m, 10, 1, null, Guid.NewGuid());

        var act = () => sut.UpdateItemAsync(1, request);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task DeactivateItemAsync_SetsIsActiveFalse()
    {
        var existing = new StationeryItem { Id = 1, ItemName = "Stapler", UnitOfMeasure = "Each", IsActive = true };

        var repository = new Mock<IRepository<StationeryItem>>();
        repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);

        var sut = CreateSut(repository, new Mock<IItemQueries>());

        await sut.DeactivateItemAsync(1);

        existing.IsActive.Should().BeFalse();
        repository.Verify(r => r.UpdateAsync(existing), Times.Once);
    }
}
