using Microsoft.Data.Sqlite;
using MqttBroker.Models;

namespace MqttBroker.Managers
{
    internal class AlertManager
    {
        private readonly DatabaseManager databaseManager;

        public AlertManager(DatabaseManager databaseManager)
        {
            this.databaseManager = databaseManager;
        }

        public bool InsertAlert(AlertModel alert)
        {

            if (alert == null || string.IsNullOrWhiteSpace(alert.DeviceID) || string.IsNullOrWhiteSpace(alert.Message) || string.IsNullOrWhiteSpace(alert.AlertType))
            {
                return false;
            }

            try
            {
                using var connection = new SqliteConnection(databaseManager.ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText =
                    @"INSERT INTO Alerts (Timestamp, AlertType, DeviceID, Description) VALUES (@Timestamp, @AlertType, @DeviceID, @Description);";
                command.Parameters.AddWithValue("@Timestamp", alert.Timestamp);
                command.Parameters.AddWithValue("@AlertType", alert.AlertType);
                command.Parameters.AddWithValue("@DeviceID", alert.DeviceID);
                command.Parameters.AddWithValue("@Description", alert.Message);

                int rowsAffected = command.ExecuteNonQuery();

                Console.WriteLine("Inserted Alert");

                return rowsAffected > 0;
            }
            catch
            {
                Console.WriteLine("Failed while inserting alert");

                return false;
            }

        }
    }
}
