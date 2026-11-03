using Microsoft.Data.Sqlite;
using RoomBooking.Data;
using RoomBooking.Models;
namespace RoomBooking.Services;
public sealed class BookingService(BookingDatabase database)
{
    public IReadOnlyList<Room> GetRooms()
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Name FROM Rooms ORDER BY Id";
        using var reader = command.ExecuteReader();
        var rooms = new List<Room>();
        while (reader.Read()) rooms.Add(new(reader.GetInt64(0), reader.GetString(1)));
        return rooms;
    }
    public IReadOnlyList<Booking> GetBookings(DateOnly day)
    {
        var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,RoomId,Name,StartUtc,EndUtc FROM Bookings WHERE IsCancelled=0 AND StartUtc >= $start AND StartUtc < $end ORDER BY StartUtc,Id";
        command.Parameters.AddWithValue("$start", start.UtcTicks);
        command.Parameters.AddWithValue("$end", start.AddDays(1).UtcTicks);
        using var reader = command.ExecuteReader();
        var bookings = new List<Booking>();
        while (reader.Read()) bookings.Add(new(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), new(reader.GetInt64(3), TimeSpan.Zero), new(reader.GetInt64(4), TimeSpan.Zero)));
        return bookings;
    }
    public Booking Create(CreateBookingRequest request)
    {
        using var connection = database.Open();
        // deferred:false issues BEGIN IMMEDIATE. Acquire the database write lock
        // before the availability read; competing writers wait, then see this commit.
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$room", request.RoomId);
        command.CommandText = "SELECT COUNT(*) FROM Rooms WHERE Id=$room";
        if (Convert.ToInt64(command.ExecuteScalar()) == 0) throw new RoomNotFoundException();
        command.Parameters.AddWithValue("$start", request.StartUtc.UtcTicks);
        command.Parameters.AddWithValue("$end", request.EndUtc.UtcTicks);
        command.CommandText = "SELECT COUNT(*) FROM Bookings WHERE RoomId=$room AND IsCancelled=0 AND StartUtc < $end AND EndUtc > $start";
        if (Convert.ToInt64(command.ExecuteScalar()) != 0) throw new BookingConflictException();
        command.Parameters.AddWithValue("$name", request.Name.Trim());
        command.CommandText = "INSERT INTO Bookings(RoomId,Name,StartUtc,EndUtc) VALUES($room,$name,$start,$end); SELECT last_insert_rowid();";
        var id = Convert.ToInt64(command.ExecuteScalar());
        transaction.Commit();
        return new(id, request.RoomId, request.Name.Trim(), request.StartUtc, request.EndUtc);
    }
    public bool Cancel(long id)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Bookings SET IsCancelled=1 WHERE Id=$id AND IsCancelled=0";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() == 1;
    }
}
