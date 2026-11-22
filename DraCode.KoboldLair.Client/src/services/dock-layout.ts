/**
 * How the docked Dragon is laid out (TASK-152, FEATURE-026 D13): the "Keep expanded" pin, remembered per signed-in user
 * in this browser's localStorage, and the keyboard shortcut that puts focus in Dragon. Storage that cannot be read
 * (a private window, blocked site data) reads as unpinned.
 */

import { authStore } from '../auth-store.js';

const PIN_KEY = 'koboldlair.dragon-pinned';

function pinKey(): string {
  const userId = authStore.get('userId') as string | null | undefined;
  return userId ? `${PIN_KEY}:${userId}` : PIN_KEY;
}

/** Whether the user pinned Dragon expanded. */
export function isDragonPinned(): boolean {
  try {
    return localStorage.getItem(pinKey()) === '1';
  } catch {
    return false;
  }
}

export function setDragonPinned(pinned: boolean): void {
  try {
    if (pinned) localStorage.setItem(pinKey(), '1');
    else localStorage.removeItem(pinKey());
  } catch {
    // Not remembered; the pin still holds for this page.
  }
}

/** Focus Dragon from anywhere in the workspace. `code`, not `key`: Alt+Shift+D types a character on a Mac. */
export const DRAGON_SHORTCUT = {
  label: 'Alt+Shift+D',
  matches: (e: KeyboardEvent) => e.altKey && e.shiftKey && !e.ctrlKey && !e.metaKey && e.code === 'KeyD',
};

/** Fired (bubbling, composed) by the dock when its pin changes; `detail.pinned` is the new value. */
export const DRAGON_PIN_EVENT = 'dragon-pin';
