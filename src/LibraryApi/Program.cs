using Microsoft.EntityFrameworkCore;
using LibraryApi.Data;
using LibraryApi.Models;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình CSDL SQLite tách ra appsettings
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=../../data/library.db";
builder.Services.AddDbContext<LibraryDbContext>(opt => opt.UseSqlite(connectionString));

// Cấu hình CORS để Client gọi được API
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

// Tự động khởi tạo CSDL và nạp dữ liệu mẫu
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    db.Database.EnsureCreated();
}

// -------------------------------------------------------------
// CÁC ENDPOINT REST API THEO ĐỀ TÀI 20
// -------------------------------------------------------------

// 1. GET /books: Tra cứu danh sách và số bản khả dụng
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

// 5. POST /reservations: Gửi yêu cầu đặt mượn -> Lưu Pending và đẩy vào RabbitMQ
app.MapPost("/reservations", async (CreateReservationDto dto, LibraryDbContext db, IConfiguration config, ILogger<Program> logger) =>
{
    var book = await db.Books.FindAsync(dto.BookId);
    if (book is null) 
        return Results.BadRequest(new { message = "Mã sách không tồn tại!" });

    // Tạo phiếu ở trạng thái Pending ban đầu
    var reservation = new Reservation
    {
        UserId = dto.UserId,
        BookId = dto.BookId,
        Status = "Pending",
        Message = "Yêu cầu đã tiếp nhận, đang xếp hàng chờ Worker kiểm tra số bản...",
        CreatedAt = DateTime.UtcNow
    };

    db.Reservations.Add(reservation);
    await db.SaveChangesAsync();

    logger.LogInformation("[API] Đã tiếp nhận yêu cầu #{Id} (Pending) cho User '{UserId}', Sách '{Title}'", reservation.Id, dto.UserId, book.Title);

    // Đẩy thông điệp vào RabbitMQ để Worker xử lý bất đồng bộ
    try
    {
        var rabbitHost = config["RabbitMQ:Host"] ?? "localhost";
        var queueName = config["RabbitMQ:QueueName"] ?? "book_reservations";

        var factory = new ConnectionFactory { HostName = rabbitHost };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);

        var payload = JsonSerializer.Serialize(new 
        { 
            ReservationId = reservation.Id, 
            BookId = reservation.BookId, 
            UserId = reservation.UserId 
        });
        var body = Encoding.UTF8.GetBytes(payload);

        channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
        logger.LogInformation("[API -> RabbitMQ] Đã đẩy thông điệp yêu cầu #{Id} vào hàng đợi '{Queue}' thành công.", reservation.Id, queueName);
    }
    catch (Exception ex)
    {
        // Tính năng chịu lỗi (Resilience): Nếu RabbitMQ chưa bật, API không bị chết, dữ liệu vẫn an toàn ở DB
        logger.LogWarning("[CẢNH BÁO CHỊU LỖI] Chưa kết nối RabbitMQ: {Msg}. Yêu cầu vẫn lưu an toàn ở trạng thái Pending.", ex.Message);
    }

    return Results.Created($"/reservations/{reservation.Id}", reservation);
});

app.Run();