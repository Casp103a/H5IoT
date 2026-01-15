namespace MqttBroker.Models
{
    internal class AlertModel
    {
        public int ID { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public string DeviceID { get; set; }
        public string AlertType { get; set; }
    }
}
