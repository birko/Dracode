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
 */

import type { ChatMessage } from 'birko-web-components/layout';
import { WsClient, type WsClientOptions } from 'birko-web-core/http';
import { getToken } from '../auth-store.js';
import { renderMarkdown } from './markdown.js';

export type DockPhase = 'idle' | 'signed-out' | 'switching' | 'ready' | 'not-found';

export interface DockState {
  projectId: string | null;
  phase: DockPhase;
  connected: boolean;
  messages: ChatMessage[];
  typing: boolean;
  /** What Dragon (or a council member) is doing right now, while it works on a reply. */
  status: string | null;
}

type Listener = (state: DockState) => void;
type Msg = Record<string, unknown>;

const str = (v: unknown): string => (typeof v === 'string' ? v : '');

/** How long to wait for `session_resumed` / `session_not_found` after reconnecting before selecting again anyway. */
const RESUME_WAIT_MS = 1500;

export class DragonConnection {
  private _state: DockState = { projectId: null, phase: 'idle', connected: false, messages: [], typing: false, status: null };
  private readonly _listeners = new Set<Listener>();
  private _client: WsClient | null = null;
  private _options: WsClientOptions | null = null;
  private _sessionId: string | null = null;
  /** The project the server last confirmed with `project_selected`. */
  private _serverProject: string | null = null;
  private _awaitingResume = false;
  private _resumeTimer: ReturnType<typeof setTimeout> | null = null;
  private _streamId: string | null = null;
  private _streamText = '';
  private _seq = 0;

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
    if (!getToken()) {
      this.set({ projectId, phase: 'signed-out', messages: [], typing: false, status: null });
      return;
    }
    if (this._state.projectId === projectId && (this._state.phase === 'switching' || this._state.phase === 'ready')) return;
    this.endStream();
    this.set({ projectId, phase: 'switching', messages: [], typing: false, status: null });
    if (!this._client) this.connect();
    else if (this._client.connected && !this._awaitingResume) this.sendSelect();
  }

  /** Send the person's message to Dragon; false when no project's conversation is open yet. */
  send(text: string): boolean {
    if (this._state.phase !== 'ready' || !this._client?.connected) return false;
    this.endStream();
    this.set({
      messages: [...this._state.messages, { id: this.nextId('user'), role: 'user', content: text, timestamp: Date.now() }],
      typing: true,
    });
    this._client.send(JSON.stringify({ type: 'message', message: text }));
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
    } else if (this._state.projectId) {
      this.sendSelect();
    }
  }

  /** After a reconnect: the session came back (with the server's project) or it did not. */
  private resumeSettled(resumed: boolean): void {
    if (this._resumeTimer) clearTimeout(this._resumeTimer);
    this._resumeTimer = null;
    if (!this._awaitingResume) return;
    this._awaitingResume = false;
    const projectId = this._state.projectId;
    if (resumed && projectId && projectId === this._serverProject) {
      this.endStream();
      this.set({ phase: 'ready', messages: [], typing: false, status: null }); // the replay that follows rebuilds it
    } else if (projectId) {
      this.set({ phase: 'switching', messages: [], typing: false, status: null });
      this.sendSelect();
    }
  }

  private sendSelect(): void {
    if (this._state.projectId) this._client?.send(JSON.stringify({ type: 'select_project', projectId: this._state.projectId }));
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
      if (this._serverProject === this._state.projectId) {
        this.endStream();
        this.set({ phase: 'ready', messages: [], typing: false, status: null });
      }
      return;
    }

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
        if (msg.statusType === 'complete' || msg.statusType === 'error') this.set({ status: null });
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
