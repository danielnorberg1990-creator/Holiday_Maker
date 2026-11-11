// BookingDb.cs – databasskiktets bokningsrelaterade SQL:
// skapa bokning (med status-, kapacitets- och dubbelbokningskontroll
// i transaktion), lista kundens bokningar och avboka.

namespace Holiday_Maker;

/// <summary>
/// SQL-frågor som rör Bookings-tabellen: skapa bokning (med status-,
/// kapacitets- och dubbelbokningskontroll i transaktion), lista
/// kundens bokningar och avboka. Använder de gemensamma
/// anslutningshjälpmedlen i HolidayMakerDb.
/// </summary>
public static class BookingDb
{
    /// <summary>
    /// Skapar en bokning i en transaktion:
    /// 1. Låser raden för rummet, kontrollerar att rummet inte är flaggat
    ///    som bokat (Rooms.status = 1) och att antalet personer ryms.
    /// 2. Kontrollerar att ingen annan bokning överlappar perioden.
    /// 3. Beräknar totalpriset (nätter × pris/natt).
    /// 4. Sparar bokningen.
    /// Misslyckas något steg ångas transaktionen ut och ett
    /// InvalidOperationException kastas med ett användarvänligt meddelande.
    /// </summary>
    public static BookingConfirmation CreateBooking(
        long customerId, long roomId, DateOnly startDate, DateOnly endDate, int occupants)
    {
        // Grundvalidering innan vi rör databasen alls.
        if (endDate <= startDate)
            throw new InvalidOperationException("Check-out-datumet måste vara efter check-in-datumet.");
        if (occupants < 1)
            throw new InvalidOperationException("Antalet personer måste vara minst 1.");

        using var connection = HolidayMakerDb.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            // 1. Hämta rummet och LÅS raden (FOR UPDATE). Detta stoppar
            //    en annan transaktion från att skapa en dubbelbokning
            //    samtidigt.
            int roomCapacity;
            decimal pricePerNight;
            int? roomStatus;
            using (var roomCommand = connection.CreateCommand())
            {
                roomCommand.Transaction = transaction;
                roomCommand.CommandText = "SELECT capacity, id_price_per_night, status FROM Rooms WHERE id = @roomId FOR UPDATE";
                roomCommand.Parameters.Add(HolidayMakerDb.P("@roomId", roomId));
                using var reader = roomCommand.ExecuteReader();
                if (!reader.Read())
                    throw new InvalidOperationException("Rummet hittades inte.");
                roomCapacity = reader.GetInt32(0);
                pricePerNight = reader.GetDecimal(1);
                // status är NULL-tillåten: både NULL och 0 betyder att
                // rummet är tillgängligt, bara 1 betyder bokat.
                roomStatus = reader.IsDBNull(2) ? null : reader.GetInt32(2);
            }

            // 2. Rummet får inte vara flaggat som bokat (status = 1).
            if (roomStatus == 1)
                throw new InvalidOperationException("Rummet är redan bokat.");

            // 3. Antalet personer får aldrig överstiga rummets kapacitet.
            if (occupants > roomCapacity)
                throw new InvalidOperationException($"Rummet rymmer högst {roomCapacity} personer.");

            // 4. Kontrollera överlappande bokningar. Det halvöppna
            //    intervallet [start, end) gör att utcheckning och
            //    incheckning samma dag inte räknas som dubbelbokning.
            using (var overlapCommand = connection.CreateCommand())
            {
                overlapCommand.Transaction = transaction;
                overlapCommand.CommandText = """
                    SELECT COUNT(*) FROM Bookings
                    WHERE room_id = @roomId
                      AND start_date < @endDate
                      AND end_date > @startDate
                    FOR UPDATE
                    """;
                overlapCommand.Parameters.Add(HolidayMakerDb.P("@roomId", roomId));
                overlapCommand.Parameters.Add(HolidayMakerDb.P("@startDate", startDate));
                overlapCommand.Parameters.Add(HolidayMakerDb.P("@endDate", endDate));
                // COUNT(*) returnerar alltid exakt ett värde (aldrig null),
                // så null-forgiving-operatorn "!" är säkert här.
                long overlapping = (long)overlapCommand.ExecuteScalar()!;
                if (overlapping > 0)
                    throw new InvalidOperationException(
                        $"Rummet är redan bokat under en del av perioden {startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}.");
            }

            // 5. Nätter = dagar mellan datumen (skillnaden i dagnummer),
            //    totalpris = nätter × pris per natt.
            int nights = endDate.DayNumber - startDate.DayNumber;
            decimal totalPrice = nights * pricePerNight;

            // 6. Sparar bokningen. total_price sparas så att priset
            //    inte ändras om rummets pris senare uppdateras.
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Bookings (customer_id, room_id, start_date, end_date, total_price, number_of_occupant)
                VALUES (@customerId, @roomId, @startDate, @endDate, @totalPrice, @occupants)
                """;
            command.Parameters.Add(HolidayMakerDb.P("@customerId", customerId));
            command.Parameters.Add(HolidayMakerDb.P("@roomId", roomId));
            command.Parameters.Add(HolidayMakerDb.P("@startDate", startDate));
            command.Parameters.Add(HolidayMakerDb.P("@endDate", endDate));
            command.Parameters.Add(HolidayMakerDb.P("@totalPrice", totalPrice));
            command.Parameters.Add(HolidayMakerDb.P("@occupants", occupants));
            command.ExecuteNonQuery();

            // Alla steg klara – committing transaktionen.
            transaction.Commit();

            return new BookingConfirmation((long)command.LastInsertedId, nights, totalPrice);
        }
        catch
        {
            // Vid vilket fel som helst (även egna "rymmer högst N")
            // ångas transaktionen ut så att ingen halvfärdig
            // bokning lämnas kvar.
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Returnerar kundens bokningar som inte är slut (end_date efter
    /// idag), sorterade efter startdatum. Avslutade vistelser kan inte
    /// avbokas och visas därför inte. Används i meny 3 ("Avboka") för
    /// att lista kundens bokningar.
    /// </summary>
    public static List<CustomerBooking> ListBookingsForCustomer(long customerId)
    {
        List<CustomerBooking> bookings = new();
        using var connection = HolidayMakerDb.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.booking_number, h.name, h.location, r.room_number,
                   b.start_date, b.end_date, b.number_of_occupant, b.total_price
            FROM Bookings AS b
            JOIN Rooms AS r ON r.id = b.room_id
            JOIN Hotels AS h ON h.id = r.hotel_id
            WHERE b.customer_id = @customerId
              AND b.end_date > CURDATE()
            ORDER BY b.start_date
            """;
        command.Parameters.Add(HolidayMakerDb.P("@customerId", customerId));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            // reader.Read() läser den aktuella raden; kolumnvärdena
            // plockas ut i den ordning de SELECT:ades.
            bookings.Add(new CustomerBooking(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetDateOnly(4),
                reader.GetDateOnly(5),
                reader.GetInt32(6),
                reader.GetDecimal(7)));
        }
        return bookings;
    }

    /// <summary>
    /// Returnerar ett rums bokningar som inte är slut (end_date efter
    /// idag), sorterade efter startdatum, med kundens namn för varje
    /// bokning. Avslutade vistelser kan inte avbokas och visas därför
    /// inte. Används i meny 3 ("Avboka", val 2) för att se vem som har
    /// bokat rummet.
    /// </summary>
    public static List<RoomBooking> ListBookingsForRoom(long roomId)
    {
        List<RoomBooking> bookings = new();
        using var connection = HolidayMakerDb.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.booking_number, c.name,
                   b.start_date, b.end_date, b.number_of_occupant, b.total_price
            FROM Bookings AS b
            JOIN Customers AS c ON c.id = b.customer_id
            WHERE b.room_id = @roomId
              AND b.end_date > CURDATE()
            ORDER BY b.start_date
            """;
        command.Parameters.Add(HolidayMakerDb.P("@roomId", roomId));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            bookings.Add(new RoomBooking(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetDateOnly(2),
                reader.GetDateOnly(3),
                reader.GetInt32(4),
                reader.GetDecimal(5)));
        }
        return bookings;
    }

    /// <summary>
    /// Avbokar (radera) en bokning. Returnerar true om en rad raderades,
    /// false om ingen bokning med numret fanns.
    /// </summary>
    public static bool DeleteBooking(long bookingNumber)
    {
        using var connection = HolidayMakerDb.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Bookings WHERE booking_number = @bookingNumber";
        command.Parameters.Add(HolidayMakerDb.P("@bookingNumber", bookingNumber));
        return command.ExecuteNonQuery() > 0;
    }
}
