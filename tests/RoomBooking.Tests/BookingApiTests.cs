using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoomBooking.Data;
using RoomBooking.Models;
using Xunit;
namespace RoomBooking.Tests;
public sealed class TestApplication : WebApplicationFactory<Program>
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"booking-{Guid.NewGuid()}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Path"] = path }));
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-shm", "-wal" }) File.Delete(path + suffix);
    }
}
public sealed class BookingApiTests : IDisposable
{
    private readonly TestApplication app = new();
    private readonly HttpClient client;
    public BookingApiTests() => client = app.CreateClient();
    private static CreateBookingRequest Request(string start = "10:00", string end = "11:00", long room = 1) => new(room, "Tanveer", DateTimeOffset.Parse($"2026-11-04T{start}:00Z"), DateTimeOffset.Parse($"2026-11-04T{end}:00Z"));
    private Task<HttpResponseMessage> Create(CreateBookingRequest? request = null) => client.PostAsJsonAsync("/api/bookings", request ?? Request());
    private long ActiveCount()
    {
        using var connection = app.Services.GetRequiredService<BookingDatabase>().Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Bookings WHERE IsCancelled=0";
        return Convert.ToInt64(command.ExecuteScalar());
    }
    [Fact]
    public async Task ListsSeededRoomsAndBookingsForSelectedDay()
    {
        var rooms = await client.GetFromJsonAsync<Room[]>("/api/rooms");
        Assert.Equal(3, rooms!.Length);
        Assert.Equal(HttpStatusCode.Created, (await Create()).StatusCode);
        var bookings = await client.GetFromJsonAsync<Booking[]>("/api/bookings?day=2026-11-04");
        Assert.Single(bookings!);
        Assert.Empty((await client.GetFromJsonAsync<Booking[]>("/api/bookings?day=2026-11-05"))!);
        Assert.Equal(1, ActiveCount());
    }
    [Theory]
    [InlineData("10:30", "11:30")]
    [InlineData("09:30", "10:30")]
    [InlineData("09:00", "12:00")]
    [InlineData("10:15", "10:45")]
    [InlineData("10:00", "11:00")]
    public async Task RejectsAllOverlapShapes(string start, string end)
    {
        await Create();
        Assert.Equal(HttpStatusCode.Conflict, (await Create(Request(start, end))).StatusCode);
        Assert.Equal(1, ActiveCount());
    }
    [Fact]
    public async Task AllowsAdjacentBookingsAndDifferentRooms()
    {
        await Create();
        Assert.Equal(HttpStatusCode.Created, (await Create(Request("11:00", "12:00"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Create(Request("09:00", "10:00"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Create(Request(room: 2))).StatusCode);
        Assert.Equal(4, ActiveCount());
    }
    [Theory]
    [InlineData("10:00", "10:00")]
    [InlineData("11:00", "10:00")]
    public async Task RejectsInvalidTimeOrder(string start, string end)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(Request(start, end))).StatusCode);
        Assert.Equal(0, ActiveCount());
    }
    [Fact]
    public async Task RejectsCrossDayAndNonUtcTimes()
    {
        var request = Request();
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(request with { EndUtc = request.EndUtc.AddDays(1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(request with { StartUtc = request.StartUtc.ToOffset(TimeSpan.FromHours(2)) })).StatusCode);
        Assert.Equal(0, ActiveCount());
    }
    [Fact]
    public async Task ValidatesNameRoomAndDay()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(Request() with { Name = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Create(Request(room: 999))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/bookings")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/bookings?day=bad")).StatusCode);
        Assert.Equal(0, ActiveCount());
    }
    [Fact]
    public async Task CancelledSlotCanBeBookedAgain()
    {
        var booking = (await (await Create()).Content.ReadFromJsonAsync<Booking>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/bookings/{booking.Id}")).StatusCode);
        Assert.Equal(0, ActiveCount());
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/bookings/{booking.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Create()).StatusCode);
        Assert.Equal(1, ActiveCount());
    }
    [Fact]
    public async Task ConcurrentCompetingRequestsSaveExactlyOneBooking()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<HttpResponseMessage> Compete(CreateBookingRequest request) => Task.Run(async () => { await gate.Task; return await Create(request); });
        var first = Compete(Request());
        var second = Compete(Request("10:30", "11:30"));
        gate.SetResult();
        var responses = await Task.WhenAll(first, second);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, ActiveCount());
        using var connection = app.Services.GetRequiredService<BookingDatabase>().Open();
        using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM Bookings";
        Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
    }
    public void Dispose() { client.Dispose(); app.Dispose(); }
}
