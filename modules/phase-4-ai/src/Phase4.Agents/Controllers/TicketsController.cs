using Microsoft.AspNetCore.Mvc;
using Phase4.Agents.Workflow;

namespace Phase4.Agents.Controllers;

// The workflow next to the agent: same model, but code decides the steps.
[ApiController]
[Route("tickets")]
public class TicketsController(TicketWorkflow workflow) : ControllerBase
{
    public record TicketRequest(string Subject, string Body);

    // POST /tickets   { subject, body }  →  { summary, classification, queue }
    [HttpPost]
    public async Task<IActionResult> Create(TicketRequest req, CancellationToken ct) =>
        Ok(await workflow.RunAsync(req.Subject, req.Body, ct));
}
