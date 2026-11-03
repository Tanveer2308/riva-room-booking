using System.ComponentModel.DataAnnotations;
namespace RoomBooking.Models;
public sealed record Room(long Id, string Name);
public sealed record Booking(long Id, long RoomId, string Name, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
public sealed record CreateBookingRequest(
    [Range(1, long.MaxValue)] long RoomId,
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc);
public sealed class BookingConflictException : Exception { }
public sealed class RoomNotFoundException : Exception { }
