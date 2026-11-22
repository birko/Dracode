/**
 * One run, live (TASK-057, FEATURE-026 D7/D20), opened from the Runs tab: what it is for, its status, its plan's steps as a
 * checklist with progress (a run with a task: `GET /api/v1/plans/{taskId}`), and a feed of its tool calls, reflections
 * and how it ended, from `GET /api/v1/runs/{id}/events` on Birko.Web.Core's `SseClient` (`?token=`, reconnect).
 *
 * The stream sends only what happens from the moment of connecting (no replay) and closes after a run's last frame, so:
 * a run that has already ended is shown from its status (`GET /api/v1/runs/{id}`) without a stream; the stream is
 * closed on the last frame, or the client would reconnect to an ended run; and after a dropped stream reconnects, frames
 * already shown are skipped by their sequence number (one counter per run, across connections) while the status and the
 * plan are read again for whatever happened in between.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { SseClient } from 'birko-web-core/http';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient, createSseClient } from '../services/http.js';
import type { RunView as RunSummary } from './runs-tab.js';

interface PlanStep { index: number; title: string; description?: string; status: string }
interface PlanView { status: string; totalSteps: number; completedSteps: number; steps: PlanStep[] }

/** A `/kobold` wire frame, as the stream's `data` (camelCase, null fields left out). */
interface Frame {
  type: string;
  seq?: number;
  toolName?: string;
  phase?: 'started' | 'result';
  inputJson?: string;
  resultPreview?: string;
  reflection?: { stepIndex: number; progressPercent: number; confidencePercent: number; decision: number; blockers?: string; adjustment?: string };
  stepIndex?: number;
  stepStatus?: string;
  output?: string;
  completedSteps?: number;
  totalSteps?: number;
  finalStatus?: string;
  message?: string;
}

interface FeedItem { seq: number; kind: 'tool' | 'result' | 'reflect' | 'step' | 'end'; text: string; detail?: string; bad?: boolean }

/** How many feed lines the view keeps. */
const FEED_LIMIT = 300;
const PREVIEW = 240;
const DECISION = ['continue', 'pivot', 'escalate'];
const STATUS_VARIANT: Record<string, string> = { pending: 'neutral', running: 'info', completed: 'success', failed: 'danger' };
const STEP_MARK: Record<string, string> = { Completed: '✓', InProgress: '▸', Failed: '✗', Skipped: '–', Pending: '○' };
const RUN_EVENTS = ['kobold_run_started', 'kobold_tool_call', 'kobold_reflect', 'kobold_stream', 'kobold_complete', 'error'];

/** How far along a step status is: a reload never moves a step back past what the stream already showed. */
const STEP_RANK: Record<string, number> = { Pending: 0, InProgress: 1, Completed: 2, Failed: 2, Skipped: 2 };
const furthest = (a: string, b: string) => ((STEP_RANK[b] ?? 0) > (STEP_RANK[a] ?? 0) ? b : a);

/** A tool's input JSON, re-serialized so the server's unicode escapes (quotes, backticks, angle brackets) read as characters. */
const readableJson = (json: string | undefined) => {
  if (!json) return json;
  try { return JSON.stringify(JSON.parse(json)); } catch { return json; }
};

const cut = (text: string | undefined, max = PREVIEW) => (!text ? '' : text.length > max ? `${text.slice(0, max)}…` : text);

export class RunViewPanel extends KoboldComponent {
  private _run: RunSummary | null = null;
  private _plan: PlanView | null = null;
  private _feed: FeedItem[] = [];
  private _lastSeq = 0;
  private _ended: { ok: boolean; text: string } | null = null;
  private _error: string | null = null;
  private _sse: SseClient | null = null;
  private _reconnects = 0;

