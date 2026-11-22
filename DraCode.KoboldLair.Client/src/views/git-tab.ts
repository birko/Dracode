/**
 * The workspace's Git tab (TASK-156, FEATURE-026 D20): the project's branches other than main — how far ahead and behind
 * main each is, whether it merges cleanly (or the files it would conflict in), its last commit — and, for the branch
 * chosen, the files it changes and its diff against main, file by file (`git diff main...branch`, what merging would
 * bring). Data from `GET /api/v1/projects/{id}/git/branches` and `.../git/branches/{name}/diff`. Loaded when the tab
 * opens and on Refresh (each listing runs a merge check per branch, so it does not poll).
 *
 * Merging (TASK-157): the chosen branch has "Merge into main", confirmed in the page (not a browser dialog), then
 * `POST .../git/branches/{name}/merge`: a merged branch is deleted and leaves the list, and its ready-to-merge item leaves
 * the bell and Needs you; a conflict lists the files, and the repository is left as it was.
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';
import { needsYouStore } from '../stores/needs-you-store.js';
import { notificationStore } from '../stores/notification-store.js';

export interface BranchView {
  name: string;
  ahead: number;
  behind: number;
  lastCommitHash: string;
  lastCommitMessage: string;
  lastCommitDate: string | null;
  mergesCleanly: boolean | null;
  conflicts: string[];
  featureId: string | null;
  featureName: string | null;
}

interface FileChange { path: string; oldPath: string | null; status: string; added: number; deleted: number; binary: boolean; patch: string; truncated: boolean }
interface DiffView { branch: string; base: string; files: FileChange[]; truncated: boolean }

type ListState =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'loaded'; repository: boolean; base: string; branches: BranchView[] };

type MergeState =
  | { kind: 'idle' }
  | { kind: 'confirm' }
  | { kind: 'merging' }
  | { kind: 'conflict'; files: string[]; message: string }
  | { kind: 'failed'; message: string };

type DiffState = { kind: 'none' } | { kind: 'loading'; branch: string } | { kind: 'error'; branch: string; message: string } | { kind: 'loaded'; diff: DiffView };

const STATUS_MARK: Record<string, string> = { Added: 'A', Modified: 'M', Deleted: 'D', Renamed: 'R', Copied: 'C', Changed: '?' };

function ago(iso: string | null): string {
  const t = iso ? Date.parse(iso) : NaN;
  if (!t) return '';
  const minutes = Math.round((Date.now() - t) / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  return new Date(t).toLocaleDateString();
}

/** A file's patch as lines, each classed by what it is (added, removed, hunk header, file header). */
function patchHtml(patch: string): string {
  return patch.split('\n').map((line) => {
    const kind = line.startsWith('@@') ? 'hunk'
      : line.startsWith('+++') || line.startsWith('---') || line.startsWith('diff --git') || line.startsWith('index ') ? 'meta'
        : line.startsWith('+') ? 'add' : line.startsWith('-') ? 'del' : 'ctx';
    return `<span class="line ${kind}">${escapeHtml(line) || ' '}</span>`;
  }).join('');
}

export class GitTab extends KoboldComponent {
  private _list: ListState = { kind: 'loading' };
  private _diff: DiffState = { kind: 'none' };
  private _merge: MergeState = { kind: 'idle' };
  /** What the last merge did, shown above the list until the next one. */
  private _merged: string | null = null;

