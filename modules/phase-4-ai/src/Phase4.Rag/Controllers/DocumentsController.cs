using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Phase4.Rag.Ingestion;

namespace Phase4.Rag.Controllers;

// Ingestion: document in → chunks + embeddings stored, ready to search.
// hash (skip if unchanged) → chunk → embed (one batched call) → replace in one transaction.
[ApiController]
[Route("documents")]
public class DocumentsController(
    Chunker chunker,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    ChunkStore store) : ControllerBase
{
    public record UploadRequest(string DocumentId, string Title, string Url, string[] AllowedGroups, string Text);

    // POST /documents   header X-Tenant-Id: acme   body { documentId, title, url, allowedGroups, text }
    [HttpPost]
    public async Task<IActionResult> Upload(UploadRequest req, [FromHeader(Name = "X-Tenant-Id")] string tenantId, CancellationToken ct)
    {
        // real app: tenantId from the JWT of an admin / the sync job, never from the body

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(req.Text)));
        if (await store.GetHashAsync(tenantId, req.DocumentId, ct) == hash)
            return Ok(new { req.DocumentId, status = "unchanged" });   // no embedding cost for a re-sync

        var chunks = chunker.Split(req.Text);
        if (chunks.Count == 0) return BadRequest("document has no text");

        // Embed BEFORE opening the transaction: never hold DB locks while waiting on a slow API.
        var embeddings = await embedder.GenerateAsync(chunks.Select(c => c.Text), cancellationToken: ct);
        var vectors = embeddings.Select(e => e.Vector.ToArray()).ToList();

        var doc = new DocumentInfo(tenantId, req.DocumentId, req.Title, req.Url, req.AllowedGroups, hash);
        await store.ReplaceDocumentAsync(doc, chunks, vectors, ct);

        return Ok(new { req.DocumentId, status = "indexed", chunks = chunks.Count });
    }
}
