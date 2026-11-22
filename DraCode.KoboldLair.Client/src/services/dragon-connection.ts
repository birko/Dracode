/**
 * The page's one `/dragon` connection and the docked chat's state (TASK-151, FEATURE-026 D12). It goes through the
 * client host's proxy (same origin), on Birko's WsClient, which reconnects with backoff. It lives for the page, not for a
 * dock: opening another project re-creates the workspace, and the new dock picks up the same session.
 *
 * Following a project: `select(id)` sends `select_project`; the server answers `project_selected` and replays that
 * project's conversation. Until then, live Dragon traffic belongs to the project being left and is dropped. After a
 * dropped connection it reconnects with its `sessionId`: `session_resumed` replays the session (the selected project's
 * conversation); if the session is gone (`session_not_found`, e.g. after a server restart) it selects the project again,
 * and the server reloads its saved conversation.
 *
 * Decision cards (TASK-160, FEATURE-026 D14/D15): once a project is open (and after every resumed session) it sends
 * `client_ready`, and the server answers with a `decision_required` card for each open decision of the user's projects;
 * new ones arrive live, and `decision_resolved` retires one. Cards are kept for every project and the dock shows the
 * followed project's. `answer(id, text)` sends `answer_decision`; the card is pending until `decision_answer_result`,
 * which can come only after a reply Dragon is still writing.
 *
 * The home conversation (TASK-149, FEATURE-026 D12): `selectHome()` sends `select_home` for the homepage, where no
 * project is open; the server answers `home_selected` and replays the user's own home conversation, kept apart from every
 * project's. A specification Dragon writes there registers a project; `onProjectsChanged` tells the homepage to reload.
 *
 * Another project Dragon loaded from the chat (TASK-190): the session stays on the followed project and the server
 * sends `project_switch_offered`; the state's `offer` holds it until the person answers (`dismissOffer`) or the dock
 * follows another project.
 *
 * One reply at a time (TASK-187): the server answers a session's messages in order, each request ending with a
 * `dragon_status` of `complete` or `error`. A message sent while Dragon is still answering is shown as waiting
 * (`status: 'sending'`, and a line under the chat) until the reply before it is done.
 *
 * The connection belongs to the signed-in user (TASK-056): signing out, or another user signing in, closes it and
 * forgets its session, so the next user never reuses it; a refreshed token for the same user keeps it.
 */

import type { ChatMessage } from 'birko-web-components/layout';
import { WsClient, type WsClientOptions } from 'birko-web-core/http';
import { authStore, getToken } from '../auth-store.js';
import { renderMarkdown } from './markdown.js';
import { notificationStore } from '../stores/notification-store.js';
import { needsYouStore } from '../stores/needs-you-store.js';

export type DockPhase = 'idle' | 'signed-out' | 'switching' | 'ready' | 'not-found';

export interface DockState {
  projectId: string | null;
  /** Following the home conversation (no project). */
  home: boolean;
  phase: DockPhase;
  connected: boolean;
  messages: ChatMessage[];
  typing: boolean;
  /** What Dragon (or a council member) is doing right now, while it works on a reply. */
  status: string | null;
  /** The followed project's name, as the server confirmed it (decision cards carry the name). */
  projectName: string | null;
  /** Open decisions of every project the user can see, oldest first. */
  decisions: DecisionCard[];
  /** A project Dragon loaded from the chat, offered to open (TASK-190); null when none is offered. */
  offer: { projectId: string; projectName: string } | null;
}

/** An open decision an agent waits on (`decision_required`, TASK-024's metadata contract). */
export interface DecisionCard {
  id: string;
  projectName: string;
  question: string;
  context: string;
  /** Escalation, AskHuman or Approval. */
  source: string;
  options: string[];
  allowFreeText: boolean;
  timeoutAt: string | null;
  /** An answer was sent and its result has not come back. */
  pending: boolean;
  /** Why the last answer was not taken (an answer not among the options). */
  error: string | null;
}

type Listener = (state: DockState) => void;
type Msg = Record<string, unknown>;

