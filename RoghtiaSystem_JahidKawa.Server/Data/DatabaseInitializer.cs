using Microsoft.EntityFrameworkCore;
using RoghtiaSystem_JahidKawa.Server.Services;
using System.Data.Common;

namespace RoghtiaSystem_JahidKawa.Server.Data
{
    public static class DatabaseInitializer
    {
        public static async Task InitializeAsync(MainDbContext context)
        {
            // Fresh databases use the checked-in migrations, so dotnet ef database update
            // and normal startup create the same schema and migration history.
            await context.Database.OpenConnectionAsync();
            bool hasUsers;
            try
            {
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Users')";
                hasUsers = Convert.ToInt32(await command.ExecuteScalarAsync()) != 0;
            }
            finally { await context.Database.CloseConnectionAsync(); }
            if (!hasUsers) await context.Database.MigrateAsync();

            await context.Database.OpenConnectionAsync();
            try
            {
                // Upgrade the starter schema transactionally without removing existing records.
                var connection = context.Database.GetDbConnection();
                await using var transaction = await connection.BeginTransactionAsync();
                var columns = new HashSet<string>();
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "PRAGMA table_info(Users)";
                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
                }
                if (!columns.Contains("NormalizedUserName")) await ExecuteAsync(connection, transaction, "ALTER TABLE Users ADD COLUMN NormalizedUserName TEXT");
                if (!columns.Contains("PasswordVersion")) await ExecuteAsync(connection, transaction, "ALTER TABLE Users ADD COLUMN PasswordVersion INTEGER NOT NULL DEFAULT 0");
                if (!columns.Contains("TokenVersion")) await ExecuteAsync(connection, transaction, "ALTER TABLE Users ADD COLUMN TokenVersion INTEGER NOT NULL DEFAULT 0");
                if (!columns.Contains("PINCode")) await ExecuteAsync(connection, transaction, "ALTER TABLE Users ADD COLUMN PINCode TEXT NOT NULL DEFAULT ''");
                if (!columns.Contains("PINFailedAttempts")) await ExecuteAsync(connection, transaction, "ALTER TABLE Users ADD COLUMN PINFailedAttempts INTEGER NOT NULL DEFAULT 0");
                if (!columns.Contains("PINLockedUntilUtc")) await ExecuteAsync(connection, transaction, "ALTER TABLE Users ADD COLUMN PINLockedUntilUtc TEXT NULL");
                if (!columns.Contains("Role")) await ExecuteAsync(connection, transaction, "ALTER TABLE Users ADD COLUMN Role TEXT NOT NULL DEFAULT 'User'");

                var users = new List<(long Id, string Name)>();
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT Id, UserName FROM Users";
                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) users.Add((reader.GetInt64(0), reader.IsDBNull(1) ? "" : reader.GetString(1)));
                }
                var normalizedNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var user in users)
                {
                    if (!UserNames.IsValid(user.Name) || !normalizedNames.Add(UserNames.Normalize(user.Name)))
                        throw new InvalidOperationException("Existing users contain blank or duplicate normalized usernames. Resolve those records before starting authentication; no records have been removed.");
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE Users SET NormalizedUserName = $name WHERE Id = $id AND (NormalizedUserName IS NULL OR NormalizedUserName <> $name)";
                    var name = command.CreateParameter(); name.ParameterName = "$name"; name.Value = UserNames.Normalize(user.Name); command.Parameters.Add(name);
                    var id = command.CreateParameter(); id.ParameterName = "$id"; id.Value = user.Id; command.Parameters.Add(id);
                    await command.ExecuteNonQueryAsync();
                }
                await ExecuteAsync(connection, transaction, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_NormalizedUserName ON Users (NormalizedUserName)");
                await ExecuteAsync(connection, transaction, """
                    CREATE TABLE IF NOT EXISTS DoctorInformation (
                        LoggedInUserId INTEGER NOT NULL PRIMARY KEY,
                        DoctorNameEnglish TEXT NOT NULL,
                        DoctorNamePashto TEXT NOT NULL,
                        DoctorProfessionPashto TEXT NOT NULL,
                        DoctorProfessionEnglish TEXT NOT NULL,
                        DoctorPhoto BLOB NULL,
                        DoctorLogo BLOB NULL,
                        HospitalLogo BLOB NULL,
                        HospitalNamePashto TEXT NOT NULL,
                        FOREIGN KEY (LoggedInUserId) REFERENCES Users (Id) ON DELETE CASCADE
                    )
                    """);
                await ExecuteAsync(connection, transaction, """
                    CREATE TABLE IF NOT EXISTS Medications (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Type TEXT NOT NULL,
                        Quantity INTEGER NOT NULL CONSTRAINT CK_Medications_Quantity CHECK (Quantity >= 0),
                        Remarks TEXT NOT NULL,
                        UserId INTEGER NOT NULL,
                        FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT
                    )
                    """);
                await ExecuteAsync(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Medications_UserId_Id ON Medications (UserId, Id)");
                await ExecuteAsync(connection, transaction, """
                CREATE TABLE IF NOT EXISTS Patients (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Phone TEXT NOT NULL,
                    Age INTEGER NOT NULL CONSTRAINT CK_Patients_Age CHECK (Age BETWEEN 0 AND 150),
                    Gender TEXT NOT NULL,
                    Address TEXT NOT NULL,
                    UserId INTEGER NOT NULL,
                    FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_Patients_UserId_Code ON Patients (UserId, Code);
                CREATE INDEX IF NOT EXISTS IX_Patients_UserId_Id ON Patients (UserId, Id);
                """);
                var legacyTreatments = await PrepareLegacyTreatmentsAsync(connection, transaction);
                await ExecuteAsync(connection, transaction, """
                CREATE TABLE IF NOT EXISTS ClinicalNotes (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Text TEXT NOT NULL,
                    FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_ClinicalNotes_UserId_Text ON ClinicalNotes (UserId, Text);
                CREATE TABLE IF NOT EXISTS Treatments (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    PatientId INTEGER NOT NULL,
                    RecordedAtUtc TEXT NOT NULL,
                    BloodPressure TEXT NOT NULL,
                    AdditionalInstructions TEXT NOT NULL DEFAULT '',
                    FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT,
                    FOREIGN KEY (PatientId) REFERENCES Patients (Id) ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS IX_Treatments_UserId_RecordedAtUtc ON Treatments (UserId, RecordedAtUtc);
                CREATE INDEX IF NOT EXISTS IX_Treatments_UserId_PatientId ON Treatments (UserId, PatientId);
                CREATE TABLE IF NOT EXISTS PrescriptionItems (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    TreatmentId INTEGER NOT NULL,
                    MedicationId INTEGER NOT NULL,
                    Position INTEGER NOT NULL,
                    MedicationName TEXT NOT NULL,
                    MedicationType TEXT NOT NULL,
                    Dose INTEGER NOT NULL CONSTRAINT CK_PrescriptionItems_Dose CHECK (Dose > 0),
                    StockDeducted INTEGER NOT NULL DEFAULT 0,
                    Frequency TEXT NOT NULL DEFAULT 'Once daily',
                    Duration TEXT NOT NULL,
                    FOREIGN KEY (TreatmentId) REFERENCES Treatments (Id) ON DELETE CASCADE,
                    FOREIGN KEY (MedicationId) REFERENCES Medications (Id) ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_PrescriptionItems_TreatmentId_Position ON PrescriptionItems (TreatmentId, Position);
                CREATE INDEX IF NOT EXISTS IX_PrescriptionItems_MedicationId ON PrescriptionItems (MedicationId);
                CREATE TABLE IF NOT EXISTS TreatmentClinicalNotes (
                    TreatmentId INTEGER NOT NULL,
                    ClinicalNoteId INTEGER NOT NULL,
                    PRIMARY KEY (TreatmentId, ClinicalNoteId),
                    FOREIGN KEY (TreatmentId) REFERENCES Treatments (Id) ON DELETE CASCADE,
                    FOREIGN KEY (ClinicalNoteId) REFERENCES ClinicalNotes (Id) ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS IX_TreatmentClinicalNotes_ClinicalNoteId ON TreatmentClinicalNotes (ClinicalNoteId);
                CREATE TABLE IF NOT EXISTS AdditionalInstructions (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Text TEXT NOT NULL,
                    FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_AdditionalInstructions_UserId_Text ON AdditionalInstructions (UserId, Text);
                CREATE TABLE IF NOT EXISTS TreatmentAdditionalInstructions (
                    TreatmentId INTEGER NOT NULL,
                    AdditionalInstructionId INTEGER NOT NULL,
                    PRIMARY KEY (TreatmentId, AdditionalInstructionId),
                    FOREIGN KEY (TreatmentId) REFERENCES Treatments (Id) ON DELETE CASCADE,
                    FOREIGN KEY (AdditionalInstructionId) REFERENCES AdditionalInstructions (Id) ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS IX_TreatmentAdditionalInstructions_AdditionalInstructionId ON TreatmentAdditionalInstructions (AdditionalInstructionId);
                """);
                var treatmentColumns = await GetColumnsAsync(connection, transaction, "Treatments");
                if (!treatmentColumns.Contains("AdditionalInstructions"))
                    await ExecuteAsync(connection, transaction, "ALTER TABLE Treatments ADD COLUMN AdditionalInstructions TEXT NOT NULL DEFAULT ''");
                var prescriptionColumns = await GetColumnsAsync(connection, transaction, "PrescriptionItems");
                if (!prescriptionColumns.Contains("Dose"))
                    await ExecuteAsync(connection, transaction, "ALTER TABLE PrescriptionItems ADD COLUMN Dose INTEGER NOT NULL DEFAULT 1 CONSTRAINT CK_PrescriptionItems_Dose CHECK (Dose > 0)");
                if (!prescriptionColumns.Contains("StockDeducted"))
                    await ExecuteAsync(connection, transaction, "ALTER TABLE PrescriptionItems ADD COLUMN StockDeducted INTEGER NOT NULL DEFAULT 0");
                if (!prescriptionColumns.Contains("Frequency"))
                    await ExecuteAsync(connection, transaction, "ALTER TABLE PrescriptionItems ADD COLUMN Frequency TEXT NOT NULL DEFAULT 'Once daily'");
                if (prescriptionColumns.Contains("Refills"))
                    await RemoveLegacyPrescriptionRefillsAsync(connection, transaction);
                if (legacyTreatments) await CopyLegacyTreatmentsAsync(connection, transaction);
                await transaction.CommitAsync();
            }
            finally { await context.Database.CloseConnectionAsync(); }
        }

        private static async Task ExecuteAsync(DbConnection connection, DbTransaction transaction, string sql)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<HashSet<string>> GetColumnsAsync(DbConnection connection, DbTransaction transaction, string table)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"PRAGMA table_info({table})";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
            return columns;
        }

        private static async Task RemoveLegacyPrescriptionRefillsAsync(DbConnection connection, DbTransaction transaction)
        {
            await ExecuteAsync(connection, transaction, """
                CREATE TABLE PrescriptionItems_WithoutRefills (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    TreatmentId INTEGER NOT NULL,
                    MedicationId INTEGER NOT NULL,
                    Position INTEGER NOT NULL,
                    MedicationName TEXT NOT NULL,
                    MedicationType TEXT NOT NULL,
                    Dose INTEGER NOT NULL CONSTRAINT CK_PrescriptionItems_Dose CHECK (Dose > 0),
                    StockDeducted INTEGER NOT NULL DEFAULT 0,
                    Frequency TEXT NOT NULL DEFAULT 'Once daily',
                    Duration TEXT NOT NULL,
                    FOREIGN KEY (TreatmentId) REFERENCES Treatments (Id) ON DELETE CASCADE,
                    FOREIGN KEY (MedicationId) REFERENCES Medications (Id) ON DELETE RESTRICT
                );
                INSERT INTO PrescriptionItems_WithoutRefills
                    (Id, TreatmentId, MedicationId, Position, MedicationName, MedicationType, Dose, StockDeducted, Frequency, Duration)
                SELECT Id, TreatmentId, MedicationId, Position, MedicationName, MedicationType, Dose, StockDeducted, Frequency, Duration
                FROM PrescriptionItems;
                DROP TABLE PrescriptionItems;
                ALTER TABLE PrescriptionItems_WithoutRefills RENAME TO PrescriptionItems;
                CREATE UNIQUE INDEX IX_PrescriptionItems_TreatmentId_Position ON PrescriptionItems (TreatmentId, Position);
                CREATE INDEX IX_PrescriptionItems_MedicationId ON PrescriptionItems (MedicationId);
                """);
        }

        private static async Task<bool> PrepareLegacyTreatmentsAsync(DbConnection connection, DbTransaction transaction)
        {
            var treatmentColumns = await GetColumnsAsync(connection, transaction, "Treatments");
            if (treatmentColumns.Count == 0 || treatmentColumns.Contains("RecordedAtUtc")) return false;
            var itemColumns = await GetColumnsAsync(connection, transaction, "PrescriptionItems");
            if (!treatmentColumns.Contains("CreatedAtUtc") || !treatmentColumns.Contains("UserId") ||
                !treatmentColumns.Contains("PatientId") || !itemColumns.Contains("Name") ||
                !itemColumns.Contains("SortOrder") || !itemColumns.Contains("TreatmentId"))
                throw new InvalidOperationException("The existing treatment schema is not recognized. No treatment records were changed.");
            if ((await GetColumnsAsync(connection, transaction, "LegacyTreatments")).Count != 0 ||
                (await GetColumnsAsync(connection, transaction, "LegacyPrescriptionItems")).Count != 0)
                throw new InvalidOperationException("A legacy treatment archive already exists. No treatment records were changed.");

            // SQLite cannot remove the old required columns. Archive both complete tables,
            // then copy their usable data into the new schema in this same transaction.
            await ExecuteAsync(connection, transaction, "ALTER TABLE PrescriptionItems RENAME TO LegacyPrescriptionItems");
            await ExecuteAsync(connection, transaction, "ALTER TABLE Treatments RENAME TO LegacyTreatments");
            return true;
        }

        private static async Task CopyLegacyTreatmentsAsync(DbConnection connection, DbTransaction transaction)
        {
            await ExecuteAsync(connection, transaction, """
                INSERT INTO Treatments (Id, UserId, PatientId, RecordedAtUtc, BloodPressure)
                SELECT old.Id, old.UserId, old.PatientId, old.CreatedAtUtc, ''
                FROM LegacyTreatments old
                JOIN Patients patient ON patient.Id = old.PatientId AND patient.UserId = old.UserId
                """);

            // Legacy prescriptions allowed free-text medicines. Keep each original row in
            // the archive and create an owned medication when it has no valid catalog entry.
            await ExecuteAsync(connection, transaction, """
                INSERT INTO Medications (Name, Type, Quantity, Remarks, UserId)
                SELECT DISTINCT
                    COALESCE(NULLIF(TRIM(item.Name), ''), 'Legacy medication ' || item.Id),
                    COALESCE(NULLIF(TRIM(item.Type), ''), 'Other'),
                    0, '', old.UserId
                FROM LegacyPrescriptionItems item
                JOIN LegacyTreatments old ON old.Id = item.TreatmentId
                JOIN Treatments current ON current.Id = old.Id
                WHERE NOT EXISTS (SELECT 1 FROM Medications valid WHERE valid.Id = item.MedicationId AND valid.UserId = old.UserId)
                  AND NOT EXISTS (
                      SELECT 1 FROM Medications existing
                      WHERE existing.UserId = old.UserId
                        AND existing.Name = COALESCE(NULLIF(TRIM(item.Name), ''), 'Legacy medication ' || item.Id)
                        AND existing.Type = COALESCE(NULLIF(TRIM(item.Type), ''), 'Other'))
                """);
            await ExecuteAsync(connection, transaction, """
                INSERT INTO PrescriptionItems
                    (Id, TreatmentId, MedicationId, Position, MedicationName, MedicationType, Dose, StockDeducted, Duration)
                SELECT item.Id, item.TreatmentId,
                    COALESCE(
                        (SELECT valid.Id FROM Medications valid WHERE valid.Id = item.MedicationId AND valid.UserId = old.UserId),
                        (SELECT owned.Id FROM Medications owned
                         WHERE owned.UserId = old.UserId
                           AND owned.Name = COALESCE(NULLIF(TRIM(item.Name), ''), 'Legacy medication ' || item.Id)
                           AND owned.Type = COALESCE(NULLIF(TRIM(item.Type), ''), 'Other')
                         ORDER BY owned.Id LIMIT 1)),
                    ROW_NUMBER() OVER (PARTITION BY item.TreatmentId ORDER BY item.SortOrder, item.Id),
                    COALESCE(NULLIF(TRIM(item.Name), ''), 'Legacy medication ' || item.Id),
                    COALESCE(NULLIF(TRIM(item.Type), ''), 'Other'),
                    1, 0, item.Duration
                FROM LegacyPrescriptionItems item
                JOIN LegacyTreatments old ON old.Id = item.TreatmentId
                JOIN Treatments current ON current.Id = old.Id
                """);

            foreach (var (column, label) in new[]
            {
                ("Symptoms", "Symptoms"), ("Diagnosis", "Diagnosis"),
                ("Notes", "Notes"), ("PrescriptionNotes", "Prescription instructions")
            })
            {
                if (!(await GetColumnsAsync(connection, transaction, "LegacyTreatments")).Contains(column)) continue;
                await ExecuteAsync(connection, transaction, $"""
                    INSERT OR IGNORE INTO ClinicalNotes (UserId, Text)
                    SELECT old.UserId, '{label}: ' || TRIM(old.{column})
                    FROM LegacyTreatments old
                    JOIN Treatments current ON current.Id = old.Id
                    WHERE TRIM(old.{column}) <> ''
                    """);
                await ExecuteAsync(connection, transaction, $"""
                    INSERT OR IGNORE INTO TreatmentClinicalNotes (TreatmentId, ClinicalNoteId)
                    SELECT old.Id, note.Id
                    FROM LegacyTreatments old
                    JOIN Treatments current ON current.Id = old.Id
                    JOIN ClinicalNotes note ON note.UserId = old.UserId
                        AND note.Text = '{label}: ' || TRIM(old.{column})
                    WHERE TRIM(old.{column}) <> ''
                    """);
            }
        }
    }
}
