using MySqlConnector;

namespace Holiday_Maker;

/// <summary>
/// All kommunikation med databasen. Varje SQL-anrop är parameteriserat
/// (värden skickas som @parametrar och aldrig strängsammanfogade,
/// vilket skyddar mot SQL-injection). Metoder som skriver data
/// (CreateBooking, UpdateBookingOccupants) körs i transaktioner så att
/// antingen ALLT sparas eller INGET.
/// </summary>
public static class HolidayMakerDb
{
    // Anpassa Server, User Id och Password till din MySQL-server.
    public const string ConnectionString =
        "Server=185.240.210.183;Port=3306;Database=Holiday_Maker;User Id=ANVANDARNAMN;Password=LOSSENORD;SslMode=Preferred";

    // Record-typer som förpackar data som läses ur databasen, så att
    // resten av programmet inte behöver känna till kolumnordningen.

    /// <summary>En kund ur Customers-tabellen.</summary>
    public sealed record Customer(long Id, string Name, string Email);

    /// <summary>Ett sökträffat rum med tillhörande hotellinformation.</summary>
    public sealed record RoomResult(
        long RoomId, string HotelName, string Location,
        int RoomNumber, int Capacity, decimal PricePerNight, decimal Rating);

    /// <summary>Bekräftelse på en nyss sparad bokning.</summary>
    public sealed record BookingConfirmation(long BookingNumber, int Nights, decimal TotalPrice);

    /// <summary>En bokning, så som den visas för kunden (meny 5).</summary>
    public sealed record CustomerBooking(
        long BookingNumber, string HotelName, int RoomNumber,
        DateOnly StartDate, DateOnly EndDate, int Occupants, decimal TotalPrice);

    /// <summary>En bokning med detaljer, t.ex. kundens namn och rummets kapacitet (meny 6–7).</summary>
    public sealed record BookingDetails(
        long BookingNumber, string CustomerName, string HotelName, int RoomNumber,
        DateOnly StartDate, DateOnly EndDate, int Occupants, decimal TotalPrice, int RoomCapacity);

