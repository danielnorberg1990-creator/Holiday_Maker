// BookingMenu.cs – menyfunktioner som handlar om bokningar:
// avboka (meny 3), ändra antal personer (meny 4) samt de delade
// bokningsstegen som används vid direktbokning i rumssökningen
// (meny 2).

namespace Holiday_Maker;

/// <summary>
/// Konsolgränssnittet för de bokningsrelaterade menyfunktionerna:
/// avboka (meny 3), med två vägar – via kund eller via rum, och ändra
/// antalet personer på en bokning (meny 4). Klassen innehåller även de
/// delade bokningsstegen (val av rum + bokning) som rumssökningen
/// (meny 2) använder för direktbokning.
/// Klassen skriver aldrig SQL själv – alla databasanrop görs via
/// databasskiktet (BookingDb och SearchDb).
/// </summary>
internal static class BookingMenu
{
    // ------------------------------------------------------------------
    // Bokningsstegen nedan används vid direktbokningen i meny 2
    // ("Välj kund och sök lediga rum").
    // -------------------------------------------------
    /// <summary>
    /// Visar de funna rummen som numrerad lista och låter användaren
    /// välja ett rum att boka. "b"/Enter kastas som BackToMenuException.
    /// </summary>
    internal static RoomResult PickRoomFromResults(List<RoomResult> results)
    {
        Console.WriteLine("\nVill du boka ett av rummen?");
        // Numrera rummen 1..n (Select lägger till en index på varje
        // element) så att användaren kan peka på dem med en siffra.
        foreach (var (i, r) in results.Select((r, i) => (i + 1, r)))
            Console.WriteLine($"{i,3} | {r.HotelName}, {r.Location}, rum {r.RoomNumber}, {r.Capacity} platser, {r.PricePerNight:0.00} kr/natt"); 

        int choice = ConsoleInput.ReadInt("Välj rum att boka: ", 1, results.Count);
        return results[choice - 1];
    }

    /// <summary>
    /// Skapar en bokning för ett valt rum. Check-in- och check-out-
    /// datum samt antalet personer är de som angavs vid den sökning
    /// rummet kom ifrån (Program.LastSearchStartDate, LastSearchEndDate
    /// och LastSearchOccupants) – sökningen visar ju bara rum som är
    /// lediga under just den perioden och som rymmer antalet personer,
    /// så användaren behöver aldrig ange dem på nytt. (Om BookRoom nås
    /// utan att en sökning körts frågas datumen och antalet personer
    /// istället nu.) Kapacitet och dubbelbokning valideras i databasen
    /// (se BookingDb.CreateBooking).
    /// </summary>
    internal static void BookRoom(RoomResult room)
    {
        // Bokningen behöver en kund – starta "Välj kund" om ingen är vald.
        if (Program.SelectedCustomerId is null)
        {
            CustomerMenu.ChooseCustomer();
            if (Program.SelectedCustomerId is null)
                return;
        }
        long customerId = Program.SelectedCustomerId.Value;

        // Antalet personer är redan känt från den sökning rummet kom
        // ifrån (sökningen returnerar bara rum som rymmer minst så
        // många det efterfrågas), så det frågas inte på nytt. (Om ingen sökning körts
        // finns inget tal sparat – då frågas det istället nu.)
        int occupants = Program.LastSearchOccupants;
        if (occupants < 1)
        {
            occupants = ConsoleInput.ReadInt("Antal personer: ", 1, 99);
            Program.LastSearchOccupants = occupants;
        }

        // Check-in och check-out angavs redan vid sökningen (meny 2) –
        // sökningen visar ju bara rum som är lediga under just den
        // perioden – så datumen tas direkt från Program. (Om BookRoom
        // nås utan att en sökning körts frågas datumen istället.)
        DateOnly startDate = Program.LastSearchStartDate
            ?? ConsoleInput.ReadDate("Check-in-datum");
        DateOnly endDate = Program.LastSearchEndDate
            ?? ConsoleInput.ReadDateAfter("Check-out-datum", startDate);

        Console.WriteLine($"\nRum {room.RoomNumber} på {room.HotelName}, {room.Location} är valt ({occupants} personer, {startDate:yyyy-MM-dd} -> {endDate:yyyy-MM-dd}).");

        try
        {
            // Validering (kapacitet + dubbelbokning) och priset beräknas
            // i databasen i en transaktion.
            BookingConfirmation confirmation = BookingDb.CreateBooking(
                customerId, room.RoomId, startDate, endDate, occupants);

            // Tydlig bekräftelse till användaren, inklusive prissättning.
            Console.WriteLine();
            Console.WriteLine($"Bokning {confirmation.BookingNumber} skapad:");
            Console.WriteLine($"  Rum {room.RoomNumber} på {room.HotelName}, {room.Location}");
            Console.WriteLine($"  {startDate:yyyy-MM-dd} -> {endDate:yyyy-MM-dd} ({confirmation.Nights} nätter)");
            Console.WriteLine($"  {occupants} personer, totalt {confirmation.TotalPrice:0.00} kr");
        }
        catch (InvalidOperationException ex)
        {
            // Förväntade valideringsfel från databasen
            // (dubbelbokning eller "rymmer högst N personer").
            Console.WriteLine(ex.Message);
        }
    }

