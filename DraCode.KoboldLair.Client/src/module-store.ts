/**
 * Module Store for KoboldLair
 * Built on Birko.Web.Shell module store factory
 *
 * The manifests feed the router (routes.ts): each option is a route. The header frame (TASK-145) has no ribbon, so
 * they no longer render as tabs; they list the screens that stay reachable until their rebuilt replacements land.
 */

import { createModuleStore, type ModuleManifest } from 'birko-web-shell/modules';

const mod = createModuleStore();

export const moduleStore = mod.store;
export const hasPermission = mod.hasPermission;
export const hasModulePermission = mod.hasModulePermission;
export const getVisibleOptions = mod.getVisibleOptions;
export const resolveModuleFromHash = mod.resolveModuleFromHash;

/** The module manifests: Dragon and settings. */
export async function loadModules(): Promise<ModuleManifest[]> {
  return [
    {
      id: 'dragon',
      label: 'Dragon',
      labelKey: 'modules.dragon',
      icon: '🐉',
      order: 1,
      options: [
        { id: 'chat', label: 'Chat', route: '#/dragon' },
        { id: 'specifications', label: 'Specifications', route: '#/dragon/specifications' }
      ],
      permissions: [],
      status: { text: 'active', variant: 'success' }
    },
    {
      id: 'settings',
      label: 'Settings',
      labelKey: 'modules.settings',
      icon: '⚙️',
      order: 2,
      options: [
        { id: 'providers', label: 'Providers', route: '#/settings/providers' },
        { id: 'general', label: 'General', route: '#/settings/general' }
      ],
      permissions: [],
      status: { text: 'active', variant: 'success' }
    }
  ];
}
