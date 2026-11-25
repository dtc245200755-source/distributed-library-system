using Microsoft.EntityFrameworkCore;
using LibraryApi.Models;

namespace LibraryApi.Data;

public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options) { }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Nạp sẵn 3 cuốn sách mẫu vào CSDL để kiểm thử ngay
        modelBuilder.Entity<Book>().HasData(
            new Book { Id = 1, Title = "Lập trình C# và .NET 10", Author = "Microsoft", Isbn = "ISBN-001", TotalCopies = 5, AvailableCopies = 2 },
            new Book { Id = 2, Title = "Thiết kế Hệ thống phân tán", Author = "Martin Kleppmann", Isbn = "ISBN-002", TotalCopies = 3, AvailableCopies = 1 },
            new Book { Id = 3, Title = "Nhập môn RabbitMQ & Microservices", Author = "O'Reilly", Isbn = "ISBN-003", TotalCopies = 1, AvailableCopies = 0 } // Cuốn này hết sách để test trường hợp bị Rejected
        );
    }
}