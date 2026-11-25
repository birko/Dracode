/**
 * Project workspace frame (TASK-150, FEATURE-026 D11): a project opens at `#/projects/{id}/{tab}` with its tabs in the
 * centre, in D11's order. A tab is listed once its screen exists: Board (TASK-153), Spec (TASK-150), Runs (TASK-155), Git (TASK-156) and Stats (TASK-158). An unknown or not-owned project (the API
 * answers 404 for both) and an unknown tab show the not-found view. Dragon is docked on the right and follows the
 * project (TASK-151). Dragon widens with focus (TASK-152, D13): ~360px, ~60% of the width while focus is inside it (a
 * click, or the shortcut) or while pinned; no timers.
 *
 * By window width (TASK-164, D21/D22): on a tablet Dragon opens as an overlay over the page (`dock-overlay.ts`); on a
 * phone there is no dock (Dragon is the tab bar's own tab), Git is not offered and a link to it says it is on a
 * computer, and the other tabs are read only. Opening a project makes it the one the phone's Board and Dragon tabs
 * follow.
 *
 * Layouts (TASK-058), each person's own across their projects (`layoutStore`):
 * - The tabs can be reordered (`b-tabs reorderable`), by drag or Ctrl+Shift+←/→, and the order is kept. A phone shows
 *   them in that order but cannot reorder them.
 * - On a desktop the tabs and Dragon sit in a `b-split-panel`. Its divider sets Dragon's width at rest, kept as Dragon's
 *   own width (not the tabs'), so a wider window gives the tabs the room. Focus or the pin still widen Dragon to ~60%
 *   and give the width back; while Dragon is wide there is no divider.
 * - The Layouts menu (not on a phone) saves the tab order, Dragon's width, the pin and the open tab by name, loads one
 *   (which becomes the current layout and opens its tab) and deletes one.
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';
import { getToken } from '../auth-store.js';
import type { ProjectSummary } from './home-view.js';
import './board-tab.js';
import './spec-tab.js';
import './runs-tab.js';
import './git-tab.js';
import './stats-tab.js';
import './dragon-dock.js';
import type { DragonDock } from './dragon-dock.js';
import { DRAGON_PIN_EVENT, DRAGON_SHORTCUT, isDragonPinned } from '../services/dock-layout.js';
import type { BDropdownMenu, BSplitPanel, BTabs, DropdownItem, SplitSizes } from 'birko-web-components/layout';
import { toast } from 'birko-web-components/feedback';
import { layoutStore, WORKSPACE_KEY, type SavedLayout, type WorkspaceLayout } from '../stores/layout-store.js';
import { layoutMode, onLayoutModeChange, rememberProject, type LayoutMode } from '../services/layout-mode.js';
import { applyDockOverlay, DOCK_CLOSE_HTML, dockOverlayStyles, dragonToggleHtml, wireDockOverlay } from '../services/dock-overlay.js';

/** The workspace tabs whose screens exist, in display order: id, label and the element that renders it. */
export const WORKSPACE_TABS: readonly { id: string; label: string; tag: string }[] = [
  { id: 'board', label: 'Board', tag: 'kobold-board-tab' },
  { id: 'spec', label: 'Spec', tag: 'kobold-spec-tab' },
  { id: 'runs', label: 'Runs', tag: 'kobold-runs-tab' },
  { id: 'git', label: 'Git', tag: 'kobold-git-tab' },
  { id: 'stats', label: 'Stats', tag: 'kobold-stats-tab' },
];

export const DEFAULT_TAB = WORKSPACE_TABS[0].id;

/** Tabs a phone does not offer (D21: Git stays on a computer). */
const DESKTOP_ONLY_TABS = new Set(['git']);

