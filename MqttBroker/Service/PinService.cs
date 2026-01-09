using MqttBroker.Dtos;
using MqttBroker.Managers;
using MqttBroker.Models;
using MQTTnet;
using System.Text.Json;

namespace MqttBroker.Service
{
    internal class PinService
    {
        private readonly PinManager pinManager;
        private readonly CardManager cardManager;
        private readonly UserManager userManager;
        private readonly EncryptionService encryptionService;
        private readonly IMqttClient mqttClient;

        public PinService(IMqttClient mqttClient, DatabaseManager databaseManager)
        {
            pinManager = new PinManager(databaseManager);
            cardManager = new CardManager(databaseManager);
            userManager = new UserManager(databaseManager);
            this.mqttClient = mqttClient;
            encryptionService = new EncryptionService();
        }

        public void Handle(string message, string clientId)
        {
            try
            {
                // Expected format: {"Pin":"1234","UniqueID":"device123"}
                var pinData = JsonSerializer.Deserialize<PinDto>(message);

                if (pinData != null)
                {
                    // Get the latest card entry to find the most recent CardID
                    var latestCard = cardManager.GetLatestCardEntry();
                    if (latestCard == null)
                    {
                        return;
                    }
                    var cardId = latestCard.CardID;

                    var timestampDiff = DateTime.UtcNow - latestCard.Timestamp;

                    if (timestampDiff.Minutes > 1)
                    {
                        return;
                    }

                    var pinModel = new PinModel
                    {
                        Timestamp = DateTime.UtcNow,
                        Pin = encryptionService.Encrypt(pinData.Pin),
                        UniqueID = pinData.UniqueID,
                        CardID = cardId
                    };

                    // Insert pin entry for logging (before validation)
                    var logSuccess = pinManager.InsertPin(pinModel);
                    Console.WriteLine(logSuccess ? "Pin entry logged." : "Failed to log pin entry.");

                    // First check if user exists in database
                    var user = userManager.GetUserByCardID(cardId);

                    if (user != null)
                    {
                        // User exists, now validate PIN
                        var isValidPin = userManager.ValidateUser(user, cardId, pinData.Pin);

                        if (isValidPin)
                        {
                            //Send unlock to door;
                            var unlockMessage = new MqttApplicationMessageBuilder()
                                .WithTopic("hansenberg/door/unlock")
                                .WithPayload("UNLOCK")
                                .Build();

                            mqttClient.PublishAsync(unlockMessage).Wait();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                Console.WriteLine("Failed to parse pin message as JSON.");
            }
        }

    }
}
