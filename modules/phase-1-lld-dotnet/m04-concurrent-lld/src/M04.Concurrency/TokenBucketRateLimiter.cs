namespace M04.Concurrency;

// Strategy: every algorithm (token bucket, sliding window, fixed window) answers the same question.
public interface IRateLimiter { bool TryAcquire(); }

// Token bucket: holds up to `capacity` tokens, refills `refillPerSecond`. Each request takes 1 token.
// Idle clients save tokens → short bursts allowed; the long-run average is capped at the refill rate.
public class TokenBucketRateLimiter(int capacity, double refillPerSecond, TimeProvider clock) : IRateLimiter
{
    private readonly object _lock = new();
    private double _tokens = capacity;                    // start full
    private long _lastRefill = clock.GetTimestamp();

    public bool TryAcquire()
    {
        lock (_lock)   // refill + check + take = one atomic step (else 2 threads spend the last token)
        {
            Refill();
            if (_tokens < 1) return false;   // caller returns 429 Too Many Requests
            _tokens -= 1;
            return true;
        }
    }

    // Lazy refill: no timer. Add the tokens "earned" since the last call, capped at capacity.
    private void Refill()
    {
        var now = clock.GetTimestamp();
        var elapsed = clock.GetElapsedTime(_lastRefill, now).TotalSeconds;
        _tokens = Math.Min(capacity, _tokens + elapsed * refillPerSecond);
        _lastRefill = now;
    }
}

// Per-client limiting: one limiter per key (user / API key / IP). Works with ANY algorithm.
// Usage: new KeyedRateLimiter(() => new TokenBucketRateLimiter(10, 1, TimeProvider.System))
public class KeyedRateLimiter(Func<IRateLimiter> create)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IRateLimiter> _limiters = new();

    public bool TryAcquire(string clientKey) => _limiters.GetOrAdd(clientKey, _ => create()).TryAcquire();
}
