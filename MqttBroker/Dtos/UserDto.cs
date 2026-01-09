namespace MqttBroker.Dtos
{
    public class UserDto
    {
        public string Pin { get; set; }
        public string UserName { get; set; }
        public DateTime? ExpireDate { get; set; }
    }
}
