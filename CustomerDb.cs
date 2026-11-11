// CustomerDb.cs – databasskiktets kundrelaterade SQL:
// lista alla kunder, hämta en kunds namn och registrera en ny kund.

namespace Holiday_Maker;

/// <summary>
/// SQL-frågor som rör Customers-tabellen: listar alla kunder, hämtar
/// en kunds namn och registrerar en ny kund. Använder de gemensamma
/// anslutningshjälpmedlen
/// i HolidayMakerDb. Varje SQL-anrop är parameteriserat, så att
/// användartext aldrig kan tolkas som SQL (SQL-injection).
/// </summary>
public static class CustomerDb
{
    /// <summary>
    /// Returnerar alla kunder (id, namn, e-post) sorterade efter namn.
    /// Används i meny 2:s första steg ("Välj kund och sök lediga rum")
    /// och i avbokningsflödet (meny 3).
    /// </summary>
    public static List<Customer> ListCustomers()
    {
        List<Customer> customers = new();
        using var connection = HolidayMakerDb.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, email FROM Customers ORDER BY name";

        // ExecuteReader returnerar en "pekare" som vi stegar genom med Read().
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            // reader.GetInt64(0) m.m. plockar ut värdet i kolumn 0, 1, 2
            // (i den ordning de SELECT:ades).
            customers.Add(new Customer(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2)));
        }
        return customers;
    }

    /// <summary>
    /// Returnerar namnet på kunden med givet id (null om ingen kund
    /// finns med det id:et). Används i avbokningsflödet (meny 3) för
    /// att visa bokningar som kan avbokas.
    /// </summary>
    public static string? GetCustomerName(long customerId)
    {
        using var connection = HolidayMakerDb.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM Customers WHERE id = @id";
        command.Parameters.Add(HolidayMakerDb.P("@id", customerId));
        return (string?)command.ExecuteScalar();
    }

    /// <summary>
    /// Sparar en ny kund och returnerar dess autoinkrementerade id.
    /// Kastar MySqlException (fel 1062) om e-postadressen redan finns.
    /// </summary>
    public static long InsertCustomer(string name, string address, string email, string phone)
    {
        using var connection = HolidayMakerDb.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Customers (name, address, email, phone)
            VALUES (@name, @address, @email, @phone)
            """;
        command.Parameters.Add(HolidayMakerDb.P("@name", name));
        command.Parameters.Add(HolidayMakerDb.P("@address", address));
        command.Parameters.Add(HolidayMakerDb.P("@email", email));
        command.Parameters.Add(HolidayMakerDb.P("@phone", phone));
        command.ExecuteNonQuery();

        // LastInsertedId returnerar det id som databasen precis genererade.
        return (long)command.LastInsertedId;
    }
}
