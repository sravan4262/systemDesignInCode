using Microsoft.AspNetCore.Mvc;
using Phase4.Agents.Evals;

namespace Phase4.Agents.Controllers;

// Step 6: one eval example. Runs the golden cases against the real agent and returns a pass/fail gate.
// Real app: run from CI on every prompt/model/tool change (not a public endpoint); 422 fails the pipeline.
[ApiController]
[Route("evals")]
public class EvalsController(EvalRunner runner) : ControllerBase
{
    // POST /evals/run?trials=3   → 5 cases × 3 trials ≈ 15 agent runs (~45 model calls)
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct, int trials = 3)
    {
        if (trials is < 1 or > 10) return BadRequest("trials must be 1–10");
        var report = await runner.RunAsync(trials, ct);
        return report.GatePassed ? Ok(report) : UnprocessableEntity(report);
    }
}
