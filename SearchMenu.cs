// SearchMenu.cs – kundval och rumssökning med direktbokning (meny 2).

namespace Holiday_Maker;

/// <summary>
/// Konsolgränssnittet för sökfunktionen (meny 2): användaren väljer
/// först en kund och anger sedan ort, antal personer, check-in och
/// check-out, valfria filter (pool, restaurang, maximalt avstånd
/// till strand respektive centrum) och sortering. Endast rum som är
/// lediga under hela perioden visas, och resultatet visas som en
/// tabell med information om pool, restaurang och avstånd. I slutet
/// kan användaren boka direkt – datumen och antalet personer tas
/// från sökningen, så de behöver aldrig anges på nytt
/// (se BookingMenu.BookRoom).
/// Alla databasanrop görs via SearchDb.
/// </summary>
internal static class SearchMenu
{
    // ------------------------------------------------------------------
    // 2. Välj kund och sök lediga rum
    // -------------------------------------------------
    /// <summary>
    /// Menyval 2: "Välj kund och sök lediga rum". Första steget är
    /// alltid kundvalet (CustomerMenu.ChooseCustomer) – avbryter
    /// användaren där ("b") hamnar de tillbaka i huvudmenyn – varefter
    /// sökningen (SearchRooms) startar för den valda kunden.
    /// </summary>
    public static void ChooseCustomerAndSearchRooms()
    {
        CustomerMenu.ChooseCustomer();
        if (Program.SelectedCustomerId is null)
            return;
        SearchRooms();
    }
    /// <summary>
    /// Leder användaren igenom en rumssökning: ort, antal personer,
    /// check-in och check-out, valfria filter (pool, restaurang,
    /// maximalt avstånd till strand/centrum – Enter hoppas över) och
    /// sortering. Endast rum som är lediga under hela perioden visas;
    /// pool, restaurang och avstånd visas även som information i
    /// resultatlistan. I slutet kan användaren boka direkt –
    /// check-in/check-out och antalet personer tas från sökningen
    /// (se BookingMenu.BookRoom). Sökta datum, antal personer och
    /// sortering sparas i Program (LastSearchStartDate,
    /// LastSearchEndDate och LastSearchOccupants) så att de kan
    /// användas i bokningen.
    /// </summary>
    public static void SearchRooms()
    {
        Console.WriteLine("\n-- Sök lediga rum --");
        Console.WriteLine("  (Tryck b för att gå tillbaka till huvudmenyn)");

        // Ort. Endast de orter som finns i databasen kan väljas – de
        // visas som en numrerad lista i alfabetisk ordning och
        // användaren anger siffran. ReadInt frågar om vid felaktig
        // inmatning (inte ett tal eller utanför intervallet) och
        // "b"/Enter backar till huvudmenyn.
        List<string> locations = SearchDb.ListLocations();
        if (locations.Count == 0)
        {
            Console.WriteLine("Inga orter finns i databasen, så ingen sökning kan göras.");
            return;
        }
        Console.WriteLine("Välj ort:");
        // Visa orterna med löpnummer 1..n (Select lägger till indexen)
        // så att användaren kan välja ort med en siffra.
        foreach (var (i, loc) in locations.Select((l, i) => (i + 1, l)))
            Console.WriteLine($"{i,3} | {loc}");
        string location = locations[ConsoleInput.ReadInt("Ort: ", 1, locations.Count) - 1];

        // Antal personer – rummet måste rymma minst så många. Talet
        // sparas också så att det används som antalet personer i
        // bokningen när ett rum bokas från resultatet – användaren
        // ska inte behöva ange det två gånger.
        int occupants = ConsoleInput.ReadInt("Antal personer: ", 1, 99);
        Program.LastSearchOccupants = occupants;

        // Check-in och check-out: sökningen visar bara rum som är
        // lediga under hela perioden, så datumen anges redan vid
        // sökningen. De sparas i Program och återanvänds i bokningen –
        // användaren behöver aldrig ange datumen på nytt.
        DateOnly startDate = ConsoleInput.ReadDate("Check-in-datum");
        DateOnly endDate = ConsoleInput.ReadDateAfter("Check-out-datum", startDate);
        Program.LastSearchStartDate = startDate;
        Program.LastSearchEndDate = endDate;

        // Valfria filter: tomt svar (Enter) betyder "hoppa över",
        // alltså inget krav.
        bool requirePool = ConsoleInput.ReadYesNoOptional("Kräver boendet pool?");
        bool requireRestaurant = ConsoleInput.ReadYesNoOptional("Kräver boendet restaurang?");
        int? maxBeach = ConsoleInput.ReadIntOptional("Maximalt avstånd till stranden (m)", 1, 100_000);
        int? maxCity = ConsoleInput.ReadIntOptional("Maximalt avstånd till centrum (m)", 1, 100_000);

        // Sortering. 1 = pris per natt (billigast först), 2 = omdöme
        // (bäst först).
        int sortChoice = ConsoleInput.ReadInt("Sortera efter 1 = pris/natt, 2 = omdöme: ", 1, 2);

        List<RoomResult> results = SearchDb.SearchRooms(
            location, occupants, startDate, endDate,
            requirePool, requireRestaurant, maxBeach, maxCity, sortChoice);

        if (results.Count == 0)
        {
            Console.WriteLine($"Inga lediga rum i {location} rymmer {occupants} personer under perioden {startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}.");
            return;
        }

        // Tabell. Pool, restaurang och avstånd visas som information
        // för varje rum i resultatet. Cellerna
        // samlas i en matris med rubrikrad och renderas av Table.Print,
        // som beräknar kolumnbredderna från rubriken och alla rader, så
        // att kolumnerna alltid räcker upp – även om boendenamnen eller
        // orterna är längre än förväntat.
        List<string[]> table = new()
        {
            new[] { "Id", "Boende", "Ort", "Rum", "Platser", "Pris/natt", "Omdöme", "Pool", "Restaurang", "Till strand", "Till centrum" }
        };
        foreach (RoomResult r in results)
            table.Add(new[]
            {
                r.RoomId.ToString(),
                r.HotelName,
                r.Location,
                r.RoomNumber.ToString(),
                r.Capacity.ToString(),
                r.PricePerNight.ToString("0.00"),
                r.Rating.ToString("F1"),
                r.Pool ? "ja" : "nej",
                r.Restaurant ? "ja" : "nej",
                Distance(r.DistanceToBeach),
                Distance(r.DistanceToCity)
            });

        // Vilka kolumner som högerjusteras (tal) respektive
        // vänsterjusteras (text).
        bool[] right = { true, false, false, true, true, true, true, false, false, true, true };

        Console.WriteLine();
        Table.Print(table, right);
        Console.WriteLine($"\n{results.Count} rum hittade.");

        // Sista steget: erbjud direktbokning. Check-in/check-out frågas
        // först när användaren valt ett rum, och antalet personer är
        // det som angavs ovan (se BookingMenu.BookRoom).
        RoomResult room = BookingMenu.PickRoomFromResults(results);
        BookingMenu.BookRoom(room);
    }

    /// <summary>Formaterar ett avstånd i meter; "-" om det saknas.</summary>
    private static string Distance(int? meters) => meters is null ? "-" : $"{meters} m";
}
