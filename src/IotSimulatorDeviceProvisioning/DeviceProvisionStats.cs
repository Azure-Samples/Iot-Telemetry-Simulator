namespace IotSimulatorDeviceProvisioning
{
    using System;
    using System.Threading;

    class DeviceProvisionStats
    {
        private const int ReportRate = 100;

        private int lastReportedCreatedCount = 0;
        private long lastReportedCreateTime = DateTime.UtcNow.Ticks;
        private int createdCount;

        internal int TotalCreated => this.createdCount;

        private int lastReportedDeletedCount = 0;
        private long lastReportedDeleteTime = DateTime.UtcNow.Ticks;
        private int deletedCount;

        internal int TotalDeleted => this.deletedCount;

        private int errorCount;

        internal int TotalErrors => this.errorCount;

        internal void IncrementCreated(int totalCreated)
        {
            var report = false;
            long cycleStart = 0;
            int deviceCountInCurrentCycle = 0;
            int totalDevicesToReport = 0;

            lock (this)
            {
                this.createdCount += totalCreated;
                deviceCountInCurrentCycle = this.createdCount - this.lastReportedCreatedCount;
                if (deviceCountInCurrentCycle >= ReportRate)
                {
                    report = true;
                    totalDevicesToReport = this.createdCount;
                    this.lastReportedCreatedCount = this.createdCount;
                    cycleStart = this.lastReportedCreateTime;
                    this.lastReportedCreateTime = DateTime.UtcNow.Ticks;
                }
            }

            if (report)
            {
                var elapsed = DateTime.UtcNow - new DateTime(cycleStart, DateTimeKind.Utc);
                var amountPerSec = deviceCountInCurrentCycle / elapsed.TotalSeconds;
                Console.WriteLine($"{DateTime.UtcNow:o}: {totalDevicesToReport} devices have been created @ {amountPerSec:0.00}/sec");
            }
        }

        internal void IncrementErrors(int totalErrors)
        {
            Interlocked.Add(ref this.errorCount, totalErrors);
        }

        internal void IncrementDeleted(int totalDeleted)
        {
            var report = false;
            long cycleStart = 0;
            int countInCurrentCycle = 0;
            int totalDevicesToReport = 0;

            lock (this)
            {
                this.deletedCount += totalDeleted;
                countInCurrentCycle = this.createdCount - this.lastReportedDeletedCount;
                if (countInCurrentCycle >= ReportRate)
                {
                    report = true;
                    totalDevicesToReport = this.deletedCount;
                    this.lastReportedDeletedCount = this.deletedCount;
                    cycleStart = this.lastReportedDeleteTime;
                    this.lastReportedDeleteTime = DateTime.UtcNow.Ticks;
                }
            }

            if (report)
            {
                var elapsed = DateTime.UtcNow - new DateTime(cycleStart, DateTimeKind.Utc);
                var amountPerSec = countInCurrentCycle / elapsed.TotalSeconds;
                Console.WriteLine($"{DateTime.UtcNow:o}: {totalDevicesToReport} devices have been deleted @ {amountPerSec:0.00}/sec");
            }
        }
    }
}