  static get styles() {
    return `
      :host { display: block; min-width: 0; color: var(--b-text); container-type: inline-size; }
      .message { color: var(--b-text-secondary); }
      .bar { display: flex; align-items: center; gap: var(--b-space-sm, 0.5rem); margin-bottom: var(--b-space-sm, 0.5rem);
        font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      .bar .spacer { flex: 1; }
      .grid { display: grid; grid-template-columns: minmax(14rem, 1fr) minmax(0, 2.2fr); gap: var(--b-space-lg, 1rem); align-items: start; }
      @container (max-width: 44rem) { .grid { grid-template-columns: minmax(0, 1fr); } }
      .branches { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--b-space-xs, 0.25rem); }
      .branch {
        display: block; width: 100%; text-align: left; padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius, 0.375rem);
        color: var(--b-text); font: inherit; cursor: pointer; min-width: 0;
      }
      .branch:hover { border-color: var(--b-color-primary); }
      .branch:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
      .branch[aria-current="true"] { border-color: var(--b-color-primary); box-shadow: inset 3px 0 0 var(--b-color-primary); }
      .name { display: block; font-family: var(--b-font-mono, monospace); font-size: var(--b-text-sm, 0.8125rem); overflow-wrap: anywhere; }
      .feature { display: block; font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-secondary); }
      .meta { display: flex; flex-wrap: wrap; align-items: center; gap: var(--b-space-xs, 0.25rem); margin-top: var(--b-space-xs, 0.25rem);
        font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
      .last { display: block; margin-top: 0.125rem; font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted);
        overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      h3 { margin: 0 0 var(--b-space-sm, 0.5rem); font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); overflow-wrap: anywhere; }
      .files { list-style: none; margin: 0 0 var(--b-space-md, 0.75rem); padding: 0; font-size: var(--b-text-sm, 0.8125rem); }
      .files li { display: flex; gap: var(--b-space-sm, 0.5rem); align-items: baseline; min-width: 0; }
      .files a { color: var(--b-color-primary); text-decoration: none; overflow-wrap: anywhere; min-width: 0; }
      .mark { flex-shrink: 0; width: 1rem; font-family: var(--b-font-mono, monospace); font-weight: var(--b-font-weight-bold, 700); text-align: center; }
      .mark.A { color: var(--b-color-success); } .mark.D { color: var(--b-color-danger); } .mark.M, .mark.R { color: var(--b-color-warning-text, var(--b-color-warning)); }
      .counts { flex-shrink: 0; margin-left: auto; font-family: var(--b-font-mono, monospace); font-size: var(--b-text-xs, 0.75rem); }
      .plus { color: var(--b-color-success); } .minus { color: var(--b-color-danger); }
      details.file { margin-bottom: var(--b-space-sm, 0.5rem); border: 1px solid var(--b-border); border-radius: var(--b-radius, 0.375rem); background: var(--b-bg-elevated); }
      details.file > summary { display: flex; gap: var(--b-space-sm, 0.5rem); align-items: baseline; padding: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem);
        cursor: pointer; font-family: var(--b-font-mono, monospace); font-size: var(--b-text-xs, 0.75rem); overflow-wrap: anywhere; }
      .patch { margin: 0; padding: var(--b-space-xs, 0.25rem) 0; overflow-x: auto; border-top: 1px solid var(--b-border);
        font-family: var(--b-font-mono, monospace); font-size: var(--b-text-xs, 0.75rem); line-height: 1.45; }
      .line { display: block; padding: 0 var(--b-space-sm, 0.5rem); white-space: pre; }
      .line.add { background: var(--b-color-success-light, rgba(34, 197, 94, 0.12)); }
      .line.del { background: var(--b-color-danger-light, rgba(239, 68, 68, 0.12)); }
      .line.hunk { color: var(--b-color-info, var(--b-color-primary)); background: var(--b-bg-secondary); }
      .line.meta { color: var(--b-text-muted); }
      .note { margin: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem); font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
      .merge-box { display: flex; flex-wrap: wrap; align-items: center; gap: var(--b-space-sm, 0.5rem); margin: 0 0 var(--b-space-md, 0.75rem);
        padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem); border: 1px solid var(--b-border); border-radius: var(--b-radius, 0.375rem);
        background: var(--b-bg-secondary); font-size: var(--b-text-sm, 0.8125rem); }
      .merge-box.conflict { border-color: var(--b-color-danger); }
      .merge-box p { margin: 0; flex: 1 1 14rem; overflow-wrap: anywhere; }
      .merge-box ul { flex-basis: 100%; margin: 0; padding-left: 1.25rem; font-family: var(--b-font-mono, monospace); font-size: var(--b-text-xs, 0.75rem); }
      .merged { margin: 0 0 var(--b-space-sm, 0.5rem); padding: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem); border-radius: var(--b-radius, 0.375rem);
        background: var(--b-color-success-light, var(--b-bg-secondary)); color: var(--b-color-success-text, var(--b-text)); font-size: var(--b-text-sm, 0.8125rem); overflow-wrap: anywhere; }
    `;
  }

