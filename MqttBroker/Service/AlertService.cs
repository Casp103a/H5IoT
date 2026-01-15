using MqttBroker.Dtos;
using MqttBroker.Managers;
using MqttBroker.Models;
using System.Text.Json;

namespace MqttBroker.Service
{
    internal class AlertService
    {
        private readonly AlertManager alertManager;

        public AlertService(DatabaseManager databaseManager)
        {
            alertManager = new AlertManager(databaseManager);
        }

        public void Handle(string message, string clientID)
        {
            try
            {
                var alertData = JsonSerializer.Deserialize<AlertDto>(message);

                if (alertData != null)
                {
                    var alertModel = new AlertModel
                    {
                        Timestamp = DateTime.UtcNow,
                        AlertType = alertData.AlertType,
                        Message = alertData.Message,
                        DeviceID = alertData.DeviceID
                    };

                    var success = alertManager.InsertAlert(alertModel);

                    if (success)
                    {
                        Console.WriteLine($"Alert from DeviceID {alertModel.DeviceID} inserted successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to insert alert from DeviceID {alertModel.DeviceID}.");
                    }
                }
            }
            catch (JsonException)
            {
                Console.WriteLine("Failed to parse card message as JSON.");
            }
        }
    }
}
