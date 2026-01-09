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
        private readonly EncryptionService encryptionService;

        public UserService(DatabaseManager databaseManager)
        {
            userManager = new UserManager(databaseManager);
            cardManager = new CardManager(databaseManager);
            encryptionService = new EncryptionService();
        }

        public void Create(string message)
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
                        Pin = encryptionService.Encrypt(userData.Pin),
                        UserName = userData.UserName,
                        ExpireDate = userData.ExpireDate,
                        IsActive = true
                    };

                    var success = userManager.InsertUser(userModel);
                }
            }
            catch (JsonException)
            {
                Console.WriteLine("Failed to parse user message as JSON.");
            }
        }

        public void Deactivate(string cardID)
        {
            userManager.DeactivateUser(cardID);
        }
    }
}
