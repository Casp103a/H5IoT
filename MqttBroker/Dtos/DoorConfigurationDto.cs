namespace MqttBroker.Dtos
{
    internal class DoorConfigurationDto
    {
        public int StartTime { get; set; } = 8;
        public int EndTime { get; set; } = 18;
    }
}
