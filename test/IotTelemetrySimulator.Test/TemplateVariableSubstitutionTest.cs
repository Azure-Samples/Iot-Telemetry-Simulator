namespace IotTelemetrySimulator.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class TemplateVariableSubstitutionTest
    {
        private class CustomClass
        {
            private readonly string message;

            public CustomClass(string message)
            {
                this.message = message;
            }

            public override string ToString()
            {
                return this.message;
            }
        }

        [Fact]
        public void ShouldSubstituteSpecialVariables()
        {
            var variables = new TelemetryValues(new[]
            {
                new TelemetryVariable
                {
                    Min = 1,
                    Name = "Counter",
                    Step = 1,
                },
            });

            var telemetryTemplate = new TelemetryTemplate(
                $"$.Counter $.{Constants.TicksValueName} $.{Constants.GuidValueName}",
                variables.VariableNames());

            var generatedPayload = telemetryTemplate.Create(
                variables.NextValues(previous: null));

            var parts = generatedPayload.Split();

            Assert.Equal(3, parts.Length);
            Assert.Equal("1", parts[0]); // Counter must be 1
            Assert.True(parts[1].All(char.IsDigit));  // Ticks must be an int
            Assert.True(Guid.TryParse(parts[2], out _));  // Guid should be parseable
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void ShouldSubstituteVariablesCorrectly(
            Dictionary<string, object> previousValues,
            Dictionary<string, object> vars,
            string template,
            string expectedPayload)
        {
            var variables = new TelemetryValues(
                vars.Select(var => new TelemetryVariable
                {
                    Name = var.Key,
                    Values = new[] { var.Value },
                }).ToArray());

            var telemetryTemplate = new TelemetryTemplate(
                template,
                variables.VariableNames());

            var generatedPayload = telemetryTemplate.Create(
                variables.NextValues(previous: previousValues));

            Assert.Equal(expectedPayload, generatedPayload);
        }

        public static IEnumerable<object[]> GetTestData()
        {
            // Should convert variable values to string
            var customObj = new CustomClass("some_data");
            yield return new object[]
            {
                null,
                new Dictionary<string, object> { { "name", "World" }, { "var1", customObj }, { "var2", -0.5 }, { "var3", string.Empty } },
                "Hello, $.name! I like $.var1, $.var2 and $.var3, $.name.",
                $"Hello, World! I like some_data, -0.5 and , World.",
            };

            // Should ignore extra variables
            yield return new object[]
            {
                null,
                new Dictionary<string, object> { { "var1", 1 }, { "var2", 2 } },
                "Only $.var1",
                "Only 1",
            };

            // Should allow empty variables
            yield return new object[]
            {
                null,
                new Dictionary<string, object>(),
                "$.something",
                "$.something",
            };

            // Should ignore non-existent variables in the template
            yield return new object[]
            {
                null,
                new Dictionary<string, object> { { "var1", 1 } },
                "$.var1 $.var",
                "1 $.var",
            };

            // ..even with the special name
            yield return new object[]
            {
                null,
                new Dictionary<string, object> { { "var1", 1 } },
                $"$.{Constants.DeviceIdValueName} $.var1 $.var",
                $"$.{Constants.DeviceIdValueName} 1 $.var",
            };

            // Should substitute longer names first
            yield return new object[]
            {
                null,
                new Dictionary<string, object> { { "var1", 1 }, { "var", 2 }, { "var11", 3 } },
                "$.var1$.var11, $.var $.var$.var1$.var11!",
                "13, 2 213!",
            };

            // Should substitute DeviceID if it's in the previous values
            yield return new object[]
            {
                new Dictionary<string, object> { { Constants.DeviceIdValueName, "dummy" } },
                new Dictionary<string, object> { { "var1", 1 } },
                $"$.{Constants.DeviceIdValueName} $.var1!",
                "dummy 1!",
            };
        }
    }
}
