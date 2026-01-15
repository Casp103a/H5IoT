namespace MqttBroker.Dtos
{
    internal class CoolingConfigurationDto
    {
        /// <summary>
        /// It is set in milliseconds how long the fan should be turned on
        /// </summary>
        public int TimeFanIsTurnedOn { get; set; } = 10000;
        
        /// <summary>
        /// Is the max allowed temperature in celsius
        /// </summary>
        public int MaxAllowedTemperatur { get; set; } = 26;
        
        /// <summary>
        /// Is the max allowed humidity in percentage
        /// </summary>
        public int MaxAllowedHumidity { get; set; } = 60;
    }
}
