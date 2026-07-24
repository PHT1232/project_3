using System.Linq.Expressions;
using Application.Interfaces;
using Core.Interfaces;

namespace Application.Services;

public class Service<T>(IRepository<T> repository) : IService<T> where T : class
{
    public async Task<T?> GetByIdAsync(int id)
    {
        return await repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await repository.GetAllAsync();
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await repository.FindAsync(predicate);
    }

    public async Task<T> AddAsync(T entity)
    {
        return await repository.AddAsync(entity);
    }

    public async Task UpdateAsync(T entity)
    {
        await repository.UpdateAsync(entity);
    }

    public async Task DeleteAsync(T entity)
    {
        await repository.DeleteAsync(entity);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return await repository.CountAsync(predicate);
    }
}
