namespace M04.Concurrency;

// In-memory KV store with nested transactions: a dictionary with an undo button.
// Committed data = one dictionary. Each BEGIN pushes a "draft" layer of changes onto a stack.
// In a layer, a null value is a TOMBSTONE: "deleted in this transaction" (hides the value underneath).
public class KeyValueStore
{
    private readonly Dictionary<string, string> _committed = new();
    private readonly Stack<Dictionary<string, string?>> _transactions = new();

    public void Set(string key, string value) => Write(key, value);
    public void Delete(string key) => Write(key, null);

    // Inside a transaction → write to the top draft. Otherwise → write straight to committed data.
    private void Write(string key, string? value)
    {
        if (_transactions.TryPeek(out var top)) { top[key] = value; return; }

        if (value is null) _committed.Remove(key);
        else _committed[key] = value;
    }

    // Read top draft first, then lower drafts, then committed. First hit wins (a tombstone = "not found").
    public string? Get(string key)
    {
        foreach (var layer in _transactions)              // Stack enumerates top → bottom
            if (layer.TryGetValue(key, out var value)) return value;

        return _committed.GetValueOrDefault(key);
    }

    public void Begin() => _transactions.Push(new());

    // Undo only the innermost transaction: O(1), just drop the draft.
    public bool Rollback() => _transactions.TryPop(out _);   // false = "NO TRANSACTION"

    // Commit ALL open transactions: apply drafts bottom → top so newer changes win.
    public bool Commit()
    {
        if (_transactions.Count == 0) return false;          // "NO TRANSACTION"

        foreach (var layer in _transactions.Reverse())
            foreach (var (key, value) in layer)
                if (value is null) _committed.Remove(key);
                else _committed[key] = value;

        _transactions.Clear();
        return true;
    }
}