  protected onMount() {
    void this.load();
  }

  private get projectId(): string {
    return this.attr('project-id') ?? '';
  }

  async load(): Promise<void> {
    this._list = { kind: 'loading' };
    this.update();
    try {
      const response = await createRestClient().get<{ repository: boolean; baseBranch: string; branches: BranchView[] }>(
        `/projects/${encodeURIComponent(this.projectId)}/git/branches`);
      this._list = response.ok && response.data
        ? { kind: 'loaded', repository: response.data.repository, base: response.data.baseBranch, branches: response.data.branches }
        : { kind: 'error', message: `The server answered ${response.status}.` };
    } catch (error) {
      this._list = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
    }
    // Keep the chosen branch open when it is still there; read its diff again (it may have moved on)
    const chosen = this._diff.kind === 'loaded' ? this._diff.diff.branch : this._diff.kind !== 'none' ? this._diff.branch : null;
    if (chosen && this._list.kind === 'loaded' && this._list.branches.some((b) => b.name === chosen)) void this.open(chosen);
    else this._diff = { kind: 'none' };
    if (this.isConnected) this.update();
  }

  private async open(branch: string): Promise<void> {
    if (this._diff.kind !== 'loaded' || this._diff.diff.branch !== branch) this._merge = { kind: 'idle' };
    this._diff = { kind: 'loading', branch };
    this.update();
    try {
      const response = await createRestClient().get<DiffView>(
        `/projects/${encodeURIComponent(this.projectId)}/git/branches/${encodeURIComponent(branch)}/diff`);
      if (this._diff.kind !== 'loading' || this._diff.branch !== branch) return; // another branch was chosen meanwhile
      this._diff = response.ok && response.data
        ? { kind: 'loaded', diff: response.data }
        : { kind: 'error', branch, message: response.status === 404 ? 'This branch is gone.' : `The server answered ${response.status}.` };
    } catch (error) {
      this._diff = { kind: 'error', branch, message: error instanceof Error ? error.message : String(error) };
    }
    if (this.isConnected) this.update();
  }

  render() {
    const list = this._list;
    if (list.kind === 'loading') return `<b-spinner></b-spinner>`;
    if (list.kind === 'error') return `<p class="message"><strong>Could not load the branches.</strong> ${escapeHtml(list.message)}</p>`;
    if (!list.repository) return `<p class="message"><strong>No git repository yet.</strong> The project's repository is made when its folder is.</p>`;
    const merged = this._merged ? `<p class="merged" role="status">${escapeHtml(this._merged)}</p>` : '';
    const bar = `${merged}<div class="bar"><span>${list.branches.length === 1 ? '1 branch' : `${list.branches.length} branches`} besides ${escapeHtml(list.base)}</span>
      <span class="spacer"></span><b-button class="refresh" size="sm" variant="ghost">Refresh</b-button></div>`;
    if (list.branches.length === 0) {
      return `${bar}<p class="message"><strong>No branches besides ${escapeHtml(list.base)}.</strong> Each feature gets its branch when the
        agents start on it.</p>`;
    }
    const chosen = this._diff.kind === 'loaded' ? this._diff.diff.branch : this._diff.kind !== 'none' ? this._diff.branch : null;
    return `${bar}<div class="grid">
      <ul class="branches" aria-label="Branches">${list.branches.map((b) => this.branchHtml(b, b.name === chosen)).join('')}</ul>
      <section class="detail" aria-live="polite">${this.detailHtml()}</section>
    </div>`;
  }

