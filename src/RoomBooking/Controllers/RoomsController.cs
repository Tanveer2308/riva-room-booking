using Microsoft.AspNetCore.Mvc;
using RoomBooking.Services;

namespace RoomBooking.Controllers;

[ApiController]
[Route("api/rooms")]
public sealed class RoomsController(BookingService service) : ControllerBase
{
    [HttpGet] public IActionResult List() => Ok(service.GetRooms());
}
