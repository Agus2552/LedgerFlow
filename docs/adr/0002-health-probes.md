# ADR 0002: Separate liveness and database readiness

Status: Accepted

## Context and decision

An unavailable database should prevent routing financial work to the API without
implying the API process itself needs restarting. Expose GET /health/live with no
dependency checks and GET /health/ready with a tagged PostgreSQL check.
Use Npgsql directly to execute SELECT 1, with bounded connection, command and health
check timeouts. A missing connection string returns unhealthy. Public responses
contain only a status, never credentials or provider diagnostics.

Use standard configuration (Development user secrets or environment variables).
The local setup script reads the Compose password from .env and writes user secrets.

## Alternatives and consequences

A port-open probe does not validate database authentication or SQL execution.
EF Core would introduce persistence modeling before any entity exists. A dedicated
health-check package would reduce custom code, but this single driver-level check
keeps behavior explicit and introduces only the driver needed for PostgreSQL.
SELECT 1 cannot prove migrations, tables or business operations are correct.
Readiness performs a real database round trip; avoid probing at excessive frequency.
