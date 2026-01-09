namespace MqttBroker.Models
{
    internal class UserModel
    {
        public string CardID { get; set; }
        public string Pin { get; set; }
        public string UserName { get; set; }
        public DateTime? ExpireDate { get; set; }
        public bool IsActive { get; set; }
    }
}