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
