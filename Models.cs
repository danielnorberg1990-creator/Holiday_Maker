// Models.cs – record-typer som bär på data som läses ur databasen.
// Samlade i en egen fil så att både databasskiktet och menyn kan
// använda samma typer utan att behöva känna till SQL-kolumnordningen.

namespace Holiday_Maker;

/// <summary>En kund ur Customers-tabellen.</summary>
public sealed record Customer(long Id, string Name, string Email);

/// <summary>
/// Ett sökträffat rum med tillhörande hotellinformation. Utöver
/// sökfilter (pris, omdöme, kapacitet) bär den på informationen som
/// visas i resultatlistan – pool, restaurang samt avstånd till strand
/// och stadskärna. RoomId används som nyckel när användaren väljer
/// rummet för direktbokning i sökningen (meny 2).
/// </summary>
public sealed record RoomResult(
    long RoomId, string HotelName, string Location,
    int RoomNumber, int Capacity, decimal PricePerNight, decimal Rating,
    bool Pool, bool Restaurant, int? DistanceToBeach, int? DistanceToCity);

/// <summary>Bekräftelse på en nyss sparad bokning.</summary>
public sealed record BookingConfirmation(long BookingNumber, int Nights, decimal TotalPrice);

/// <summary>
/// En bokning, så som den listas för kunden vid avbokning (meny 3) och
/// vid ändring av antal personer (meny 4). Capacity är rummets kapacitet
/// – den visas i listan och är den övre gränsen för antalet personer i
/// meny 4.
/// </summary>
public sealed record CustomerBooking(
    long BookingNumber, string HotelName, string Location, int RoomNumber,
    DateOnly StartDate, DateOnly EndDate, int Occupants, int Capacity, decimal TotalPrice);

/// <summary>
/// Ett rum med tillhörande boende, så som det listas i avbokningsmenyn
/// (meny 3, val 2 – se vem som har bokat rummet).
/// </summary>
public sealed record RoomInfo(long RoomId, string HotelName, string Location, int RoomNumber);

/// <summary>
/// Ett boende (hotell), så som det listas i avbokningsmenyn (meny 3,
/// val 2) som första valet, innan användaren väljer rum.
/// </summary>
public sealed record HotelInfo(long HotelId, string Name, string Location);

/// <summary>
/// En bokning, så som den listas per rum vid avbokning (meny 3, val 2).
/// Bär på kundens namn eftersom rummet redan är valt.
/// </summary>
public sealed record RoomBooking(
    long BookingNumber, string CustomerName,
    DateOnly StartDate, DateOnly EndDate, int Occupants, decimal TotalPrice);

