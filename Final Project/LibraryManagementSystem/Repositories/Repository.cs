using LibraryManagementSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Repositories;

public class Repository<T>(LibraryDbContext context) : IRepository<T> where T : class
{
    protected readonly LibraryDbContext Context = context;
    protected readonly DbSet<T> Set = context.Set<T>();
    public Task<List<T>> GetAllAsync() => Set.ToListAsync();
    public Task<T?> GetByIdAsync(int id) => Set.FindAsync(id).AsTask();
    public void Add(T entity) => Set.Add(entity);
    public void Update(T entity) => Set.Update(entity);
    public void Delete(T entity) => Set.Remove(entity);
}
