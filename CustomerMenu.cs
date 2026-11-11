// CustomerMenu.cs – menyfunktioner som handlar om kunder:
// registrera kund (meny 1) och välj kund (första steget i meny 2
// "Välj kund och sök lediga rum"; används även i avbokningsflödet,
// meny 3).

using MySqlConnector;

namespace Holiday_Maker;

/// <summary>
/// Konsolgränssnittet för de kundrelaterade menyfunktionerna:
/// registrera kund (meny 1) och välj kund (första steget i meny 2;
/// används även i avbokningsflödet). Klassen skriver
/// aldrig SQL själv – alla databasanrop görs via CustomerDb.
/// </summary>
internal static class CustomerMenu
{
    // ------------------------------------------------------------------
    // 1. Registrera kund
    // -------------------------------------------------
    /// <summary>
    /// Leder användaren igenom registrering av en ny kund genom att
    /// fråga efter namn, adress, e-post och telefon. Varje fält
    /// valideras: ett tomt svar eller ogiltiga tecken ger ett
    /// felmeddelande och användaren kan skriva om raden (se
    /// ConsoleInput). Om e-postadressen redan finns (UNIQUE-violation,
    /// fel 1062) fångas felet och ett tydligt meddelande visas. Den
    /// nya kunden blir inte automatiskt vald – en kund väljs alltid
    /// med "Välj kund" (meny 2, eller avbokningsväg 1), som frågar
    /// efter valet varje gång.
    /// </summary>
    public static void RegisterCustomer()
    {
        Console.WriteLine("\n-- Registrera kund --");
        Console.WriteLine("  (Tryck b för att gå tillbaka till huvudmenyn)");
        // Varje fält valideras: tomt svar eller ogiltiga tecken ger
        // ett felmeddelande och användaren kan skriva om raden.
        // "b" backar till huvudmenyn när som helst.
        string name = ConsoleInput.ReadName("Namn: ");
        string address = ConsoleInput.ReadAddress("Adress: ");
        string email = ConsoleInput.ReadEmail("E-post: ");
        string phone = ConsoleInput.ReadPhone("Telefon: ");

        try
        {
            long newId = CustomerDb.InsertCustomer(name, address, email, phone);
            // Den nya kunden blir medvetet INTE vald: kundvalet
            // (Program.SelectedCustomerId) ändras bara av "Välj kund",
            // så att menyval 1 aldrig väljer en kund.
            Console.WriteLine($"Kunden {name} har registrerats (id {newId}).");
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            // Fel 1062 = dubblett i en UNIQUE-nyckel. Enda UNIQUE-nyckeln
            // i Customers utöver id är email, så vi vet att det är e-posten.
            Console.WriteLine($"E-posten {email} är redan registrerad. Kunden skapades inte.");
        }
    }

    // ------------------------------------------------------------------
    // 2. Välj kund
    // -------------------------------------------------
    /// <summary>
    /// Listar alla kunder som en komplett tabell (nr, id, namn och
    /// e-post) och låter användaren välja en genom att skriva in
    /// dess nummer. Den valda kunden sparas i
    /// Program.SelectedCustomerId.
    /// </summary>
    public static void ChooseCustomer()
    {
        Console.WriteLine("\n-- Välj kund --");
        Console.WriteLine("  (Tryck b för att gå tillbaka till huvudmenyn)");
        List<Customer> customers = CustomerDb.ListCustomers();
        if (customers.Count == 0)
        {
            Console.WriteLine("Det finns inga kunder. Registrera en först (meny 1).");
            return;
        }

        // Komplett tabell med kolumnrubriker, så att det är tydligt
        // vad varje kolumn innehåller: Nr är det nummer man skriver
        // in för att välja kunden, Id är kundens nummer i databasen,
        // sedan följer namn och e-post.
        List<string[]> table = new()
        {
            new[] { "Nr", "Id", "Namn", "E-post" }
        };
        // n++ är "post-increment": uttrycket får det gamla värdet och
        // n ökar först efteråt – så blir första raden nummer 1.
        int n = 1;
        foreach (Customer customer in customers)
            table.Add(new[] { n++.ToString(), customer.Id.ToString(), customer.Name, customer.Email });

        Console.WriteLine();
        Table.Print(table, new[] { true, true, false, false });

        int choice = ConsoleInput.ReadInt("Välj kund: ", 1, customers.Count);
        var selected = customers[choice - 1];
        Program.SelectedCustomerId = selected.Id;
        Console.WriteLine($"{selected.Name} är nu vald.");
    }
}
