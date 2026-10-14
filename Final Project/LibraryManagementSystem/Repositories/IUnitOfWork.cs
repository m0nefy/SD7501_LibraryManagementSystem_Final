using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public interface IUnitOfWork : IDisposable
{
    IRepository<Book> Books { get; }
    IRepository<Category> Categories { get; }
    IRepository<Loan> Loans { get; }
    Task<int> SaveAsync();
}