  private branchHtml(b: BranchView, current: boolean): string {
    const merge = b.mergesCleanly === true ? `<b-badge class="merge" variant="success">Merges cleanly</b-badge>`
      : b.mergesCleanly === false ? `<b-badge class="merge" variant="danger" title="${escapeHtml(b.conflicts.join(', '))}">${
        b.conflicts.length === 1 ? 'Conflicts in 1 file' : `Conflicts in ${b.conflicts.length} files`}</b-badge>`
        : `<b-badge class="merge" variant="neutral">Merge unknown</b-badge>`;
    return `<li><button class="branch" type="button" data-branch="${escapeHtml(b.name)}" aria-current="${current}">
      <span class="name">${escapeHtml(b.name)}</span>
      ${b.featureName ? `<span class="feature">${escapeHtml(b.featureName)}</span>` : ''}
      <span class="meta"><span class="ahead">${b.ahead} ahead</span>·<span class="behind">${b.behind} behind</span>${merge}</span>
      <span class="last" title="${escapeHtml(b.lastCommitMessage)}">${escapeHtml(b.lastCommitMessage)}${b.lastCommitDate ? ` · ${escapeHtml(ago(b.lastCommitDate))}` : ''}</span>
    </button></li>`;
  }

  private detailHtml(): string {
    const d = this._diff;
    if (d.kind === 'none') return `<p class="message">Choose a branch to see what it changes.</p>`;
    if (d.kind === 'loading') return `<b-spinner></b-spinner>`;
    if (d.kind === 'error') return `<p class="message">${escapeHtml(d.message)}</p>`;
    const diff = d.diff;
    if (diff.files.length === 0) return `<h3>${escapeHtml(diff.branch)}</h3><p class="message">This branch changes nothing against ${escapeHtml(diff.base)}.</p>`;
    const added = diff.files.reduce((n, f) => n + f.added, 0);
    const deleted = diff.files.reduce((n, f) => n + f.deleted, 0);
    return `
      <h3>${escapeHtml(diff.branch)} — ${diff.files.length === 1 ? '1 file' : `${diff.files.length} files`}, <span class="plus">+${added}</span> <span class="minus">−${deleted}</span></h3>
      ${this.mergeHtml(diff)}
      <ul class="files">${diff.files.map((f, i) => `<li data-path="${escapeHtml(f.path)}">
        <span class="mark ${STATUS_MARK[f.status] ?? ''}" title="${escapeHtml(f.status)}">${STATUS_MARK[f.status] ?? '?'}</span>
        <a href="#" data-file="${i}">${f.oldPath ? `${escapeHtml(f.oldPath)} → ` : ''}${escapeHtml(f.path)}</a>
        <span class="counts">${f.binary ? 'binary' : `<span class="plus">+${f.added}</span> <span class="minus">−${f.deleted}</span>`}</span></li>`).join('')}</ul>
      ${diff.files.map((f, i) => `<details class="file" id="file-${i}" open>
        <summary>${escapeHtml(f.oldPath ? `${f.oldPath} → ${f.path}` : f.path)}</summary>
        ${f.binary ? `<p class="note">Binary file; no text diff.</p>` : f.patch ? `<pre class="patch">${patchHtml(f.patch)}</pre>` : ''}
        ${f.truncated ? `<p class="note">${f.patch ? 'The rest of this file’s diff is too long to show.' : 'This diff is too long to show here.'}</p>` : ''}
      </details>`).join('')}
      ${diff.truncated ? `<p class="note">The whole diff is too long; later files are listed without their changes.</p>` : ''}`;
  }

