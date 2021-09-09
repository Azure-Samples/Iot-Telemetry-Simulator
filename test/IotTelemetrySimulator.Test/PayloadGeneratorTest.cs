namespace IotTelemetrySimulator.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Moq;
    using Xunit;

    public class PayloadGeneratorTest
    {
        private byte[] GetBytes(string v) => Encoding.UTF8.GetBytes(v);

        [Fact]
        public void When_Getting_Payload_Should_Distribute_Correctly()
        {
            var randomizer = new Mock<IRandomizer>();

            var target = new PayloadGenerator(
                new[]
                {
                    new FixPayload(30, this.GetBytes("30")),
                    new FixPayload(55, this.GetBytes("55")),
                    new FixPayload(15, this.GetBytes("15"))
                },
                randomizer.Object);

            var t = new (int distribution, string expectedPayload)[]
            {
                (1, "55"),
                (55, "55"),
                (56, "30"),
                (85, "30"),
                (86, "15"),
                (100, "15"),
            };

            foreach (var tt in t)
            {
                randomizer.Setup(x => x.Next(It.IsAny<int>(), It.IsAny<int>())).Returns(tt.distribution);
                var (p, v) = target.Generate(null, null);
                Assert.Equal(tt.expectedPayload, Encoding.UTF8.GetString(p));
            }
        }

        [Fact]
        public void Sequence_Generator()
        {
            var telemetryTemplate = new TelemetryTemplate("{\"val\":\"$.Value\"}", new[] { "Value", "Counter" });
            var telemetryVariables = new[]
            {
                new TelemetryVariable
                {
                    Name = "Value",
                    Sequence = true,
                    Values = new object[] { "$.Counter", "true", "false", "$.Counter" },
                },

                new TelemetryVariable
                {
                    Name = "Counter",
                    Step = 1,
                    Min = 1
                }
            };
            var telemetryValues = new TelemetryValues(telemetryVariables);

            var payload = new TemplatedPayload(100, telemetryTemplate, telemetryValues);

            var target = new PayloadGenerator(new[] { payload }, new DefaultRandomizer());

            var expectedValues = new[]
            {
                "{\"val\":\"1\"}",
                "{\"val\":\"true\"}",
                "{\"val\":\"false\"}",
                "{\"val\":\"2\"}",
                "{\"val\":\"3\"}",
                "{\"val\":\"true\"}",
                "{\"val\":\"false\"}",
                "{\"val\":\"4\"}",
            };

            var variables = new Dictionary<string, object>
            {
                { Constants.DeviceIdValueName, "mydevice" },
            };

            byte[] result;
            foreach (var expectedValue in expectedValues)
            {
                (result, variables) = target.Generate(null, variables);
                Assert.NotEmpty(variables);
                Assert.Equal(expectedValue, Encoding.UTF8.GetString(result));
            }
        }
    }
}
