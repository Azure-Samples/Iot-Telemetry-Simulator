namespace IotTelemetrySimulator.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Newtonsoft.Json;
    using Xunit;

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
            Assert.Equal(RunnerConfiguration.DefaultTemplate, templatedPayload.Template.ToString());
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
            Assert.Equal(RunnerConfiguration.DefaultTemplate, templatedPayload.Template.ToString());

            var fixPayload10 = Assert.IsType<FixPayload>(target.PayloadGenerator.Payloads[2]);
            Assert.Equal(10, fixPayload10.Distribution);
            Assert.Equal("10", Encoding.UTF8.GetString(fixPayload10.Payload));
            Assert.Equal(2, fixPayload10.Payload.Length);
        }

        [Fact]
        public void When_Using_Sequence_Payload_Loads_Correctly()
        {
            const string rawTemplate = "{\"value\": \"$.Value\" }";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>()
                {
                    { "Variables", "[{\"name\":\"Value\", \"sequence\":true, \"values\":[\"$.Counter\", \"true\"]}, {\"name\":\"Counter\"}]" },
                    { "Template", rawTemplate },
                })
                .Build();

            var target = RunnerConfiguration.Load(configuration, NullLogger.Instance);
            Assert.NotNull(target.PayloadGenerator);
            var payload = Assert.Single(target.PayloadGenerator.Payloads);
            var templatedPayload = Assert.IsType<TemplatedPayload>(payload);
            Assert.Equal(2, templatedPayload.Variables.Variables.Count);
            Assert.True(templatedPayload.Variables.Variables[0].Sequence);
            Assert.Equal(new[] { "Counter" }, templatedPayload.Variables.Variables[0].GetReferenceVariableNames());
        }

        [Fact]
        public void When_Loading_From_File_Loads_Correctly()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("./test_files/test1-config.json", false, false)
                .Build();

            var target = RunnerConfiguration.Load(configuration, NullLogger.Instance);
            Assert.NotNull(target.PayloadGenerator);
            var payload = Assert.Single(target.PayloadGenerator.Payloads);
            var templatedPayload = Assert.IsType<TemplatedPayload>(payload);
            Assert.Equal(2, templatedPayload.Variables.Variables.Count);
            Assert.True(templatedPayload.Variables.Variables[0].Sequence);
            Assert.Equal(new[] { "Counter" }, templatedPayload.Variables.Variables[0].GetReferenceVariableNames());
        }

        [Fact]
        public void When_Loading_From_File_With_Multiple_Payloads_Loads_Correctly()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("./test_files/test2-config-multiple-payloads.json", false, false)
                .Build();

            var target = RunnerConfiguration.Load(configuration, NullLogger.Instance);
            Assert.NotNull(target.PayloadGenerator);
            Assert.Equal(2, target.PayloadGenerator.Payloads.Length);

            var templatedPayload = Assert.IsType<TemplatedPayload>(target.PayloadGenerator.Payloads[0]);
            Assert.Equal(2, templatedPayload.Variables.Variables.Count);
            Assert.True(templatedPayload.Variables.Variables[0].Sequence);
            Assert.Equal("device0001", templatedPayload.DeviceId);
            Assert.Equal(new[] { "Counter" }, templatedPayload.Variables.Variables[0].GetReferenceVariableNames());

            var fixPayload = Assert.IsType<FixPayload>(target.PayloadGenerator.Payloads[1]);
            Assert.Equal("{\"value\":\"myfixvalue\"}", Encoding.UTF8.GetString(fixPayload.Payload));
            Assert.Equal("device0002", fixPayload.DeviceId);
        }

        [Fact]
        public void When_Loading_From_File_With_Non_Encoded_Json_Payloads_Loads_Correctly()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("./test_files/test5-config-payloads-as-json.json", false, false)
                .Build();

            var target = RunnerConfiguration.Load(configuration, NullLogger.Instance);
            Assert.NotNull(target.PayloadGenerator);
            Assert.Equal(2, target.PayloadGenerator.Payloads.Length);

            var templatedPayload = Assert.IsType<TemplatedPayload>(target.PayloadGenerator.Payloads[0]);

            var device1Vars = new Dictionary<string, object>
            {
                { Constants.DeviceIdValueName, "device0001" },
            };
            var (device1Message, _) = templatedPayload.Generate(device1Vars);
            var device1MessageMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(device1Message));
            Assert.Equal(2, device1MessageMap.Count);
            Assert.Equal("1", device1MessageMap["value"]);
            Assert.Equal("20", device1MessageMap["a_second_value"]);

            var fixPayload = Assert.IsType<FixPayload>(target.PayloadGenerator.Payloads[1]);
            var device2Vars = new Dictionary<string, object>
            {
                { Constants.DeviceIdValueName, "device0002" },
            };
            var (device2Message, _) = fixPayload.Generate(device2Vars);
            var device2MessageMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(device2Message));
            Assert.Single(device2MessageMap);
            Assert.Equal("myfixvalue", device2MessageMap["value"]);
        }

        [Fact]
        public void When_Loading_From_File_With_Custom_Intervals_Loads_Correctly()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("./test_files/test4-config-multiple-internals-per-device.json", false, false)
                .Build();

            var target = RunnerConfiguration.Load(configuration, NullLogger.Instance);

            Assert.Equal(10_000, target.GetMessageIntervalForDevice("sim000001"));
            Assert.Equal(100, target.GetMessageIntervalForDevice("sim000002"));
            Assert.Equal(1_000, target.GetMessageIntervalForDevice("sim000003"));
        }
    }
}
