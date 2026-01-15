using MqttBroker.Dtos;

namespace MqttBroker.Models
{
    internal class ConfigurationsModel
    {
        public DHTConfigurationDto DHTConfig { get; set; } = new();
        public CoolingConfigurationDto CoolingConfig { get; set; } = new();
    }
}
