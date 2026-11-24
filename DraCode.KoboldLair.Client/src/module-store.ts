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

/** The module manifests: settings. Dragon has no screen of its own; it is docked in each project (TASK-151). */
export async function loadModules(): Promise<ModuleManifest[]> {
  return [
    {
      id: 'settings',
      label: 'Settings',
      labelKey: 'modules.settings',
      icon: '⚙️',
      order: 2,
      options: [
        { id: 'providers', label: 'Providers', route: '#/settings/providers' },
        { id: 'agents', label: 'Agents', route: '#/settings/agents' },
        { id: 'keys', label: 'API keys', route: '#/settings/keys' },
        { id: 'budgets', label: 'Budgets', route: '#/settings/budgets' }
      ],
      permissions: [],
      status: { text: 'active', variant: 'success' }
    }
  ];
}
