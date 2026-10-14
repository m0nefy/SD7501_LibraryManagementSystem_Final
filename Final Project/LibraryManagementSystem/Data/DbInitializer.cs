using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        await db.Database.EnsureCreatedAsync();
        await EnsureOrderSchemaAsync(db);
        if (!await db.Users.AnyAsync(u => u.Username == "admin"))
        {
            var admin = new AppUser { Username = "admin", Role = UserRole.Administrator };
            admin.PasswordHash = passwordHasher.HashPassword(admin, "Admin123!");
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
        var fiction = await GetOrCreateCategoryAsync(db, "Fiction", "Novels and imaginative works");
        var technology = await GetOrCreateCategoryAsync(db, "Technology", "Programming and computing");
        var history = await GetOrCreateCategoryAsync(db, "History", "Historical studies");
        await db.SaveChangesAsync();
        var newBooks = new[]
            {
                new Book { Title = "1984", Author = "George Orwell", ISBN = "9780451524935", TotalCopies = 10, AvailableCopies = 10, Price = 35, BorrowingFee = 3, BorrowingPeriodDays = 21, DailyFineRate = 1, Category = fiction },
                new Book { Title = "Pride and Prejudice", Author = "Jane Austen", ISBN = "9780141439518", TotalCopies = 10, AvailableCopies = 10, Price = 32, BorrowingFee = 2.5m, BorrowingPeriodDays = 21, DailyFineRate = 0.8m, Category = fiction },
                new Book { Title = "The Hobbit", Author = "J. R. R. Tolkien", ISBN = "9780547928227", TotalCopies = 10, AvailableCopies = 10, Price = 40, BorrowingFee = 3.5m, BorrowingPeriodDays = 21, DailyFineRate = 1, Category = fiction },
                new Book { Title = "The Pragmatic Programmer", Author = "Andrew Hunt", ISBN = "9780135957059", TotalCopies = 10, AvailableCopies = 10, Price = 75, BorrowingFee = 5, BorrowingPeriodDays = 28, DailyFineRate = 2, Category = technology },
                new Book { Title = "Sapiens", Author = "Yuval Noah Harari", ISBN = "9780062316097", TotalCopies = 10, AvailableCopies = 10, Price = 55, BorrowingFee = 4, BorrowingPeriodDays = 21, DailyFineRate = 1.2m, Category = history }
            };

        foreach (var book in newBooks)
        {
            if (!await db.Books.AnyAsync(b => b.ISBN == book.ISBN)) db.Books.Add(book);
        }

        await db.SaveChangesAsync();
    }

    private static async Task<Category> GetOrCreateCategoryAsync(
        LibraryDbContext db,
        string name,
        string description)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == name);
        if (category != null) return category;

        category = new Category { Name = name, Description = description };
        db.Categories.Add(category);
        return category;
    }

    private static async Task EnsureOrderSchemaAsync(LibraryDbContext db)
    {
        const string sql = """
            IF COL_LENGTH(N'Books', N'Price') IS NULL
                ALTER TABLE [Books] ADD [Price] decimal(18,2) NOT NULL CONSTRAINT [DF_Books_Price] DEFAULT 0;
            IF COL_LENGTH(N'Books', N'BorrowingFee') IS NULL
                ALTER TABLE [Books] ADD [BorrowingFee] decimal(18,2) NOT NULL CONSTRAINT [DF_Books_BorrowingFee] DEFAULT 0;
            IF COL_LENGTH(N'Books', N'BorrowingPeriodDays') IS NULL
                ALTER TABLE [Books] ADD [BorrowingPeriodDays] int NOT NULL CONSTRAINT [DF_Books_BorrowingPeriodDays] DEFAULT 21;
            IF COL_LENGTH(N'Books', N'DailyFineRate') IS NULL
                ALTER TABLE [Books] ADD [DailyFineRate] decimal(18,2) NOT NULL CONSTRAINT [DF_Books_DailyFineRate] DEFAULT 1;
            IF COL_LENGTH(N'BorrowingOrders', N'StripeSessionId') IS NULL AND OBJECT_ID(N'[BorrowingOrders]', N'U') IS NOT NULL
                ALTER TABLE [BorrowingOrders] ADD [StripeSessionId] nvarchar(200) NULL;
            IF COL_LENGTH(N'Payments', N'StripeSessionId') IS NULL AND OBJECT_ID(N'[Payments]', N'U') IS NOT NULL
                ALTER TABLE [Payments] ADD [StripeSessionId] nvarchar(200) NULL;
            IF COL_LENGTH(N'BorrowingOrderItems', N'ReturnedQuantity') IS NULL AND OBJECT_ID(N'[BorrowingOrderItems]', N'U') IS NOT NULL
                ALTER TABLE [BorrowingOrderItems] ADD [ReturnedQuantity] int NOT NULL CONSTRAINT [DF_BorrowingOrderItems_ReturnedQuantity] DEFAULT 0;
            IF COL_LENGTH(N'BorrowingOrderItems', N'RejectedQuantity') IS NULL AND OBJECT_ID(N'[BorrowingOrderItems]', N'U') IS NOT NULL
                ALTER TABLE [BorrowingOrderItems] ADD [RejectedQuantity] int NOT NULL CONSTRAINT [DF_BorrowingOrderItems_RejectedQuantity] DEFAULT 0;
            IF COL_LENGTH(N'BorrowingOrderItems', N'RejectionReason') IS NULL AND OBJECT_ID(N'[BorrowingOrderItems]', N'U') IS NOT NULL
                ALTER TABLE [BorrowingOrderItems] ADD [RejectionReason] nvarchar(250) NULL;
            IF COL_LENGTH(N'BorrowingOrderItems', N'DueDate') IS NULL AND OBJECT_ID(N'[BorrowingOrderItems]', N'U') IS NOT NULL
                ALTER TABLE [BorrowingOrderItems] ADD [DueDate] datetime2 NULL;
            IF COL_LENGTH(N'BorrowingOrderItems', N'FineAmount') IS NULL AND OBJECT_ID(N'[BorrowingOrderItems]', N'U') IS NOT NULL
                ALTER TABLE [BorrowingOrderItems] ADD [FineAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_BorrowingOrderItems_FineAmount] DEFAULT 0;
            IF COL_LENGTH(N'BorrowingOrderItems', N'ReturnRequestedQuantity') IS NULL AND OBJECT_ID(N'[BorrowingOrderItems]', N'U') IS NOT NULL
                ALTER TABLE [BorrowingOrderItems] ADD [ReturnRequestedQuantity] int NOT NULL CONSTRAINT [DF_BorrowingOrderItems_ReturnRequestedQuantity] DEFAULT 0;
            IF COL_LENGTH(N'BorrowingOrderItems', N'BookTitle') IS NULL AND OBJECT_ID(N'[BorrowingOrderItems]', N'U') IS NOT NULL
                EXEC(N'ALTER TABLE [BorrowingOrderItems] ADD [BookTitle] nvarchar(200) NULL');
            IF OBJECT_ID(N'[BorrowingOrders]', N'U') IS NULL
            BEGIN
                CREATE TABLE [BorrowingOrders]
                (
                    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_BorrowingOrders] PRIMARY KEY,
                    [ReaderName] nvarchar(100) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [DueDate] datetime2 NOT NULL,
                    [ReturnRequestedAt] datetime2 NULL,
                    [ReturnedAt] datetime2 NULL,
                    [Status] nvarchar(max) NOT NULL,
                    [BorrowingFee] decimal(18,2) NOT NULL,
                    [FineAmount] decimal(18,2) NOT NULL,
                    [BookChargeAmount] decimal(18,2) NOT NULL,
                    [RejectionReason] nvarchar(500) NULL,
                    [StripeSessionId] nvarchar(200) NULL
                );
            END;

            IF OBJECT_ID(N'[BorrowingOrderItems]', N'U') IS NULL
            BEGIN
                CREATE TABLE [BorrowingOrderItems]
                (
                    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_BorrowingOrderItems] PRIMARY KEY,
                    [BorrowingOrderId] int NOT NULL,
                    [BookId] int NULL,
                    [BookTitle] nvarchar(200) NULL,
                    [Quantity] int NOT NULL,
                    [UnitBorrowingFee] decimal(18,2) NOT NULL,
                    [UnitBookPrice] decimal(18,2) NOT NULL,
                    [DailyFineRate] decimal(18,2) NOT NULL,
                    [ReturnedQuantity] int NOT NULL,
                    [RejectedQuantity] int NOT NULL,
                    [RejectionReason] nvarchar(250) NULL,
                    [DueDate] datetime2 NULL,
                    [FineAmount] decimal(18,2) NOT NULL,
                    [ReturnRequestedQuantity] int NOT NULL,
                    CONSTRAINT [FK_BorrowingOrderItems_BorrowingOrders_BorrowingOrderId] FOREIGN KEY ([BorrowingOrderId]) REFERENCES [BorrowingOrders] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_BorrowingOrderItems_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE SET NULL
                );
                CREATE INDEX [IX_BorrowingOrderItems_BorrowingOrderId] ON [BorrowingOrderItems] ([BorrowingOrderId]);
                CREATE INDEX [IX_BorrowingOrderItems_BookId] ON [BorrowingOrderItems] ([BookId]);
            END;

            IF OBJECT_ID(N'[BorrowingOrderItems]', N'U') IS NOT NULL
            BEGIN
                EXEC(N'UPDATE oi
                       SET [BookTitle] = b.[Title]
                       FROM [BorrowingOrderItems] oi
                       INNER JOIN [Books] b ON b.[Id] = oi.[BookId]
                       WHERE oi.[BookTitle] IS NULL');

                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BorrowingOrderItems_Books_BookId')
                    ALTER TABLE [BorrowingOrderItems] DROP CONSTRAINT [FK_BorrowingOrderItems_Books_BookId];
                ALTER TABLE [BorrowingOrderItems] ALTER COLUMN [BookId] int NULL;
                ALTER TABLE [BorrowingOrderItems] ADD CONSTRAINT [FK_BorrowingOrderItems_Books_BookId]
                    FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE SET NULL;
            END;

            IF OBJECT_ID(N'[Payments]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Payments]
                (
                    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Payments] PRIMARY KEY,
                    [BorrowingOrderId] int NOT NULL,
                    [Type] nvarchar(max) NOT NULL,
                    [Amount] decimal(18,2) NOT NULL,
                    [PaidAt] datetime2 NOT NULL,
                    [StripeSessionId] nvarchar(200) NULL,
                    CONSTRAINT [FK_Payments_BorrowingOrders_BorrowingOrderId] FOREIGN KEY ([BorrowingOrderId]) REFERENCES [BorrowingOrders] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_Payments_BorrowingOrderId] ON [Payments] ([BorrowingOrderId]);
            END;

            IF OBJECT_ID(N'[Users]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Users]
                (
                    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY,
                    [Username] nvarchar(80) NOT NULL,
                    [PasswordHash] nvarchar(max) NOT NULL,
                    [Role] nvarchar(max) NOT NULL
                );
                CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
            END;
            """;

        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