const str = (v: unknown): string => (typeof v === 'string' ? v : '');

/** How long to wait for `session_resumed` / `session_not_found` after reconnecting before selecting again anyway. */
const RESUME_WAIT_MS = 1500;

/** What the server follows for the home conversation, in place of a project id. */
const HOME = '#home';

export class DragonConnection {
  private _state: DockState = {
    projectId: null, home: false, phase: 'idle', connected: false, messages: [], typing: false, status: null, projectName: null, decisions: [],
    offer: null,
  };
  private readonly _listeners = new Set<Listener>();
  private _client: WsClient | null = null;
  private _options: WsClientOptions | null = null;
  private _sessionId: string | null = null;
  /** The project the server last confirmed with `project_selected`, or HOME after `home_selected`. */
  private _serverProject: string | null = null;
  private readonly _projectListeners = new Set<() => void>();
  private _awaitingResume = false;
  private _resumeTimer: ReturnType<typeof setTimeout> | null = null;
  private _streamId: string | null = null;
  private _streamText = '';
  private _seq = 0;
  /** Messages sent and not yet answered in this conversation, and the ids of those waiting behind the one being answered. */
  private _inFlight = 0;
  private _waiting: string[] = [];
  /** Whose connection this is. */
  private _userId: string | null = authStore.get('userId') as string | null;

  constructor() {
    authStore.onChange('token', () => {
      const userId = getToken() ? authStore.get('userId') as string | null : null;
      if (userId !== this._userId || !getToken()) this.reset();
      this._userId = userId;
    });
  }

  /** Close the connection and forget its session and cards; the next select() connects afresh. */
  reset(): void {
    if (this._resumeTimer) clearTimeout(this._resumeTimer);
    this._resumeTimer = null;
    this._awaitingResume = false;
    this._client?.disconnect();
    this._client = null;
    this._options = null;
    this._sessionId = null;
    this._serverProject = null;
    this.forgetPending();
    this.endStream();
    this.set({ projectId: null, home: false, phase: 'idle', connected: false, messages: [], typing: false, status: null, projectName: null, decisions: [], offer: null });
  }

  get state(): DockState {
    return this._state;
  }

  subscribe(listener: Listener): () => void {
    this._listeners.add(listener);
    listener(this._state);
    return () => this._listeners.delete(listener);
  }

  /** Follow `projectId`: its conversation replaces the current one. Selecting the project already followed does nothing. */
  select(projectId: string): void {
    this.follow(projectId, false);
  }

  /** Follow the user's home conversation (the homepage's Dragon). Already following it does nothing. */
  selectHome(): void {
    this.follow(null, true);
  }

  /** The person declined the offered project: the session stays where it is (TASK-190). */
  dismissOffer(): void {
    if (this._state.offer) this.set({ offer: null });
  }

  /** Called when Dragon registered a project (a specification it wrote); returns the unsubscribe. */
  onProjectsChanged(listener: () => void): () => void {
    this._projectListeners.add(listener);
    return () => this._projectListeners.delete(listener);
  }

  private follow(projectId: string | null, home: boolean): void {
    if (!getToken()) {
      this.set({ projectId, home, phase: 'signed-out', messages: [], typing: false, status: null });
      return;
    }
    if (this.target() === (home ? HOME : projectId) && (this._state.phase === 'switching' || this._state.phase === 'ready')) return;
    this.endStream();
    this.forgetPending();
    this.set({ projectId, home, phase: 'switching', messages: [], typing: false, status: null, projectName: null, offer: null });
    if (!this._client) this.connect();
    else if (this._client.connected && !this._awaitingResume) this.sendSelect();
  }

  /** What the dock follows: a project id, HOME, or nothing yet. */
  private target(): string | null {
    return this._state.home ? HOME : this._state.projectId;
  }

