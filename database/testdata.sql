-- ============================================================================
-- Holiday_Maker – testdata
-- Kör schema.sql först. Skriptet är idempotent (rensar och lägger in på nytt).
--
-- Innehåll (uppfyller uppgiftens krav):
--   * 5 boenden på 3 orter (Ystad, Helsingborg, Åre)
--   * 20 rum med kapacitet 1, 2, 4 och 6
--   * 4 faciliteter: Pool, Restaurang, Fitness, Parkering
--   * 5 kunder
--   * 25 bokningar i december 2026, varierande datum och längd
--     (inga dubbelbokningar – testdatan är giltig)
--
-- total_price = antal nätter (end_date - start_date) x pris per natt.
-- ============================================================================

USE Holiday_Maker;

-- Rensa befintlig testdata (ordning: döttertabeller först).
SET FOREIGN_KEY_CHECKS = 0;
TRUNCATE TABLE Bookings;
TRUNCATE TABLE Hotel_x_Amenities;
TRUNCATE TABLE Rooms;
TRUNCATE TABLE Customers;
TRUNCATE TABLE Hotels;
TRUNCATE TABLE Amenities;
SET FOREIGN_KEY_CHECKS = 1;

-- ----------------------------------------------------------------------------
-- Boenden
-- ----------------------------------------------------------------------------
INSERT INTO Hotels (id, name, location, distance_to_beach, distance_to_city, rating) VALUES
    (1, 'Strandhotell Ystad',   'Ystad',       200,   800, 4.70),
    (2, 'Pensionat Vid Hamnen', 'Ystad',       900,   450, 4.10),
    (3, 'Stadscentrum Hotell',  'Helsingborg', 6500,  100, 3.80),
    (4, 'Fjällpensionatet',     'Åre',         25000, 300, 4.90),
    (5, 'Lilla Gården',         'Åre',         18000, 1200, 3.20);

-- ----------------------------------------------------------------------------
-- Faciliteter och vilka boenden som har dem
-- ----------------------------------------------------------------------------
INSERT INTO Amenities (id, name) VALUES
    (1, 'Pool'),
    (2, 'Restaurang'),
    (3, 'Fitness'),
    (4, 'Parkering');

INSERT INTO Hotel_x_Amenities (id, hotel_id, amenities_id) VALUES
    (1,  1, 1),   -- Strandhotell Ystad: Pool
    (2,  1, 2),   -- Strandhotell Ystad: Restaurang
    (3,  1, 3),   -- Strandhotell Ystad: Fitness
    (4,  2, 2),   -- Pensionat Vid Hamnen: Restaurang
    (5,  3, 1),   -- Stadscentrum Hotell: Pool
    (6,  3, 2),   -- Stadscentrum Hotell: Restaurang
    (7,  3, 4),   -- Stadscentrum Hotell: Parkering
    (8,  4, 1),   -- Fjällpensionatet: Pool
    (9,  4, 3),   -- Fjällpensionatet: Fitness
    (10, 5, 4);   -- Lilla Gården: Parkering

-- ----------------------------------------------------------------------------
-- Rum (20 st, kapacitet 1/2/4/6, varierande priser)
-- ----------------------------------------------------------------------------
INSERT INTO Rooms (id, id_price_per_night, capacity, hotel_id, room_number) VALUES
    (1,  1450.00, 2, 1, 101),
    (2,  1500.00, 2, 1, 102),
    (3,  2200.00, 4, 1, 103),
    (4,   900.00, 1, 1, 104),
    (5,  2400.00, 4, 1, 201),
    (6,   750.00, 1, 2, 11),
    (7,  1100.00, 2, 2, 12),
    (8,  1800.00, 4, 2, 13),
    (9,  1150.00, 2, 2, 14),
    (10, 1200.00, 2, 3, 201),
    (11, 1900.00, 4, 3, 202),
    (12,  850.00, 1, 3, 203),
    (13, 2800.00, 6, 3, 204),
    (14, 1250.00, 2, 3, 205),
    (15, 2600.00, 4, 4, 301),
    (16, 3400.00, 6, 4, 302),
    (17, 1700.00, 2, 4, 303),
    (18,  800.00, 1, 5, 101),
    (19, 2000.00, 4, 5, 102),
    (20, 1300.00, 2, 5, 103);

