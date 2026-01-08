using Microsoft.Data.Sqlite;
using MqttBroker.Models;

namespace MqttBroker.Managers
{
    internal class PinManager
    {
        private const string ConnectionString = "Data Source=mqttbroker.db";

        public bool InsertPin(PinModel pin)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                const string insertQuery = @"
                    INSERT INTO Pin (Timestamp, Pin, UniqueID, CardID)
                    VALUES (@Timestamp, @Pin, @UniqueID, @CardID);";

                using var command = new SqliteCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@Timestamp", pin.Timestamp.ToString("o"));
                command.Parameters.AddWithValue("@Pin", pin.Pin);
                command.Parameters.AddWithValue("@UniqueID", pin.UniqueID);
                command.Parameters.AddWithValue("@CardID", pin.CardID);
                
                
                Console.WriteLine("Inserted pin");

                return command.ExecuteNonQuery() == 1;
            }
            catch
            {
                // Optionally log the exception
                Console.WriteLine("Failed while inserted pin");
                return false;
            }
        }

        public PinModel? FindPinBaseOnCardID(string cardID)
        {
            if (string.IsNullOrWhiteSpace(cardID))
            {
                return null;
            }

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                const string selectQuery = @"
                    SELECT Timestamp, Pin, UniqueID, CardID
                    FROM Pin
                    WHERE CardID = @CardID
                    ORDER BY Timestamp DESC
                    LIMIT 1;";

                using var command = new SqliteCommand(selectQuery, connection);
                command.Parameters.AddWithValue("@CardID", cardID);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new PinModel
                    {
                        Timestamp = DateTime.Parse(reader.GetString(0)),
                        Pin = reader.GetString(1),
                        UniqueID = reader.GetString(2),
                        CardID = reader.GetString(3)
                    };
                }
            }
            catch
            {
                // Optionally log the exception
            }

            return null;
        }
    }
}
