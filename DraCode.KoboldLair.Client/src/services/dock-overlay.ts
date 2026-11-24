/**
 * Dragon's dock by window width (TASK-164, FEATURE-026 D22), shared by the homepage and the workspace: each lays out a
 * container holding its main column and `aside.dock`.
 *
 * - Desktop: docked beside the main column, as each view styles it.
 * - Tablet: the desktop layout in one column, with Dragon as an overlay over its right side, opened by a "Dragon"
 *   button and closed by its ✕, Escape, or the button again. The dock stays in the page while closed, so its
 *   conversation is not reloaded.
 * - Phone: no dock at all; Dragon is the bottom tab bar's own tab (`#/dragon`).
 */

import { PHONE_MEDIA, TABLET_MEDIA } from './layout-mode.js';

/** The overlay's rules for `container` (the dock's parent, e.g. `.workspace`). Append after the view's own rules. */
export function dockOverlayStyles(container: string): string {
  return `
    .dragon-toggle, .dock-close { display: none; }
    @media ${TABLET_MEDIA} {
      ${container}, ${container}.expanded { grid-template-columns: minmax(0, 1fr); }
      ${container} > .dock {
        display: none; position: fixed; z-index: 50; top: var(--b-header-height, 3rem); right: 0; bottom: 0;
        width: min(28rem, 92vw); height: auto; min-height: 0; box-sizing: border-box;
        flex-direction: column; gap: var(--b-space-xs, 0.25rem); padding: var(--b-space-sm, 0.5rem);
        background: var(--b-bg-elevated); border-left: 1px solid var(--b-border);
        box-shadow: var(--b-shadow-lg, 0 8px 24px rgba(0, 0, 0, 0.18));
      }
      ${container}.overlay-open > .dock { display: flex; }
      ${container} > .dock > kobold-dragon-dock { flex: 1; min-height: 0; }
      .dock-close {
        display: inline-flex; align-self: flex-end; align-items: center; justify-content: center;
        width: 2.75rem; height: 2.75rem; border: none; border-radius: var(--b-radius, 0.375rem);
        background: none; color: var(--b-text-secondary); font-size: 1.1rem; cursor: pointer;
      }
      .dock-close:hover { background: var(--b-bg-tertiary, var(--b-bg-secondary)); color: var(--b-text); }
      .dragon-toggle {
        display: inline-flex; align-items: center; gap: var(--b-space-xs, 0.25rem);
        position: fixed; z-index: 40; right: var(--b-space-lg, 1rem); bottom: var(--b-space-lg, 1rem);
        min-height: 2.75rem; padding: 0 var(--b-space-lg, 1rem); border: none; border-radius: 999px;
        background: var(--b-color-primary); color: var(--b-text-on-primary, #fff);
        font: inherit; font-weight: var(--b-font-weight-medium, 500); cursor: pointer;
        box-shadow: var(--b-shadow-md, 0 4px 12px rgba(0, 0, 0, 0.18));
      }
      ${container}.overlay-open .dragon-toggle { display: none; }
      .dragon-toggle:focus-visible, .dock-close:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
    }
    @media ${PHONE_MEDIA} {
      ${container}, ${container}.expanded { grid-template-columns: minmax(0, 1fr); }
      ${container} > .dock { display: none; }
    }
  `;
}

/** The overlay's button, rendered inside the container (shown on a tablet only). */
export function dragonToggleHtml(open: boolean): string {
  return `<button class="dragon-toggle" type="button" aria-expanded="${open}">🐉 Dragon</button>`;
}

/** The overlay's close button, rendered first inside `aside.dock` (shown on a tablet only). */
export const DOCK_CLOSE_HTML = `<button class="dock-close" type="button" aria-label="Close Dragon" title="Close Dragon">✕</button>`;

type Listen = <T extends Event = Event>(target: EventTarget, event: string, handler: (e: T) => void) => void;

/**
 * Wire the overlay's controls after each render. The view keeps whether it is open (`open`/`setOpen`) and renders
 * `overlay-open` on the container from it, so a re-render keeps it as it was.
 */
export function wireDockOverlay(listen: Listen, container: HTMLElement, open: () => boolean, setOpen: (open: boolean) => void): void {
  const toggle = container.querySelector<HTMLElement>('.dragon-toggle');
  const close = container.querySelector<HTMLElement>('.dock-close');
  if (toggle) listen(toggle, 'click', () => setOpen(!open()));
  if (close) listen(close, 'click', () => setOpen(false));
  listen<KeyboardEvent>(container, 'keydown', (e) => {
    if (e.key === 'Escape' && open()) {
      e.preventDefault();
      setOpen(false);
    }
  });
}

/** Show or hide the overlay on `container` and move focus with it: into Dragon on opening, back to the button on closing. */
export function applyDockOverlay(container: HTMLElement, open: boolean, focusDragon: () => void): void {
  container.classList.toggle('overlay-open', open);
  const toggle = container.querySelector<HTMLElement>('.dragon-toggle');
  toggle?.setAttribute('aria-expanded', String(open));
  if (open) focusDragon();
  else toggle?.focus();
}
