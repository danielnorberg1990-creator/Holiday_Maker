// Holiday Maker – konsolprogram för en resebyrås bokningssystem.
// Program.cs innehåller bara gränssnittet (huvudmeny + inläsning);
// all kommunikation med MySQL finns samlad i HolidayMakerDb.cs.

using MySqlConnector;

namespace Holiday_Maker;

/// <summary>
/// Konsolgränssnittet (programets "yta"). Klassen innehåller huvudmenyn,
/// de sju menyfunktionerna och ett par hjälpmetoder för inläsning av
/// användarinput. Klassen skriver aldrig SQL själv – alla databasanrop
/// görs via HolidayMakerDb.
/// </summary>
internal static class Program
{
    // Id:t på den kund som användaren just nu har valt (meny 1 eller 2).
    // Null om ingen kund är vald ännu. "long?" (nullable) gör det möjligt
    // att skilja på "ingen vald" (null) och ett faktiskt kund-id.
    private static long? selectedCustomerId;

    // Rummen från den senaste sökningen (meny 3). Meny 4 ("Skapa bokning")
    // använder listan för att erbjuda exakt de rum användaren nyss såg,
    // så att ingen ny sökning behövs.
    private static List<HolidayMakerDb.RoomResult> lastSearchResults = new();

    /// <summary>
    /// Programets startpunkt. Visar huvudmenyn i en oändlig loop,
    /// läser av användarens val och dirigerar det till rätt metod.
    /// Loopen bryts först när användaren väljer 0 (Avsluta).
    /// </summary>
    private static void Main()
    {
        // Låt konsolen skriva svenska tecken (å, ä, ö) i stället för "skräptecken".
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=== Holiday Maker – bokningssystem för resebyrå ===");

        // Testa anslutningen direkt: om databasen inte går att nå är
        // hela menyn värdelös, så avbryt med ett tydligt besked.
        if (!HolidayMakerDb.CheckConnection())
        {
            Console.WriteLine("Kunde inte ansluta till databasen.");
            Console.WriteLine("Kontrollera Server, User Id och Password i HolidayMakerDb.cs.");
            return;
        }

        // Huvudloopen: visa menyn, läs av ett val, utför det, repetera.
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Vad vill du göra?");
            Console.WriteLine("  1. Registrera kund");
            Console.WriteLine("  2. Välj kund");
            Console.WriteLine("  3. Sök lediga rum");
            Console.WriteLine("  4. Skapa bokning");
            Console.WriteLine("  5. Visa kundens bokningar");
            Console.WriteLine("  6. Ändra antal personer i en bokning");
            Console.WriteLine("  7. Avboka");
            Console.WriteLine("  0. Avsluta");

            // Frågar om tills ett giltigt heltal 0–7 skrivs in.
            int menuChoice = ReadInt("Välj: ", 0, 7);

            // Varje menyval anropar en metod; val 0 avslutar programmet.
            switch (menuChoice)
            {
                case 1: RegisterCustomer(); break;
                case 2: ChooseCustomer(); break;
                case 3: SearchRooms(); break;
                case 4: CreateBookingFlow(); break;
                case 5: ShowBookings(); break;
                case 6: ChangeOccupants(); break;
                case 7: CancelBookingFlow(); break;
                case 0:
                    Console.WriteLine("Hej då!");
                    return;
            }
        }
    }

    // ------------------------------------------------------------------
    // 1. Registrera kund
    // ------------------------------------------------------------------
    /// <summary>
    /// Meny 1: frågar efter kundedata (namn, adress, e-post, telefon),
    /// sparar kunden i databasen och sätter den nya kunden som vald
    /// så att man omedelbart kan boka för kunden.
    /// </summary>
    private static void RegisterCustomer()
    {
        Console.WriteLine("--- Registrera ny kund ---");

        // ReadNonEmpty fortsätter fråga tills användaren skrivit något.
        string name = ReadNonEmpty("Namn: ");
        string address = ReadNonEmpty("Adress: ");
        string email = ReadNonEmpty("E-post: ");
        string phone = ReadNonEmpty("Telefon: ");

        try
        {
            long id = HolidayMakerDb.InsertCustomer(name, address, email, phone);
            selectedCustomerId = id; // Ny kund ska omedelbart kunna användas.
            Console.WriteLine($"Kunden {name} är registrerad (id {id}) och nu vald.");
        }
        // Felkod 1062 i MySQL = "Duplicate entry", d.v.s. e-postadressen
        // finns redan (unikt index). Förväntade fel fångas separat med
        // ett tydligt användarbesked i stället för ett tekniskt meddelande.
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            Console.WriteLine("Kunde inte registrera kunden: e-postadressen är redan tagen.");
        }
        // Allt annat (t.ex. avhoppad anslutning) rapporteras som okänt fel.
        catch (Exception ex)
        {
            Console.WriteLine($"Okänt fel: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // 2. Välj kund
    // ------------------------------------------------------------------
    /// <summary>
    /// Meny 2: listar alla kunder i databasen och låter användaren
    /// välja en av dem. Det valda kund-id:t sparas i
    /// <see cref="selectedCustomerId"/> för de följande menyerna.
    /// </summary>
    private static void ChooseCustomer()
    {
        Console.WriteLine("--- Välj kund ---");

        // Hämta kundlistan; vid fel (t.ex. avhoppad anslutning)
        // rapporteras felet och metoden avbryts.
        List<HolidayMakerDb.Customer> customers;
        try
        {
            customers = HolidayMakerDb.ListCustomers();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Okänt fel: {ex.Message}");
            return;
        }

        if (customers.Count == 0)
        {
            Console.WriteLine("Inga kunder finns registrerade.");
            return;
        }

        // Menylista: kunder numreras från 1 så att användaren kan svara med ett nummer.
        for (int i = 0; i < customers.Count; i++)
            Console.WriteLine($"  {i + 1,3}. {customers[i].Name}  <{customers[i].Email}>");

        int choice = ReadInt("Välj kund: ", 1, customers.Count);

        // Användarens val är 1-baserat, listan är 0-baserad – därför "- 1".
        selectedCustomerId = customers[choice - 1].Id;
        Console.WriteLine($"Vald kund: {customers[choice - 1].Name}");
    }

    // ------------------------------------------------------------------
    // 3. Sök lediga rum
    // ------------------------------------------------------------------
    /// <summary>
    /// Meny 3: samlar in sökkriterier – ort, in-/utcheckningsdatum,
    /// antal personer samt valfria extrakrav (pool, restaurang, max
    /// avstånd) – och visar alla rum som är lediga under perioden.
    /// Resultatet sparas i <see cref="lastSearchResults"/> så att
    /// meny 4 kan erbjuda samma rum igen vid bokning.
    /// </summary>
    private static void SearchRooms()
    {
        Console.WriteLine("--- Sök lediga rum ---");

        // Visa vilka orter som finns i databasen som hjälp vid valet.
        List<string> locations;
        try
        {
            locations = HolidayMakerDb.ListLocations();
            if (locations.Count > 0)
                Console.WriteLine($"Tillgängliga orter: {string.Join(", ", locations)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Okänt fel: {ex.Message}");
            return;
        }

        string location = ReadNonEmpty("Ort: ");
        DateOnly start = ReadDate("Incheckningsdatum");
        DateOnly end = ReadDate("Utcheckningsdatum");

        // Utcheckning måste komma efter incheckning – fråga om igen annars.
        while (end <= start)
        {
            Console.WriteLine("Utcheckningsdatumet måste ligga efter incheckningsdatumet.");
            end = ReadDate("Utcheckningsdatum");
        }
        int persons = ReadInt("Antal personer: ", 1, 100);

        // Valfria extrakrav. null/false betyder "kravet ställdes inte"
        // och SQL-frågan släpper då igenom alla hotell.
        bool wantPool = false;
        bool wantRestaurant = false;
        int? maxBeach = null;
        int? maxCity = null;

        if (ReadYesNo("Vill du ange fler filter?"))
        {
            wantPool = ReadYesNo("Pool krävs?");
            wantRestaurant = ReadYesNo("Restaurang krävs?");
            if (ReadYesNo("Maximalt avstånd till strand (meter)?"))
                maxBeach = ReadInt("Meters: ", 1, int.MaxValue);
            if (ReadYesNo("Maximalt avstånd till centrum (meter)?"))
                maxCity = ReadInt("Meters: ", 1, int.MaxValue);
        }

        Console.WriteLine("Sortering: 1 = pris per natt (lägst först), 2 = omdöme (högst först)");
        int sortChoice = ReadInt("Välj sortering: ", 1, 2);

        List<HolidayMakerDb.RoomResult> results;
        try
        {
            // Den egentliga sökningen (SQL) ligger i HolidayMakerDb;
            // här skickas bara parametrarna, null = "inget krav".
            results = HolidayMakerDb.SearchAvailableRooms(
                location, start, end, persons,
                wantPool, wantRestaurant, maxBeach, maxCity, sortByPrice: sortChoice == 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Okänt fel: {ex.Message}");
            return;
        }

        lastSearchResults = results; // Spara för meny 4 (Skapa bokning).

        if (results.Count == 0)
        {
            Console.WriteLine("Inga lediga rum matchade sökningen.");
            return;
        }

        // Skriv ut träffarna som en tabell med fasta kolumnbredder.
        Console.WriteLine();
        Console.WriteLine($"Lede {results.Count} rum:");
        Console.WriteLine();
        Console.WriteLine($"  {"#",4}  {"Boende",-24}  {"Ort",-14}  {"Rum",6}  {"Platser",8}  {"Pris/natt",10}  {"Omdöme",6}");
        for (int i = 0; i < results.Count; i++)
        {
            var room = results[i];
            Console.WriteLine($"  {i + 1,4}  {room.HotelName,-24}  {room.Location,-14}  {room.RoomNumber,6}  {room.Capacity,8}  {room.PricePerNight,10:0.00}  {room.Rating,6:0.00}");
        }
        Console.WriteLine();
        Console.WriteLine("Välj ett av rummen via meny 4 (Skapa bokning).");
    }

    // ------------------------------------------------------------------
    // 4. Skapa bokning
    // ------------------------------------------------------------------
    /// <summary>
    /// Meny 4: bokar ett av rummen från den senaste sökningen för den
    /// valda kunden. Om ingen kund är vald eller ingen sökning gjorts
    /// görs det först. Totalpriset beräknas i HolidayMakerDb
    /// (antal nätter × pris per natt).
    /// </summary>
    private static void CreateBookingFlow()
    {
        Console.WriteLine("--- Skapa bokning ---");

        // En kund måste vara vald innan man kan boka – annars erbjuds meny 2.
        // Om användaren avbryter kundvalet finns det ingen att boka för.
        if (selectedCustomerId is null)
            ChooseCustomer();
        if (selectedCustomerId is null)
            return;

        // Ingen sökning gjord ännu? Sök lediga rum först.
        if (lastSearchResults.Count == 0)
        {
            Console.WriteLine("Inga sökresultat finns. Söker lediga rum först...");
            Console.WriteLine();
            SearchRooms();
        }

        // Ännu inga resultat efter sökningen – då finns inget att boka.
        if (lastSearchResults.Count == 0)
            return;

        // Visa rummen från sista sökningen så att användaren kan välja.
        Console.WriteLine();
        Console.WriteLine("Lediga rum från sista sökningen:");
        for (int i = 0; i < lastSearchResults.Count; i++)
        {
            var r = lastSearchResults[i];
            Console.WriteLine($"  {i + 1,3}. {r.HotelName} ({r.Location}), rum {r.RoomNumber}, {r.Capacity} platser, {r.PricePerNight:0.00} kr/natt");
        }

        int choice = ReadInt("Välj rum: ", 1, lastSearchResults.Count);
        HolidayMakerDb.RoomResult room = lastSearchResults[choice - 1];

        DateOnly start = ReadDate("Incheckningsdatum");
        DateOnly end = ReadDate("Utcheckningsdatum");
        while (end <= start)
        {
            Console.WriteLine("Utcheckningsdatumet måste ligga efter incheckningsdatumet.");
            end = ReadDate("Utcheckningsdatum");
        }
        // Antalet personer får aldrig överstiga rummets kapacitet.
        int persons = ReadInt($"Antal personer (rummet rymmer max {room.Capacity}): ", 1, room.Capacity);

        try
        {
            HolidayMakerDb.BookingConfirmation confirmation = HolidayMakerDb.CreateBooking(
                selectedCustomerId.Value, room.RoomId, start, end, persons);

            // Visar ett bekräftelsekvitto med all bokad information.
            Console.WriteLine();
            Console.WriteLine("BOKNING BEKRÄFTAD");
            Console.WriteLine($"  Bokningsnummer: {confirmation.BookingNumber}");
            Console.WriteLine($"  Boende: {room.HotelName}, rum {room.RoomNumber}");
            Console.WriteLine($"  Datum: {start:yyyy-MM-dd} till {end:yyyy-MM-dd} ({confirmation.Nights} natt/er)");
            Console.WriteLine($"  Antal personer: {persons}");
            Console.WriteLine($"  Totalpris: {confirmation.TotalPrice:0.00} kr");
        }
        // InvalidOperationException används för förväntade affärsfel,
        // t.ex. "rummet är upptaget" om någon annan hann boka samtidigt.
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Bokningen kunde inte sparas: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Okänt fel: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // 5. Visa kundens bokningar
    // ------------------------------------------------------------------
    /// <summary>
    /// Meny 5: visar alla bokningar som tillhör den valda kunden.
    /// Bokningsnumrera behövs bl.a. för att ändra (meny 6)
    /// eller avboka (meny 7).
    /// </summary>
    private static void ShowBookings()
    {
        Console.WriteLine("--- Visa kundens bokningar ---");

        // En kund måste vara vald – annars erbjuds meny 2 först.
        if (selectedCustomerId is null)
            ChooseCustomer();
        if (selectedCustomerId is null)
            return;

        List<HolidayMakerDb.CustomerBooking> bookings;
        try
        {
            bookings = HolidayMakerDb.ListBookingsForCustomer(selectedCustomerId.Value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Okänt fel: {ex.Message}");
            return;
        }

        if (bookings.Count == 0)
        {
            Console.WriteLine("Kunden har inga bokningar.");
            return;
        }

        // En rad per bokning: nummer, boende, rum, period, antal personer och pris.
        foreach (var booking in bookings)
        {
            Console.WriteLine($"  Bokning {booking.BookingNumber,3}: {booking.HotelName}, rum {booking.RoomNumber}, " +
                              $"{booking.StartDate:yyyy-MM-dd} till {booking.EndDate:yyyy-MM-dd}, " +
                              $"{booking.Occupants} pers, {booking.TotalPrice:0.00} kr");
        }
    }

    // ------------------------------------------------------------------
    // 6. Ändra antal personer i en bokning
    // ------------------------------------------------------------------
    /// <summary>
    /// Meny 6: ändrar antalet personer i en befintlig bokning.
    /// Bokningen visas först så att användaren kan verifiera att det
    /// är rätt bokning, och det nya talet får aldrig överstiga
    /// rummets kapacitet (kontrollen görs i HolidayMakerDb).
    /// </summary>
    private static void ChangeOccupants()
    {
        Console.WriteLine("--- Ändra antal personer i en bokning ---");

        long bookingNumber = ReadLong("Bokningsnummer: ", 1, long.MaxValue);

        HolidayMakerDb.BookingDetails? booking;
        try
        {
            booking = HolidayMakerDb.GetBooking(bookingNumber);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Okänt fel: {ex.Message}");
            return;
        }

        if (booking is null)
        {
            Console.WriteLine("Ingen bokning med det numret.");
            return;
        }

        // Fråga efter det nya antalet; ReadInt accepterar bara värden
        // upp till rummets kapacitet.
        int newCount = ReadInt($"Nytt antal personer (rummet rymmer max {booking.RoomCapacity}): ", 1, booking.RoomCapacity);

        try
        {
            HolidayMakerDb.UpdateBookingOccupants(bookingNumber, newCount);
            Console.WriteLine($"Antal personer är nu {newCount} för bokning {bookingNumber}.");
        }
        // InvalidOperationException = förväntat affärsfel (t.ex. felaktigt antal).
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Okänt fel: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // 7. Avboka
    // ------------------------------------------------------------------
    /// <summary>
    /// Meny 7: avbokar en bokning efter bokningsnummer. Bokningen
    /// visas först och användaren måste svara "ja" innan raden
    /// raderas – på så sätt kan man inte avboka av misstag.
    /// </summary>
    private static void CancelBookingFlow()
    {
        Console.WriteLine("--- Avboka ---");

        long bookingNumber = ReadLong("Bokningsnummer: ", 1, long.MaxValue);

        HolidayMakerDb.BookingDetails? booking;
        try
        {
            booking = HolidayMakerDb.GetBooking(bookingNumber);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Okänt fel: {ex.Message}");
            return;
        }

        if (booking is null)
        {
            Console.WriteLine("Ingen bokning med det numret.");
            return;
        }

        // Visar vad som ska raderas så att användaren kan verifiera.
        Console.WriteLine($"  {booking.CustomerName}: {booking.HotelName}, rum {booking.RoomNumber}, " +
                          $"{booking.StartDate:yyyy-MM-dd} till {booking.EndDate:yyyy-MM-dd}, {booking.TotalPrice:0.00} kr");

        // Bekräftelsesteg: bara ett "ja" leder till faktisk radering.
        if (!ReadYesNo("Avboka denna bokning?"))
        {
            Console.WriteLine("Avbokningen avbröts.");
            return;
        }

        try
        {
            HolidayMakerDb.DeleteBooking(bookingNumber);
            Console.WriteLine($"Bokning {bookingNumber} är avbokad. Rummet kan nu bokas igen under perioden.");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Okänt fel: {ex.Message}");
        }
    }
    // ------------------------------------------------------------------
    // Inläsningshjälpare – alla metoder i denna sektion frågar om
    // indata upprepade gånger tills ett giltigt värde har skrivits in.
    // ------------------------------------------------------------------

    /// <summary>Läser in en sträng och frågar om tills den är inte-tom.</summary>
    private static string ReadNonEmpty(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            // Trim tar bort mellanslag i början/slut; ?-operatören hanterar
            // att användaren trycker Enter utan att skriva något.
            string? value = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(value))
                return value;
            Console.WriteLine("Värdet får inte vara tomt.");
        }
    }

    /// <summary>Läser in ett heltal inom intervallet [min, max].</summary>
    private static int ReadInt(string prompt, int min, int max)
    {
        while (true)
        {
            Console.Write(prompt);
            // TryParse misslyckas tyst (ingen exception) om inmatningen
            // inte är ett giltigt tal – då meddelas felet och vi frågar igen.
            if (int.TryParse(Console.ReadLine()?.Trim(), out int value) && value >= min && value <= max)
                return value;
            Console.WriteLine($"Ange ett heltal mellan {min} och {max}.");
        }
    }

    /// <summary>Läser in ett större heltal (long) inom intervallet [min, max].</summary>
    private static long ReadLong(string prompt, long min, long max)
    {
        while (true)
        {
            Console.Write(prompt);
            if (long.TryParse(Console.ReadLine()?.Trim(), out long value) && value >= min && value <= max)
                return value;
            Console.WriteLine($"Ange ett heltal mellan {min} och {max}.");
        }
    }

    /// <summary>Läser in ett datum i formatet åååå-mm-dd och frågar om vid fel.</summary>
    private static DateOnly ReadDate(string label)
    {
        while (true)
        {
            Console.Write($"{label} (åååå-mm-dd): ");
            if (DateOnly.TryParse(Console.ReadLine()?.Trim(), out DateOnly value))
                return value;
            Console.WriteLine("Ange datumet i formatet åååå-mm-dd, t.ex. 2026-12-05.");
        }
    }

    /// <summary>
    /// Läser in ett ja/nej-svar. Accepterar även genvägarna
    /// j/n, yes/no och x (för nej).
    /// </summary>
    private static bool ReadYesNo(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt} (ja/nej): ");
            // ToLowerInvariant gör att "JA", "Ja" och "ja" alla accepteras.
            string? answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (answer is "ja" or "j" or "yes" or "y")
                return true;
            if (answer is "nej" or "n" or "no" or "x")
                return false;
            Console.WriteLine("Svara ja eller nej.");
        }
    }
}
