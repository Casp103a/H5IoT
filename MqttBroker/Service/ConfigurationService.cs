using MqttBroker.Dtos;
using MqttBroker.Managers;

namespace MqttBroker.Service
{
    internal class ConfigurationService
    {
        private readonly ConfigurationManager configurationManager;
        
        public ConfigurationService()
        {
            this.configurationManager = new();
        }

        public DHTConfigurationDto GetDHTConfiguration()
        {
            var configurations = configurationManager.GetConfigurations();
            return configurations.DHTConfig;
        }

        public CoolingConfigurationDto GetCoolingConfiguration()
        {
            var configurations = configurationManager.GetConfigurations();
            return configurations.CoolingConfig;
        }
    }
}
