using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Phase4.Rag.Ingestion;

namespace Phase4.Rag.Controllers;

// Ingestion + freshness: keeps the index in step with the source documents.
// real app: tenantId from the JWT of an admin / the sync job, never from the body.
[ApiController]
[Route("documents")]
public class DocumentsController(Chunker chunker, EmbeddingVersions models, ChunkStore store) : ControllerBase
{
    public record UploadRequest(string DocumentId, string Title, string Url, string[] AllowedGroups, string Text);
    public record PermissionsRequest(string[] AllowedGroups);

    // POST /documents   create or update: hash (skip if unchanged) → chunk → embed → replace in one transaction
    [HttpPost]
    public async Task<IActionResult> Upload(UploadRequest req, [FromHeader(Name = "X-Tenant-Id")] string tenantId, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(req.Text)));
        if (await store.GetHashAsync(tenantId, req.DocumentId, ct) == hash)
            return Ok(new { req.DocumentId, status = "unchanged" });   // no embedding cost for a re-sync

        var chunks = chunker.Split(req.Text);
        if (chunks.Count == 0) return BadRequest("document has no text");

        // Embed BEFORE opening the transaction: never hold DB locks while waiting on a slow API.
        var texts = chunks.Select(c => c.Text).ToList();
        var v1 = await models.EmbedAsync(texts, "v1", ct);
        var v2 = models.DualWrite ? await models.EmbedAsync(texts, "v2", ct) : null;   // only during a model upgrade

        var doc = new DocumentInfo(tenantId, req.DocumentId, req.Title, req.Url, req.AllowedGroups, hash);
        await store.ReplaceDocumentAsync(doc, chunks, v1, v2, ct);

        return Ok(new { req.DocumentId, status = "indexed", chunks = chunks.Count });
    }

    // DELETE /documents/refund-policy   the source document was deleted → it must stop showing up in answers NOW
    [HttpDelete("{documentId}")]
    public async Task<IActionResult> Delete(string documentId, [FromHeader(Name = "X-Tenant-Id")] string tenantId, CancellationToken ct)
    {
        if (!await store.DeleteDocumentAsync(tenantId, documentId, ct)) return NotFound();
        // build 3: also remove cached answers that cited this document (semantic cache)
        return NoContent();
    }

    // PUT /documents/refund-policy/permissions   { "allowedGroups": ["hr"] }   who may see it changed, the text didn't
    [HttpPut("{documentId}/permissions")]
    public async Task<IActionResult> UpdatePermissions(string documentId, PermissionsRequest req,
        [FromHeader(Name = "X-Tenant-Id")] string tenantId, CancellationToken ct)
    {
        if (!await store.UpdatePermissionsAsync(tenantId, documentId, req.AllowedGroups, ct)) return NotFound();
        return NoContent();   // the very next search filters with the new groups: no re-embedding, no delay
    }
}
