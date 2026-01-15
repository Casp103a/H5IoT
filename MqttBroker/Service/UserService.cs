using MqttBroker.Dtos;
using MqttBroker.Exceptions;
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

        public bool Create(string message)
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
                        UserName = userData.UserName,
                        ExpireDate = userData.ExpireDate,
                        IsActive = true
                    };

                    var success = userManager.InsertUser(userModel);
                    return success;
                }

                return false;
            }
            catch (JsonException)
            {
                Console.WriteLine("Failed to parse user message as JSON.");
                return false;
            }
            catch (CardAlreadyExistException)
            {
                Console.WriteLine("Card ID already exists.");
                throw;
            }
        }

        public bool Deactivate(string cardID)
        {
            var card = JsonSerializer.Deserialize<CardDto>(cardID);
            return userManager.DeactivateUser(card.CardID);
        }
    }
}
