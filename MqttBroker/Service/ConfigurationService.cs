using MqttBroker.Dtos;

namespace MqttBroker.Service
{
    internal class ConfigurationService
    {
        public DHTConfigurationDto GetTemporaryConfiguration()
        {
            return new DHTConfigurationDto
            {
                TimeBetweenSample = 10000,
                MaxAllowedTemperatur = 26,
                MaxAllowedHumidity = 60
            };
        }
    }
}
