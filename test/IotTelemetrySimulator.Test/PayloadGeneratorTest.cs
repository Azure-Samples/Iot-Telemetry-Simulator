﻿namespace IotTelemetrySimulator.Test
{
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

            foreach (var (distribution, expectedPayload) in t)
            {
                randomizer.Setup(x => x.Next(It.IsAny<int>(), It.IsAny<int>())).Returns(distribution);
                var (p, _) = target.Generate(null);
                Assert.Equal(expectedPayload, Encoding.UTF8.GetString(p));
            }
        }
    }
}