  /** Send the person's message to Dragon; false when no project's conversation is open yet. */
  send(text: string): boolean {
    if (this._state.phase !== 'ready' || !this._client?.connected) return false;
    const waits = this._inFlight > 0;
    const id = this.nextId('user');
    this._inFlight++;
    if (waits) this._waiting.push(id);
    else this.endStream();
    this.set({
      messages: [...this._state.messages, { id, role: 'user', content: text, timestamp: Date.now(), ...(waits ? { status: 'sending' as const } : {}) }],
      typing: true,
      ...(waits ? { status: 'Your message waits until Dragon finishes this reply' } : {}),
    });
    this._client.send(JSON.stringify({ type: 'message', message: text }));
    return true;
  }

  /** Answer a decision card; false when it cannot be sent now. */
  answer(decisionId: string, answer: string): boolean {
    if (!answer.trim() || !this._client?.connected || !this._state.decisions.some((d) => d.id === decisionId)) return false;
    this.patchDecision(decisionId, { pending: true, error: null });
    this._client.send(JSON.stringify({ type: 'answer_decision', decisionId, answer }));
    return true;
  }

  private connect(): void {
    const origin = window.location.origin.replace(/^http/, 'ws');
    this._options = {
      url: `${origin}/dragon`,
      getToken,
      reconnectMs: 2000,
      onOpen: () => this.onOpen(),
    };
    this._client = new WsClient(this._options);
    this._client.on('message', (data) => this.onMessage(data as Msg));
    this._client.on('_close', () => this.set({ connected: false, typing: false, status: null }));
    this._client.connect();
  }

  private onOpen(): void {
    this.set({ connected: true });
    if (this._sessionId) {
      // A reconnect: the server answers session_resumed or session_not_found first. Until then the old chat stays in
      // view but closed, as it may be about to be replaced.
      this._awaitingResume = true;
      if (this._state.phase === 'ready') this.set({ phase: 'switching' });
      this._resumeTimer = setTimeout(() => this.resumeSettled(false), RESUME_WAIT_MS);
    } else if (this.target()) {
      this.sendSelect();
    }
  }

  /** After a reconnect: the session came back (with the server's project) or it did not. */
  private resumeSettled(resumed: boolean): void {
    if (this._resumeTimer) clearTimeout(this._resumeTimer);
    this._resumeTimer = null;
    if (!this._awaitingResume) return;
    this._awaitingResume = false;
    const target = this.target();
    this.forgetPending();
    if (resumed && target && target === this._serverProject) {
      this.endStream();
      this.set({ phase: 'ready', messages: [], typing: false, status: null }); // the replay that follows rebuilds it
      this.sendReady();
    } else if (target) {
      this.set({ phase: 'switching', messages: [], typing: false, status: null });
      this.sendSelect();
    }
  }

  /** Ask for the open decisions again: cards answered or timed out while away are not sent, so they go. */
  private sendReady(): void {
    this.set({ decisions: [] });
    this._client?.send(JSON.stringify({ type: 'client_ready' }));
  }

  private sendSelect(): void {
    if (this._state.home) this._client?.send(JSON.stringify({ type: 'select_home' }));
    else if (this._state.projectId) this._client?.send(JSON.stringify({ type: 'select_project', projectId: this._state.projectId }));
  }

