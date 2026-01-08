using MQTTnet;
using MqttBroker.Managers;
using MqttBroker.Models;
using System.Text.Json;

namespace MqttBroker
{
    internal class Program
    {
        private static DatabaseManager _databaseManager = null!;
        private static PinManagers _pinManager = null!;
        private static CardManager _cardManager = null!;
        private static UserManager _userManager = null!;

        static void Main(string[] args)
        {
            // Initialize database and managers
            InitializeManagers();

            var mqttFactory = new MqttClientFactory();
            using var mqttClient = mqttFactory.CreateMqttClient();
            var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("192.168.10.50").Build();

            mqttClient.ApplicationMessageReceivedAsync += MessageReceived;

            try
            {
                mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None).Wait();
                Console.WriteLine("MQTT client connected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"### CONNECTING FAILED ### {Environment.NewLine} {ex}");
                return;
            }

            // Subscribe to multiple topics
            var topics = new[]
            {
                "hansenberg/pin",
                "hansenberg/card",
                "hansenberg/user"
            };

            foreach (var topic in topics)
            {
                var topicFilter = mqttFactory.CreateTopicFilterBuilder().WithTopic(topic).WithAtLeastOnceQoS();
                var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter(topicFilter).Build();
                
                var response = mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
                response.Wait();
                
                Console.WriteLine($"MQTT client subscribed to topic: {topic}");
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();

            // Unsubscribe from all topics
            foreach (var topic in topics)
            {
                mqttClient.UnsubscribeAsync(topic, CancellationToken.None).Wait();
            }
            
            mqttClient.DisconnectAsync().Wait();
        }

        private static void InitializeManagers()
        {
            try
            {
                _databaseManager = new DatabaseManager();
                _pinManager = new PinManagers();
                _cardManager = new CardManager();
                _userManager = new UserManager();
                
                Console.WriteLine("Database and managers initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize managers: {ex.Message}");
                throw;
            }
        }

        private static async Task MessageReceived(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var message = args.ApplicationMessage.ConvertPayloadToString();
            var clientId = args.ClientId;

            Console.WriteLine("### RECEIVED APPLICATION MESSAGE ###");
            Console.WriteLine($"Topic = {topic}");
            Console.WriteLine($"Message = {message}");
            Console.WriteLine($"Client ID = {clientId}");

            try
            {
                switch (topic)
                {
                    case "hansenberg/pin":
                        await HandlePinMessage(message, clientId);
                        break;
                    case "hansenberg/card":
                        await HandleCardMessage(message, clientId);
                        break;
                    case "hansenberg/user":
                        await HandleUserMessage(message);
                        break;
                    default:
                        Console.WriteLine($"Unhandled topic: {topic}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }

            Console.WriteLine();
        }

        private static async Task HandlePinMessage(string message, string clientId)
        {
            try
            {
                // Expected format: {"Pin":"1234","UniqueID":"device123"} or {"Pin":"1234","UniqueID":"device123","CardID":"12345678"}
                var pinData = JsonSerializer.Deserialize<Dictionary<string, string>>(message);
                
                if (pinData != null && pinData.ContainsKey("Pin") && pinData.ContainsKey("UniqueID"))
                {
                    string cardId;
                    
                    // Check if CardID is provided in the PIN message (for testing/standalone use)
                    if (pinData.ContainsKey("CardID"))
                    {
                        cardId = pinData["CardID"];
                        Console.WriteLine($"Using CardID from PIN message: {cardId}");
                    }
                    else
                    {
                        // Get the latest card entry to find the most recent CardID
                        var latestCard = _cardManager.GetLatestCardEntry();
                        if (latestCard == null)
                        {
                            Console.WriteLine("❌ No CardID provided in message and no card has been scanned.");
                            Console.WriteLine("   Please either:");
                            Console.WriteLine("   1. Include 'CardID' in your PIN message, or");
                            Console.WriteLine("   2. Scan your card first");
                            return;
                        }
                        cardId = latestCard.CardID;
                        Console.WriteLine($"Using CardID from latest scan: {cardId}");
                    }

                    var pinModel = new PinModel
                    {
                        Timestamp = DateTime.UtcNow,
                        Pin = pinData["Pin"],
                        UniqueID = pinData["UniqueID"],
                        CardID = cardId
                    };

                    // Insert pin entry for logging (before validation)
                    var logSuccess = _pinManager.InsertPin(pinModel);
                    Console.WriteLine(logSuccess ? "Pin entry logged." : "Failed to log pin entry.");

                    // First check if user exists in database
                    var user = _userManager.GetUserByCardID(cardId);
                    
                    if (user == null)
                    {
                        Console.WriteLine($"🚫 ACCESS DENIED - Card not registered in system");
                        Console.WriteLine($"   CardID: {cardId}");
                        Console.WriteLine($"   Device: {pinData["UniqueID"]}");
                        Console.WriteLine($"   Reason: User not found in database");
                        Console.WriteLine($"   💡 Tip: Register this card first using the user topic");
                    }
                    else
                    {
                        // User exists, now validate PIN
                        var isValidPin = _userManager.ValidateUser(cardId, pinData["Pin"]);
                        
                        if (isValidPin)
                        {
                            Console.WriteLine($"🔓 ACCESS GRANTED for user: {user.UserName}");
                            Console.WriteLine($"   CardID: {cardId}");
                            Console.WriteLine($"   Device: {pinData["UniqueID"]}");
                        }
                        else
                        {
                            Console.WriteLine($"🔒 ACCESS DENIED - Incorrect PIN");
                            Console.WriteLine($"   User: {user.UserName}");
                            Console.WriteLine($"   CardID: {cardId}");
                            Console.WriteLine($"   Device: {pinData["UniqueID"]}");
                            Console.WriteLine($"   Reason: PIN does not match");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Invalid pin message format. Expected JSON with 'Pin' and 'UniqueID' fields.");
                    Console.WriteLine("Optional: 'CardID' field for standalone testing.");
                    Console.WriteLine("Example: {\"Pin\":\"1234\",\"UniqueID\":\"device123\",\"CardID\":\"12345678\"}");
                }
            }
            catch (JsonException)
            {
                Console.WriteLine("Failed to parse pin message as JSON.");
            }
        }

        private static async Task HandleCardMessage(string message, string clientId)
        {
            try
            {
                // Expected format: {"CardID":"12345678","UniqueID":"device123"}
                var cardData = JsonSerializer.Deserialize<Dictionary<string, string>>(message);
                
                if (cardData != null && cardData.ContainsKey("CardID") && cardData.ContainsKey("UniqueID"))
                {
                    var cardModel = new CardModel
                    {
                        Timestamp = DateTime.UtcNow,
                        CardID = cardData["CardID"],
                        CardName = $"Card-{cardData["CardID"]}" // Default name if not provided
                    };

                    var success = _cardManager.InsertCard(cardModel);
                    if (success)
                    {
                        Console.WriteLine($"💳 Card scanned: {cardModel.CardID}");
                        Console.WriteLine($"   Device: {cardData["UniqueID"]}");
                        
                        // Check if user exists for this card
                        var user = _userManager.GetUserByCardID(cardModel.CardID);
                        if (user != null)
                        {
                            Console.WriteLine($"   User: {user.UserName} - Waiting for PIN entry...");
                        }
                        else
                        {
                            Console.WriteLine($"   ⚠️  Warning: Card not registered in system");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to log card scan.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid card message format. Expected JSON with 'CardID' and 'UniqueID' fields.");
                }
            }
            catch (JsonException)
            {
                Console.WriteLine("Failed to parse card message as JSON.");
            }
        }

        private static async Task HandleUserMessage(string message)
        {
            try
            {
                // Expected format: {"CardID":"12345678","Pin":"1234","UserName":"John Doe"}
                var userData = JsonSerializer.Deserialize<Dictionary<string, string>>(message);
                
                if (userData != null && userData.ContainsKey("CardID") && userData.ContainsKey("Pin"))
                {
                    var userModel = new UserModel
                    {
                        CardID = userData["CardID"],
                        Pin = userData["Pin"],
                        UserName = userData.GetValueOrDefault("UserName", "Unknown User")
                    };

                    var success = _userManager.InsertUser(userModel);
                    Console.WriteLine(success ? $"👤 User registered: {userModel.UserName} (Card: {userModel.CardID})" : "Failed to register user.");
                }
                else
                {
                    Console.WriteLine("Invalid user message format. Expected JSON with 'CardID' and 'Pin' fields.");
                }
            }
            catch (JsonException)
            {
                Console.WriteLine("Failed to parse user message as JSON.");
            }
        }
    }
}
