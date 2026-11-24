/**
 * Which layout the window gets (TASK-164, FEATURE-026 D21/D22), and the project the phone's Board and Dragon tabs
 * follow.
 *
 * - **Phone**, below 900px: for checking and responding. A bottom tab bar (Projects, Board, Dragon, Notifications), the
 *   workspace read only and without Git, no Settings or API keys.
 * - **Tablet**, 900–1279px: the desktop layout, with Dragon opening as an overlay instead of docking.
 * - **Desktop**, 1280px and wider: Dragon docked.
 *
 * Components' stylesheets use {@link PHONE_MEDIA} and {@link TABLET_MEDIA}; what CSS cannot do (which tabs and menu
 * items exist, whether the board takes moves) reads {@link layoutMode} and re-renders on {@link onLayoutModeChange}.
 */

export type LayoutMode = 'phone' | 'tablet' | 'desktop';

export const PHONE_MEDIA = '(max-width: 899.98px)';
export const TABLET_MEDIA = '(min-width: 900px) and (max-width: 1279.98px)';

export function layoutMode(): LayoutMode {
  if (window.matchMedia(PHONE_MEDIA).matches) return 'phone';
  return window.matchMedia(TABLET_MEDIA).matches ? 'tablet' : 'desktop';
}

export const isPhone = () => layoutMode() === 'phone';

/** Call `listener` with the new mode whenever the window crosses a breakpoint; returns the unsubscribe. */
export function onLayoutModeChange(listener: (mode: LayoutMode) => void): () => void {
  const queries = [window.matchMedia(PHONE_MEDIA), window.matchMedia(TABLET_MEDIA)];
  let last = layoutMode();
  const changed = () => {
    const mode = layoutMode();
    if (mode === last) return;
    last = mode;
    listener(mode);
  };
  for (const q of queries) q.addEventListener('change', changed);
  return () => { for (const q of queries) q.removeEventListener('change', changed); };
}

export interface LastProject { id: string; name: string }

const LAST_PROJECT_KEY = 'koboldlair.last-project';
const lastProjectListeners = new Set<(project: LastProject | null) => void>();
let lastProjectInMemory: LastProject | null = null;

/** The project opened last in this tab, which the phone's Board and Dragon tabs follow; kept for the session. */
export function lastProject(): LastProject | null {
  try {
    const stored = sessionStorage.getItem(LAST_PROJECT_KEY);
    if (stored) return JSON.parse(stored) as LastProject;
  } catch {
    // Storage blocked or unreadable: this page's memory still holds it.
  }
  return lastProjectInMemory;
}

export function rememberProject(project: LastProject): void {
  const before = lastProject();
  lastProjectInMemory = project;
  try {
    sessionStorage.setItem(LAST_PROJECT_KEY, JSON.stringify(project));
  } catch {
    // Kept in memory only.
  }
  if (before?.id !== project.id || before?.name !== project.name) for (const l of lastProjectListeners) l(project);
}

export function onLastProjectChange(listener: (project: LastProject | null) => void): () => void {
  lastProjectListeners.add(listener);
  return () => lastProjectListeners.delete(listener);
}
