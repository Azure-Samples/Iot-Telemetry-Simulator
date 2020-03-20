using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace IotTelemetrySimulator.Test
{
    public class RunnerConfigurationTest
    {        

        [Fact]
        public void When_Using_Dynamic_Payload_Loads_Correctly()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>()
                {
                    { Constants.PayloadDistributionConfigName, "fixSize(10,12) template(25,    default) fix(65, aaaaBBBBBCCC)" },                    
                })
                .Build();

            var target = RunnerConfiguration.Load(configuration, NullLogger.Instance);
            Assert.NotNull(target.PayloadGenerator);
            Assert.Equal(3, target.PayloadGenerator.Payloads.Length);

            // will be ordered by percentage
            Assert.IsType<FixPayload>(target.PayloadGenerator.Payloads[0]);
            Assert.Equal(65, target.PayloadGenerator.Payloads[0].Distribution);
            Assert.Equal(9, ((FixPayload)target.PayloadGenerator.Payloads[0]).Payload.Length);

            Assert.IsType<TemplatedPayload>(target.PayloadGenerator.Payloads[1]);
            Assert.Equal(25, target.PayloadGenerator.Payloads[1].Distribution);

            Assert.IsType<FixPayload>(target.PayloadGenerator.Payloads[2]);
            Assert.Equal(10, target.PayloadGenerator.Payloads[2].Distribution);
            Assert.Equal(12, ((FixPayload)target.PayloadGenerator.Payloads[2]).Payload.Length);
        }

        [Fact]
        public void When_Using_Custom_Named_Template_Payload_Loads_Correctly()
        {
            const string rawTemplate = "{\"deviceId\": \"$.DeviceId\" }";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>()
                {
                    { Constants.PayloadDistributionConfigName, "fixSize(51,12) template(49, mytemplate)" },
                    { "mytemplate", rawTemplate }
                })
                .Build();

            var target = RunnerConfiguration.Load(configuration, NullLogger.Instance);
            Assert.NotNull(target.PayloadGenerator);
            Assert.Equal(2, target.PayloadGenerator.Payloads.Length);

            // will be ordered by percentage
            var fixPayload = Assert.IsType<FixPayload>(target.PayloadGenerator.Payloads[0]);
            Assert.Equal(51, target.PayloadGenerator.Payloads[0].Distribution);
            Assert.Equal(12, fixPayload.Payload.Length);

            var templatedPayload = Assert.IsType<TemplatedPayload>(target.PayloadGenerator.Payloads[1]);
            Assert.Equal(49, target.PayloadGenerator.Payloads[1].Distribution);
            Assert.Equal(rawTemplate, templatedPayload.Template.ToString());
        }


        [Fact]
        public void When_No_Template_Is_Set_Loads_Default_Template()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>()
                {                    
                })
                .Build();

            var target = RunnerConfiguration.Load(configuration, NullLogger.Instance);
            Assert.NotNull(target.PayloadGenerator);
            Assert.Single(target.PayloadGenerator.Payloads);

            var templatedPayload = Assert.IsType<TemplatedPayload>(target.PayloadGenerator.Payloads[0]);
            Assert.Equal(TelemetryTemplate.DefaultTemplate, templatedPayload.Template.ToString());
            Assert.Equal(100, target.PayloadGenerator.Payloads[0].Distribution);
        }

        [Fact]
        public void When_Using_Base64_Fix_Payload_Loads_Correctly()
        {
            const string rawTemplate = "{\"deviceId\": \"$.DeviceId\" }";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>()
                {
                    { Constants.PayloadDistributionConfigName, "fix(10, MTA=) template(25, default) fix(65, NjU=)" },
                    { "mytemplate", rawTemplate }
                })
                .Build();

            var target = RunnerConfiguration.Load(configuration, NullLogger.Instance);
            Assert.NotNull(target.PayloadGenerator);
            Assert.Equal(3, target.PayloadGenerator.Payloads.Length);

            // will be ordered by percentage
            var fixPayload65 = Assert.IsType<FixPayload>(target.PayloadGenerator.Payloads[0]);
            Assert.Equal(65, fixPayload65.Distribution);
            Assert.Equal("65", Encoding.UTF8.GetString(fixPayload65.Payload));
            Assert.Equal(2, fixPayload65.Payload.Length);

            var templatedPayload = Assert.IsType<TemplatedPayload>(target.PayloadGenerator.Payloads[1]);
            Assert.Equal(25, templatedPayload.Distribution);
            Assert.Equal(TelemetryTemplate.DefaultTemplate, templatedPayload.Template.ToString());

            var fixPayload10 = Assert.IsType<FixPayload>(target.PayloadGenerator.Payloads[2]);
            Assert.Equal(10, fixPayload10.Distribution);
            Assert.Equal("10", Encoding.UTF8.GetString(fixPayload10.Payload));
            Assert.Equal(2, fixPayload10.Payload.Length);
        }
    }
}
