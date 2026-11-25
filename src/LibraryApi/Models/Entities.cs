namespace LibraryApi.Models;

// Bảng Sách
public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public int TotalCopies { get; set; }       // Tổng số bản
    public int AvailableCopies { get; set; }   // Số bản khả dụng đang có sẵn
}

// Bảng Yêu cầu đặt mượn
public class Reservation
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty; // Mã bạn đọc
    public int BookId { get; set; }                    // Mã sách cần mượn
    public string Status { get; set; } = "Pending";    // Pending, Reserved, Rejected, Borrowed, Returned
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
}

public record CreateReservationDto(string UserId, int BookId);