namespace IotTelemetrySimulator
{
    using System;
    using System.Threading;

    public class RunnerStats
    {
        private const long ReportRate = 100;
        private long connectedDevices;
        private long completedDevices;
        private long messagesSent;
        private long totalSendTelemetryErrors;

        private long messagesSendingStart;

        public long MessagesSent => this.messagesSent;

        public long TotalSendTelemetryErrors => this.totalSendTelemetryErrors;

        public RunnerStats()
        {
            this.messagesSendingStart = DateTime.UtcNow.Ticks;
        }

        internal void IncrementDeviceConnected()
        {
            var newValue = Interlocked.Increment(ref this.connectedDevices);
            if (newValue % ReportRate == 0)
                Console.WriteLine($"{DateTime.UtcNow:o}: {newValue} devices connected");
        }

        internal void IncrementCompletedDevice()
        {
            var newValue = Interlocked.Increment(ref this.completedDevices);
            if (newValue % ReportRate == 0)
                Console.WriteLine($"{DateTime.UtcNow:o}: {newValue} devices have completed sending messages");
        }

        internal void IncrementMessageSent()
        {
            var newValue = Interlocked.Increment(ref this.messagesSent);
            if (newValue % ReportRate == 0)
            {
                var now = DateTime.UtcNow;
                var currentStart = Interlocked.Exchange(ref this.messagesSendingStart, now.Ticks);
                var start = new DateTime(currentStart, DateTimeKind.Utc);
                var elapsedMs = (now - start).TotalMilliseconds;
                var ratePerSecond = (ReportRate / elapsedMs) * 1000;

                Console.WriteLine($"{DateTime.UtcNow:o}: {newValue} total messages have been sent @ {ratePerSecond:0.00} msgs/sec");
            }
        }

        internal void IncrementSendTelemetryErrors()
        {
            var newValue = Interlocked.Increment(ref this.totalSendTelemetryErrors);
            if (newValue % ReportRate == 0)
                Console.WriteLine($"{DateTime.UtcNow:o}: {newValue} errors sending telemetry");
        }
    }
}
