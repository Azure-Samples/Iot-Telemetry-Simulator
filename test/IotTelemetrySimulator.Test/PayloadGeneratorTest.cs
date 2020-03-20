using Moq;
using System;
using System.Text;
using Xunit;

namespace IotTelemetrySimulator.Test
{
    public class PayloadGeneratorTest
    {
        private byte[] GetBytes(string v) => Encoding.UTF8.GetBytes(v);

        [Fact]
        public void When_Getting_Payload_Should_Distribute_Correctly()
        {
            var randomizer = new Mock<IRandomizer>();

            var target = new PayloadGenerator(new[] { 
                new FixPayload(30, GetBytes("30")), 
                new FixPayload(55, GetBytes("55")),
                new FixPayload(15, GetBytes("15"))},
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
                randomizer.Setup(x => x.GetNext(It.IsAny<int>(), It.IsAny<int>())).Returns(tt.distribution);
                var (p, v) = target.Generate(null);
                Assert.Equal(tt.expectedPayload, Encoding.UTF8.GetString(p));
            }            
        }        
    }
}