  static get styles() {
    return `
      :host { display: block; min-width: 0; color: var(--b-text); }
      .back { display: inline-block; margin-bottom: var(--b-space-sm, 0.5rem); color: var(--b-color-primary); font-size: var(--b-text-sm, 0.8125rem);
        background: none; border: 0; padding: 0; cursor: pointer; font-family: inherit; }
      .back:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
      .head { display: flex; flex-wrap: wrap; align-items: center; gap: var(--b-space-sm, 0.5rem); min-width: 0; margin-bottom: var(--b-space-md, 0.75rem); }
      .what { margin: 0; flex: 1 1 16rem; min-width: 0; font-size: var(--b-text-lg, 1rem); overflow-wrap: anywhere; }
      .ended { margin: 0 0 var(--b-space-md, 0.75rem); padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem);
        border-radius: var(--b-radius, 0.375rem); font-size: var(--b-text-sm, 0.8125rem); overflow-wrap: anywhere; }
      .ended.ok { background: var(--b-color-success-light, var(--b-bg-secondary)); color: var(--b-color-success-text, var(--b-text)); }
      .ended.bad { background: var(--b-color-danger-light, var(--b-bg-secondary)); color: var(--b-color-danger-text, var(--b-text)); }
      .grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr); gap: var(--b-space-lg, 1rem); align-items: start; }
      @container (max-width: 44rem) { .grid { grid-template-columns: minmax(0, 1fr); } }
      :host { container-type: inline-size; }
      h3 { margin: 0 0 var(--b-space-sm, 0.5rem); font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      .progress { display: flex; align-items: center; gap: var(--b-space-sm, 0.5rem); margin-bottom: var(--b-space-sm, 0.5rem);
        font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
      .progress b-progress { flex: 1; }
      .steps { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.25rem; }
      .step { display: flex; gap: var(--b-space-sm, 0.5rem); font-size: var(--b-text-sm, 0.8125rem); overflow-wrap: anywhere; }
      .mark { flex-shrink: 0; width: 1rem; text-align: center; color: var(--b-text-muted); }
      .step[data-status="Completed"] .mark { color: var(--b-color-success); }
      .step[data-status="InProgress"] { font-weight: var(--b-font-weight-semibold, 600); }
      .step[data-status="InProgress"] .mark { color: var(--b-color-primary); }
      .step[data-status="Failed"] .mark { color: var(--b-color-danger); }
      .step[data-status="Skipped"] { color: var(--b-text-muted); }
      .feed { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.25rem;
        max-height: 32rem; overflow-y: auto; font-size: var(--b-text-xs, 0.75rem); }
      .item { padding: 0.25rem var(--b-space-sm, 0.5rem); border-left: 2px solid var(--b-border); overflow-wrap: anywhere; }
      .item[data-kind="tool"] { border-left-color: var(--b-color-primary); }
      .item[data-kind="reflect"] { border-left-color: var(--b-color-warning); }
      .item.bad { border-left-color: var(--b-color-danger); color: var(--b-color-danger-text, var(--b-color-danger)); }
      .item .detail { display: block; margin-top: 0.125rem; color: var(--b-text-muted); font-family: var(--b-font-mono, monospace); white-space: pre-wrap; }
      .muted { color: var(--b-text-muted); font-size: var(--b-text-sm, 0.8125rem); }
      .conn { font-size: var(--b-text-xs, 0.75rem); color: var(--b-color-warning); }
    `;
  }

  protected onMount() {
    void this.load(true);
  }

  protected onUnmount() {
    this._sse?.disconnect();
    this._sse = null;
  }

  private get runId(): string {
    return this.attr('run-id') ?? '';
  }

  /** The run's status and plan; then the stream, unless the run has ended. */
  private async load(first: boolean): Promise<void> {
    const client = createRestClient();
    try {
      const run = await client.get<RunSummary>(`/runs/${encodeURIComponent(this.runId)}`);
      if (!run.ok || !run.data) {
        this._error = run.status === 404 ? 'This run is not known to the server (it may have restarted since).' : `The server answered ${run.status}.`;
        this.update();
        return;
      }
      // A reload (after a reconnect) may answer older than a frame that already arrived: keep what is further along.
      const ended = this._ended && this._run ? this._run.status : null;
      this._run = ended ? { ...run.data, status: ended, completedSteps: Math.max(run.data.completedSteps, this._run!.completedSteps) } : run.data;
      if (run.data.taskId) {
        const plan = await client.get<PlanView>(`/plans/${encodeURIComponent(run.data.taskId)}`).catch(() => null);
        if (plan?.ok && plan.data) this._plan = this.merge(plan.data);
      }
    } catch (error) {
      this._error = error instanceof Error ? error.message : String(error);
      this.update();
      return;
    }
    const status = this._run.status;
    if (status === 'completed' || status === 'failed') {
      this._ended ??= { ok: status === 'completed', text: status === 'completed' ? 'The run completed.' : `The run failed${this._run.summary ? `: ${this._run.summary}` : '.'}` };
      this._sse?.disconnect();
      this._sse = null;
    } else if (first) {
      this.stream();
    }
    this.update();
  }