  private onMessage(msg: Msg): void {
    if (!msg || typeof msg !== 'object') return;
    const type = str(msg.type);
    const sessionId = str(msg.sessionId) || str(msg.newSessionId);
    if (sessionId && sessionId !== this._sessionId) {
      this._sessionId = sessionId;
      if (this._options) this._options.url = `${window.location.origin.replace(/^http/, 'ws')}/dragon?sessionId=${encodeURIComponent(sessionId)}`;
    }

    if (type === 'session_resumed') return this.resumeSettled(true);
    if (type === 'session_not_found') return this.resumeSettled(false);

    if (type === 'project_selected') {
      this._serverProject = str(msg.projectId);
      if (!this._state.home && this._serverProject === this._state.projectId) {
        this.endStream();
        this.set({ phase: 'ready', messages: [], typing: false, status: null, projectName: str(msg.projectName) || null });
        this.sendReady();
      }
      return;
    }

    if (type === 'project_switch_offered') {
      const projectId = str(msg.projectId);
      if (projectId && !this._state.home && projectId !== this._state.projectId && msg.isReplay !== true) {
        this.set({ offer: { projectId, projectName: str(msg.projectName) || projectId } });
      }
      return;
    }

    if (type === 'home_selected') {
      this._serverProject = HOME;
      if (this._state.home) {
        this.endStream();
        this.set({ phase: 'ready', messages: [], typing: false, status: null, projectName: null });
        this.sendReady();
      }
      return;
    }

    // A project Dragon registered: lists of projects reload. The message itself is shown below, as a live one.
    if (type === 'specification_created' && msg.isReplay !== true) {
      for (const listener of this._projectListeners) listener();
    }

    // Decisions belong to the user, not to the conversation, so they count whatever project is open. A replayed one is
    // history: client_ready brings what is still open.
    if (type === 'project_notification') {
      if (msg.isReplay !== true) this.onNotification(msg);
      return;
    }
    if (type === 'decision_answer_result') return this.onAnswerResult(msg);

    if (msg.isReplay === true) {
      const replayed = this.toMessage(msg);
      if (replayed && this._state.phase === 'ready') this.set({ messages: [...this._state.messages, replayed] });
      return;
    }

    if (type === 'error' && str(msg.errorType) === 'project_not_found') {
      if (this._state.phase === 'switching' && str(msg.projectId) === this._state.projectId) {
        this.set({ phase: 'not-found', typing: false, status: null });
      }
      return;
    }

    // Live traffic while switching belongs to the project being left.
    if (this._state.phase !== 'ready') return;

    switch (type) {
      case 'dragon_typing':
        this.set({ typing: true });
        break;
      case 'dragon_thinking': {
        const who = str(msg.agent);
        const what = str(msg.description);
        if (what) this.set({ typing: true, status: who && who !== 'Dragon' ? `${who}: ${what}` : what });
        break;
      }
      case 'dragon_status':
        if (msg.statusType === 'complete' || msg.statusType === 'error') this.replyDone();
        break;
      case 'dragon_stream':
        this.appendStream(str(msg.chunk));
        break;
      case 'dragon_message':
        this.finishReply(str(msg.message));
        break;
      default: {
        const shown = this.toMessage(msg);
        if (shown) this.set({ messages: [...this._state.messages, shown], typing: false, status: null });
      }
    }
  }

  private onNotification(msg: Msg): void {
    const meta = (msg.metadata && typeof msg.metadata === 'object' ? msg.metadata : {}) as Record<string, unknown>;
    const id = str(meta.decisionId);
    if (!id) return;
    if (str(msg.notificationType) === 'decision_resolved') {
      this.dropDecision(id);
      return;
    }
    if (str(msg.notificationType) !== 'decision_required') return;
    let schema: { kind?: string; options?: unknown; allowFreeText?: unknown } = {};
    try {
      schema = JSON.parse(str(meta.responseSchema) || '{}');
    } catch {
      // Answered in free text, as a decision with no schema is.
    }
    const options = Array.isArray(schema.options) ? schema.options.filter((o): o is string => typeof o === 'string') : [];
    const card: DecisionCard = {
      id,
      projectName: str(msg.projectName),
      question: str(meta.question) || str(msg.message),
      context: str(meta.context),
      source: str(meta.source),
      options,
      allowFreeText: schema.allowFreeText !== false || options.length === 0,
      timeoutAt: str(meta.timeoutAt) || null,
      pending: false,
      error: null,
    };
    const known = this._state.decisions.find((d) => d.id === id);
    this.set({
      decisions: known
        ? this._state.decisions.map((d) => (d.id === id ? { ...card, pending: d.pending, error: d.error } : d))
        : [...this._state.decisions, card],
    });
  }

