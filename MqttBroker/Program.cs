using MqttBroker.Managers;
using MqttBroker.Service;
using MQTTnet;

namespace MqttBroker
{
    internal class Program
    {
        private static DatabaseManager databaseManager = null!;
        private static PinService pinService = null!;
        private static CardService cardService = null!;
        private static UserService userService = null!;

        static void Main(string[] args)
        {
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
                "hansenberg/user/register",
            };

            foreach (var topic in topics)
            {
                var topicFilter = mqttFactory.CreateTopicFilterBuilder().WithTopic(topic).WithAtLeastOnceQoS();
                var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter(topicFilter).Build();

                var response = mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
                response.Wait();

                Console.WriteLine($"MQTT client subscribed to topic: {topic}");
            }

            // Initialize database and managers
            InitializeManagers(mqttClient);

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();

            // Unsubscribe from all topics
            foreach (var topic in topics)
            {
                mqttClient.UnsubscribeAsync(topic, CancellationToken.None).Wait();
            }

            mqttClient.DisconnectAsync().Wait();
        }

        private static void InitializeManagers(IMqttClient mqttClient)
        {
            try
            {
                databaseManager = new DatabaseManager();
                userService = new UserService(databaseManager);
                pinService = new PinService(mqttClient, databaseManager);
                cardService = new CardService(databaseManager);

                Console.WriteLine("Database and managers initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize managers: {ex.Message}");
                throw;
            }
        }

        private static Task MessageReceived(MqttApplicationMessageReceivedEventArgs args)
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
                        pinService.Handle(message, clientId);
                        break;
                    case "hansenberg/card":
                        cardService.Handle(message, clientId);
                        break;
                    case "hansenberg/user/register":
                        userService.HandleUserMessage(message);
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

            return Task.CompletedTask;
        }
    }
}