  private stream(): void {
    const sse = createSseClient(`/api/v1/runs/${encodeURIComponent(this.runId)}/events`, undefined, { reconnectDelays: [1000, 3000, 10000] });
    for (const type of RUN_EVENTS) sse.on(type, (data) => this.onFrame(data));
    // After a drop: frames from before are skipped by sequence; the status and plan are read again for the gap.
    sse.on('_reconnect', () => { this._reconnects++; void this.load(false); });
    sse.on('_error', () => this.update());
    sse.on('_open', () => this.update());
    this._sse = sse;
    sse.connect();
  }

  private onFrame(data: unknown): void {
    let frame: Frame;
    try {
      frame = (typeof data === 'string' ? JSON.parse(data) : data) as Frame;
    } catch {
      return;
    }
    const seq = frame.seq ?? 0;
    if (seq && seq <= this._lastSeq) return; // shown already, before a reconnect
    if (seq) this._lastSeq = seq;

    switch (frame.type) {
      case 'kobold_run_started':
        if (this._run) this._run = { ...this._run, status: 'running' };
        break;
      case 'kobold_tool_call':
        this.push(frame.phase === 'result'
          ? { seq, kind: 'result', text: `${frame.toolName ?? 'tool'} returned`, detail: cut(frame.resultPreview) }
          : { seq, kind: 'tool', text: `Called ${frame.toolName ?? 'a tool'}`, detail: cut(readableJson(frame.inputJson)) });
        if (this._run) this._run = { ...this._run, status: 'running' };
        break;
      case 'kobold_reflect': {
        const r = frame.reflection;
        if (r) {
          this.push({
            seq, kind: 'reflect', bad: r.decision === 2,
            text: `Reflection on step ${r.stepIndex}: ${r.progressPercent}% done, ${r.confidencePercent}% confident — ${DECISION[r.decision] ?? r.decision}`,
            detail: cut([r.blockers && `Blockers: ${r.blockers}`, r.adjustment && `Adjustment: ${r.adjustment}`].filter(Boolean).join('\n')),
          });
        }
        break;
      }
      case 'kobold_stream':
        this.applyStep(frame);
        this.push({ seq, kind: 'step', bad: frame.stepStatus === 'Failed', text: `Step ${frame.stepIndex}: ${frame.stepStatus}`, detail: cut(frame.output) });
        break;
      case 'kobold_complete':
      case 'error': {
        const ok = frame.type === 'kobold_complete' && frame.finalStatus === 'Done';
        const text = ok ? 'The run completed.' : `The run failed${frame.message ? `: ${frame.message}` : frame.finalStatus ? ` (${frame.finalStatus})` : '.'}`;
        this._ended = { ok, text };
        if (this._run) {
          this._run = {
            ...this._run, status: ok ? 'completed' : 'failed',
            completedSteps: frame.completedSteps ?? this._run.completedSteps, totalSteps: frame.totalSteps ?? this._run.totalSteps,
          };
        }
        this.push({ seq, kind: 'end', bad: !ok, text });
        // The server closes the stream after its last frame; without this the client would reconnect to an ended run.
        this._sse?.disconnect();
        this._sse = null;
        break;
      }
    }
    this.update();
  }

