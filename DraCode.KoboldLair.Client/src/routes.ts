/**
 * Routing for KoboldLair on Birko.Web.Core's Router (TASK-113). Routes come from the module manifests
 * (buildModuleRoutes), so a module option and its route cannot drift apart; a few other screens are added by hand, and
 * anything else shows the not-found view.
 */

import { Router, type Route } from 'birko-web-core/router';
import { buildModuleRoutes, type ModuleManifest } from 'birko-web-shell/modules';
import { createAuthGuard } from 'birko-web-shell/auth';
import { authStore } from './auth-store.js';
import configService from './services/config.js';

import './views/home-view.js';
import './views/settings-view.js';
import './views/login-view.js';
import './views/not-found-view.js';
import { DEFAULT_TAB } from './views/project-workspace.js';

/** The element (and attributes) each module option opens, keyed `moduleId/optionId`. */
const OPTION_VIEWS: Record<string, { tag: string; attrs?: Record<string, string> }> = {
  'settings/providers': { tag: 'kobold-settings-view', attrs: { tab: 'providers' } },
  'settings/agents': { tag: 'kobold-settings-view', attrs: { tab: 'agents' } },
};

/** Screens no module option claims; `#/` is the projects homepage (TASK-145). */
const EXTRA_VIEWS: Record<string, { tag: string; attrs?: Record<string, string> }> = {
  '/': { tag: 'kobold-home-view' },
  '/settings': { tag: 'kobold-settings-view' },
};

const authGuard = createAuthGuard(authStore, '/login');

/**
 * Protected routes need a signed-in user only when the host says the server requires auth (`authRequired` from
 * GET /api/config, KoboldLair:AuthRequired on the client host). With auth off, which is the server's default, every
 * route is open.
 */
export function protectedGuard(): boolean | string {
  return configService.getConfig().authRequired ? authGuard() : true;
}

function view(tag: string, attrs: Record<string, string> = {}, title?: string): () => HTMLElement {
  return () => {
    const el = document.createElement(tag);
    for (const [name, value] of Object.entries(attrs)) el.setAttribute(name, value);
    document.title = title ? `${title} - KoboldLair` : 'KoboldLair';
    return el;
  };
}

/** Strip the leading `#` the manifests use; the Router matches the path after it. */
function routePath(route: string): string {
  return route.replace(/^#/, '');
}

export function buildRoutes(modules: ModuleManifest[]): Route[] {
  const routes: Route[] = [
    { path: '/login', component: view('kobold-login-view', {}, 'Sign in') },
  ];

  for (const entry of buildModuleRoutes(modules)) {
    const target = OPTION_VIEWS[`${entry.moduleId}/${entry.optionId}`];
    const mod = modules.find((m) => m.id === entry.moduleId);
    const option = mod?.options.find((o) => o.id === entry.optionId);
    routes.push({
      path: routePath(entry.route),
      component: target
        ? view(target.tag, target.attrs, option?.label ?? mod?.label)
        : view('kobold-not-found-view', {}, 'Not found'),
      guard: protectedGuard,
    });
  }

  for (const [path, target] of Object.entries(EXTRA_VIEWS)) {
    if (routes.some((r) => r.path === path)) continue; // a module option already claims it
    routes.push({ path, component: view(target.tag, target.attrs), guard: protectedGuard });
  }

  // A project's workspace (TASK-150): #/projects/{id}/{tab}; #/projects/{id} opens its first tab.
  routes.push({
    path: '/projects/:id/:tab',
    component: () => {
      const params = router?.current?.params ?? {};
      return view('kobold-project-workspace', { 'project-id': params.id ?? '', tab: params.tab ?? '' }, 'Project')();
    },
    guard: protectedGuard,
  });
  routes.push({
    path: '/projects/:id',
    component: view('kobold-not-found-view', {}, 'Not found'),
    guard: () => {
      const allowed = protectedGuard();
      if (allowed !== true) return allowed;
      const id = window.location.hash.slice(1).split('?')[0].split('/').filter(Boolean)[1] ?? '';
      return `/projects/${id}/${DEFAULT_TAB}`;
    },
  });

  routes.push({ path: '*', component: view('kobold-not-found-view', {}, 'Not found') });
  return routes;
}

let router: Router | null = null;

/** Create the router over `outlet` and resolve the current hash. */
export function startRouter(modules: ModuleManifest[], outlet: HTMLElement | string = '#route-outlet'): Router {
  router ??= new Router([], outlet);
  router.setRoutes(buildRoutes(modules));
  router.resolve();
  return router;
}

export function getRouter(): Router | null {
  return router;
}
