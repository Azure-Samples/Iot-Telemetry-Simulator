namespace IotTelemetrySimulator
{
    using System;

    public class DefaultRandomizer : IRandomizer
    {
        Random randomGenerator = new Random();

        public int GetNext(int min, int max)
        {
            lock (this.randomGenerator)
            {
                return this.randomGenerator.Next(min, max);
            }
        }
    }
}
