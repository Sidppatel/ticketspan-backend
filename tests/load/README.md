# Load tests (k6)

Performance scenarios for the event-platform API. Executed via [k6](https://k6.io).

## Install k6

| OS            | Command                                                 |
|---------------|---------------------------------------------------------|
| macOS         | `brew install k6`                                       |
| Windows       | `winget install k6` or `choco install k6`               |
| Linux (deb)   | see https://grafana.com/docs/k6/latest/set-up/install/  |
| Docker        | `docker run --rm -i grafana/k6 run - < scenario.js`     |

## Scenarios

| Script                           | Load                                        | Purpose                                   |
|----------------------------------|---------------------------------------------|-------------------------------------------|
| `scenarios/browse-events.js`     | 100 VUs × 5 min                             | Public catalog: list → detail → ticket types |
| `scenarios/read-heavy.js`        | 1000 req/min × 5 min (constant arrival rate)| Stress-test list endpoint under high RPS  |
| `scenarios/checkout.js`          | 20 VUs × 5 min                              | Auth → quote → booking → confirm flow    |

All scenarios have thresholds attached. Run exits non-zero if thresholds fail.

## Run against local dev

```bash
# 1. Start stack (needs seeded data for checkout.js)
./scripts/start.sh

# 2. Browse + read-heavy work out of the box
k6 run tests/load/scenarios/browse-events.js
k6 run tests/load/scenarios/read-heavy.js

# 3. Checkout requires pre-seeded test fixtures — see env vars in the script header.
```

## Run against staging

```bash
BASE_URL=https://staging-api.event-platform.example \
  k6 run tests/load/scenarios/browse-events.js

# Checkout against staging (requires seeded E2E test user):
BASE_URL=https://staging-api.event-platform.example \
E2E_TEST_USER=loadtest+staging@example.com \
E2E_TEST_TOKEN=... \
EVENT_ID=... \
TICKET_TYPE_ID=... \
  k6 run tests/load/scenarios/checkout.js
```

**Never point checkout.js at production** — it creates real booking rows even in Stripe test mode.

## Output

By default k6 prints a summary to stdout. To ship metrics to Prometheus or a file:

```bash
k6 run --out json=results.json scenarios/browse-events.js
k6 run --out experimental-prometheus-rw scenarios/browse-events.js
```

Feed summary results into `docs/performance-baseline.md` after each staging run.
