using Microsoft.EntityFrameworkCore;
using LibraryApi.Data;
using LibraryApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình CSDL SQLite dùng chung trong thư mục data
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=../../data/library.db";
builder.Services.AddDbContext<LibraryDbContext>(opt => opt.UseSqlite(connectionString));

// Cấu hình CORS để Client bên ngoài gọi được API
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

// Tự động khởi tạo CSDL và nạp sách mẫu khi chạy
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    db.Database.EnsureCreated();
}

// -------------------------------------------------------------
// CÁC API THEO YÊU CẦU ĐỀ TÀI 20
// -------------------------------------------------------------

// 1. GET /books: Tra cứu danh sách sách & số bản khả dụng
app.MapGet("/books", async (LibraryDbContext db) =>
    await db.Books.ToListAsync());

// 2. GET /books/{id}: Tra cứu chi tiết một cuốn sách
app.MapGet("/books/{id:int}", async (int id, LibraryDbContext db) =>
    await db.Books.FindAsync(id) is Book b 
        ? Results.Ok(b) 
        : Results.NotFound(new { message = "Không tìm thấy sách" }));

// 3. GET /reservations/{id}: Tra cứu trạng thái yêu cầu đặt mượn
app.MapGet("/reservations/{id:int}", async (int id, LibraryDbContext db) =>
    await db.Reservations.FindAsync(id) is Reservation r 
        ? Results.Ok(r) 
        : Results.NotFound(new { message = "Không tìm thấy yêu cầu" }));

// 4. GET /users/{userId}/reservations: Tra cứu danh sách đã đặt của một người dùng
app.MapGet("/users/{userId}/reservations", async (string userId, LibraryDbContext db) =>
    await db.Reservations.Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAt).ToListAsync());

app.Run();