/** Dragon at rest until the person sets a width (TASK-152's 360px), and while widened (~60%, D13). */
const REST_SIZES: SplitSizes = { master: '1fr', detail: '360px' };
const WIDE_SIZES: SplitSizes = { master: '1fr', detail: '60%' };
/** The narrowest Dragon a drag leaves, and the least room it leaves the tabs. */
const MIN_DRAGON_PX = 280;
const MIN_TABS_PX = 320;

/** A saved layout in a word or two for the menu: the tab it opens on, Dragon's width, the pin. */
function summary(layout: WorkspaceLayout): string {
  const parts: string[] = [];
  const tab = WORKSPACE_TABS.find((t) => t.id === layout.openTab);
  if (tab) parts.push(tab.label);
  if (layout.dragonPinned) parts.push('Dragon wide');
  else if (layout.splits?.[WORKSPACE_KEY]?.detail) parts.push(`Dragon ${layout.splits[WORKSPACE_KEY].detail}`);
  return parts.join(', ');
}

type WorkspaceState =
  | { kind: 'loading' }
  | { kind: 'signed-out' }
  | { kind: 'not-found' }
  | { kind: 'error'; message: string }
  | { kind: 'loaded'; project: Omit<ProjectSummary, 'tasks'> };

export class ProjectWorkspace extends KoboldComponent {
  private _state: WorkspaceState = { kind: 'loading' };
  /** Focus is inside the dock. */
  private _dockFocused = false;
  private _pinned = isDragonPinned();
  private _mode: LayoutMode = layoutMode();
  /** The tablet's Dragon overlay is open. */
  private _overlayOpen = false;
  private _unsubscribeLayout: (() => void) | null = null;
  /** The person's named layouts, for the Layouts menu; null while unread or when they cannot be read. */
  private _saved: SavedLayout[] | null = null;
  /** The "Save current as…" form is open. */
  private _saving = false;
  private readonly _onKey = (e: KeyboardEvent) => {
    if (!DRAGON_SHORTCUT.matches(e) || this._state.kind !== 'loaded' || this._mode === 'phone') return;
    e.preventDefault();
    if (this._mode === 'tablet') this.setOverlay(true);
    else this.focusDragon();
  };

  static get styles() {
    return `
      :host { display: block; min-width: 0; }
      /* Dragon widens over ~150 ms (TASK-152, D13): b-split-panel animates its columns with this variable once Birko.Web
         has it (asked for with TASK-058); until then the change is instant */
      .workspace { display: block; min-width: 0; --b-split-transition: 150ms ease; }
      @media (prefers-reduced-motion: reduce) { .workspace { --b-split-transition: 0s; } }
      .main { min-width: 0; }
      .dock { display: block; min-width: 0; height: calc(100vh - 7rem); min-height: 24rem; }
      .dock:focus { outline: none; }
      .project-head { display: flex; flex-wrap: wrap; align-items: center; gap: var(--b-space-sm, 0.5rem); margin-bottom: var(--b-space-md, 0.75rem); min-width: 0; }
      .back { color: var(--b-color-primary); font-size: var(--b-text-sm, 0.8125rem); }
      .project-title {
        margin: 0; min-width: 0; font-size: var(--b-text-2xl, 1.5rem); color: var(--b-text);
        overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
      }
      .badges { display: flex; flex-wrap: wrap; gap: var(--b-space-xs, 0.25rem); }
      .head-spacer { flex: 1; }
      .layouts-trigger {
        min-height: 2.25rem; padding: 0 var(--b-space-md, 0.75rem); border: 1px solid var(--b-border);
        border-radius: var(--b-radius, 0.375rem); background: var(--b-bg-elevated); color: var(--b-text);
        font: inherit; font-size: var(--b-text-sm, 0.8125rem); cursor: pointer;
      }
      .layouts-trigger:hover { border-color: var(--b-color-primary); }
      .layouts-trigger:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
      .save-layout { display: flex; flex-wrap: wrap; gap: var(--b-space-xs, 0.25rem); align-items: center; width: 100%; }
      .save-layout input {
        flex: 1 1 12rem; min-height: 2.25rem; padding: 0 var(--b-space-sm, 0.5rem); box-sizing: border-box;
        border: 1px solid var(--b-border); border-radius: var(--b-radius, 0.375rem); background: var(--b-bg); color: var(--b-text); font: inherit;
      }
      .save-layout input:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
      .save-layout button {
        min-height: 2.25rem; padding: 0 var(--b-space-md, 0.75rem); border-radius: var(--b-radius, 0.375rem); font: inherit; cursor: pointer;
        border: 1px solid var(--b-border); background: var(--b-bg-elevated); color: var(--b-text);
      }
      .save-layout button[type="submit"] { background: var(--b-color-primary); border-color: var(--b-color-primary); color: var(--b-text-on-primary, #fff); }
      b-tabs { min-width: 0; }
      .message {
        padding: var(--b-space-xl, 1.5rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem);
        color: var(--b-text-secondary);
      }
      .message strong { display: block; margin-bottom: var(--b-space-xs, 0.25rem); color: var(--b-text); }
      .desktop-only a { color: var(--b-color-primary); }
      ${dockOverlayStyles('.workspace')}
    `;
  }

