using MqttBroker.Models;
using System.Text.Json;

namespace MqttBroker.Managers
{
    internal class ConfigurationManager
    {
        private const string ConfigFilePath = "data/configuration.json";
        
        public ConfigurationManager()
        {
            SetupFile();
        }

        private void SetupFile()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    File.Create(ConfigFilePath).Close();

                    var json = JsonSerializer.Serialize(new ConfigurationsModel());
                    File.WriteAllText(ConfigFilePath, json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed with creating config file: {ex.Message}");
                return;
            }
        }

        public ConfigurationsModel GetConfigurations()
        {
            var json = File.ReadAllText(ConfigFilePath);
            var configurations = JsonSerializer.Deserialize<ConfigurationsModel>(json);
            return configurations ?? new ConfigurationsModel();
        }
    }
}
