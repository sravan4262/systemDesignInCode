-- Postgres + pgvector. One row per document, many chunk rows per document.
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE documents (
    tenant_id    text        NOT NULL,
    id           text        NOT NULL,
    title        text        NOT NULL,               -- citation: shown to the user
    url          text        NOT NULL,               -- citation: the link
    content_hash text        NOT NULL,               -- same hash on re-upload → skip re-embedding
    updated_at   timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, id)
);

CREATE TABLE chunks (
    id             bigserial PRIMARY KEY,
    tenant_id      text   NOT NULL,                  -- filter: never search another tenant
    document_id    text   NOT NULL,
    chunk_index    int    NOT NULL,                  -- order inside the document
    heading        text   NOT NULL,                  -- citation: "Refunds"
    content        text   NOT NULL,                  -- heading + text; what we embed and show the LLM
    allowed_groups text[] NOT NULL,                  -- filter: user's groups (from the JWT) must overlap
    embedding_v1   vector(1536) NOT NULL,            -- text-embedding-3-small
    content_tsv    tsvector GENERATED ALWAYS AS (to_tsvector('english', content)) STORED,  -- keyword search
    FOREIGN KEY (tenant_id, document_id) REFERENCES documents (tenant_id, id) ON DELETE CASCADE
);

CREATE INDEX ON chunks USING hnsw (embedding_v1 vector_cosine_ops);  -- vector search (approximate)
CREATE INDEX ON chunks USING gin (content_tsv);                     -- keyword search
CREATE INDEX ON chunks USING gin (allowed_groups);                  -- allowed_groups && @userGroups
CREATE INDEX ON chunks (tenant_id, document_id);                    -- replace / delete one document