    // ------------------------------------------------------------------
    // 3. Avboka
    // -------------------------------------------------
    /// <summary>
    /// Avbokningsmenyn. Användaren väljer först vilken väg man vill
    /// använda för att hitta bokningen:
    ///   1. Via kund – visar först kundlistan, sedan den valda
    ///      kundens namn och dennes bokningar (boende, ort, rum,
    ///      check-in/check-out, antal personer, kapacitet och totalpris)
    ///      som en komplett tabell.
    ///   2. Via rum – stegvis navigation med korta listor i varje steg:
    ///      användaren väljer först boende, sedan ett rum på boendet
    ///      och ser sedan rummets bokningar som en komplett tabell.
    /// Endast bokningar som inte är slut (check-outdatum efter idag)
    /// visas. I båda vägarna bekräftas raderingen med "ja" innan den
    /// utförs, så att rummet därefter kan bokas igen under samma period.
    /// </summary>
    public static void CancelBookingFlow()
    {
        Console.WriteLine("\n-- Avboka --");
        Console.WriteLine("  (Tryck b för att gå tillbaka till huvudmenyn)");
        Console.WriteLine("  1. Välj kund – se kundens bokningar");
        Console.WriteLine("  2. Via rum – välj boende och rum, se bokningarna");
        int choice = ConsoleInput.ReadInt("Välj: ", 1, 2);
        if (choice == 1)
            CancelByCustomer();
        else
            CancelByRoom();
    }

    /// <summary>
    /// Avbokningsväg 1: visar först kundlistan så att användaren kan
    /// välja vilken kunds bokningar hon vill se, visar sedan den
    /// valda kundens namn och dennes bokningar som inte är avslutad och
    /// avbokar den valda efter bekräftelse.
    /// </summary>
    private static void CancelByCustomer()
    {
        // Visa alltid kundlistan först, så att användaren kan välja
        // vilken kunds bokningar man vill se – även om en kund redan
        // är vald (t.ex. från en sökning i meny 2). Det tidigare valet
        // rensas innan, så att "b" i kundlistan inte behåller den
        // gamla kundens val.
        Program.SelectedCustomerId = null;
        CustomerMenu.ChooseCustomer();
        if (Program.SelectedCustomerId is null)
            return;
        long customerId = Program.SelectedCustomerId.Value;
        // Hämta kundens namn så att listan nedan visar vilka
        // bokningar det gäller. Namnet kan vara null om kunden inte
        // längre finns – då visas ett fallback-namn i stället.
        string customerName = CustomerDb.GetCustomerName(customerId)
            ?? $"kund {customerId}";

        // Lista kundens bokningar så att användaren kan välja.
        List<CustomerBooking> bookings = BookingDb.ListBookingsForCustomer(customerId);
        if (bookings.Count == 0)
        {
            Console.WriteLine($"Kunden {customerName} har inga kommande bokningar att avboka.");
            return;
        }

        // Gemensam tabell med avbokningen via rum och meny 4 – se
        // PrintCustomerBookings.
        PrintCustomerBookings(customerName, bookings);

        int choice = ConsoleInput.ReadInt("Välj bokning: ", 1, bookings.Count);
        CustomerBooking booking = bookings[choice - 1];

        DeleteWithConfirmation(
            booking.BookingNumber,
            $"bokning {booking.BookingNumber} ({booking.HotelName}, rum {booking.RoomNumber}, {booking.StartDate:yyyy-MM-dd} -> {booking.EndDate:yyyy-MM-dd})");
    }

