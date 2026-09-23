using Microsoft.EntityFrameworkCore;
using RoghtiaSystem_JahidKawa.Server.Services;
using System.Data.Common;

namespace RoghtiaSystem_JahidKawa.Server.Data
{
    public static class DatabaseInitializer
    {
        public static async Task InitializeAsync(MainDbContext context)
        {
            await context.Database.EnsureCreatedAsync();
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
    }
}
