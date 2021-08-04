namespace TemperatureSensors.Messages
{
    public class RespondTemperature
    {
        public RespondTemperature(long requestId, double? value)
        {
            RequestId = requestId;
            Value = value;
        }

        public long RequestId { get; set; }
        public double? Value { get; set; }
    }
}
