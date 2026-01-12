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
            //ExpireDatePicker.DisplayDateStart = DateTime.Today;
        }

        DateTime lastPublish = DateTime.MinValue;
        TimeSpan publishInterval = TimeSpan.FromSeconds(15);
        IMqttClient mqttClient;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var userName = UsernameTextBox.Text;
            var pin = PinPasswordBox.Password;
            var confirmPin = ConfirmationPinPasswordBox.Password;
            var expireDate = ExpireDatePicker.SelectedDate;
            bool shouldExpire = ExpireCheckBox.IsChecked.HasValue ? ExpireCheckBox.IsChecked.Value : false;

            if (!pin.Equals(confirmPin) || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(pin))
            {
                return;
            }

            if (DateTime.Now - lastPublish < publishInterval)
            {
                return;
            }

            var mqttFactory = new MqttClientFactory();
            mqttClient = mqttFactory.CreateMqttClient();
            mqttClient.ApplicationMessageReceivedAsync += MessageReceived;
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

            var topics = new[]
            {
                "hansenberg/user/success",
                "hansenberg/user/failed"
            };

            foreach (var topic in topics)
            {
                var topicFilter = mqttFactory.CreateTopicFilterBuilder().WithTopic(topic).WithAtLeastOnceQoS();
                var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter(topicFilter).Build();

                var response = mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
                response.Wait();
            }

            var payload = JsonSerializer.Serialize(new UserDto
            {
                Pin = pin,
                UserName = userName,
                ExpireDate = shouldExpire ? expireDate : null
            });

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("hansenberg/user/register")
                .WithPayload(payload)
                .Build();

            var result = mqttClient.PublishAsync(message, CancellationToken.None);
            result.Wait();
        }

        private async Task MessageReceived(MqttApplicationMessageReceivedEventArgs args)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                switch (args.ApplicationMessage.Topic)
                {
                    case "hansenberg/user/success":
                        lastPublish = DateTime.Now;
                        UsernameTextBox.Text = string.Empty;
                        PinPasswordBox.Password = string.Empty;
                        ConfirmationPinPasswordBox.Password = string.Empty;
                        ExpireCheckBox.IsChecked = false;
                        ExpireDatePicker.SelectedDate = null;
                        MessageBox.Show("User registered successfully!", "User registration", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    case "hansenberg/user/failed":
                        MessageBox.Show($"Failed to register user, \ndue to: {args.ApplicationMessage.ConvertPayloadToString()}.", "User registration", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    default:
                        break;
                }
            });
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