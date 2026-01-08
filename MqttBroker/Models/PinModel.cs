namespace MqttBroker.Models
{
    internal class PinModel
    {
        public DateTime Timestamp { get; set; }
        public string Pin { get; set; }
        public string UniqueID { get; set; }
        public string CardID { get; set; }
    }
}