  private get tab() {
    return WORKSPACE_TABS.find((t) => t.id === this.attr('tab'));
  }

  protected onUnmount() {
    document.removeEventListener('keydown', this._onKey);
    this._unsubscribeLayout?.();
  }

  protected async onMount() {
    document.addEventListener('keydown', this._onKey);
    this._unsubscribeLayout = onLayoutModeChange((mode) => {
      this._mode = mode;
      if (mode !== 'tablet') this._overlayOpen = false;
      this.update();
    });
    const id = this.attr('project-id');
    if (!id || !this.tab) {
      this._state = { kind: 'not-found' };
    } else if (!getToken()) {
      this._state = { kind: 'signed-out' };
    } else {
      try {
        // The person's current layout comes with the project, so the tabs and Dragon open as they left them
        const [response] = await Promise.all([
          createRestClient().get<ProjectSummary>(`/projects/${encodeURIComponent(id)}`),
          layoutStore.load(),
        ]);
        this._pinned = isDragonPinned();
        this._state = response.ok && response.data
          ? { kind: 'loaded', project: response.data }
          : response.status === 404
            ? { kind: 'not-found' }
            : { kind: 'error', message: `The server answered ${response.status}.` };
      } catch (error) {
        this._state = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
      }
    }
    if (this._state.kind === 'loaded') {
      document.title = `${this._state.project.name} - KoboldLair`;
      rememberProject({ id: this._state.project.id, name: this._state.project.name });
    }
    this.update();
    if (this._state.kind === 'loaded' && this._mode !== 'phone') {
      this._saved = await layoutStore.list();
      if (this.isConnected) this.update();
    }
  }

