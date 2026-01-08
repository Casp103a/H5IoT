using MqttBroker.Dtos;
using MQTTnet;
using System.Text.Json;
using System.Windows;

namespace UserRegistaionApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var userName = UsernameTextBox.Text;
            var pin = PinTextBox.Text;
            var confirmPin = ConfirmationPinTextBox.Text;

            if (!pin.Equals(confirmPin))
            {
                return;
            }

            var mqttFactory = new MqttClientFactory();
            using var mqttClient = mqttFactory.CreateMqttClient();
            var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("192.168.10.50").Build();
           
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

            var payload = JsonSerializer.Serialize(new UserDto
            {
                Pin = pin,
                UserName = userName
            });

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("hansenberg/user/register")
                .WithPayload(payload)
                .Build();

            mqttClient.PublishAsync(message, CancellationToken.None).Wait();
        }

        private void PinTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void PinTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string)); if (!text.All(char.IsDigit)) e.CancelCommand();
            }
            else 
            { 
                e.CancelCommand();
            }
        }
    }
}