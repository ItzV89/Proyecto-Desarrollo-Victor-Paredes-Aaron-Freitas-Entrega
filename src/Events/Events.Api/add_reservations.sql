CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127183535_AddOrganizerKeycloakId') THEN
    CREATE TABLE "Events" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "Date" timestamp with time zone NOT NULL,
        "OrganizerKeycloakId" text,
        CONSTRAINT "PK_Events" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127183535_AddOrganizerKeycloakId') THEN
    CREATE TABLE "Scenarios" (
        "Id" uuid NOT NULL,
        "EventId" uuid NOT NULL,
        "Name" text NOT NULL,
        CONSTRAINT "PK_Scenarios" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127183535_AddOrganizerKeycloakId') THEN
    CREATE TABLE "Seats" (
        "Id" uuid NOT NULL,
        "ScenarioId" uuid NOT NULL,
        "Code" text NOT NULL,
        "IsAvailable" boolean NOT NULL,
        "LockOwner" uuid,
        "LockExpiresAt" timestamp with time zone,
        CONSTRAINT "PK_Seats" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Seats_Scenarios_ScenarioId" FOREIGN KEY ("ScenarioId") REFERENCES "Scenarios" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127183535_AddOrganizerKeycloakId') THEN
    CREATE INDEX "IX_Seats_ScenarioId" ON "Seats" ("ScenarioId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127183535_AddOrganizerKeycloakId') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251127183535_AddOrganizerKeycloakId', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127192815_AddScenariosAndSeats') THEN
    CREATE INDEX "IX_Scenarios_EventId" ON "Scenarios" ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127192815_AddScenariosAndSeats') THEN
    ALTER TABLE "Scenarios" ADD CONSTRAINT "FK_Scenarios_Events_EventId" FOREIGN KEY ("EventId") REFERENCES "Events" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127192815_AddScenariosAndSeats') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251127192815_AddScenariosAndSeats', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127193115_AddSeatTypeAndPrice') THEN
    ALTER TABLE "Seats" ADD "Price" numeric(10,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127193115_AddSeatTypeAndPrice') THEN
    ALTER TABLE "Seats" ADD "Type" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127193115_AddSeatTypeAndPrice') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251127193115_AddSeatTypeAndPrice', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127194009_AddEventDetails') THEN
    ALTER TABLE "Events" ADD "Description" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127194009_AddEventDetails') THEN
    ALTER TABLE "Events" ADD "EventType" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127194009_AddEventDetails') THEN
    ALTER TABLE "Events" ADD "Place" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127194009_AddEventDetails') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251127194009_AddEventDetails', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127210915_AddReservations') THEN
    CREATE TABLE "Reservations" (
        "Id" uuid NOT NULL,
        "UserKeycloakId" text NOT NULL,
        "EventId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "Status" text NOT NULL,
        CONSTRAINT "PK_Reservations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127210915_AddReservations') THEN
    CREATE TABLE "ReservationSeat" (
        "Id" uuid NOT NULL,
        "ReservationId" uuid NOT NULL,
        "SeatId" uuid NOT NULL,
        "Code" text NOT NULL,
        "Type" text,
        "Price" numeric(10,2) NOT NULL,
        "ReservationId1" uuid,
        CONSTRAINT "PK_ReservationSeat" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ReservationSeat_Reservations_ReservationId" FOREIGN KEY ("ReservationId") REFERENCES "Reservations" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ReservationSeat_Reservations_ReservationId1" FOREIGN KEY ("ReservationId1") REFERENCES "Reservations" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127210915_AddReservations') THEN
    CREATE INDEX "IX_ReservationSeat_ReservationId" ON "ReservationSeat" ("ReservationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127210915_AddReservations') THEN
    CREATE INDEX "IX_ReservationSeat_ReservationId1" ON "ReservationSeat" ("ReservationId1");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127210915_AddReservations') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251127210915_AddReservations', '8.0.0');
    END IF;
END $EF$;
COMMIT;

