namespace IotTelemetrySimulator
{
    public static class Constants
    {
        public const string AppVersion = "1.0";

        public const string TemplateConfigName = "Template";
        public const string PayloadDistributionConfigName = "PayloadDistribution";

        public const string TimeValueName = "Time";
        public const string LocalTimeValueName = "LocalTime";
        public const string EpochValueName = "Epoch";
        public const string TicksValueName = "Ticks";
        public const string DeviceIdValueName = "DeviceId";
        public const string GuidValueName = "Guid";
        public const string MachineNameValueName = "MachineName";
        public const string IterationNumberValueName = "IterationNumber";
        public static readonly string[] AllSpecialValueNames =
        {
            TimeValueName, LocalTimeValueName, EpochValueName, TicksValueName,
            DeviceIdValueName, GuidValueName, MachineNameValueName, IterationNumberValueName
        };
    }
}
