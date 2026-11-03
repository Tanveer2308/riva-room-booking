using Microsoft.AspNetCore.Mvc;
using RoomBooking.Models;
using RoomBooking.Services;
namespace RoomBooking.Controllers;
[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(BookingService service) : ControllerBase
{
    [HttpGet]
    public IActionResult List([FromQuery] DateOnly? day)
    {
        if (day is null || day == DateOnly.MaxValue) return Problem(statusCode: 400, detail: "Provide a valid day as YYYY-MM-DD.");
        return Ok(service.GetBookings(day.Value));
    }
    [HttpPost]
    public IActionResult Create(CreateBookingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) ModelState.AddModelError("name", "Name is required.");
        if (request.StartUtc.Offset != TimeSpan.Zero || request.EndUtc.Offset != TimeSpan.Zero)
            ModelState.AddModelError("time", "Times must use UTC (Z or +00:00).");
        if (request.EndUtc <= request.StartUtc) ModelState.AddModelError("endUtc", "End time must be after start time.");
        if (request.StartUtc.UtcDateTime.Date != request.EndUtc.UtcDateTime.Date)
            ModelState.AddModelError("endUtc", "Bookings must start and end on the same UTC calendar day.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var booking = service.Create(request);
            return StatusCode(StatusCodes.Status201Created, booking);
        }
        catch (BookingConflictException) { return Problem(statusCode: 409, detail: "This room is already booked for part of that time. Choose another time or room."); }
        catch (RoomNotFoundException) { return Problem(statusCode: 404, detail: "Room not found."); }
    }
    [HttpDelete("{id:long}")]
    public IActionResult Cancel(long id) => service.Cancel(id) ? NoContent() : Problem(statusCode: 404, detail: "Active booking not found.");
}
