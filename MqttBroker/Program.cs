using MQTTnet;

namespace MqttBroker
{
    internal class Program
    {
        //Need to have client that subscribes to topics and logs messages for testing
        static void Main(string[] args)
        {
            var mqttFactory = new MqttClientFactory();
            using var mqttClient = mqttFactory.CreateMqttClient();
            var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("192.168.10.50").Build();

            mqttClient.ApplicationMessageReceivedAsync += MessageRecived;

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

            var topicFilter = mqttFactory.CreateTopicFilterBuilder().WithTopic("hansenberg/pin").WithAtLeastOnceQoS();

            var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter(topicFilter).Build();

            var response = mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
            response.Wait();

            Console.WriteLine("MQTT client subscribed to topic.");

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();

            mqttClient.UnsubscribeAsync("hansenberg/test/1", CancellationToken.None).Wait();
            mqttClient.DisconnectAsync().Wait();
        }

        private static async Task MessageRecived(MqttApplicationMessageReceivedEventArgs args)
        {
            if (args.ApplicationMessage.Topic == "hansenberg/pin")
            {
                Console.WriteLine("### RECEIVED APPLICATION MESSAGE ###");
                Console.WriteLine($"Topic = {args.ApplicationMessage.Topic}");
                Console.WriteLine($"Message = {args.ApplicationMessage.ConvertPayloadToString()}");
                Console.WriteLine($"Uid = {args.ClientId}");
            }
        }
    }
}
