-- ============================================================================
-- Holiday_Maker – schema
-- MySQL 8.x. Kör detta skript för att skapa databasen från noll.
-- OBS: skriptet raderar en redan befintlig databas Holiday_Maker.
-- Motsvarar E/R-diagrammet: Hotels, Rooms, Customers, Bookings,
-- Amenities och Hotel_x_Amenities.
-- ============================================================================

DROP DATABASE IF EXISTS Holiday_Maker;

CREATE DATABASE Holiday_Maker
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

USE Holiday_Maker;

-- ----------------------------------------------------------------------------
-- Boenden (hotell, pensionat m.m.)
-- ----------------------------------------------------------------------------
CREATE TABLE Hotels (
    id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name              VARCHAR(255)    NOT NULL,
    location          VARCHAR(255)    NOT NULL,
    distance_to_beach INT             NOT NULL,
    distance_to_city  INT             NOT NULL,
    rating            DECIMAL(8, 2)   NOT NULL,
    PRIMARY KEY (id),
    CONSTRAINT chk_hotels_rating     CHECK (rating >= 1 AND rating <= 5),
    CONSTRAINT chk_hotels_dist_beach CHECK (distance_to_beach >= 0),
    CONSTRAINT chk_hotels_dist_city  CHECK (distance_to_city >= 0)
);

-- ----------------------------------------------------------------------------
-- Serviceutbud (Pool, Restaurang, ...)
-- ----------------------------------------------------------------------------
CREATE TABLE Amenities (
    id   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name VARCHAR(255)    NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY amenities_name_unique (name)
);

-- ----------------------------------------------------------------------------
-- Många-till-många: vilka faciliteter varje boende har
-- ----------------------------------------------------------------------------
CREATE TABLE Hotel_x_Amenities (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    hotel_id     BIGINT UNSIGNED NOT NULL,
    amenities_id BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_hotel_amenity (hotel_id, amenities_id),
    CONSTRAINT fk_hxa_hotel     FOREIGN KEY (hotel_id)     REFERENCES Hotels (id)     ON DELETE CASCADE,
    CONSTRAINT fk_hxa_amenities FOREIGN KEY (amenities_id) REFERENCES Amenities (id) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- Rum inom ett boende
-- ----------------------------------------------------------------------------
CREATE TABLE Rooms (
    id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    id_price_per_night DECIMAL(8, 2)   NOT NULL,
    capacity           INT             NOT NULL,
    hotel_id           BIGINT UNSIGNED NOT NULL,
    room_number        INT             NOT NULL,
    -- 1 = rummet är bokat (visas inte som ledigt, kan inte bokas),
    -- 0 eller NULL = tillgängligt.
    status             TINYINT(1)      NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_hotel_room_number (hotel_id, room_number),
    CONSTRAINT chk_rooms_capacity CHECK (capacity > 0),
    CONSTRAINT chk_rooms_price    CHECK (id_price_per_night > 0),
    CONSTRAINT fk_rooms_hotel     FOREIGN KEY (hotel_id) REFERENCES Hotels (id) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- Kunder
-- ----------------------------------------------------------------------------
CREATE TABLE Customers (
    id      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name    VARCHAR(255)    NOT NULL,
    address VARCHAR(255)    NOT NULL,
    email   VARCHAR(255)    NOT NULL,
    phone   VARCHAR(255)    NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY customers_email_unique (email)
);

-- ----------------------------------------------------------------------------
-- Bokningar: en kund + ett rum + en period.
-- Avbokning görs genom att radera raden – då blir rummet ledigt igen.
-- ----------------------------------------------------------------------------
CREATE TABLE Bookings (
    booking_number     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    customer_id        BIGINT UNSIGNED NOT NULL,
    room_id            BIGINT UNSIGNED NOT NULL,
    start_date         DATE            NOT NULL,
    end_date           DATE            NOT NULL,
    total_price        DECIMAL(8, 2)   NOT NULL,
    number_of_occupant INT             NOT NULL,
    PRIMARY KEY (booking_number),
    CONSTRAINT chk_booking_dates     CHECK (end_date > start_date),
    CONSTRAINT chk_booking_occupants CHECK (number_of_occupant > 0),
    CONSTRAINT fk_bookings_customer  FOREIGN KEY (customer_id) REFERENCES Customers (id),
    CONSTRAINT fk_bookings_room      FOREIGN KEY (room_id)     REFERENCES Rooms (id)
);
