namespace Phase4.Agents.Support;

// Fix 2: once an action is waiting for a human, the agent takes NO more actions in this task.
// Before: after "waiting for approval", the loop kept going, so the model could still call RefundOrder again.
// After:  the tool marks the gate; the loop sees it, skips any remaining tool calls, and ends with one last
//         model call that has NO tools, so the model can only write the reply.
// One gate per request (created in the controller, shared by the tools and the loop).
public class ApprovalGate
{
    public string? PendingApprovalId { get; private set; }
    public bool IsWaiting => PendingApprovalId is not null;

    public void MarkWaiting(string approvalId) => PendingApprovalId = approvalId;
}
