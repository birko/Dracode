/**
 * The theme settings the shell and the boot script must agree on (TASK-188): where the chosen theme is kept
 * (`{THEME_STORAGE_PREFIX}-theme` in localStorage, as the shell's header menu writes it) and the theme a user who has
 * chosen none starts on: Auto, which follows the browser's light or dark preference. `build.js` writes
 * `wwwroot/dist/theme-boot.js` from these, which `index.html` runs before anything renders, so a dark browser never
 * sees a light flash.
 */
export const THEME_STORAGE_PREFIX = 'app';
export const DEFAULT_THEME = 'auto';
