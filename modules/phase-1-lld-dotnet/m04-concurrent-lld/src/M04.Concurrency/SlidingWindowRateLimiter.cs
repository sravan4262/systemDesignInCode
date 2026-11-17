namespace M04.Concurrency;

// Sliding window COUNTER: approximates "requests in the last `window`" with just 2 counters.
// estimate = current window count + previous window count × (how much of the previous window still overlaps)
// Fixes the fixed-window boundary burst without storing a timestamp per request.
public class SlidingWindowRateLimiter(int limit, TimeSpan window, TimeProvider clock) : IRateLimiter
{
    private readonly object _lock = new();
    private DateTimeOffset _windowStart = clock.GetUtcNow();
    private int _current;    // requests in the current fixed window
    private int _previous;   // requests in the window before it

    public bool TryAcquire()
    {
        lock (_lock)   // roll + estimate + increment = one atomic step
        {
            var now = clock.GetUtcNow();
            Roll(now);

            var overlap = 1 - (now - _windowStart) / window;   // 1.0 at window start → 0.0 at its end
            var estimate = _previous * overlap + _current;
            if (estimate >= limit) return false;               // 429

            _current++;
            return true;
        }
    }

    // Move to the window that contains `now`. Skipped 2+ windows (idle client)? Both counts reset.
    private void Roll(DateTimeOffset now)
    {
        var windowsPassed = (int)((now - _windowStart) / window);
        if (windowsPassed == 0) return;

        _previous = windowsPassed == 1 ? _current : 0;
        _current = 0;
        _windowStart += window * windowsPassed;
    }
}
