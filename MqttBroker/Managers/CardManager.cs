using MqttBroker.Models;
using Microsoft.Data.Sqlite;

namespace MqttBroker.Managers
{
    internal class CardManager
    {
        private readonly DatabaseManager databaseManager;

        public CardManager(DatabaseManager databaseManager)
        {
            this.databaseManager = databaseManager;
        }

        public bool InsertCard(CardModel cardModel)
        {
            if (cardModel == null || string.IsNullOrWhiteSpace(cardModel.CardID))
            {
                return false;
            }

            try
            {
                using var connection = new SqliteConnection(databaseManager.ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText =
                    @"INSERT INTO Card (Timestamp, CardID, CardName) VALUES (@Timestamp, @CardID, @CardName);";
                command.Parameters.AddWithValue("@Timestamp", cardModel.Timestamp.ToString("o"));
                command.Parameters.AddWithValue("@CardID", cardModel.CardID);
                command.Parameters.AddWithValue("@CardName", cardModel.CardName ?? string.Empty);

                int rowsAffected = command.ExecuteNonQuery();

                Console.WriteLine("Inserted card");
                return rowsAffected > 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed while inserted card");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return false;
            }
        }

        public CardModel? GetLatestCardEntry()
        {
            try
            {
                using var connection = new SqliteConnection(databaseManager.ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText =
                    @"SELECT Timestamp, CardID, CardName
                      FROM Card
                      ORDER BY Timestamp DESC
                      LIMIT 1;";

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new CardModel
                    {
                        Timestamp = reader.GetDateTime(0),
                        CardID = reader.GetString(1),
                        CardName = reader.GetString(2)
                    };
                }
            }
            catch
            {
                // Optionally log exception
            }

            return null;
        }
    }
}
