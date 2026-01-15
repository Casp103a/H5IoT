namespace MqttBroker.Dtos
{
    internal class DHTConfigurationDto
    {
        /// <summary>
        /// It is defined in milliseconds
        /// </summary>
        public int TimeBetweenSample { get; set; } = 10000;

        /// <summary>
        /// The temp in celsius that should not be exceeded
        /// </summary>
        public int MaxAllowedTemperatur { get; set; } = 26;

        /// <summary>
        /// The humidity in percentage that should not be exceeded
        /// </summary>
        public int MaxAllowedHumidity { get; set; } = 55;
    }
}
