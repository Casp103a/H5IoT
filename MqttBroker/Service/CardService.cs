using MqttBroker.Dtos;
using MqttBroker.Managers;
using MqttBroker.Models;
using System.Text.Json;

namespace MqttBroker.Service
{
    internal class CardService
    {
        private readonly CardManager cardManager;

        public CardService(DatabaseManager databaseManager)
        {
            cardManager = new CardManager(databaseManager);
        }

        public void Handle(string message, string clientId)
        {
            try
            {
                // Expected format: {"CardID":"12345678","UniqueID":"device123"}
                var cardData = JsonSerializer.Deserialize<CardDto>(message);

                if (cardData != null)
                {
                    var cardModel = new CardModel
                    {
                        Timestamp = DateTime.UtcNow,
                        CardID = cardData.CardID,
                    };

                    var success = cardManager.InsertCard(cardModel);                 
                }
            }
            catch (JsonException)
            {
                Console.WriteLine("Failed to parse card message as JSON.");
            }
        }
    }
}
