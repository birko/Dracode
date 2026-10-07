# Run events over SSE

`GET /api/v1/runs/{id}/events` streams a Kobold run's events as [Server-Sent Events](https://html.spec.whatwg.org/multipage/server-sent-events.html)
from the moment you connect until the run ends. It is the stream for clients that cannot hold the `/kobold` WebSocket
(a browser `EventSource`, the Python SDK, `curl`). The frames are the same as the WebSocket's.

## Using it

1. Start a run: `POST /api/v1/runs` → `202 { runId, mode, worktree }`.
2. Open the stream for that `runId`. Authenticate with the usual `Authorization: Bearer …` header, or with `?token=…`
   — a browser `EventSource` cannot set headers.

```bash
curl -N "http://localhost:57087/api/v1/runs/$RUN_ID/events?token=$TOKEN"
```

```js
const source = new EventSource(`/api/v1/runs/${runId}/events?token=${token}`);
source.addEventListener("kobold_tool_call", e => console.log(JSON.parse(e.data)));
source.addEventListener("kobold_complete", () => source.close());
```

The `/api/v1` routes exist only when JWT authentication is enabled.

## Frames

Each frame is `id: <seq>`, `event: <type>`, `data: <JSON>`. The JSON is the `/kobold` WebSocket message
(`KoboldWireMessage`, camelCase, null fields left out).

| `event` | When |
|---|---|
| `kobold_run_started` | The Kobold started working |
| `kobold_tool_call` | A tool call started (`phase: "started"`) or returned (`phase: "result"`) |
| `kobold_reflect` | A self-reflection checkpoint (runs with a plan) |
| `kobold_stream` | A plan step changed status (runs with a plan) |
| `kobold_complete` | The run finished — `finalStatus` is `Done` or `Failed`. Last frame |
| `error` | The run failed — `message` says why. Last frame |

- **Only events from the moment you connect.** Nothing is replayed. A run that has already ended sends one closing frame
  (`kobold_complete` or `error`) and the stream closes.
- **The server closes the stream** after the last frame. A client that disconnects is detached at once.
- **Heartbeat:** an idle stream gets a `: heartbeat` comment line every 15 seconds.
- **Ownership:** the same as `GET /api/v1/runs/{id}` — your own runs, or any run for an admin. Another caller's run is `404`.

## Behind a reverse proxy

A run can go quiet for longer than a proxy's default read timeout, and a buffering proxy holds frames back. The server
sends `X-Accel-Buffering: no` and a heartbeat every 15 seconds; set the proxy to match. For nginx:

```nginx
location /api/v1/runs/ {
    proxy_pass http://koboldlair;
    proxy_http_version 1.1;
    proxy_set_header Connection "";
    proxy_buffering off;
    proxy_read_timeout 1h;   # longer than the longest run; the heartbeat keeps shorter timeouts alive too
}
```
