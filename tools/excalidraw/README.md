# Excalidraw generator

This builds the `.excalidraw` files for the HLD case designs from a small PowerShell layout script. The layout uses three helpers: `Box` (a rectangle with text), `Arrow` (an arrow bound to two boxes, with an optional label) and `Note` (free text).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/excalidraw/excal.ps1 `
  -Layout tools/excalidraw/case-03-twitter.layout.ps1 `
  -Out modules/phase-3-hld/m19-hld-patterns-cases-1/designs/case-03-twitter/case-03-twitter.excalidraw
```

**Case diagram format (agreed for Case 3, reuse for every case):**
- Each row is one actor type, e.g. A = regular user, B = celebrity or other hot entity.
- Each row runs left to right: User → API Gateway. After the gateway it splits into the **write path** (top, blue) and the **read path** (bottom, green). Async steps are dashed; skipped steps are red.
- Every functional requirement appears as a service or arrow on the path it uses.
- Each row ends with a note saying which components the write path touches and which the read path touches.

**Without PowerShell (Mac/Linux):** `excal.py` is a Python port with the same helpers and output. Layouts are `.layout.py` files.

```bash
python3 tools/excalidraw/excal.py tools/excalidraw/case-02-whatsapp.layout.py \
  modules/phase-3-hld/m19-hld-patterns-cases-1/designs/case-02-whatsapp/case-02-whatsapp.excalidraw
```
