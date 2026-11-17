namespace M04.Concurrency.Primitives;

// ReaderWriterLockSlim: readers share (like SQL's S lock), a writer is alone (like SQL's X lock).
public static class ReaderWriterLockExample
{
    // ❌ Violation (slow, not wrong): a price list read 10,000×/s, updated once an hour.
    //    With `lock`, every read waits for every other read, even though reads can't harm each other.
    public class PriceListBefore
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<string, decimal> _prices = new();

        public decimal? Get(string sku) { lock (_lock) return _prices.TryGetValue(sku, out var p) ? p : null; }
        public void Set(string sku, decimal price) { lock (_lock) _prices[sku] = price; }
    }

    // ✅ Fix: shared read lock, exclusive write lock.
    public class PriceList
    {
        private readonly ReaderWriterLockSlim _rw = new();
        private readonly Dictionary<string, decimal> _prices = new();

        public decimal? Get(string sku)
        {
            _rw.EnterReadLock();                    // many readers at once; waits only if a writer is in/waiting
            try { return _prices.TryGetValue(sku, out var p) ? p : null; }
            finally { _rw.ExitReadLock(); }         // always try/finally (unlike `lock`, nothing does it for you)
        }

        public void Set(string sku, decimal price)
        {
            _rw.EnterWriteLock();                   // waits for current readers; blocks new readers and writers
            try { _prices[sku] = price; }
            finally { _rw.ExitWriteLock(); }
        }
    }
    // How the fix solves it: reads run in parallel; only writes take turns.
    // Only worth it when reads are frequent, writes rare, and reads TRULY read-only (not the LRU cache).
}
