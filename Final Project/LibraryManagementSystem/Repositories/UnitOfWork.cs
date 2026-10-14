using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public class UnitOfWork(LibraryDbContext context) : IUnitOfWork
{
    private IRepository<Book>? _books;
    private IRepository<Category>? _categories;
    private IRepository<Loan>? _loans;
    public IRepository<Book> Books => _books ??= new Repository<Book>(context);
    public IRepository<Category> Categories => _categories ??= new Repository<Category>(context);
    public IRepository<Loan> Loans => _loans ??= new Repository<Loan>(context);
    public Task<int> SaveAsync() => context.SaveChangesAsync();
    public void Dispose() => context.Dispose();
}
