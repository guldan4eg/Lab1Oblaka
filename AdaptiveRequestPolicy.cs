
using RRC.Abstractions;

namespace RRC.Student;

public sealed class AdaptiveRequestPolicy : IRequestPolicy
{
    private readonly object _lock = new();

    private double _averageLatency = 100.0;

    private double _latencyVariation = 20.0;

    private double _timeoutRate = 0.0;

    private const double Alpha = 0.15;
    private const double Beta = 0.10;

    private const double MinTimeout = 80.0;
    private const double MaxTimeout = 1000.0;

    private int _successCount;
    private int _timeoutCount;

    public TimeSpan GetTimeout(RequestContext context, Random random)
    {
        lock (_lock)
        {
            double timeout =
                _averageLatency + 3.0 * _latencyVariation;

            timeout *= 1.0 + _timeoutRate;

            timeout = Math.Clamp(
                timeout,
                MinTimeout,
                MaxTimeout);

            return TimeSpan.FromMilliseconds(timeout);
        }
    }

    public RetryDecision OnResult(
        RequestObservation observation,
        Random random)
    {
        lock (_lock)
        {
            if (observation.Outcome == RequestOutcome.Success)
            {
                double latency = observation.Elapsed.TotalMilliseconds;

                double error = Math.Abs(latency - _averageLatency);

                _averageLatency =
                    (1.0 - Alpha) * _averageLatency +
                    Alpha * latency;

                _latencyVariation =
                    (1.0 - Beta) * _latencyVariation +
                    Beta * error;

                _successCount++;

                UpdateTimeoutRate(false);
            }
            else
            {
                _timeoutCount++;

                UpdateTimeoutRate(true);
            }

            if (observation.Outcome != RequestOutcome.Timeout ||
                observation.Attempt >= 2)
            {
                return RetryDecision.NoRetry();
            }


            if (_timeoutRate > 0.65)
                return RetryDecision.NoRetry();

            double delay = Math.Min(
                50.0 * Math.Pow(2, observation.Attempt - 1),
                500.0);

            return RetryDecision.RetryAfter(
                TimeSpan.FromMilliseconds(delay));
        }
    }

    private void UpdateTimeoutRate(bool timeout)
    {
        double value = timeout ? 1.0 : 0.0;

        _timeoutRate =
            0.9 * _timeoutRate +
            0.1 * value;
    }
}