  /** The merge control for the chosen branch: a button, the confirmation, or what the merge answered. */
  private mergeHtml(diff: DiffView): string {
    const m = this._merge;
    const branch = escapeHtml(diff.branch);
    const base = escapeHtml(diff.base);
    const summary = this._list.kind === 'loaded' ? this._list.branches.find((b) => b.name === diff.branch) : undefined;
    switch (m.kind) {
      case 'confirm':
        return `<div class="merge-box confirm" role="group" aria-label="Confirm the merge">
          <p>Merge <strong>${branch}</strong> into ${base} and delete the branch?</p>
          <b-button class="merge-confirm" size="sm" variant="primary">Merge</b-button>
          <b-button class="merge-cancel" size="sm" variant="secondary">Cancel</b-button></div>`;
      case 'merging':
        return `<div class="merge-box"><p>Merging ${branch} into ${base}…</p><b-button size="sm" variant="primary" loading>Merge</b-button></div>`;
      case 'conflict':
        return `<div class="merge-box conflict" role="alert"><p>Not merged: ${escapeHtml(m.message)} Nothing in the repository changed. It conflicts in:</p>
          <b-button class="merge-dismiss" size="sm" variant="secondary">Close</b-button>
          <ul class="conflicts">${m.files.map((f) => `<li>${escapeHtml(f)}</li>`).join('')}</ul></div>`;
      case 'failed':
        return `<div class="merge-box conflict" role="alert"><p>Not merged: ${escapeHtml(m.message)}</p>
          <b-button class="merge-dismiss" size="sm" variant="secondary">Close</b-button></div>`;
      default:
        return `<div class="merge-box"><p>${summary?.mergesCleanly === false
          ? `Git expects conflicts with ${base}; a merge would be refused.`
          : `Merging brings these changes into ${base} and deletes the branch.`}</p>
          <b-button class="merge" size="sm" variant="primary">Merge into ${base}</b-button></div>`;
    }
  }

  private async merge(branch: string): Promise<void> {
    this._merge = { kind: 'merging' };
    this.update();
    try {
      const response = await createRestClient().post<{ mergeCommit?: string; deleted?: boolean; message?: string; conflicts?: string[] }>(
        `/projects/${encodeURIComponent(this.projectId)}/git/branches/${encodeURIComponent(branch)}/merge`);
      if (response.ok) {
        const commit = response.data?.mergeCommit ? ` (${response.data.mergeCommit.slice(0, 8)})` : '';
        this._merged = `Merged ${branch} into main${commit}; ${response.data?.deleted === false ? 'the branch could not be deleted.' : 'the branch was deleted.'}`;
        this._merge = { kind: 'idle' };
        this._diff = { kind: 'none' };
        void needsYouStore.refresh();
        void notificationStore.refresh();
        await this.load();
        return;
      }
      this._merge = response.status === 409
        ? { kind: 'conflict', files: response.data?.conflicts ?? [], message: response.data?.message ?? 'the branch does not merge cleanly.' }
        : { kind: 'failed', message: response.data?.message ?? `the server answered ${response.status}.` };
    } catch (error) {
      this._merge = { kind: 'failed', message: error instanceof Error ? error.message : String(error) };
    }
    if (this.isConnected) this.update();
  }

  protected onUpdated() {
    const branchName = this._diff.kind === 'loaded' ? this._diff.diff.branch : null;
    const on = (selector: string, action: () => void) => {
      const el = this.$(selector);
      if (el) this.listen(el, 'click', action);
    };
    on('b-button.merge', () => { this._merge = { kind: 'confirm' }; this._merged = null; this.update(); });
    on('b-button.merge-cancel', () => { this._merge = { kind: 'idle' }; this.update(); });
    on('b-button.merge-dismiss', () => { this._merge = { kind: 'idle' }; this.update(); });
    on('b-button.merge-confirm', () => { if (branchName) void this.merge(branchName); });
    const refresh = this.$('b-button.refresh');
    if (refresh) this.listen(refresh, 'click', () => void this.load());
    const branches = this.$('.branches');
    if (branches) {
      this.listen(branches, 'click', (e) => {
        const button = (e.target as HTMLElement).closest<HTMLElement>('.branch');
        if (button?.dataset.branch) void this.open(button.dataset.branch);
      });
    }
    const files = this.$('.files');
    if (files) {
      this.listen(files, 'click', (e) => {
        const link = (e.target as HTMLElement).closest<HTMLElement>('a[data-file]');
        if (!link) return;
        e.preventDefault(); // the route is the hash; jump within the tab instead
        const target = this.$<HTMLDetailsElement>(`#file-${link.dataset.file}`);
        if (target) { target.open = true; target.scrollIntoView({ block: 'start', behavior: 'smooth' }); }
      });
    }
  }
}

define('kobold-git-tab', GitTab);
