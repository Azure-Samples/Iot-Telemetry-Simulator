namespace IotTelemetrySimulator.Automation
{
    using Microsoft.Extensions.Configuration;

    public class Configuration
    {
        private static Configuration configuration;

        public string AuthenticationMethod { get; set; }

        public string AadClientId { get; set; }

        public string AadClientSecret { get; set; }

        public string AzureSubscriptionName { get; set; }

        public string AadTenantId { get; set; }

        public string AzureSubscriptionId { get; set; }

        public string AzureRegion { get; set; }

        public string ResourceGroupName { get; set; }

        public string ContainerGroupName { get; set; }

        public string ContainerImage { get; set; }

        public string AcrServer { get; set; }

        public string AcrUsername { get; set; }

        public string AcrPassword { get; set; }

        public string IotHubConnectionString { get; set; }

        public string EventHubConnectionString { get; set; }

        public string KafkaConnectionProperties { get; set; }

        public string KafkaTopic { get; set; }

        public string PayloadDistribution { get; set; }

        public string Template1 { get; set; }

        public string Template2 { get; set; }

        public string Variables { get; set; }

        public string DevicePrefix { get; set; }

        public string Header { get; set; }

        public string PartitionKey { get; set; }

        public int DeviceCount { get; set; }

        public int MessageCount { get; set; }

        public int Interval { get; set; }

        public int DuplicateEvery { get; set; }

        public int ContainerCount { get; set; }

        public int CpuCores { get; set; }

        public double MemoryGb { get; set; }

        public static Configuration GetConfiguration()
        {
            if (configuration == null)
            {
                configuration = new Configuration();

                new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .AddJsonFile("appsettings.json", optional: false)
                    .AddJsonFile("appsettings.Development.json", optional: true)
                    .Build()
                    .Bind(configuration);
            }

            return configuration;
        }
    }
}
