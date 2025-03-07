using System;
using System.Threading;

namespace STATCodingExercise.Services.Metrics
{
    public class OperationMetrics
    {
        private long _successCount;
        private long _failureCount;
        private long _totalDurationTicks;
        private readonly object _lock = new object();

        public long SuccessCount => Interlocked.Read(ref _successCount);
        public long FailureCount => Interlocked.Read(ref _failureCount);
        public TimeSpan AverageDuration => TimeSpan.FromTicks(Interlocked.Read(ref _totalDurationTicks) / (_successCount + _failureCount));

        public void RecordSuccess(TimeSpan duration)
        {
            Interlocked.Increment(ref _successCount);
            Interlocked.Add(ref _totalDurationTicks, duration.Ticks);
        }

        public void RecordFailure(TimeSpan duration)
        {
            Interlocked.Increment(ref _failureCount);
            Interlocked.Add(ref _totalDurationTicks, duration.Ticks);
        }
    }
} 