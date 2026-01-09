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
                    @"INSERT INTO Users (CardID, Pin, UserName) VALUES (@CardID, @Pin, @UserName);";
                command.Parameters.AddWithValue("@CardID", userModel.CardID);
                command.Parameters.AddWithValue("@Pin", userModel.Pin);
                command.Parameters.AddWithValue("@UserName", userModel.UserName ?? string.Empty);

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
                    @"SELECT CardID, Pin, UserName
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
                        UserName = reader.GetString(2)
                    };
                }
            }
            catch
            {
                // Optionally log exception
            }

            return null;
        }

        public bool ValidateUser(string cardID, string pin)
        {
            if (string.IsNullOrWhiteSpace(cardID) || string.IsNullOrWhiteSpace(pin))
                return false;

            try
            {
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
    }
}