    /// <summary>
    /// Avbokningsväg 2, i tre steg med korta listor i varje steg
    /// (boende -> rum -> bokning) istället för en lång platt lista
    /// över alla rum i databasen, så att det är tydligt var man är:
    /// 1. Välj boende – bara boenden som har minst ett rum.
    /// 2. Välj rum – bara rummen på det valda boendet.
    /// 3. Välj bokning – rummets bokningar som inte är slut, inklusive
    ///    vem som har bokat. Den valda bokningen avbokas efter
    ///    bekräftelse.
    /// </summary>
    private static void CancelByRoom()
    {
        // Steg 1: välj boende. Endast boenden med minst ett rum visas,
        // så att varje val leder till en rumslista att fortsätta med.
        List<HotelInfo> hotels = SearchDb.ListHotels();
        if (hotels.Count == 0)
        {
            Console.WriteLine("Det finns inga boenden i databasen.");
            return;
        }

        Console.WriteLine("\n-- Välj boende --");
        Console.WriteLine("  (Tryck b för att gå tillbaka till huvudmenyn)");
        List<string[]> hotelTable = new()
        {
            new[] { "Nr", "Boende", "Ort" }
        };
        int n = 1;
        foreach (HotelInfo h in hotels)
            hotelTable.Add(new[] { n++.ToString(), h.Name, h.Location });
        Table.Print(hotelTable, new[] { true, false, false });

        int hotelChoice = ConsoleInput.ReadInt("Välj boende: ", 1, hotels.Count);
        HotelInfo hotel = hotels[hotelChoice - 1];

        // Steg 2: välj rum – bara rummen på det valda boendet.
        List<RoomInfo> rooms = SearchDb.ListRoomsForHotel(hotel.HotelId);
        if (rooms.Count == 0)
        {
            Console.WriteLine($"{hotel.Name} har inga rum i databasen.");
            return;
        }

        Console.WriteLine($"\n-- Välj rum på {hotel.Name} ({hotel.Location}) --");
        Console.WriteLine("  (Tryck b för att gå tillbaka till huvudmenyn)");
        List<string[]> roomTable = new()
        {
            new[] { "Nr", "Rum" }
        };
        n = 1;
        foreach (RoomInfo r in rooms)
            roomTable.Add(new[] { n++.ToString(), $"rum {r.RoomNumber}" });
        Table.Print(roomTable, new[] { true, false });

        int choice = ConsoleInput.ReadInt("Välj rum: ", 1, rooms.Count);
        RoomInfo room = rooms[choice - 1];

        List<RoomBooking> bookings = BookingDb.ListBookingsForRoom(room.RoomId);
        if (bookings.Count == 0)
        {
            Console.WriteLine($"Rum {room.RoomNumber} på {room.HotelName} har inga kommande bokningar.");
            return;
        }

        Console.WriteLine($"\n-- Bokningar för rum {room.RoomNumber} på {room.HotelName} --");
        Console.WriteLine("  (Tryck b för att gå tillbaka till huvudmenyn)");
        List<string[]> table = new()
        {
            new[] { "Nr", "Bokning", "Kund", "Från", "Till", "Personer", "Totalpris" }
        };
        n = 1;
        foreach (RoomBooking b in bookings)
            table.Add(new[]
            {
                n++.ToString(),
                b.BookingNumber.ToString(),
                b.CustomerName,
                b.StartDate.ToString("yyyy-MM-dd"),
                b.EndDate.ToString("yyyy-MM-dd"),
                $"{b.Occupants} pers",
                $"{b.TotalPrice:0.00} kr"
            });
        Table.Print(table, new[] { true, true, false, true, true, true, true });

        int bookingChoice = ConsoleInput.ReadInt("Välj bokning: ", 1, bookings.Count);
        RoomBooking booking = bookings[bookingChoice - 1];

        DeleteWithConfirmation(
            booking.BookingNumber,
            $"bokning {booking.BookingNumber} ({booking.CustomerName}, rum {room.RoomNumber} på {room.HotelName}, {booking.StartDate:yyyy-MM-dd} -> {booking.EndDate:yyyy-MM-dd})");
    }

    /// <summary>
    /// Gemensamt sista steg för båda avbokningsvägarna: en
    /// dubbelkontroll ("ja/nej") så att användaren inte avbokar fel
    /// bokning, och sedan radering av bokningen. Returnerar DeleteBooking
    /// false om bokningen inte längre fanns vid raderingstillfället
    /// (t.ex. om någon annan avbokade i mellan) – då får användaren ett
    /// tydligt meddelande i stället för en falsk bekräftelse.
    /// </summary>
    private static void DeleteWithConfirmation(long bookingNumber, string description)
    {
        if (!ConsoleInput.ReadYesNo($"Avboka {description}?"))
        {
            Console.WriteLine("Avbokningen avbröts.");
            return;
        }

        try
        {
            bool deleted = BookingDb.DeleteBooking(bookingNumber);
            if (!deleted)
            {
                Console.WriteLine(
                    $"Bokning {bookingNumber} fanns inte längre och kunde inte avbokas.");
                return;
            }
            Console.WriteLine($"Bokning {bookingNumber} är avbokad. Rummet kan nu bokas igen under perioden.");
        }
        catch (Exception ex)
        {
            // T.ex. att databasanslutningen tappades i stunden.
            Console.WriteLine($"Okänt fel: {ex.Message}");
        }
    }

