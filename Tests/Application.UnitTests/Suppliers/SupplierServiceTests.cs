using Application.DTOs.Suppliers;
using Application.Exceptions;
using Application.Interfaces.Suppliers;
using Application.Services.Suppliers;
using Application.Validators.Suppliers;
using Core.Entities;
using Core.Interfaces;
using FluentAssertions;
using Moq;

namespace Application.UnitTests.Suppliers;

public class SupplierServiceTests
{
    private static SupplierService CreateSut(Mock<IRepository<Supplier>> repository, Mock<ISupplierQueries> queries) => new(
        repository.Object,
        queries.Object,
        new CreateSupplierRequestValidator(),
        new UpdateSupplierRequestValidator());

    [Fact]
    public async Task DeactivateSupplierAsync_HasActiveItems_ThrowsConflictException()
    {
        var supplier = new Supplier { Id = 1, Name = "Acme", LeadTimeDays = 5, IsActive = true };

        var repository = new Mock<IRepository<Supplier>>();
        repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(supplier);

        var queries = new Mock<ISupplierQueries>();
        queries.Setup(q => q.HasActiveItemsAsync(1)).ReturnsAsync(true);

        var sut = CreateSut(repository, queries);

        var act = () => sut.DeactivateSupplierAsync(1);

        await act.Should().ThrowAsync<ConflictException>();
        repository.Verify(r => r.UpdateAsync(It.IsAny<Supplier>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateSupplierAsync_NoActiveItems_Deactivates()
    {
        var supplier = new Supplier { Id = 1, Name = "Acme", LeadTimeDays = 5, IsActive = true };

        var repository = new Mock<IRepository<Supplier>>();
        repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(supplier);

        var queries = new Mock<ISupplierQueries>();
        queries.Setup(q => q.HasActiveItemsAsync(1)).ReturnsAsync(false);

        var sut = CreateSut(repository, queries);

        await sut.DeactivateSupplierAsync(1);

        supplier.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateSupplierAsync_UnknownSupplier_ThrowsNotFoundException()
    {
        var repository = new Mock<IRepository<Supplier>>();
        repository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Supplier?)null);

        var sut = CreateSut(repository, new Mock<ISupplierQueries>());

        var act = () => sut.DeactivateSupplierAsync(99);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateSupplierAsync_StaleRowVersion_ThrowsConflictException()
    {
        var supplier = new Supplier { Id = 1, Name = "Acme", LeadTimeDays = 5, RowVersion = Guid.NewGuid() };

        var repository = new Mock<IRepository<Supplier>>();
        repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(supplier);

        var sut = CreateSut(repository, new Mock<ISupplierQueries>());
        var request = new UpdateSupplierRequest("Acme Corp", 7, Guid.NewGuid());

        var act = () => sut.UpdateSupplierAsync(1, request);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
