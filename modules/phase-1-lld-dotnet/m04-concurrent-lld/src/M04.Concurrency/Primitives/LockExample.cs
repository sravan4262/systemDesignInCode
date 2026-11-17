namespace M04.Concurrency.Primitives;

// lock: ONE thread at a time, inside ONE process.
public static class LockExample
{
    // ❌ Violation: balance -= amount is read → subtract → write. Two threads both read 100,
    //    both pass the check, both withdraw 80 → balance -60 (overdrawn).
    public class AccountBefore
    {
        private decimal _balance = 100;

        public bool Withdraw(decimal amount)
        {
            if (_balance < amount) return false;   // check
            _balance -= amount;                    // act (another thread can run in between)
            return true;
        }
    }

    // ✅ Fix: check + act inside one lock → the second thread waits, then sees 20 and is refused.
    public class Account
    {
        private readonly Lock _lock = new();       // .NET 9+ Lock type; `object` works the same way
        private decimal _balance = 100;

        public bool Withdraw(decimal amount)
        {
            lock (_lock)
            {
                if (_balance < amount) return false;
                _balance -= amount;
                return true;
            }
        }
    }
    // How the fix solves it: only one thread can be between the check and the act.
    // Limits: no `await` inside; does nothing for OTHER processes (each has its own _lock).
}