-- ----------------------------------------------------------------------------
-- Kunder
-- ----------------------------------------------------------------------------
INSERT INTO Customers (id, name, address, email, phone) VALUES
    (1, 'Anna Lind',       'Storgatan 1, 211 31 Malmö',        'anna.lind@example.com',     '070-123 45 67'),
    (2, 'Bengt Svensson',  'Kyrkogatan 8, 252 22 Helsingborg', 'b.svensson@example.com',    '070-234 56 78'),
    (3, 'Carla Nilsson',   'Parkvägen 15, 839 71 Åre',         'carla.nilsson@example.com', '070-345 67 89'),
    (4, 'David Johansson', 'Strandvägen 4, 247 28 Ystad',      'd.johansson@example.com',   '070-456 78 90'),
    (5, 'Emma Eriksson',   'Långgatan 22, 585 38 Linköping',   'emma.eriksson@example.com', '070-567 89 01');

-- ----------------------------------------------------------------------------
-- Bokningar (25 st i december 2026)
-- Notera:
--   * Bokning 2 (10–15/12) och bokning 4 (15–20/12) gäller samma rum:
--     utcheckning och incheckning samma dag är tillåtet.
--   * Bokning 5 (5–20/12) är en lång bokning – perfekt för att testa att
--     en kortare period som helt omsluts av den nekas.
--   * Ystad har inget 6-personersrum – sökning på 6 personer ger tomt resultat.
-- ----------------------------------------------------------------------------
INSERT INTO Bookings (booking_number, customer_id, room_id, start_date, end_date, total_price, number_of_occupant) VALUES
    ( 1,  1,  1, '2026-12-01', '2026-12-05',  5800.00, 2),
    ( 2,  2,  1, '2026-12-10', '2026-12-15',  7250.00, 1),
    ( 3,  3,  2, '2026-12-10', '2026-12-15',  7500.00, 2),
    ( 4,  4,  2, '2026-12-15', '2026-12-20',  7500.00, 2),
    ( 5,  5,  3, '2026-12-05', '2026-12-20', 33000.00, 4),
    ( 6,  1,  3, '2026-12-21', '2026-12-28', 15400.00, 3),
    ( 7,  2,  4, '2026-12-03', '2026-12-06',  2700.00, 1),
    ( 8,  3,  5, '2026-12-18', '2026-12-22',  9600.00, 2),
    ( 9,  4,  6, '2026-12-07', '2026-12-12',  3750.00, 1),
    (10,  5,  7, '2026-12-01', '2026-12-03',  2200.00, 1),
    (11,  1,  8, '2026-12-11', '2026-12-14',  5400.00, 4),
    (12,  2,  9, '2026-12-12', '2026-12-14',  2300.00, 2),
    (13,  3,  9, '2026-12-14', '2026-12-17',  3450.00, 1),
    (14,  4, 10, '2026-12-20', '2026-12-24',  4800.00, 2),
    (15,  5, 11, '2026-12-04', '2026-12-09',  9500.00, 3),
    (16,  1, 12, '2026-12-25', '2026-12-27',  1700.00, 1),
    (17,  2, 13, '2026-12-02', '2026-12-08', 16800.00, 5),
    (18,  3, 14, '2026-12-10', '2026-12-11',  1250.00, 1),
    (19,  4, 15, '2026-12-01', '2026-12-10', 23400.00, 4),
    (20,  5, 16, '2026-12-01', '2026-12-05', 13600.00, 6),
    (21,  1, 16, '2026-12-20', '2026-12-28', 27200.00, 4),
    (22,  2, 17, '2026-12-11', '2026-12-13',  3400.00, 2),
    (23,  3, 18, '2026-12-18', '2026-12-21',  2400.00, 1),
    (24,  4, 19, '2026-12-22', '2026-12-26',  8000.00, 2),
    (25,  5, 20, '2026-12-27', '2026-12-31',  5200.00, 2);

