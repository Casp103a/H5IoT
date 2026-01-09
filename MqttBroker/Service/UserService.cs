using MqttBroker.Dtos;
using MqttBroker.Managers;
using MqttBroker.Models;
using System.Text.Json;

namespace MqttBroker.Service
{
    internal class UserService
    {
        private readonly UserManager userManager;
        private readonly CardManager cardManager;

        public UserService(DatabaseManager databaseManager)
        {
            userManager = new UserManager(databaseManager);
            cardManager = new CardManager(databaseManager);
        }

        public void HandleUserMessage(string message)
        {
            try
            {
                // Expected format: {"CardID":"12345678","Pin":"1234","UserName":"John Doe"}
                var userData = JsonSerializer.Deserialize<UserDto>(message);
                var latestCard = cardManager.GetLatestCardEntry();

                if (userData != null && latestCard != null)
                {
                    var userModel = new UserModel
                    {
                        CardID = latestCard.CardID,
                        Pin = userData.Pin,
                        UserName = userData.UserName
                    };

                    var success = userManager.InsertUser(userModel);
                }
            }
            catch (JsonException)
            {
                Console.WriteLine("Failed to parse user message as JSON.");
            }
        }
    }
}
