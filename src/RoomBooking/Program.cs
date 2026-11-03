using Microsoft.Data.Sqlite;
using RoomBooking.Data;
using RoomBooking.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<BookingDatabase>();
builder.Services.AddScoped<BookingService>();
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
    {
        context.Response.Headers.RetryAfter = "1";
        await Results.Problem(statusCode: 503, detail: "The booking database is busy. Please retry shortly.").ExecuteAsync(context);
    }
});
app.Services.GetRequiredService<BookingDatabase>().Initialize();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.Run();
public partial class Program { }