  private onAnswerResult(msg: Msg): void {
    const id = str(msg.decisionId);
    if (str(msg.result) === 'InvalidAnswer') {
      this.patchDecision(id, { pending: false, error: str(msg.message) || 'That answer was not taken.' });
      return;
    }
    // Resolved, or already answered / timed out / gone: either way nothing is left to answer. The server's words say which.
    const said = str(msg.message);
    if (said && this._state.phase === 'ready') {
      this.set({ messages: [...this._state.messages, { id: this.nextId('decision'), role: 'system', content: said, timestamp: Date.now() }] });
    }
    this.dropDecision(id);
  }

  private dropDecision(id: string): void {
    if (!this._state.decisions.some((d) => d.id === id)) return;
    this.set({ decisions: this._state.decisions.filter((d) => d.id !== id) });
    void notificationStore.refresh(); // the bell drops the card's notification, which the server has marked read
    void needsYouStore.refresh(); // and Needs you its item (TASK-147)
  }

  private patchDecision(id: string, patch: Partial<DecisionCard>): void {
    this.set({ decisions: this._state.decisions.map((d) => (d.id === id ? { ...d, ...patch } : d)) });
  }

  /** A saved or live message as a chat bubble; null for what the dock does not show (notifications, decisions…). */
  private toMessage(msg: Msg): ChatMessage | null {
    const timestamp = Date.parse(str(msg.timestamp)) || Date.now();
    const id = str(msg.messageId) || this.nextId('m');
    switch (str(msg.type)) {
      case 'user_message':
        return { id, role: 'user', content: str(msg.content) || str(msg.message), timestamp };
      case 'dragon_message':
        return { id, role: 'assistant', content: renderMarkdown(str(msg.message)), timestamp };
      case 'specification_created':
        return { id, role: 'system', content: 'Specification saved.', timestamp };
      case 'error':
        return { id, role: 'system', content: str(msg.message) || 'Something went wrong.', status: 'error', timestamp };
      default:
        return null;
    }
  }

  private appendStream(chunk: string): void {
    if (!chunk) return;
    this._streamText += chunk;
    const content = renderMarkdown(this._streamText);
    if (!this._streamId) {
      this._streamId = this.nextId('stream');
      this.set({
        messages: [...this._state.messages, { id: this._streamId, role: 'assistant', content, timestamp: Date.now() }],
        typing: false,
      });
    } else {
      const id = this._streamId;
      this.set({ messages: this._state.messages.map((m) => (m.id === id ? { ...m, content } : m)), typing: false });
    }
  }

  /** The final reply: it replaces the streamed text (it is the whole answer), or arrives alone when nothing streamed. */
  private finishReply(text: string): void {
    const content = renderMarkdown(text);
    const id = this._streamId;
    this.endStream();
    const messages = id
      ? this._state.messages.map((m) => (m.id === id ? { ...m, content } : m))
      : [...this._state.messages, { id: this.nextId('dragon'), role: 'assistant' as const, content, timestamp: Date.now() }];
    this.set({ messages, typing: false, status: null });
  }

  /** A request ended: the next waiting message is the one Dragon answers now. */
  private replyDone(): void {
    if (this._inFlight > 0) this._inFlight--;
    const next = this._waiting.shift();
    this.endStream();
    if (!next) {
      this.set({ status: null, ...(this._inFlight === 0 ? { typing: false } : {}) });
      return;
    }
    this.set({
      messages: this._state.messages.map((m) => (m.id === next ? { ...m, status: undefined } : m)),
      typing: true,
      status: this._waiting.length ? 'Your message waits until Dragon finishes this reply' : null,
    });
  }

  /** The conversation was replaced (a switch, a resume, a reset): nothing of this one is still waiting here. */
  private forgetPending(): void {
    this._inFlight = 0;
    this._waiting = [];
  }

  private endStream(): void {
    this._streamId = null;
    this._streamText = '';
  }

  private nextId(prefix: string): string {
    return `${prefix}-${++this._seq}`;
  }

  private set(patch: Partial<DockState>): void {
    this._state = { ...this._state, ...patch };
    for (const listener of this._listeners) listener(this._state);
  }
}

/** The page's Dragon connection. */
export const dragonConnection = new DragonConnection();
