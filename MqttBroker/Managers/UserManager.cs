using MqttBroker.Models;
using Microsoft.Data.Sqlite;

namespace MqttBroker.Managers
{
    internal class UserManager
    {
        private readonly DatabaseManager databaseManager;

        public UserManager(DatabaseManager databaseManager)
        {
            this.databaseManager = databaseManager;
        }

        public bool InsertUser(UserModel userModel)
        {
            if (userModel == null || string.IsNullOrWhiteSpace(userModel.CardID) || string.IsNullOrWhiteSpace(userModel.Pin))
            {
                return false;
            }

            try
            {
                using var connection = new SqliteConnection(databaseManager.ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText =
                    @"INSERT INTO Users (CardID, Pin, UserName, ExpireDate, IsActive) VALUES (@CardID, @Pin, @UserName, @ExpireDate, @IsActive);";
                command.Parameters.AddWithValue("@CardID", userModel.CardID);
                command.Parameters.AddWithValue("@Pin", userModel.Pin);
                command.Parameters.AddWithValue("@UserName", userModel.UserName ?? string.Empty);
                command.Parameters.AddWithValue("@ExpireDate", userModel.ExpireDate.HasValue ? userModel.ExpireDate.Value : DBNull.Value);
                command.Parameters.AddWithValue("@IsActive", userModel.IsActive);

                int rowsAffected = command.ExecuteNonQuery();

                Console.WriteLine("Inserted user");

                return rowsAffected > 0;
            }
            catch
            {
                Console.WriteLine("Failed while inserted user");

                return false;
            }
        }

        public UserModel? GetUserByCardID(string cardID)
        {
            if (string.IsNullOrWhiteSpace(cardID))
                return null;

            try
            {
                using var connection = new SqliteConnection(databaseManager.ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText =
                    @"SELECT CardID, Pin, UserName, ExpireDate, IsActive
                      FROM Users
                      WHERE CardID = @CardID
                      LIMIT 1;";
                command.Parameters.AddWithValue("@CardID", cardID);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new UserModel
                    {
                        CardID = reader.GetString(0),
                        Pin = reader.GetString(1),
                        UserName = reader.GetString(2),
                        ExpireDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        IsActive = reader.GetBoolean(4)
                    };
                }
            }
            catch
            {
                // Optionally log exception
            }

            return null;
        }

        public bool ValidateUser(UserModel user, string cardID, string pin)
        {
            if (string.IsNullOrWhiteSpace(cardID) || string.IsNullOrWhiteSpace(pin) || user == null)
                return false;

            try
            {
                if (!user.IsActive)
                    return false;

                if (user.ExpireDate < DateTime.Now)
                {
                    DeactivateUser(cardID);
                    return false;
                }


                using var connection = new SqliteConnection(databaseManager.ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText =
                    @"SELECT COUNT(1)
                      FROM Users
                      WHERE CardID = @CardID AND Pin = @Pin;";
                command.Parameters.AddWithValue("@CardID", cardID);
                command.Parameters.AddWithValue("@Pin", pin);

                var result = command.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }
            catch
            {
                return false;
            }
        }

        public bool DeactivateUser(string cardID)
        {
            if (string.IsNullOrWhiteSpace(cardID))
                return false;
            try
            {
                using var connection = new SqliteConnection(databaseManager.ConnectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    @"UPDATE Users
                      SET IsActive = 0
                      WHERE CardID = @CardID;";
                command.Parameters.AddWithValue("@CardID", cardID);
                int rowsAffected = command.ExecuteNonQuery();
                return rowsAffected > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
