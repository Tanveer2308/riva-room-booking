using Microsoft.Data.Sqlite;
namespace RoomBooking.Data;
public sealed class BookingDatabase
{
    private readonly string connectionString;
    public BookingDatabase(IConfiguration configuration)
    {
        var path = configuration["Database:Path"] ?? "bookings.db";
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, DefaultTimeout = 15, ForeignKeys = true }.ToString();
    }
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
    public void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Rooms(Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Bookings(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RoomId INTEGER NOT NULL REFERENCES Rooms(Id),
                Name TEXT NOT NULL CHECK(length(trim(Name)) BETWEEN 1 AND 100),
                StartUtc INTEGER NOT NULL, EndUtc INTEGER NOT NULL,
                IsCancelled INTEGER NOT NULL DEFAULT 0 CHECK(IsCancelled IN (0,1)),
                CHECK(EndUtc > StartUtc));
            CREATE INDEX IF NOT EXISTS IX_Bookings_ActiveRoomTime
                ON Bookings(RoomId, StartUtc, EndUtc) WHERE IsCancelled=0;
            CREATE TRIGGER IF NOT EXISTS PreventOverlapInsert BEFORE INSERT ON Bookings
            WHEN NEW.IsCancelled=0 AND EXISTS(
                SELECT 1 FROM Bookings WHERE RoomId=NEW.RoomId AND IsCancelled=0
                AND StartUtc < NEW.EndUtc AND EndUtc > NEW.StartUtc)
            BEGIN SELECT RAISE(ABORT, 'booking_overlap'); END;
            CREATE TRIGGER IF NOT EXISTS PreventOverlapUpdate BEFORE UPDATE ON Bookings
            WHEN NEW.IsCancelled=0 AND EXISTS(
                SELECT 1 FROM Bookings WHERE Id<>NEW.Id AND RoomId=NEW.RoomId AND IsCancelled=0
                AND StartUtc < NEW.EndUtc AND EndUtc > NEW.StartUtc)
            BEGIN SELECT RAISE(ABORT, 'booking_overlap'); END;
            INSERT OR IGNORE INTO Rooms(Id,Name) VALUES(1,'Room A'),(2,'Room B'),(3,'Room C');
            """;
        command.ExecuteNonQuery();
    }
}