  render() {
    const state = this._state;
    switch (state.kind) {
      case 'loading':
        return `<b-spinner></b-spinner>`;
      case 'not-found':
        return `<kobold-not-found-view></kobold-not-found-view>`;
      case 'signed-out':
        return `<div class="message"><strong>Sign in to open this project.</strong>Projects come from the server's API,
          which needs a signed-in user.</div>`;
      case 'error':
        return `<div class="message"><strong>Could not load the project.</strong>${escapeHtml(state.message)}</div>`;
      case 'loaded': {
        const { project } = state;
        const active = this.tab!;
        return `
          <b-split-panel class="workspace${this.expanded ? ' expanded' : ''}${this._overlayOpen ? ' overlay-open' : ''}"
            layout-key="${WORKSPACE_KEY}" collapse-at="1279.98px" resize-label="Resize Dragon"
            master-width="${this.sizes.master}" detail-width="${this.sizes.detail}"${this.dividerOffered ? ' resizable' : ''}>
          <div class="main" slot="master">
          <div class="project-head">
            <a class="back" href="#/">← Projects</a>
            <h1 class="project-title" title="${escapeHtml(project.name)}">${escapeHtml(project.name)}</h1>
            <div class="badges">
              <b-badge variant="neutral">${escapeHtml(project.status)}</b-badge>
              <b-badge variant="neutral">${escapeHtml(project.executionState)}</b-badge>
            </div>
            ${this.renderLayouts()}
          </div>
          ${this.offered(active.id)
            ? `<b-tabs active="${active.id}"${this._mode === 'phone' ? '' : ' reorderable'} layout-key="${WORKSPACE_KEY}">
            <div slot="${active.id}"><${active.tag} project-id="${escapeHtml(project.id)}"></${active.tag}></div>
          </b-tabs>`
            : `<div class="message desktop-only"><strong>${escapeHtml(active.label)} is on a computer.</strong>On a phone a
            project offers its board, specification, runs and numbers.
            <a href="#/projects/${encodeURIComponent(project.id)}/${DEFAULT_TAB}">Open the board</a></div>`}
          ${dragonToggleHtml(this._overlayOpen)}
          </div>
          <aside class="dock" slot="detail" aria-label="Dragon" tabindex="-1" aria-keyshortcuts="${DRAGON_SHORTCUT.label}">${DOCK_CLOSE_HTML}<kobold-dragon-dock project-id="${escapeHtml(project.id)}"></kobold-dragon-dock></aside>
          </b-split-panel>
        `;
      }
    }
  }

  /** The Layouts menu, or the form that names a layout being saved (not on a phone). */
  private renderLayouts(): string {
    if (this._mode === 'phone') return '';
    if (this._saving) {
      return `<form class="save-layout" aria-label="Save the current layout">
        <input name="layout-name" aria-label="Layout name" placeholder="Name this layout" maxlength="60" required autocomplete="off">
        <button type="submit">Save</button><button type="button" class="cancel">Cancel</button>
      </form>`;
    }
    return `<span class="head-spacer"></span><b-dropdown-menu class="layouts" align="right">
      <button slot="trigger" class="layouts-trigger" type="button" aria-haspopup="menu">Layouts ▾</button></b-dropdown-menu>`;
  }

  private menuItems(): DropdownItem[] {
    const items: DropdownItem[] = [{ id: 'save', label: 'Save current as…' }];
    const saved = this._saved ?? [];
    saved.forEach((l, i) => {
      const note = summary(l.layout);
      items.push({
        id: `load:${l.name}`,
        label: `${escapeHtml(l.name)}${note ? ` <small style="opacity:0.7;">· ${escapeHtml(note)}</small>` : ''}`,
        ...(i === 0 ? { divider: true, heading: 'Saved layouts' } : {}),
      });
    });
    saved.forEach((l, i) => {
      items.push({ id: `delete:${l.name}`, label: `Delete “${escapeHtml(l.name)}”`, variant: 'danger', ...(i === 0 ? { divider: true } : {}) });
    });
    if (this._saved === null) items.push({ id: 'none', label: 'Saved layouts cannot be read right now.', divider: true });
    return items;
  }

  private async onMenu(id: string): Promise<void> {
    if (id === 'save') {
      this._saving = true;
      this.update();
      (this.$('.save-layout input') as HTMLInputElement | null)?.focus();
    } else if (id.startsWith('load:')) {
      const saved = this._saved?.find((l) => l.name === id.slice(5));
      if (saved) this.applyLayout(saved.layout);
    } else if (id.startsWith('delete:')) {
      const name = id.slice(7);
      const result = await layoutStore.remove(name);
      if ('error' in result) return void toast.error(result.error);
      this._saved = result.layouts;
      toast.success(`Layout “${name}” deleted.`);
      this.update();
    }
  }

