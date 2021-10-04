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

        [Theory]
        [InlineData(1, 5, 2)]
        [InlineData(5, 9, null)]
        [InlineData(null, 2, null)]
        [InlineData(-3, 2, null)]
        [InlineData(int.MaxValue - 10, int.MaxValue, 6)]
        public void Counter_With_Threshold_Should_Reset_To_Min(double? min, double? max, int? step)
        {
            var telemetryTemplate = new TelemetryTemplate("{\"val\":\"$.Value\"}", new[] { "Counter" });
            var telemetryVariables = new[]
            {
                new TelemetryVariable
                {
                    Name = "Counter",
                    Step = step,
                    Min = min,
                    Max = max
                }
            };
            var telemetryValues = new TelemetryValues(telemetryVariables);

            var payload = new TemplatedPayload(100, telemetryTemplate, telemetryValues);

            var target = new PayloadGenerator(new[] { payload }, new DefaultRandomizer());
            var variables = new Dictionary<string, object>
            {
                { Constants.DeviceIdValueName, "mydevice" },
            };

            var minValue = min ?? 1;

            for (int i = 0; i < 10; i += step ?? 1)
            {
                (_, variables) = target.Generate(null, variables);
                variables.TryGetValue("Counter", out var o);
                var result = Convert.ToInt32(o);

                if (i + minValue > max)
                {
                    Assert.Equal(result, minValue);
                    break;
                }
                else
                {
                    Assert.Equal(result, minValue + i);
                }
            }
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(null, 15.0)]
        [InlineData(10.0, null)]
        [InlineData(0.01, 0.02)]
        [InlineData(-5, 5)]
        public void RandomDouble_Generator(double? min, double? max)
        {
            var telemetryTemplate = new TelemetryTemplate("{\"val\":\"$.Value\"}", new[] { "Value" });
            var telemetryVariables = new[]
            {
                new TelemetryVariable
                {
                    Name = "Value",
                    RandomDouble = true,
                    Min = min,
                    Max = max
                }
            };
            var telemetryValues = new TelemetryValues(telemetryVariables);

            var payload = new TemplatedPayload(100, telemetryTemplate, telemetryValues);

            var target = new PayloadGenerator(new[] { payload }, new DefaultRandomizer());

            var variables = new Dictionary<string, object>
            {
                { Constants.DeviceIdValueName, "mydevice" },
            };

            if (max == null || min == null)
            {
                for (int i = 0; i < 10; i++)
                {
                    (_, variables) = target.Generate(null, variables);
                    variables.TryGetValue("Value", out var o);
                    Assert.IsType<double>(o);
                }
            }
            else
            {
                for (int i = 0; i < 10; i++)
                {
                    (_, variables) = target.Generate(null, variables);
                    variables.TryGetValue("Value", out var o);
                    var result = Convert.ToDouble(o);
                    Assert.True(result >= min && result <= max);
                }
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

        [Fact]
        public void Sequence_With_Exchanging_Counters_Generator()
        {
            var telemetryTemplate = new TelemetryTemplate("{\"val1\":\"$.Value1\",\"val2\":\"$.Value2\"}", new[] { "Value1", "Value2", "Counter1", "Counter2" });
            var telemetryVariables = new[]
            {
                new TelemetryVariable
                {
                    Name = "Value1",
                    Sequence = true,
                    Values = new object[] { "$.Counter1", "$.Counter2" },
                },

                new TelemetryVariable
                {
                    Name = "Value2",
                    Sequence = true,
                    Values = new object[] { "$.Counter2", "$.Counter1" },
                },

                new TelemetryVariable
                {
                    Name = "Counter1",
                    Step = 1,
                    Min = 1
                },

                new TelemetryVariable
                {
                    Name = "Counter2",
                    Step = 1,
                    Min = 1_001
                }
            };
            var telemetryValues = new TelemetryValues(telemetryVariables);

            var payload = new TemplatedPayload(100, telemetryTemplate, telemetryValues);

            var target = new PayloadGenerator(new[] { payload }, new DefaultRandomizer());

            var expectedValues = new[]
            {
                "{\"val1\":\"1\",\"val2\":\"1001\"}",
                "{\"val1\":\"1002\",\"val2\":\"2\"}",
                "{\"val1\":\"3\",\"val2\":\"1003\"}",
                "{\"val1\":\"1004\",\"val2\":\"4\"}",
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

        [Fact]
        public void Sequence_With_Mixed_Counters_Generator()
        {
            var telemetryTemplate = new TelemetryTemplate("{\"val1\":\"$.Value1\",\"val2\":\"$.Value2\",\"counter_3\":\"$.Counter3\"}", new[] { "Value1", "Value2", "Counter1", "Counter2", "Counter3" });
            var telemetryVariables = new[]
            {
                new TelemetryVariable
                {
                    Name = "Value1",
                    Sequence = true,
                    Values = new object[] { "$.Counter1", "$.Counter2" },
                },

                new TelemetryVariable
                {
                    Name = "Value2",
                    Sequence = true,
                    Values = new object[] { "$.Counter2", "$.Counter1" },
                },

                new TelemetryVariable
                {
                    Name = "Counter1",
                    Step = 1,
                    Min = 1
                },

                new TelemetryVariable
                {
                    Name = "Counter2",
                    Step = 1,
                    Min = 1_001
                },

                new TelemetryVariable
                {
                    Name = "Counter3",
                    Step = 1,
                    Min = 1_000_001
                }
            };
            var telemetryValues = new TelemetryValues(telemetryVariables);

            var payload = new TemplatedPayload(100, telemetryTemplate, telemetryValues);

            var target = new PayloadGenerator(new[] { payload }, new DefaultRandomizer());

            var expectedValues = new[]
            {
                "{\"val1\":\"1\",\"val2\":\"1001\",\"counter_3\":\"1000001\"}",
                "{\"val1\":\"1002\",\"val2\":\"2\",\"counter_3\":\"1000002\"}",
                "{\"val1\":\"3\",\"val2\":\"1003\",\"counter_3\":\"1000003\"}",
                "{\"val1\":\"1004\",\"val2\":\"4\",\"counter_3\":\"1000004\"}",
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

        [Fact]
        public void Sequence_As_Array_Attribute()
        {
            var telemetryTemplate = new TelemetryTemplate("{\"val1\":\"$.Value1\",\"array_var\":$.ArrayValue,\"array_fix\":[\"FixCategory\"]}", new[] { "Value1", "Counter1", "Counter2", "ArrayValue" });
            var telemetryVariables = new[]
            {
                new TelemetryVariable
                {
                    Name = "Value1",
                    Sequence = true,
                    Values = new object[] { "$.Counter1", "$.Counter2" },
                },

                new TelemetryVariable
                {
                    Name = "Counter1",
                    Step = 1,
                    Min = 1
                },

                new TelemetryVariable
                {
                    Name = "Counter2",
                    Step = 1,
                    Min = 1_001
                },

                new TelemetryVariable
                {
                    Name = "ArrayValue",
                    Sequence = true,
                    Values = new object[] { "[\"MyCategory\"]", "[]" }
                }
            };
            var telemetryValues = new TelemetryValues(telemetryVariables);

            var payload = new TemplatedPayload(100, telemetryTemplate, telemetryValues);

            var target = new PayloadGenerator(new[] { payload }, new DefaultRandomizer());

            var expectedValues = new[]
            {
                "{\"val1\":\"1\",\"array_var\":[\"MyCategory\"],\"array_fix\":[\"FixCategory\"]}",
                "{\"val1\":\"1001\",\"array_var\":[],\"array_fix\":[\"FixCategory\"]}",
                "{\"val1\":\"2\",\"array_var\":[\"MyCategory\"],\"array_fix\":[\"FixCategory\"]}",
                "{\"val1\":\"1002\",\"array_var\":[],\"array_fix\":[\"FixCategory\"]}",
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
