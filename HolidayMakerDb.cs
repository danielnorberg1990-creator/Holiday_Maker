// HolidayMakerDb.cs – grunden för databasskiktet:
// anslutningssträng och gemensamma hjälpmetoder för alla
// databasklasser (CustomerDb, SearchDb, BookingDb).

using MySqlConnector;

namespace Holiday_Maker;

/// <summary>
/// Grunden för databasskiktet. Innehåller anslutningssträngen,
/// hjälpmetoder för att öppna anslutningar och skapa SQL-parametrar
/// samt ett anslutningstest. De faktiska SQL-frågorna ligger i
/// CustomerDb, SearchDb och BookingDb. Varje SQL-anrop är
/// parameteriserat (värden skickas som @parametrar och strängfogas
/// aldrig in, vilket skyddar mot SQL-injection). Metoder som skriver
/// data (CreateBooking) körs i transaktioner
/// så att antingen allt sparas eller ingenting.
/// </summary>
public static class HolidayMakerDb
{
    // Anpassa Server, User Id och Password efter din MySQL-server.
    public const string ConnectionString =
        "Server=185.240.210.183;Port=3306;Database=Holiday_Maker;User Id=testkonto;Password=123abc;SslMode=Preferred";

    /// <summary>
    /// Skapar en SQL-parameter. Om värdet är null omvandlas det till
    /// DBNull, eftersom MySQL-parametrar inte får skickas som C#-null.
    /// Används av CustomerDb, SearchDb och BookingDb.
    /// </summary>
    internal static MySqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    /// <summary>Öppnar en ny anslutning till databasen och returnerar den.</summary>
    internal static MySqlConnection OpenConnection()
    {
        var connection = new MySqlConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Försöker öppna en anslutning och köra ett litet test ("SELECT 1").
    /// Returnerar true om allt fungerar, annars false.
    /// </summary>
    public static bool CheckConnection()
    {
        try
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            return command.ExecuteScalar() is not null;
        }
        catch
        {
            // Fel i anslutningssträngen, nätverket eller inloggningsuppgifterna –
            // vi signalerar bara att testet misslyckades.
            return false;
        }
    }
}
