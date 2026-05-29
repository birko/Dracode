/**
 * Module Store for KoboldLair
 * Built on Birko.Web.Shell module store factory
 */

import { createModuleStore, buildRibbon, type ModuleManifest, type LabelResolver } from 'birko-web-shell/modules';

const mod = createModuleStore();

export const moduleStore = mod.store;
export const hasPermission = mod.hasPermission;
export const hasModulePermission = mod.hasModulePermission;
export const getVisibleOptions = mod.getVisibleOptions;
export const resolveModuleFromHash = mod.resolveModuleFromHash;

/**
 * Load modules from KoboldLair API
 */
export async function loadModules(): Promise<ModuleManifest[]> {
  // TODO: Replace with actual API call
  // For now, return static module definitions
  return [
    {
      id: 'dashboard',
      label: 'Dashboard',
      labelKey: 'modules.dashboard',
      icon: '📊',
      order: 1,
      options: [
        { id: 'overview', label: 'Overview', route: '#/dashboard' }
      ],
      permissions: [],
      status: { text: 'active', variant: 'success' }
    },
    {
      id: 'dragon',
      label: 'Dragon',
      labelKey: 'modules.dragon',
      icon: '🐉',
      order: 2,
      options: [
        { id: 'chat', label: 'Chat', route: '#/dragon' },
        { id: 'specifications', label: 'Specifications', route: '#/dragon/specifications' }
      ],
      permissions: [],
      status: { text: 'active', variant: 'success' }
    },
    {
      id: 'projects',
      label: 'Projects',
      labelKey: 'modules.projects',
      icon: '📁',
      order: 3,
      options: [
        { id: 'list', label: 'All Projects', route: '#/projects' },
        { id: 'create', label: 'Create Project', route: '#/projects/create' }
      ],
      permissions: [],
      status: { text: 'active', variant: 'success' }
    },
    {
      id: 'hierarchy',
      label: 'Hierarchy',
      labelKey: 'modules.hierarchy',
      icon: '🌳',
      order: 4,
      options: [
        { id: 'tree', label: 'Agent Tree', route: '#/hierarchy' }
      ],
      permissions: [],
      status: { text: 'active', variant: 'success' }
    },
    {
      id: 'metrics',
      label: 'Metrics',
      labelKey: 'modules.metrics',
      icon: '$',
      order: 5,
      options: [
        { id: 'costs', label: 'Costs', route: '#/metrics/costs' },
        { id: 'performance', label: 'Performance', route: '#/metrics/performance' }
      ],
      permissions: [],
      status: { text: 'active', variant: 'success' }
    },
    {
      id: 'compare',
      label: 'Compare',
      labelKey: 'modules.compare',
      icon: '||',
      order: 6,
      options: [
        { id: 'tasks', label: 'Task Comparison', route: '#/compare' }
      ],
      permissions: [],
      status: { text: 'active', variant: 'success' }
    },
    {
      id: 'settings',
      label: 'Settings',
      labelKey: 'modules.settings',
      icon: '⚙️',
      order: 7,
      options: [
        { id: 'providers', label: 'Providers', route: '#/settings/providers' },
        { id: 'general', label: 'General', route: '#/settings/general' }
      ],
      permissions: [],
      status: { text: 'active', variant: 'success' }
    }
  ];
}

/**
 * Simple passthrough resolver (no i18n for now)
 */
export const simpleResolver: LabelResolver = (key, fallback) => {
  return key || fallback;
};

/**
 * Build KoboldLair ribbon from modules
 */
export function buildKoboldLairRibbon(modules: ModuleManifest[]) {
  return buildRibbon(modules, simpleResolver);
}
