namespace MqttBroker.Dtos
{
    internal class TemperaturConfigurationDto
    {
        /// <summary>
        /// It is defined in milliseconds
        /// </summary>
        public int TimeBetweenSample { get; set; }

        /// <summary>
        /// The temp in celsius that should not be exceeded
        /// </summary>
        public int MaxAllowedTemperatur { get; set; }
    }
}
