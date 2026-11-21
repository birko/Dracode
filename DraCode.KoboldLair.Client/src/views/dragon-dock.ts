/**
 * Dragon docked in the project workspace (TASK-151, FEATURE-026 D11/D12): the project's conversation with Dragon on
 * `b-chat`, with streaming text, typing and what Dragon is doing. It shows the page's Dragon connection
 * (`dragonConnection`), which follows the project given in `project-id`.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { BChat, ChatMessage } from 'birko-web-components/layout';
import { KoboldComponent } from '../components/kobold-component.js';
import { dragonConnection, type DockState } from '../services/dragon-connection.js';

const PLACEHOLDER: Record<DockState['phase'], string> = {
  idle: 'Dragon is starting…',
  'signed-out': 'Sign in to talk to Dragon',
  switching: 'Opening this project’s conversation…',
  ready: 'Ask Dragon about this project…',
  'not-found': 'Dragon cannot open this project',
};

export class DragonDock extends KoboldComponent {
  private _unsubscribe: (() => void) | null = null;
  private _shown: ChatMessage[] = [];
  private _state: DockState = dragonConnection.state;

  static get styles() {
    return `
      :host {
        display: flex; flex-direction: column; min-width: 0; min-height: 0; height: 100%;
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem);
        overflow: hidden;
      }
      .dock-head {
        display: flex; align-items: center; gap: var(--b-space-sm, 0.5rem);
        padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem);
        border-bottom: 1px solid var(--b-border);
        font-weight: var(--b-font-weight-bold, 700); color: var(--b-text);
      }
      .conn { margin-left: auto; font-size: var(--b-text-xs, 0.75rem); font-weight: normal; color: var(--b-text-muted); }
      .conn.offline { color: var(--b-color-warning); }
      b-chat { flex: 1; min-height: 0; }
      .status {
        min-height: 1.25rem; padding: 0 var(--b-space-md, 0.75rem) var(--b-space-xs, 0.25rem);
        font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted);
        overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
      }
    `;
  }

  protected onMount() {
    const id = this.attr('project-id');
    if (id) dragonConnection.select(id);
    this._unsubscribe = dragonConnection.subscribe((state) => this.show(state));
  }

  protected onUnmount() {
    this._unsubscribe?.();
    this._unsubscribe = null;
  }

  render() {
    const s = this._state;
    const conn = s.phase === 'signed-out' || s.phase === 'idle' ? '' : s.connected ? 'connected' : 'reconnecting…';
    return `
      <div class="dock-head">🐉 Dragon<span class="conn${s.connected ? '' : ' offline'}">${conn}</span></div>
      <b-chat ${s.phase === 'ready' && s.connected ? '' : 'disabled'} placeholder="${escapeHtml(PLACEHOLDER[s.phase])}">
        <div slot="footer" class="status">${escapeHtml(s.status ?? '')}</div>
      </b-chat>
    `;
  }

  protected onUpdated() {
    const chat = this.chat();
    if (!chat) return;
    chat.setConfig({ renderHtml: true, showTimestamps: false, sendLabel: 'Send' });
    this._shown = [];
    this.sync(chat);
    this.listen<CustomEvent<{ text: string }>>(chat, 'send', (e) => {
      if (!dragonConnection.send(e.detail.text)) chat.focusInput();
    });
  }

  private chat(): BChat | null {
    return this.$('b-chat') as BChat | null;
  }

  /** Re-render only when the frame changes (phase, connection, placeholder); messages, typing and status go to b-chat. */
  private show(state: DockState): void {
    const before = this._state;
    this._state = state;
    if (!this.isConnected) return;
    if (before.phase !== state.phase || before.connected !== state.connected || !this.chat()) {
      this.update();
      return;
    }
    const chat = this.chat()!;
    this.sync(chat);
    const status = this.$('.status');
    if (status) status.textContent = state.status ?? '';
  }

  /** Bring b-chat to the state's messages: a streamed reply grows in place, anything else resets the list. */
  private sync(chat: BChat): void {
    const next = this._state.messages;
    const prev = this._shown;
    const sameUpToLast = next.length === prev.length && next.length > 0
      && next.slice(0, -1).every((m, i) => m === prev[i]) && next[next.length - 1].id === prev[prev.length - 1].id;
    if (sameUpToLast) {
      const last = next[next.length - 1];
      if (last !== prev[prev.length - 1]) chat.updateMessage(last.id, { content: last.content });
    } else if (next !== prev) {
      chat.setMessages(next);
    }
    this._shown = next;
    chat.setTyping(this._state.typing);
  }
}

define('kobold-dragon-dock', DragonDock);
