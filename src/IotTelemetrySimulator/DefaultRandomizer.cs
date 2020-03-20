using System;

namespace IotTelemetrySimulator
{
    public class DefaultRandomizer : IRandomizer
    {
        Random randomGenerator = new Random();        

        public int GetNext(int min, int max)
        {
            lock (randomGenerator)
            {
                return randomGenerator.Next(min, max);
            }
        }
    }
}