    /// <summary>
    /// Gemensam tabell över en kunds bokningar, används i avbokningen
    /// (meny 3, val 1) och i ändringen av antal personer (meny 4).
    /// Kolumnrubriker: Bokning är bokningens nummer, Ort visas för att
    /// flera hotell kan ha samma rumsnummer, Från/Till är
    /// check-in/check-out-datum, Personer är det nuvarande antalet,
    /// Kapacitet är hur många personer rummet rymmer (övre gräns i
    /// meny 4) och Totalpris är priset för hela vistelsen (nätter ×
    /// pris per natt).
    /// </summary>
    private static void PrintCustomerBookings(string customerName, List<CustomerBooking> bookings)
    {
        Console.WriteLine($"\n-- Bokningar för {customerName} --");
        Console.WriteLine("  (Tryck b för att gå tillbaka till huvudmenyn)");
        List<string[]> table = new()
        {
            new[] { "Nr", "Bokning", "Boende", "Ort", "Rum", "Från", "Till", "Personer", "Kapacitet", "Totalpris" }
        };
        int n = 1;
        foreach (CustomerBooking b in bookings)
            table.Add(new[]
            {
                n++.ToString(),
                b.BookingNumber.ToString(),
                b.HotelName,
                b.Location,
                b.RoomNumber.ToString(),
                b.StartDate.ToString("yyyy-MM-dd"),
                b.EndDate.ToString("yyyy-MM-dd"),
                $"{b.Occupants} pers",
                $"{b.Capacity} pers",
                $"{b.TotalPrice:0.00} kr"
            });
        Table.Print(table, new[] { true, true, false, false, true, true, true, true, true, true });
    }

    // ------------------------------------------------------------------
    // 4. Ändra antal personer på en bokning
    // ------------------------------------------------------------------
    /// <summary>
    /// Menyval 4: "Ändra antal personer på en bokning". Användaren
    /// väljer en kund och ser dennes bokningar som inte är avslutade (samma
    /// tabell som i avbokningen, med en kapacitetskolumn), väljer en
    /// bokning och anger ett nytt antal personer. Talet får aldrig
    /// överstiga rummets kapacitet; ändringen sparas i databasen med
    /// UPDATE (se BookingDb.UpdateBookingOccupants).
    /// </summary>
    public static void ChangeOccupantsFlow()
    {
        Console.WriteLine("\n-- Ändra antal personer --");
        Console.WriteLine("  (Tryck b för att gå tillbaka till huvudmenyn)");

        // Visa alltid kundlistan först, så att användaren kan välja
        // vilken kunds bokningar man vill se (samma logik som i
        // avbokningen via kund).
        Program.SelectedCustomerId = null;
        CustomerMenu.ChooseCustomer();
        if (Program.SelectedCustomerId is null)
            return;
        long customerId = Program.SelectedCustomerId.Value;
        string customerName = CustomerDb.GetCustomerName(customerId)
            ?? $"kund {customerId}";

        List<CustomerBooking> bookings = BookingDb.ListBookingsForCustomer(customerId);
        if (bookings.Count == 0)
        {
            Console.WriteLine($"Kunden {customerName} har inga kommande bokningar att ändra.");
            return;
        }

        PrintCustomerBookings(customerName, bookings);

        int choice = ConsoleInput.ReadInt("Välj bokning: ", 1, bookings.Count);
        CustomerBooking booking = bookings[choice - 1];

        // Det nya talet får aldrig överstiga rummets kapacitet (gränsen
        // i frågan), och ändringen kontrolleras även i databasen.
        int newOccupants = ConsoleInput.ReadInt(
            $"Nytt antal personer (1-{booking.Capacity}): ", 1, booking.Capacity);

        if (newOccupants == booking.Occupants)
        {
            Console.WriteLine($"Antalet personer är redan {newOccupants} – ingenting att ändra.");
            return;
        }

        if (!ConsoleInput.ReadYesNo($"Ändra antalet personer på bokning {booking.BookingNumber} från {booking.Occupants} till {newOccupants}?"))
        {
            Console.WriteLine("Ändringen avbröts.");
            return;
        }

        try
        {
            BookingDb.UpdateBookingOccupants(booking.BookingNumber, newOccupants);
            Console.WriteLine($"Bokning {booking.BookingNumber} har nu {newOccupants} personer.");
        }
        catch (InvalidOperationException ex)
        {
            // T.ex. "får inte överstiga rummets kapacitet" eller "ingen
            // bokning med det numret" (t.ex. om den avbokats i mellan).
            Console.WriteLine(ex.Message);
        }
    }
}
