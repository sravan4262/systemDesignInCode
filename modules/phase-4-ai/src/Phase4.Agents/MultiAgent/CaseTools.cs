using System.ComponentModel;
using Microsoft.Extensions.AI;
using Phase4.Agents.Domain;

namespace Phase4.Agents.MultiAgent;

// Tools for the shared case, given to every worker. Two ways to handle a version conflict:
//   AddCaseNote   → code retries by itself: adding a line is safe to redo on the newer version (a merge).
//   SetCaseStatus → the conflict goes back to the MODEL: the status may have changed for a reason
//                   ("Resolved" by another worker), so it must re-read and decide again, not overwrite blindly.
public class CaseTools(CaseStore store, string caseId, string userId, string workerName)
{
    public IList<AITool> All =>
    [
        AIFunctionFactory.Create(GetCase),
        AIFunctionFactory.Create(AddCaseNote),
        AIFunctionFactory.Create(SetCaseStatus),
    ];

    [Description("Read the shared support case: status, notes from all agents, and its version")]
    private async Task<string> GetCase(CancellationToken ct) =>
        await store.GetAsync(caseId, userId, ct) is { } c
            ? $"status {c.Status}, version {c.Version}, notes:\n{c.Notes}"
            : "case not found";

    [Description("Add one line to the shared case notes saying what you did")]
    private async Task<string> AddCaseNote([Description("One short line")] string note, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            if (await store.GetAsync(caseId, userId, ct) is not { } c) return "case not found";
            var notes = c.Notes + $"[{workerName}] {note.Trim()}\n";              // append to what's there NOW
            if (await store.TryUpdateAsync(caseId, userId, c.Version, c.Status, notes, ct))
                return $"note added (version {c.Version + 1})";
            // another worker wrote between our read and our write → read again and re-append
        }
        return "error: the case is busy, note not added";
    }

    [Description("Change the case status. Pass the version you read with GetCase.")]
    private async Task<string> SetCaseStatus(CaseStatus status, [Description("The version from GetCase")] int expectedVersion, CancellationToken ct)
    {
        if (await store.GetAsync(caseId, userId, ct) is not { } c) return "case not found";
        var notes = c.Notes + $"[{workerName}] status → {status}\n";
        return await store.TryUpdateAsync(caseId, userId, expectedVersion, status, notes, ct)
            ? $"status set to {status} (version {expectedVersion + 1})"
            : $"conflict: the case changed (now version {c.Version}, status {c.Status}). Call GetCase and decide again.";
    }
}