    /// <summary>
    /// Skapar en SQL-parameter. Om värdet är null görs det om till
    /// DBNull, eftersom MySQL-parametrar inte får skickas som C#-null.
    /// </summary>
    private static MySqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    /// <summary>Öppnar en ny anslutning till databasen och returnerar den.</summary>
    private static MySqlConnection OpenConnection()
    {
        var connection = new MySqlConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Försöker öppna en anslutning och köra ett minitest ("SELECT 1").
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
            // Fel i anslutningssträngen, nätverk eller inloggningsuppgifter –
            // vi signalerar bara att testet misslyckades.
            return false;
        }
    }

    /// <summary>
    /// Hämtar alla unika orter i Hotels-tabellen, alfabetiskt sorterade.
    /// Används för att visa ett förslag när användaren ska ange ort.
    /// </summary>
    public static List<string> ListLocations()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        // DISTINCT tar bort dubbelorter: flera hotell kan ligga i samma ort.
        command.CommandText = "SELECT DISTINCT location FROM Hotels ORDER BY location";
        using var reader = command.ExecuteReader();
        var locations = new List<string>();
        while (reader.Read())
            locations.Add(reader.GetString(0));
        return locations;
    }

    /// <summary>
    /// Hämtar alla kunder, alfabetiskt sorterade på namn.
    /// </summary>
    public static List<Customer> ListCustomers()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, email FROM Customers ORDER BY name";
        using var reader = command.ExecuteReader();
        var customers = new List<Customer>();
        // Read() returnerar true för varje rad i resultatet, false när det tar slut.
        while (reader.Read())
            customers.Add(new Customer(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
        return customers;
    }

    /// <summary>
    /// Sparar en ny kund och returnerar det nya kund-id:t (autoinkrement).
    /// </summary>
    public static long InsertCustomer(string name, string address, string email, string phone)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Customers (name, address, email, phone)
            VALUES (@name, @address, @email, @phone)
            """;
        // Alla fyra fälten skickas som parametrar – användarens text
        // kan aldrig tolkas som SQL-kod.
        command.Parameters.Add(P("@name", name));
        command.Parameters.Add(P("@address", address));
        command.Parameters.Add(P("@email", email));
        command.Parameters.Add(P("@phone", phone));
        command.ExecuteNonQuery();

        // LastInsertedId innehåller det id som MySQL tilldelade raden
        // (tabellen använder AUTO_INCREMENT).
        return (long)command.LastInsertedId;
    }

    /// <summary>
    /// Söker efter rum som är lediga under given period och som uppfyller
    /// givna krav. Rummet måste rymma antalet personer och hotellet måste
    /// ha valda faciliteter (pool/restaurang) och ligga inom valda avstånd.
    /// </summary>
    public static List<RoomResult> SearchAvailableRooms(
        string location, DateOnly startDate, DateOnly endDate, int persons,
        bool wantPool, bool wantRestaurant, int? maxBeach, int? maxCity, bool sortByPrice)
    {
        const string baseSql = """
            SELECT r.id, h.name, h.location, r.room_number, r.capacity,
                   r.id_price_per_night, h.rating
            FROM Rooms AS r
            JOIN Hotels AS h ON h.id = r.hotel_id
            WHERE h.location = @location
              AND r.capacity >= @persons
              AND (@maxBeach IS NULL OR h.distance_to_beach <= @maxBeach)
              AND (@maxCity  IS NULL OR h.distance_to_city  <= @maxCity)
              AND (@wantPool = 0 OR EXISTS (
                    SELECT 1
                    FROM Hotel_x_Amenities AS hxa
                    JOIN Amenities AS a ON a.id = hxa.amenities_id
                    WHERE hxa.hotel_id = h.id AND a.name = 'Pool'))
              AND (@wantRestaurant = 0 OR EXISTS (
                    SELECT 1
                    FROM Hotel_x_Amenities AS hxa
                    JOIN Amenities AS a ON a.id = hxa.amenities_id
                    WHERE hxa.hotel_id = h.id AND a.name = 'Restaurang'))
              AND NOT EXISTS (
                    SELECT 1
                    FROM Bookings AS b
                    WHERE b.room_id = r.id
                      AND b.start_date < @endDate
                      AND b.end_date   > @startDate)
            """;

        // NOT EXISTS-klauuset utesluter rum som redan är bokat under perioden.
        // Två perioder [s1,e1] och [s2,e2] överlappar om och endast om
        // s1 < e2 OCH s2 < e1 – det är precis vad villkoren uttrycker.

        // ORDER BY-kolumnen kan inte parametriseras, därför två varianter.
        string orderBy = sortByPrice
            ? "ORDER BY r.id_price_per_night ASC, h.name ASC"
            : "ORDER BY h.rating DESC, r.id_price_per_night ASC";

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = baseSql + orderBy;

        // null-parametrar (maxBeach/maxCity) omvandlas till DBNull av P();
        // då utvärderas villkoret "@maxBeach IS NULL" till sant och
        // avståndskravet ignoreras helt.
        command.Parameters.Add(P("@location", location));
        command.Parameters.Add(P("@persons", persons));
        command.Parameters.Add(P("@maxBeach", maxBeach));
        command.Parameters.Add(P("@maxCity", maxCity));

        // Booleska filter skickas som 0/1 eftersom MySQL saknar bool-typ.
        command.Parameters.Add(P("@wantPool", wantPool ? 1 : 0));
        command.Parameters.Add(P("@wantRestaurant", wantRestaurant ? 1 : 0));
        command.Parameters.Add(P("@startDate", startDate));
        command.Parameters.Add(P("@endDate", endDate));

        using var reader = command.ExecuteReader();
        var rooms = new List<RoomResult>();
        // Reader läser raderna i kolumnordningen de SELECT:ades,
        // där 0 = första kolumnen, 1 = nästa osv.
        while (reader.Read())
            rooms.Add(new RoomResult(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6)));
        return rooms;
    }

    /// <summary>
    /// Skapar en bokning. Hela operationen körs i en transaktion:
    /// pris/kapacitet hämtas, tillgängligheten verifieras och boken sparas
    /// – antingen sparas ALLT (commit) eller INGET (rollback).
    /// </summary>
    public static BookingConfirmation CreateBooking(long customerId, long roomId, DateOnly startDate, DateOnly endDate, int persons)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            // 1. Hämta pris och kapacitet för rummet.
            // "FOR UPDATE" låser raden så att ingen annan bokning kan ändra
            // priset mellan hämtning och inskrivning.
            decimal pricePerNight;
            int capacity;
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT id_price_per_night, capacity FROM Rooms WHERE id = @roomId FOR UPDATE";
                command.Parameters.Add(P("@roomId", roomId));
                using var reader = command.ExecuteReader();
                if (!reader.Read())
                    throw new InvalidOperationException("Rummet finns inte.");
                pricePerNight = reader.GetDecimal(0);
                capacity = reader.GetInt32(1);
            }
            if (persons > capacity)
                throw new InvalidOperationException($"Rummet rymmer högst {capacity} personer.");

            // 2. Kontrollera tillgängligheten på nytt precis innan sparas.
            // (Sökresultatet kan vara gammalt om någon annan hinner boka.)
            long conflictingBookings;
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    SELECT COUNT(*)
                    FROM Bookings
                    WHERE room_id = @roomId
                      AND start_date < @endDate
                      AND end_date   > @startDate
                    FOR UPDATE
                    """;
                command.Parameters.Add(P("@roomId", roomId));
                command.Parameters.Add(P("@startDate", startDate));
                command.Parameters.Add(P("@endDate", endDate));
                conflictingBookings = (long)command.ExecuteScalar()!;
            }
            if (conflictingBookings > 0)
                throw new InvalidOperationException("Rummet är upptaget under perioden. Bokningen kunde inte sparas.");

            // 3. Spara bokningen. Totalpris = antal nätter x pris per natt.
            // DayNumber är dagens sekventiella nummer i tiden, så
            // skillnaden mellan två datum direkt ger antalet nätter.
            int nights = endDate.DayNumber - startDate.DayNumber;
            decimal totalPrice = nights * pricePerNight;
            long bookingNumber;
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO Bookings (customer_id, room_id, start_date, end_date, total_price, number_of_occupant)
                    VALUES (@customerId, @roomId, @startDate, @endDate, @totalPrice, @persons)
                    """;
                command.Parameters.Add(P("@customerId", customerId));
                command.Parameters.Add(P("@roomId", roomId));
                command.Parameters.Add(P("@startDate", startDate));
                command.Parameters.Add(P("@endDate", endDate));
                command.Parameters.Add(P("@totalPrice", totalPrice));
                command.Parameters.Add(P("@persons", persons));
                command.ExecuteNonQuery();

                // LastInsertedId ger bokningsnumret (AUTO_INCREMENT),
                // som visas till kunden och kan användas i meny 6–7.
                bookingNumber = (long)command.LastInsertedId;
            }

            transaction.Commit();
            return new BookingConfirmation(bookingNumber, nights, totalPrice);
        }
        catch
        {
            // Något gick fel – ångra ALLT som hände i transaktionen
            // (inget delsparat läge) och låt felet fortsätta uppåt.
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Hämtar alla bokningar för en given kund, sorterade på
    /// incheckningsdatum (och bokningsnummer vid lika datum).
    /// </summary>
    public static List<CustomerBooking> ListBookingsForCustomer(long customerId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.booking_number, h.name, r.room_number,
                   b.start_date, b.end_date, b.number_of_occupant, b.total_price
            FROM Bookings AS b
            JOIN Rooms AS r ON r.id = b.room_id
            JOIN Hotels AS h ON h.id = r.hotel_id
            WHERE b.customer_id = @customerId
            ORDER BY b.start_date, b.booking_number
            """;
        command.Parameters.Add(P("@customerId", customerId));
        using var reader = command.ExecuteReader();
        var bookings = new List<CustomerBooking>();
        while (reader.Read())
            bookings.Add(new CustomerBooking(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetDateOnly(3),
                reader.GetDateOnly(4),
                reader.GetInt32(5),
                reader.GetDecimal(6)));
        return bookings;
    }

    /// <summary>
    /// Hämtar en enskild bokning med tillhörande kund- och hotelluppgifter.
    /// Returnerar null om ingen bokning finns med numret.
    /// </summary>
    public static BookingDetails? GetBooking(long bookingNumber)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.booking_number, c.name, h.name, r.room_number,
                   b.start_date, b.end_date, b.number_of_occupant, b.total_price, r.capacity
            FROM Bookings AS b
            JOIN Customers AS c ON c.id = b.customer_id
            JOIN Rooms AS r ON r.id = b.room_id
            JOIN Hotels AS h ON h.id = r.hotel_id
            WHERE b.booking_number = @bookingNumber
            """;
        command.Parameters.Add(P("@bookingNumber", bookingNumber));
        using var reader = command.ExecuteReader();

        // reader.Read() returnerar false om inga rader matchade –
        // i så fall finns inga bokning med numret.
        if (!reader.Read())
            return null;

        // reader.Read() läste den första (en enda) raden;
        // kolumnvärdena plockas ut i den ordning de SELECT:ades.
        return new BookingDetails(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetDateOnly(4),
            reader.GetDateOnly(5),
            reader.GetInt32(6),
            reader.GetDecimal(7),
            reader.GetInt32(8));
    }

    /// <summary>
    /// Uppdaterar antalet personer i en bokning. Nytt antal som
    /// överstiger rummets kapacitet avvisas.
    /// </summary>
    public static void UpdateBookingOccupants(long bookingNumber, int newCount)
    {
        if (newCount < 1)
            throw new InvalidOperationException("Antalet personer måste vara minst 1.");

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            // Hämta rummets kapacitet och kontrollera det nya antalet.
            // FOR UPDATE låser raden så att kapaciteten inte kan ändras
            // medan vi verifierar och uppdaterar.
            int capacity;
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    SELECT r.capacity
                    FROM Bookings AS b
                    JOIN Rooms AS r ON r.id = b.room_id
                    WHERE b.booking_number = @bookingNumber
                    FOR UPDATE
                    """;
                command.Parameters.Add(P("@bookingNumber", bookingNumber));
                using var reader = command.ExecuteReader();
                if (!reader.Read())
                    throw new InvalidOperationException("Ingen bokning med det numret.");
                capacity = reader.GetInt32(0);
            }
            if (newCount > capacity)
                throw new InvalidOperationException($"Rummet rymmer högst {capacity} personer.");

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "UPDATE Bookings SET number_of_occupant = @newCount WHERE booking_number = @bookingNumber";
                command.Parameters.Add(P("@newCount", newCount));
                command.Parameters.Add(P("@bookingNumber", bookingNumber));
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            // Vid fel (t.ex. "rymmer högst N") ångra transaktionen,
            // så bokningen inte ändras halvvägs.
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Raderar en bokning helt. Rummet kan därmed boka igen
    /// under samma period.
    /// </summary>
    public static void DeleteBooking(long bookingNumber)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Bookings WHERE booking_number = @bookingNumber";
        command.Parameters.Add(P("@bookingNumber", bookingNumber));

        // ExecuteNonQuery returnerar antalet rader som påverkades.
        int affected = command.ExecuteNonQuery();
        if (affected == 0)
            throw new InvalidOperationException("Ingen bokning med det numret.");
    }
}