  private async saveLayout(name: string): Promise<void> {
    name = name.trim();
    if (!name) return;
    const result = await layoutStore.save(name, this.captureLayout());
    if ('error' in result) return void toast.error(result.error);
    this._saved = result.layouts;
    this._saving = false;
    toast.success(`Layout “${name}” saved.`);
    this.update();
  }

  /** What a saved layout holds: the tab order, Dragon's width at rest, the pin, and the tab open now. */
  private captureLayout(): WorkspaceLayout {
    const tabs = this.$('b-tabs') as BTabs | null;
    const order = tabs?.order ?? layoutStore.current.tabs?.[WORKSPACE_KEY] ?? WORKSPACE_TABS.map((t) => t.id);
    return {
      tabs: { [WORKSPACE_KEY]: order },
      splits: { [WORKSPACE_KEY]: this.restSizes },
      openTab: this.attr('tab') ?? DEFAULT_TAB,
      dragonPinned: this._pinned,
    };
  }

  /** Load a saved layout: it becomes the current one, and the workspace opens its tab. */
  private applyLayout(layout: WorkspaceLayout): void {
    const pinned = layout.dragonPinned ?? false;
    layoutStore.update({ tabs: layout.tabs, splits: layout.splits, dragonPinned: pinned });
    const order = layout.tabs?.[WORKSPACE_KEY];
    const tabs = this.$('b-tabs') as BTabs | null;
    if (tabs && order) tabs.order = order;
    const dock = this.$('kobold-dragon-dock') as DragonDock | null;
    if (dock) dock.setPinned(pinned); // tells this view through DRAGON_PIN_EVENT, which lays it out
    else {
      this._pinned = pinned;
      this.layout();
    }
    const id = this.attr('project-id');
    const open = layout.openTab;
    if (id && open && open !== this.attr('tab') && WORKSPACE_TABS.some((t) => t.id === open) && this.offered(open)) {
      window.location.hash = `#/projects/${encodeURIComponent(id)}/${open}`;
    }
  }

  /** Dragon's width at rest: the person's, else 360px. */
  private get restSizes(): SplitSizes {
    return layoutStore.current.splits?.[WORKSPACE_KEY] ?? REST_SIZES;
  }

  private get sizes(): SplitSizes {
    return this.expanded ? WIDE_SIZES : this.restSizes;
  }

  /** The divider: on a desktop, while Dragon is at its own width. */
  private get dividerOffered(): boolean {
    return this._mode === 'desktop' && !this.expanded;
  }

  /**
   * The divider moved: the panel gives the tabs' width, which is kept as Dragon's own (what is left after the tabs and
   * the gap), so Dragon keeps its width when the window changes.
   */
  private resized(split: BSplitPanel): void {
    // Birko.Web 0.5.3 resizes the fixed-length column, so the panel reports Dragon's own width (`detail` in px); an
    // older one reports the tabs' width (`master` in px) and Dragon has what is left.
    const { master, detail } = split.sizes;
    const grid = split.shadowRoot?.querySelector('.split');
    if (!grid) return;
    const total = split.getBoundingClientRect().width;
    const gap = parseFloat(getComputedStyle(grid).columnGap) || 0;
    const dragon = /px$/.test(detail) ? parseFloat(detail) : /px$/.test(master) ? total - parseFloat(master) - gap : NaN;
    if (!Number.isFinite(dragon)) return;
    const width = Math.round(Math.min(Math.max(dragon, MIN_DRAGON_PX), Math.max(MIN_DRAGON_PX, total - gap - MIN_TABS_PX)));
    const rest: SplitSizes = { master: '1fr', detail: `${width}px` };
    layoutStore.update({ splits: { [WORKSPACE_KEY]: rest } });
    split.sizes = rest;
  }

  /** Whether this window offers the tab: a phone leaves out Git. */
  private offered(tabId: string): boolean {
    return this._mode !== 'phone' || !DESKTOP_ONLY_TABS.has(tabId);
  }