  /** A freshly read plan, with each step as far along as either it or what the stream showed. */
  private merge(plan: PlanView): PlanView {
    if (!this._plan) return plan;
    const shown = new Map(this._plan.steps.map((s) => [s.index, s.status]));
    const steps = plan.steps.map((s) => ({ ...s, status: furthest(s.status, shown.get(s.index) ?? s.status) }));
    return {
      ...plan, steps,
      completedSteps: Math.max(plan.completedSteps, steps.filter((s) => s.status === 'Completed').length),
    };
  }

  private applyStep(frame: Frame): void {
    if (this._plan && frame.stepIndex !== undefined) {
      this._plan = {
        ...this._plan,
        steps: this._plan.steps.map((s) => (s.index === frame.stepIndex ? { ...s, status: frame.stepStatus ?? s.status } : s)),
        completedSteps: frame.completedSteps ?? this._plan.completedSteps,
        totalSteps: frame.totalSteps ?? this._plan.totalSteps,
      };
    }
    if (this._run) {
      this._run = { ...this._run, completedSteps: frame.completedSteps ?? this._run.completedSteps, totalSteps: frame.totalSteps ?? this._run.totalSteps };
    }
  }

  private push(item: FeedItem): void {
    this._feed = [...this._feed, item].slice(-FEED_LIMIT);
  }

  render() {
    const back = `<button class="back" type="button">← All runs</button>`;
    if (this._error) return `${back}<p class="muted">${escapeHtml(this._error)}</p>`;
    const run = this._run;
    if (!run) return `${back}<b-spinner></b-spinner>`;
    const what = run.prompt || this.attr('run-title') || (run.taskId ? `Task ${run.taskId.slice(0, 8)}` : 'Run');
    const live = this._sse ? (this._sse.connected ? '' : '<span class="conn">reconnecting…</span>') : '';
    const total = this._plan?.totalSteps || run.totalSteps;
    const done = this._plan?.completedSteps ?? run.completedSteps;
    return `
      ${back}
      <div class="head">
        <h2 class="what">${escapeHtml(what)}</h2>
        <b-badge class="status" variant="${STATUS_VARIANT[run.status] ?? 'neutral'}">${escapeHtml(run.status)}</b-badge>
        ${live}
      </div>
      ${this._ended ? `<p class="ended ${this._ended.ok ? 'ok' : 'bad'}" role="status">${escapeHtml(this._ended.text)}</p>` : ''}
      <div class="grid">
        <section aria-label="Plan">
          <h3>Plan</h3>
          ${total > 0 ? `<div class="progress"><b-progress value="${Math.round((done / total) * 100)}" max="100" size="sm"></b-progress>${done} of ${total} steps</div>` : ''}
          ${this._plan
            ? `<ol class="steps">${this._plan.steps.map((s) => `<li class="step" data-index="${s.index}" data-status="${escapeHtml(s.status)}">
                <span class="mark" aria-hidden="true">${STEP_MARK[s.status] ?? '○'}</span><span>${escapeHtml(s.title)}</span></li>`).join('')}</ol>`
            : `<p class="muted">${run.taskId ? 'No plan for this task yet.' : 'An ad-hoc run has no plan.'}</p>`}
        </section>
        <section aria-label="Activity">
          <h3>Activity</h3>
          ${this._feed.length
            ? `<ol class="feed">${this._feed.map((f) => `<li class="item${f.bad ? ' bad' : ''}" data-kind="${f.kind}">${escapeHtml(f.text)}${f.detail ? `<span class="detail">${escapeHtml(f.detail)}</span>` : ''}</li>`).join('')}</ol>`
            : `<p class="muted">${this._ended ? 'Nothing was recorded while this page watched; the stream shows a run only from when it is opened.' : 'Waiting for the run’s next step…'}</p>`}
        </section>
      </div>`;
  }

  protected onUpdated() {
    const back = this.$('.back');
    if (back) this.listen(back, 'click', () => this.dispatchEvent(new CustomEvent('run-close', { bubbles: true, composed: true })));
    const feed = this.$('.feed');
    if (feed) feed.scrollTop = feed.scrollHeight;
  }
}

define('kobold-run-view', RunViewPanel);
