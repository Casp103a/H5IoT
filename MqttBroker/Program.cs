using MqttBroker.Exceptions;
using MqttBroker.Managers;
using MqttBroker.Service;
using MQTTnet;
using System.Text.Json;

namespace MqttBroker
{
    internal class Program
    {
        private static DatabaseManager databaseManager = null!;
        private static PinService pinService = null!;
        private static CardService cardService = null!;
        private static UserService userService = null!;
        private static ConfigurationService configurationService = null!;
        private static AlertService alertService = null!;

        private static IMqttClient mqtt;
        private static readonly ManualResetEventSlim ShutdownEvent = new(false);
        private static readonly ManualResetEventSlim WaitingForInternet = new(false);
        private static Task WaitForInternetTask;

        static void Main(string[] args)
        {
            Action actiontask = () =>
            {
                while (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    Console.WriteLine("Waiting for internet connection...");
                    Thread.Sleep(1000);
                }
                Console.WriteLine("Internet connection available.");
                WaitingForInternet.Set();
            };

            WaitForInternetTask = new Task(actiontask);
            WaitForInternetTask.Start();
            WaitingForInternet.Wait();

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
                "hansenberg/card/deactivate",
                "hansenberg/user/register",
                "hansenberg/configuration/dht/get",
                "hansenberg/configuration/fan/get",
                "hansenberg/configuration/door/get",
                "hansenberg/system/time/get",
                "hansenberg/alert",
                "hansenberg/dht"
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
            mqtt = mqttClient;

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Shutdown signal received.");
                ShutdownEvent.Set();
                eventArgs.Cancel = true; // Prevent immediate process termination
            };

            Console.WriteLine("Broker running. Press Ctrl+C to exit or stop the container.");

            ShutdownEvent.Wait();

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
                configurationService = new ConfigurationService();
                userService = new UserService(databaseManager);
                pinService = new PinService(mqttClient, databaseManager);
                cardService = new CardService(databaseManager);
                alertService = new AlertService(databaseManager);

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
                    case "hansenberg/card/deactivate":
                        var isDeactived = userService.Deactivate(message);
                        if (isDeactived)
                            Console.WriteLine("Card deactivated successfully.");
                        else
                            Console.WriteLine("Failed to deactivate card.");
                        break;
                    case "hansenberg/user/register":
                        bool failed = false;
                        string errorMessage = string.Empty;

                        try
                        {
                            var result = userService.Create(message);
                            if (!result)
                            {
                                failed = true;
                                errorMessage = "User registration failed due to unknown error.";
                            }
                        }
                        catch (CardAlreadyExistException)
                        {
                            failed = true;
                            errorMessage = "Card already exists.";
                        }

                        if (failed)
                        {
                            Console.WriteLine("User registration failed.");
                            var mqttErrorMessage = new MqttApplicationMessageBuilder()
                                .WithTopic("hansenberg/user/failed")
                                .WithPayload(errorMessage)
                                .Build();

                            mqtt.PublishAsync(mqttErrorMessage).Wait();
                            Console.WriteLine("Success message published.");
                        }
                        else
                        {
                            Console.WriteLine("User registration succeeded.");
                            var mqttSuccessMessage = new MqttApplicationMessageBuilder()
                                .WithTopic("hansenberg/user/success")
                                .WithPayload("Success")
                                .Build();

                            mqtt.PublishAsync(mqttSuccessMessage).Wait();
                            Console.WriteLine("Success message published.");
                        }

                        break;
                    case "hansenberg/configuration/dht/get":
                        Console.WriteLine("Got asked for config");
                        var config = configurationService.GetDHTConfiguration();
                        var payload = JsonSerializer.Serialize(config);

                        var publishMessage = new MqttApplicationMessageBuilder()
                            .WithTopic("hansenberg/configuration/dht")
                            .WithPayload(payload)
                            .Build();

                        mqtt.PublishAsync(publishMessage, CancellationToken.None).Wait();
                        Console.WriteLine("Sent config.");
                        break;
                    case "hansenberg/configuration/fan/get":
                        Console.WriteLine("Got asked for config");
                        var coolingConfig = configurationService.GetCoolingConfiguration();
                        var json = JsonSerializer.Serialize(coolingConfig);

                        var publishConfigMessage = new MqttApplicationMessageBuilder()
                            .WithTopic("hansenberg/configuration/fan")
                            .WithPayload(json)
                            .Build();

                        mqtt.PublishAsync(publishConfigMessage, CancellationToken.None).Wait();
                        Console.WriteLine("Sent config.");
                        break;
                    case "hansenberg/configuration/door/get":
                        Console.WriteLine("Got asked for config");
                        var doorConfig = configurationService.GetCoolingConfiguration();
                        var doorjson = JsonSerializer.Serialize(doorConfig);

                        var publishDoorConfigMessage = new MqttApplicationMessageBuilder()
                            .WithTopic("hansenberg/configuration/door")
                            .WithPayload(doorjson)
                            .Build();

                        mqtt.PublishAsync(publishDoorConfigMessage, CancellationToken.None).Wait();
                        Console.WriteLine("Sent config.");
                        break;
                    case "hansenberg/system/time/get":
                        var currentTime = DateTime.Now;
                        var timeMessage = new MqttApplicationMessageBuilder()
                            .WithTopic("hansenberg/system/time")
                            .WithPayload(currentTime.ToString("o"))
                            .Build();
                        mqtt.PublishAsync(timeMessage, CancellationToken.None).Wait();
                        break;
                    case "hansenberg/alert":
                        alertService.Handle(message, clientId);
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
