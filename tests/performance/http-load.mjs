#!/usr/bin/env node

import { performance } from "node:perf_hooks";

const profile = process.argv[2] ?? "hard";
const requestCount = readPositiveInteger(
  process.argv[3],
  2_000,
  "requestCount",
);
const concurrency = readPositiveInteger(process.argv[4], 32, "concurrency");
const baseUrl = (
  process.env.VERISCAN_BASE_URL ?? "http://127.0.0.1:5000"
).replace(/\/$/, "");
const apiKey = process.env.VERISCAN_API_KEY;

if (!apiKey) {
  fail(
    "VERISCAN_API_KEY is required. Create a temporary application key in the admin console first.",
  );
}

if (!new Set(["hard", "mixed"]).has(profile)) {
  fail('profile must be either "hard" or "mixed".');
}

const latencies = [];
const statusCounts = new Map();
const decisionCounts = new Map();
const routeCounts = new Map();
let clientErrors = 0;
let responseErrors = 0;
let degradedItems = 0;
let nextRequest = 0;
const startedAt = performance.now();

await Promise.all(
  Array.from({ length: Math.min(concurrency, requestCount) }, () => worker()),
);

const elapsedMilliseconds = performance.now() - startedAt;
latencies.sort((left, right) => left - right);
const completedRequests = [...statusCounts.values()].reduce(
  (sum, value) => sum + value,
  0,
);
const successfulRequests = [...statusCounts.entries()]
  .filter(([status]) => Number(status) >= 200 && Number(status) < 300)
  .reduce((sum, [, count]) => sum + count, 0);
const itemsPerRequest = createItems(0).length;

console.log(
  JSON.stringify(
    {
      profile,
      baseUrl,
      requestCount,
      concurrency,
      itemsPerRequest,
      elapsedMilliseconds: round(elapsedMilliseconds),
      attemptedRequestsPerSecond: round(
        requestCount / (elapsedMilliseconds / 1_000),
      ),
      successfulRequestsPerSecond: round(
        successfulRequests / (elapsedMilliseconds / 1_000),
      ),
      successfulItemsPerSecond: round(
        (successfulRequests * itemsPerRequest) / (elapsedMilliseconds / 1_000),
      ),
      latencyMilliseconds: {
        mean: round(
          latencies.reduce((sum, value) => sum + value, 0) /
            Math.max(latencies.length, 1),
        ),
        p50: round(percentile(latencies, 0.5)),
        p95: round(percentile(latencies, 0.95)),
        p99: round(percentile(latencies, 0.99)),
      },
      completedRequests,
      successfulRequests,
      statusCounts: Object.fromEntries(statusCounts),
      clientErrors,
      responseErrors,
      decisions: Object.fromEntries(decisionCounts),
      routes: Object.fromEntries(routeCounts),
      degradedItems,
      note: "Client-observed local HTTP load. Requests are persisted and no Idempotency-Key is used.",
    },
    null,
    2,
  ),
);

if (clientErrors > 0 || responseErrors > 0) {
  process.exitCode = 1;
}

async function worker() {
  while (true) {
    const requestIndex = nextRequest++;
    if (requestIndex >= requestCount) {
      return;
    }

    const requestStartedAt = performance.now();
    try {
      const response = await fetch(`${baseUrl}/api/v1/moderation/batches`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-API-Key": apiKey,
        },
        body: JSON.stringify({
          mode: "sync",
          items: createItems(requestIndex),
        }),
      });

      latencies.push(performance.now() - requestStartedAt);
      increment(statusCounts, String(response.status));

      if (!response.ok) {
        responseErrors++;
        await response.arrayBuffer();
        continue;
      }

      const body = await response.json();
      for (const result of body.results ?? []) {
        increment(decisionCounts, result.decision ?? "null");
        increment(routeCounts, result.route ?? "unknown");
        if (result.degraded) {
          degradedItems++;
        }
      }
    } catch {
      latencies.push(performance.now() - requestStartedAt);
      clientErrors++;
    }
  }
}

function createItems(requestIndex) {
  if (profile === "hard") {
    return [
      {
        id: `hard-${requestIndex}`,
        content: "这是诈骗内容",
        contentType: "plain_text",
      },
    ];
  }

  const blockedTerms = [
    "诈骗",
    "赌博",
    "色情",
    "暴恐",
    "诈骗",
    "赌博",
    "色情",
    "暴恐",
    "诈骗",
  ];
  return [
    ...blockedTerms.map((term, itemIndex) => ({
      id: `mixed-${requestIndex}-${itemIndex}`,
      content: `这是${term}内容`,
      contentType: "plain_text",
    })),
    {
      id: `mixed-${requestIndex}-9`,
      content: "请加微信联系",
      contentType: "plain_text",
    },
  ];
}

function increment(map, key) {
  map.set(key, (map.get(key) ?? 0) + 1);
}

function percentile(samples, value) {
  if (samples.length === 0) {
    return 0;
  }

  const index = Math.ceil(value * samples.length) - 1;
  return samples[Math.max(0, Math.min(index, samples.length - 1))];
}

function readPositiveInteger(value, fallback, name) {
  if (value === undefined) {
    return fallback;
  }

  const parsed = Number.parseInt(value, 10);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    fail(`${name} must be a positive integer.`);
  }

  return parsed;
}

function round(value) {
  return Math.round(value * 100) / 100;
}

function fail(message) {
  console.error(message);
  process.exit(1);
}
