namespace IotTelemetrySimulator
{
    using System;
    using System.Threading;

    public class DefaultRandomizer : IRandomizer
    {
        private readonly ThreadLocal<Random> generator
            = new ThreadLocal<Random>(() => new Random());

        public int Next()
        {
            return this.generator.Value.Next();
        }

        public int Next(int max)
        {
            return this.generator.Value.Next(max);
        }

        public int Next(int min, int max)
        {
            return this.generator.Value.Next(min, max);
        }
    }
}