  private setOverlay(open: boolean): void {
    this._overlayOpen = open;
    const workspace = this.$('.workspace') as HTMLElement | null;
    if (workspace) applyDockOverlay(workspace, open, () => this.focusDragon());
  }

  private get expanded(): boolean {
    return this._dockFocused || this._pinned;
  }

  /** Dragon's width for the moment: widened (~60%) while focused or pinned, else the person's own, with the divider then. */
  private layout(): void {
    const split = this.$('b-split-panel') as BSplitPanel | null;
    if (!split) return;
    split.classList.toggle('expanded', this.expanded);
    split.sizes = this.sizes;
    split.toggleAttribute('resizable', this.dividerOffered);
  }

  /** The shortcut: into Dragon's input, or the dock itself while the input is disabled. */
  private focusDragon(): void {
    const dock = this.$('kobold-dragon-dock') as DragonDock | null;
    if (!dock?.focusInput()) (this.$('.dock') as HTMLElement | null)?.focus();
  }

  protected onUpdated() {
    const workspace = this.$('.workspace') as HTMLElement | null;
    if (workspace) wireDockOverlay(this.listen.bind(this), workspace, () => this._overlayOpen, (open) => this.setOverlay(open));
    const split = this.$('b-split-panel') as BSplitPanel | null;
    if (split) this.listen(split, 'split-resize', () => this.resized(split));
    const menu = this.$('b-dropdown-menu.layouts') as BDropdownMenu | null;
    if (menu) {
      menu.setItems(this.menuItems());
      this.listen<CustomEvent<{ id: string }>>(menu, 'select', (e) => void this.onMenu(e.detail.id));
    }
    const form = this.$('form.save-layout') as HTMLFormElement | null;
    if (form) {
      this.listen<SubmitEvent>(form, 'submit', (e) => {
        e.preventDefault();
        void this.saveLayout((form.querySelector('input') as HTMLInputElement).value);
      });
      const cancel = () => { this._saving = false; this.update(); };
      this.on('form.save-layout .cancel', 'click', cancel);
      this.listen<KeyboardEvent>(form, 'keydown', (e) => { if (e.key === 'Escape') { e.stopPropagation(); cancel(); } });
    }
    const dock = this.$('.dock') as HTMLElement | null;
    if (dock) {
      // A click anywhere in the dock focuses at least the dock (tabindex -1), so clicking in widens it.
      this.listen(dock, 'focusin', () => {
        this._dockFocused = true;
        this.layout();
      });
      this.listen<FocusEvent>(dock, 'focusout', (e) => {
        if (!document.hasFocus()) return; // the window lost focus; it comes back to the same place
        if (e.relatedTarget instanceof Node && dock.contains(e.relatedTarget)) return;
        this._dockFocused = false;
        this.layout();
      });
      this.listen<CustomEvent<{ pinned: boolean }>>(dock, DRAGON_PIN_EVENT, (e) => {
        this._pinned = e.detail.pinned;
        this.layout();
      });
    }
    const tabs = this.$('b-tabs') as BTabs | null;
    if (!tabs) return;
    tabs.setTabs(WORKSPACE_TABS.filter((t) => this.offered(t.id)).map(({ id, label }) => ({ id, label })), this.attr('tab') ?? DEFAULT_TAB);
    const order = layoutStore.current.tabs?.[WORKSPACE_KEY];
    if (order) tabs.order = order;
    this.listen<CustomEvent<{ order: string[] }>>(tabs, 'tab-reorder', (e) => layoutStore.update({ tabs: { [WORKSPACE_KEY]: e.detail.order } }));
    this.listen(tabs, 'tab-change', (e: Event) => {
      const next = (e as CustomEvent<{ tab?: string }>).detail?.tab;
      const id = this.attr('project-id');
      if (next && id && next !== this.attr('tab')) window.location.hash = `#/projects/${encodeURIComponent(id)}/${next}`;
    });
  }
}

define('kobold-project-workspace', ProjectWorkspace);
