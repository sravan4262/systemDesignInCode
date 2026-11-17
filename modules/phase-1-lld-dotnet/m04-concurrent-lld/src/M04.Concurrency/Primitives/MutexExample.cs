namespace M04.Concurrency.Primitives;

// Named Mutex: one at a time across PROCESSES on the same machine (an OS object, found by name).
public static class MutexExample
{
    private const string StockFile = "stock.csv";   // the SHARED RESOURCE both processes use

    // ❌ Violation: run this app twice. Both read stock 10, sell, and write back → one sale lost.
    //    A `lock` wouldn't help: each process has its own lock object.
    public static void SellBefore(int qty)
    {
        var stock = int.Parse(File.ReadAllText(StockFile));
        File.WriteAllText(StockFile, (stock - qty).ToString());
    }

    // ✅ Fix: both processes wait on the SAME named mutex, around the WHOLE read → change → write.
    public static void Sell(int qty)
    {
        using var mutex = new Mutex(initiallyOwned: false, name: @"Global\M04-StockCsv");

        try { mutex.WaitOne(); }                    // waits until the other process releases it
        catch (AbandonedMutexException) { }         // previous owner crashed: we own it now; file may be half-written

        try
        {
            var stock = int.Parse(File.ReadAllText(StockFile));   // read INSIDE the mutex (fresh value)
            File.WriteAllText(StockFile, (stock - qty).ToString());
        }
        finally { mutex.ReleaseMutex(); }           // must release (and from the same thread: no `await` in between)
    }
    // How the fix solves it: the second process waits, then reads the value the first one wrote.
    // Single-instance variant: if (!mutex.WaitOne(TimeSpan.Zero)) { /* already running */ return; }
}
