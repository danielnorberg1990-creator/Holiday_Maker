// SearchDb.cs – databasskiktets sökrelaterade SQL:
// listar orter och söker lediga rum.

namespace Holiday_Maker;

/// <summary>
/// SQL-frågor för rummen: listar befintliga orter, listar boenden
/// samt rum per boende (används i avbokningsmenyn) och hittar
/// lediga rum som matchar användarens kriterier. Använder de
/// gemensamma anslutningshjälpmedlen i HolidayMakerDb.
/// </summary>
public static class SearchDb
{
    /// <summary>
    /// Hämtar alla unika orter i Hotels-tabellen, sorterade i svensk
    /// alfabetisk ordning (å, ä, ö efter z). Används för att visa den
    /// numrerade ortlistan i sökningen (meny 2), där användaren väljer
    /// ort genom att ange siffran.
    /// </summary>
    public static List<string> ListLocations()
    {
        List<string> locations = new();
        using var connection = HolidayMakerDb.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT location FROM Hotels ORDER BY location COLLATE utf8mb4_sv_0900_ai_ci";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            locations.Add(reader.GetString(0));
        return locations;
    }

    /// <summary>
    /// Listar alla boenden (hotell) som har minst ett rum, med ort,
    /// sorterade på boendens namn. Används i avbokningsmenyn (meny 3,
    /// val 2) som första steget: användaren väljer boende och ser sedan
    /// bara rummen på det boendet, istället för en lång lista över alla
    /// rum i databasen.
    /// </summary>
    public static List<HotelInfo> ListHotels()
    {
        List<HotelInfo> hotels = new();
        using var connection = HolidayMakerDb.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT h.id, h.name, h.location
            FROM Hotels AS h
            JOIN Rooms AS r ON r.hotel_id = h.id
            ORDER BY h.name COLLATE utf8mb4_sv_0900_ai_ci
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
            hotels.Add(new HotelInfo(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2)));
        return hotels;
    }

    /// <summary>
    /// Listar alla rum på angivet boende, sorterade på rumnummer.
    /// Används i avbokningsmenyn (meny 3, val 2) som andra steget,
    /// efter att användaren valt boende – listan innehåller alltså
    /// bara rummen på det valda boendet, inte hela databasen.
    /// </summary>
    public static List<RoomInfo> ListRoomsForHotel(long hotelId)
    {
        List<RoomInfo> rooms = new();
        using var connection = HolidayMakerDb.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.id, h.name, h.location, r.room_number
            FROM Rooms AS r
            JOIN Hotels AS h ON h.id = r.hotel_id
            WHERE r.hotel_id = @hotelId
            ORDER BY r.room_number
            """;
        command.Parameters.Add(HolidayMakerDb.P("@hotelId", hotelId));
        using var reader = command.ExecuteReader();
        while (reader.Read())
            rooms.Add(new RoomInfo(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)));
        return rooms;
    }

    /// <summary>
    /// Söker efter alla rum på angiven ort som rymmer angivet antal
    /// personer. Sökningen frågar inte efter period, pool, restaurang
    /// eller avstånd – istället returneras pool, restaurang samt
    /// avstånd till strand/centrum som information för varje rum så
    /// att de kan visas i resultatlistan (se SearchMenu).
    /// Rum som är flaggat som bokat (status = 1 i Rooms) exkluderas –
    /// de är inte lediga; 0 eller NULL räknas som tillgängligt.
    /// Tillgängligheten för en viss period kontrolleras först när
    /// användaren bokar rummet (se BookingDb.CreateBooking).
    /// Resultatet sorteras på pris per natt eller omdöme.
    /// </summary>
    public static List<RoomResult> SearchRooms(
        string location, int occupants, int sortBy)
    {
        // Grunderfrågan: förenar Rooms med Hotels (pris och omdöme ligger
        // i olika tabeller) och tilläggar pool, restaurang samt avstånden
        // som information för varje rum.
        //
        // EXISTS (SELECT 1 ...) ger 1 om boendet har faciliteten och
        // annars 0 – ett LEFT JOIN hade kunnat multiplicera rader.
        var baseSql = """
            SELECT r.id, h.name, h.location, r.room_number, r.capacity,
                   r.id_price_per_night, h.rating,
                   EXISTS (SELECT 1
                           FROM Hotel_x_Amenities AS ha
                           JOIN Amenities AS a ON a.id = ha.amenities_id
                           WHERE ha.hotel_id = h.id AND a.name = @poolName) AS has_pool,
                   EXISTS (SELECT 1
                           FROM Hotel_x_Amenities AS ha
                           JOIN Amenities AS a ON a.id = ha.amenities_id
                           WHERE ha.hotel_id = h.id AND a.name = @restaurantName) AS has_restaurant,
                   h.distance_to_beach, h.distance_to_city
            FROM Rooms AS r
            JOIN Hotels AS h ON h.id = r.hotel_id
            WHERE h.location = @location
              AND r.capacity >= @occupants
              -- Rum flaggat som bokat (status = 1) visas inte som ledigt;
              -- 0 eller NULL räknas som tillgängligt.
              AND (r.status IS NULL OR r.status = 0)
            """;

        // Sorteringsordning: 1 = pris per natt (billigast först),
        // 2 = omdöme (bäst först). sortBy är ett tal som användaren
        // anger, men det väljer bara mellan två fasta SQL-fragment –
        // det hamnar aldrig i frågetexten.
        string orderBy = sortBy == 2
            ? "ORDER BY h.rating DESC, r.id_price_per_night ASC"
            : "ORDER BY r.id_price_per_night ASC, h.rating DESC";

        // Kör frågan och samlar resultaten i en lista.
        List<RoomResult> results = new();
        using var connection = HolidayMakerDb.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = baseSql + "\n" + orderBy;
        command.Parameters.Add(HolidayMakerDb.P("@location", location));
        command.Parameters.Add(HolidayMakerDb.P("@occupants", occupants));
        command.Parameters.Add(HolidayMakerDb.P("@poolName", "Pool"));
        command.Parameters.Add(HolidayMakerDb.P("@restaurantName", "Restaurang"));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            // reader.Read() läser den aktuella raden; kolumnvärdena
            // plockas ut i den ordning de SELECT:ades. Avstånden är
            // NULL-tillåtna i schemat – läs dem som null om de saknas
            // så att menyn kan visa "-".
            results.Add(new RoomResult(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                reader.GetInt32(7) == 1,
                reader.GetInt32(8) == 1,
                reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.IsDBNull(10) ? null : reader.GetInt32(10)));
        }
        return results;
    }
}
