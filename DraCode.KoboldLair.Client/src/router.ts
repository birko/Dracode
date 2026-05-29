/**
 * Simple Router for KoboldLair
 * Handles hash-based routing and view component instantiation
 */

import './views/dashboard-view.js';
import './views/dragon-view.js';
import './views/projects-view.js';
import './views/hierarchy-view.js';
import './views/metrics-view.js';
import './views/compare-view.js';
import './views/settings-view.js';
import './views/project-config-view.js';
import './views/impact-view.js';

interface Route {
  path: string;
  component: string;
  title: string;
}

export class KoboldRouter {
  private routes: Route[] = [];
  private currentRoute: Route | null = null;
  private routeOutlet: HTMLElement | null = null;

  constructor(outletId: string) {
    this.routeOutlet = document.getElementById(outletId);
    if (!this.routeOutlet) {
      throw new Error(`Route outlet with id '${outletId}' not found`);
    }

    // Define routes
    this.routes = [
      { path: '/', component: 'kobold-dashboard-view', title: 'Dashboard' },
      { path: '/dashboard', component: 'kobold-dashboard-view', title: 'Dashboard' },
      { path: '/dragon', component: 'kobold-dragon-view', title: 'Dragon' },
      { path: '/projects', component: 'kobold-projects-view', title: 'Projects' },
      { path: '/hierarchy', component: 'kobold-hierarchy-view', title: 'Hierarchy' },
      { path: '/metrics', component: 'kobold-metrics-view', title: 'Metrics' },
      { path: '/compare', component: 'kobold-compare-view', title: 'Compare' },
      { path: '/settings', component: 'kobold-settings-view', title: 'Settings' },
      { path: '/project-config', component: 'project-config-view', title: 'Project Config' },
      { path: '/impact', component: 'impact-view', title: 'Impact' }
    ];

    // Listen for hash changes
    window.addEventListener('hashchange', () => this.handleHashChange());

    // Handle initial route
    this.handleHashChange();
  }

  private handleHashChange(): void {
    const hash = window.location.hash.slice(1) || '/';

    // Find matching route
    const route = this.routes.find(r => r.path === hash);

    if (route) {
      this.loadRoute(route);
    } else {
      console.warn('Route not found:', hash);
      this.loadRoute(this.routes[0]); // Default to dashboard
    }
  }

  private loadRoute(route: Route): void {
    if (this.currentRoute?.path === route.path) {
      return; // Already on this route
    }

    console.log('📍 Loading route:', route.path);

    // Clear current content
    if (this.routeOutlet) {
      this.routeOutlet.innerHTML = '';
    }

    // Create new view component
    const viewElement = document.createElement(route.component);

    if (this.routeOutlet) {
      this.routeOutlet.appendChild(viewElement);
    }

    // Update document title
    document.title = `${route.title} - KoboldLair`;

    this.currentRoute = route;

    // Emit route change event
    window.dispatchEvent(new CustomEvent('route-changed', {
      detail: { route }
    }));
  }

  /**
   * Navigate to a specific route
   */
  public navigate(path: string): void {
    window.location.hash = path;
  }

  /**
   * Get current route
   */
  public getCurrentRoute(): Route | null {
    return this.currentRoute;
  }
}

// Export router instance
let routerInstance: KoboldRouter | null = null;

export function initRouter(outletId: string = 'route-outlet'): KoboldRouter {
  if (!routerInstance) {
    routerInstance = new KoboldRouter(outletId);
    console.log('✅ KoboldLair Router initialized');
  }
  return routerInstance;
}

export function getRouter(): KoboldRouter | null {
  return routerInstance;
}
