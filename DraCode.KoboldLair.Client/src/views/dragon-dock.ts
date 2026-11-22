/**
 * Dragon docked in the project workspace (TASK-151, FEATURE-026 D11/D12): the project's conversation with Dragon on
 * `b-chat`, with streaming text, typing and what Dragon is doing. It shows the page's Dragon connection
 * (`dragonConnection`), which follows the project given in `project-id`. Without `project-id` it is the homepage's
 * "home Dragon" (TASK-149): the user's own conversation with no project, for starting projects; it shows no decision
 * cards (the homepage's Needs you lists them) and no pin. Its header holds the "Keep expanded" pin
 * (TASK-152): the workspace widens the dock while focus is in it, or always while pinned.
 *
 * Decision cards (TASK-160, FEATURE-026 D14/D15) sit above the chat, with their count in the header: the open decisions
 * of this project. A new card never widens the dock; clicking it does (focus enters the dock) and opens it, offering
 * the decision's options and, when allowed, free text. The card is pending until the server's answer comes back.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { BChat, ChatMessage } from 'birko-web-components/layout';
import { KoboldComponent } from '../components/kobold-component.js';
import { dragonConnection, type DecisionCard, type DockState } from '../services/dragon-connection.js';
import { DRAGON_PIN_EVENT, DRAGON_SHORTCUT, isDragonPinned, setDragonPinned } from '../services/dock-layout.js';

const SOURCE_LABEL: Record<string, string> = { Escalation: 'Escalation', AskHuman: 'Question', Approval: 'Review' };

const PLACEHOLDER: Record<DockState['phase'], string> = {
  idle: 'Dragon is starting…',
  'signed-out': 'Sign in to talk to Dragon',
  switching: 'Opening this project’s conversation…',
  ready: 'Ask Dragon about this project…',
  'not-found': 'Dragon cannot open this project',
};

const HOME_PLACEHOLDER: Partial<Record<DockState['phase'], string>> = {
  switching: 'Opening your conversation…',
  ready: 'Ask Dragon to start a project…',
};

export class DragonDock extends KoboldComponent {
  private _unsubscribe: (() => void) | null = null;
  private _shown: ChatMessage[] = [];
  private _state: DockState = dragonConnection.state;
  private _pinned = isDragonPinned();
  /** The card opened by a click, and what was typed into each card's free-text box. */
  private _openCard: string | null = null;
  private readonly _drafts = new Map<string, string>();
  /** The decisions last drawn, to redraw the cards only when they change (a redraw would reset a box being typed in). */
  private _drawn = '';

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
      .pin {
        border: 1px solid transparent; border-radius: var(--b-radius, 0.375rem); background: none; cursor: pointer;
        padding: 0.125rem var(--b-space-xs, 0.25rem); font: inherit; font-size: var(--b-text-xs, 0.75rem); font-weight: normal;
        color: var(--b-text-muted);
      }
      .pin:hover { color: var(--b-text); background: var(--b-bg-tertiary, var(--b-bg-secondary)); }
      .pin[aria-pressed="true"] { color: var(--b-color-primary); border-color: var(--b-color-primary); }
      .pin:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
      /* Below the breakpoint the dock sits under the tabs at full width: nothing to widen. */
      @media (max-width: 56rem) { .pin { display: none; } }
      .conn.offline { color: var(--b-color-warning); }
      b-chat { flex: 1; min-height: 0; }
      .decision-count[hidden] { display: none; }
      .decisions { flex: 0 1 auto; max-height: 45%; overflow-y: auto; border-bottom: 1px solid var(--b-border); }
      .decisions:empty { display: none; }
      .decision { padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem); border-left: 3px solid var(--b-color-warning); }
      .decision + .decision { border-top: 1px solid var(--b-border); }
      .decision-head {
        display: block; width: 100%; padding: 0; border: 0; background: none; text-align: left; cursor: pointer;
        font: inherit; color: var(--b-text);
      }
      .decision-head:focus-visible { outline: none; box-shadow: var(--b-focus-ring); border-radius: var(--b-radius-sm, 0.25rem); }
      .kind { font-size: var(--b-text-xs, 0.75rem); font-weight: var(--b-font-weight-bold, 700); color: var(--b-color-warning-text, var(--b-text-secondary)); }
      .due { font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
      .question {
        display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;
        margin-top: 0.125rem; font-size: var(--b-text-sm, 0.8125rem); overflow-wrap: anywhere;
      }
      .decision.open .question { -webkit-line-clamp: unset; display: block; }
      .context {
        margin: var(--b-space-xs, 0.25rem) 0 0; white-space: pre-wrap; overflow-wrap: anywhere;
        font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-secondary);
      }
      .answers { display: flex; flex-wrap: wrap; gap: var(--b-space-xs, 0.25rem); margin-top: var(--b-space-sm, 0.5rem); }
      .free { display: flex; gap: var(--b-space-xs, 0.25rem); align-items: flex-end; margin-top: var(--b-space-xs, 0.25rem); }
      .free textarea {
        flex: 1; min-width: 0; resize: vertical; padding: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem);
        font: inherit; font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text); background: var(--b-bg);
        border: 1px solid var(--b-border); border-radius: var(--b-radius, 0.375rem);
      }
      .free textarea:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
      .note { margin: var(--b-space-xs, 0.25rem) 0 0; font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
      .note.error { color: var(--b-color-danger-text, var(--b-color-danger)); }
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
    else dragonConnection.selectHome();
    this._unsubscribe = dragonConnection.subscribe((state) => this.show(state));
  }

  protected onUnmount() {
    this._unsubscribe?.();
    this._unsubscribe = null;
  }

  render() {
    const s = this._state;
    const conn = s.phase === 'signed-out' || s.phase === 'idle' ? '' : s.connected ? 'connected' : 'reconnecting…';
    const home = !this.attr('project-id');
    const placeholder = (home ? HOME_PLACEHOLDER[s.phase] : undefined) ?? PLACEHOLDER[s.phase];
    return `
      <div class="dock-head" title="Click into Dragon or press ${DRAGON_SHORTCUT.label} to widen it">🐉 Dragon
        <b-badge class="decision-count" variant="warning" hidden></b-badge><span class="conn${s.connected ? '' : ' offline'}">${conn}</span>
        ${home ? '' : `<button class="pin" type="button" aria-pressed="${this._pinned}" title="Keep Dragon wide when focus leaves it">📌 Keep expanded</button>`}
      </div>
      <section class="decisions" data-morph="skip" aria-label="Decisions waiting for you"></section>
      <b-chat ${s.phase === 'ready' && s.connected ? '' : 'disabled'} placeholder="${escapeHtml(placeholder)}">
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
    const pin = this.$('.pin');
    if (pin) this.listen(pin, 'click', () => this.togglePin());
    const decisions = this.$('.decisions');
    if (decisions) {
      this.listen(decisions, 'click', (e) => this.onDecisionClick(e));
      this.listen(decisions, 'input', (e) => {
        const box = e.target as HTMLTextAreaElement;
        if (box.dataset.draft) this._drafts.set(box.dataset.draft, box.value);
      });
      this.listen<KeyboardEvent>(decisions, 'keydown', (e) => {
        const box = e.target as HTMLTextAreaElement;
        if (e.key === 'Enter' && !e.shiftKey && box.dataset.draft) {
          e.preventDefault();
          this.answer(box.dataset.draft, box.value);
        }
      });
    }
    this._drawn = '';
    this.drawDecisions();
  }

  /** This project's open decisions. */
  private cards(): DecisionCard[] {
    const name = this._state.projectName;
    return name ? this._state.decisions.filter((d) => d.projectName === name) : [];
  }

  /** Draw the cards and the header's count, when the cards changed since they were last drawn. */
  private drawDecisions(): void {
    const section = this.$('.decisions');
    const badge = this.$('.decision-count');
    if (!section || !badge) return;
    const cards = this.cards();
    const key = JSON.stringify([cards, this._openCard]);
    if (key === this._drawn) return;
    this._drawn = key;
    badge.textContent = cards.length === 1 ? '1 decision' : `${cards.length} decisions`;
    badge.toggleAttribute('hidden', cards.length === 0);
    // Focus inside a card survives the redraw: back to the same box, else the card's head; an answered card's focus goes
    // to the chat input. Left on nothing, focus would leave the dock and narrow it (TASK-152).
    const active = this.shadowRoot?.activeElement as HTMLElement | null;
    const focusedCard = active && section.contains(active) ? active.closest<HTMLElement>('article.decision')?.dataset.decisionId : undefined;
    const inBox = !!active?.dataset?.draft;
    section.innerHTML = cards.map((d) => this.cardHtml(d)).join('');
    if (focusedCard === undefined) return;
    const article = this.$(`article[data-decision-id="${CSS.escape(focusedCard)}"]`);
    const box = inBox ? article?.querySelector<HTMLTextAreaElement>('textarea:not([disabled])') : null;
    if (box) box.focus();
    else if (article) article.querySelector<HTMLElement>('.decision-head')?.focus();
    else this.chat()?.focusInput();
  }

  private cardHtml(d: DecisionCard): string {
    const open = d.id === this._openCard;
    const id = escapeHtml(d.id);
    const due = d.timeoutAt && Date.parse(d.timeoutAt)
      ? ` <span class="due">· answer by ${escapeHtml(new Date(d.timeoutAt).toLocaleString([], { dateStyle: 'short', timeStyle: 'short' }))}</span>` : '';
    const body = !open ? '' : `
      ${d.context ? `<p class="context">${escapeHtml(d.context)}</p>` : ''}
      ${d.options.length ? `<div class="answers">${d.options.map((o) => `<b-button size="sm" variant="secondary" data-answer="${escapeHtml(o)}"
        ${d.pending ? 'disabled' : ''}>${escapeHtml(o)}</b-button>`).join('')}</div>` : ''}
      ${d.allowFreeText ? `<div class="free"><textarea rows="2" data-draft="${id}" aria-label="Your answer"
        placeholder="${d.options.length ? 'Or write your own answer…' : 'Your answer…'}" ${d.pending ? 'disabled' : ''}>${escapeHtml(this._drafts.get(d.id) ?? '')}</textarea>
        <b-button size="sm" variant="primary" data-send ${d.pending ? 'loading' : ''}>Send</b-button></div>` : ''}
      ${d.pending ? `<p class="note">Sending your answer… it is taken once Dragon finishes what it is writing.</p>` : ''}
      ${d.error ? `<p class="note error" role="alert">${escapeHtml(d.error)}</p>` : ''}`;
    return `<article class="decision${open ? ' open' : ''}" data-decision-id="${id}">
      <button class="decision-head" type="button" aria-expanded="${open}">
        <span class="kind">${escapeHtml(SOURCE_LABEL[d.source] ?? 'Decision')}</span>${due}
        <span class="question">${escapeHtml(d.question)}</span>
      </button>${body}
    </article>`;
  }

  private onDecisionClick(e: Event): void {
    const path = e.composedPath();
    const card = path.find((n): n is HTMLElement => n instanceof HTMLElement && n.matches('article.decision'));
    if (!card) return;
    const id = card.dataset.decisionId!;
    const hit = (selector: string) => path.find((n): n is HTMLElement => n instanceof HTMLElement && n.matches(selector));
    const option = hit('b-button[data-answer]');
    if (option) return void this.answer(id, option.dataset.answer!);
    if (hit('b-button[data-send]')) return void this.answer(id, this._drafts.get(id) ?? '');
    if (hit('.decision-head')) {
      this._openCard = this._openCard === id ? null : id;
      this.drawDecisions();
      (this.$(`article[data-decision-id="${CSS.escape(id)}"] .decision-head`) as HTMLElement | null)?.focus();
    }
  }

  private answer(id: string, text: string): void {
    const card = this.cards().find((d) => d.id === id);
    if (!card || card.pending || !text.trim()) return;
    if (dragonConnection.answer(id, text.trim())) this._drafts.delete(id);
  }

  /** Put focus in Dragon's input (the workspace's shortcut); false when the input cannot take it. */
  focusInput(): boolean {
    this.chat()?.focusInput();
    return this.shadowRoot?.activeElement != null;
  }

  private togglePin(): void {
    this._pinned = !this._pinned;
    setDragonPinned(this._pinned);
    this.$('.pin')?.setAttribute('aria-pressed', String(this._pinned));
    this.dispatchEvent(new CustomEvent(DRAGON_PIN_EVENT, { detail: { pinned: this._pinned }, bubbles: true, composed: true }));
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
    this.drawDecisions();
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
