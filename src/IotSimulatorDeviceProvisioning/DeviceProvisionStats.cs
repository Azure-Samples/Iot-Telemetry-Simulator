using System;
using System.Threading;

namespace IotSimulatorDeviceProvisioning
{
    class DeviceProvisionStats
    {
        const int reportRate = 100;

        int lastReportedCreatedCount = 0;
        long lastReportedCreateTime = DateTime.UtcNow.Ticks;
        int createdCount;
        internal int TotalCreated => createdCount;


        int lastReportedDeletedCount = 0;
        long lastReportedDeleteTime = DateTime.UtcNow.Ticks;
        int deletedCount;
        internal int TotalDeleted => deletedCount;


        int errorCount;
        internal int TotalErrors => errorCount;
        

        internal void IncrementCreated(int totalCreated)
        {
            var report = false;
            long cycleStart = 0;
            int deviceCountInCurrentCycle = 0;
            int totalDevicesToReport = 0;

            lock (this)
            {
                createdCount += totalCreated;
                deviceCountInCurrentCycle = createdCount - lastReportedCreatedCount;
                if (deviceCountInCurrentCycle >= reportRate)
                {
                    report = true;
                    totalDevicesToReport = createdCount;
                    lastReportedCreatedCount = createdCount;
                    cycleStart = lastReportedCreateTime;
                    lastReportedCreateTime = DateTime.UtcNow.Ticks;
                }
            }

            if (report)
            {
                var elapsed = DateTime.UtcNow - new DateTime(cycleStart, DateTimeKind.Utc);
                var amountPerSec = deviceCountInCurrentCycle / elapsed.TotalSeconds;
                Console.WriteLine($"{DateTime.UtcNow.ToString("o")}: {totalDevicesToReport} devices have been created @ {amountPerSec.ToString("0.00")}/sec");
            }

        }

        internal void IncrementErrors(int totalErrors)
        {
            Interlocked.Add(ref errorCount, totalErrors);
        }

        internal void IncrementDeleted(int totalDeleted)
        {
            var report = false;
            long cycleStart = 0;
            int countInCurrentCycle = 0;
            int totalDevicesToReport = 0;

            lock (this)
            {
                deletedCount += totalDeleted;
                countInCurrentCycle = createdCount - lastReportedDeletedCount;
                if (countInCurrentCycle >= reportRate)
                {
                    report = true;
                    totalDevicesToReport = deletedCount;
                    lastReportedDeletedCount = deletedCount;
                    cycleStart = lastReportedDeleteTime;
                    lastReportedDeleteTime = DateTime.UtcNow.Ticks;
                }
            }

            if (report)
            {
                var elapsed = DateTime.UtcNow - new DateTime(cycleStart, DateTimeKind.Utc);
                var amountPerSec = countInCurrentCycle / elapsed.TotalSeconds;
                Console.WriteLine($"{DateTime.UtcNow.ToString("o")}: {totalDevicesToReport} devices have been deleted @ {amountPerSec.ToString("0.00")}/sec");
            }
        }
    }
}
