-- SQL Server. Orders the agent can read, and the two tables its refunds write to.
CREATE TABLE orders (
    id       varchar(20)   NOT NULL PRIMARY KEY,
    owner    varchar(50)   NOT NULL,          -- the tools filter on this with the JWT user
    item     nvarchar(200) NOT NULL,
    total    decimal(10,2) NOT NULL,
    refunded decimal(10,2) NOT NULL DEFAULT 0,
    status   varchar(20)   NOT NULL,          -- shipped | delivered
    CONSTRAINT ck_refund_not_above_total CHECK (refunded <= total)   -- last line of defence
);
CREATE INDEX ix_orders_owner ON orders (owner);

-- One row per refund that ran. PK = idempotency key: the same refund can't run twice.
CREATE TABLE refunds (
    idempotency_key varchar(200)  NOT NULL PRIMARY KEY,
    order_id        varchar(20)   NOT NULL REFERENCES orders (id),
    amount          decimal(10,2) NOT NULL,
    result          nvarchar(200) NOT NULL,          -- returned again on a repeat call
    created_at      datetime2     NOT NULL DEFAULT sysutcdatetime()
);

-- Refunds above the auto limit, waiting for a human.
CREATE TABLE pending_refunds (
    id            varchar(20)   NOT NULL PRIMARY KEY,   -- apr-<hash of the idempotency key>
    order_id      varchar(20)   NOT NULL REFERENCES orders (id),
    amount        decimal(10,2) NOT NULL,
    reason        varchar(20)   NOT NULL,
    requested_for varchar(50)   NOT NULL,
    created_at    datetime2     NOT NULL DEFAULT sysutcdatetime()
);

INSERT INTO orders (id, owner, item, total, status) VALUES
    ('123', 'alice', 'running shoes', 120, 'delivered'),
    ('124', 'alice', 'headphones',     40, 'delivered'),
    ('456', 'bob',   'laptop',       1500, 'shipped');

-- ---------- Memory (step 4) ----------

-- Short-term memory: the turns of one chat. Only user + assistant TEXT is kept; tool calls and raw tool
-- results are not (they were needed inside that turn, not later), which keeps later prompts small.
CREATE TABLE conversations (
    id                varchar(50)   NOT NULL PRIMARY KEY,
    user_id           varchar(50)   NOT NULL,              -- only this user can continue the chat
    summary           nvarchar(max) NULL,                  -- older turns, compressed by the LLM
    summarized_up_to  bigint        NOT NULL DEFAULT 0     -- turns with seq <= this are in the summary
);
CREATE TABLE conversation_messages (
    seq             bigint IDENTITY PRIMARY KEY,           -- order of the turns
    conversation_id varchar(50)   NOT NULL REFERENCES conversations (id),
    role            varchar(10)   NOT NULL,                -- user | assistant
    text            nvarchar(max) NOT NULL,
    created_at      datetime2     NOT NULL DEFAULT sysutcdatetime()
);
CREATE INDEX ix_messages_conversation ON conversation_messages (conversation_id, seq);

-- Long-term memory: facts the customer stated about themselves. One value per kind (upsert), so
-- "call me" later replaces "email me" instead of both being kept and contradicting each other.
CREATE TABLE user_memories (
    user_id    varchar(50)   NOT NULL,
    kind       varchar(30)   NOT NULL,                     -- ContactChannel | Language | DeliveryNote
    value      nvarchar(100) NOT NULL,
    updated_at datetime2     NOT NULL DEFAULT sysutcdatetime(),
    PRIMARY KEY (user_id, kind)
);
