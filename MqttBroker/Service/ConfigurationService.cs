using MqttBroker.Dtos;

namespace MqttBroker.Service
{
    internal class ConfigurationService
    {
        public TemperaturConfigurationDto GetTemporaryConfiguration()
        {
            return new TemperaturConfigurationDto
            {
                TimeBetweenSample = 10000,
                MaxAllowedTemperatur = 28
            };
        }
    }
}
