/**
 * SPA Router Module
 * Handles client-side routing without page reloads
 */
export class Router {
    constructor() {
        this.routes = new Map();
        this.currentView = null;
        this.beforeNavigate = null;
        this.afterNavigate = null;
        
        // Listen for hash changes
        window.addEventListener('hashchange', () => this.handleRoute());
        window.addEventListener('load', () => this.handleRoute());
    }

    /**
     * Register a route
     * @param {string} path - Route path (e.g., 'dashboard', 'dragon')
     * @param {Function} viewLoader - Function that returns view module
     */
    addRoute(path, viewLoader) {
        this.routes.set(path, viewLoader);
    }

    /**
     * Navigate to a route
     * @param {string} path - Route path
     */
    navigate(path) {
        window.location.hash = path;
    }

    /**
     * Get current route path
     */
    getCurrentPath() {
        return window.location.hash.slice(1) || 'dashboard';
    }

    /**
     * Handle route changes
     */
    async handleRoute() {
        const path = this.getCurrentPath();
        const viewLoader = this.routes.get(path) || this.routes.get('dashboard');
        
        if (!viewLoader) {
            console.error(`No route found for: ${path}`);
            return;
        }

        // Call before navigate hook
        if (this.beforeNavigate) {
            await this.beforeNavigate(path);
        }

        // Clean up current view
        if (this.currentView && this.currentView.cleanup) {
            this.currentView.cleanup();
        }

        // Load and render new view
        try {
            const view = await viewLoader();
            this.currentView = view;
            
            if (view.render) {
                await view.render();
            }

            // Update active navigation
            this.updateActiveNav(path);

            // Call after navigate hook
            if (this.afterNavigate) {
                await this.afterNavigate(path);
            }
        } catch (error) {
            console.error('Error loading view:', error);
        }
    }

    /**
     * Update active navigation link
     */
    updateActiveNav(path) {
        document.querySelectorAll('.nav-link').forEach(link => {
            link.classList.remove('active');
            if (link.dataset.route === path) {
                link.classList.add('active');
            }
        });
    }

    /**
     * Set up navigation links
     */
    setupNavLinks() {
        document.querySelectorAll('.nav-link').forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                const route = link.dataset.route;
                if (route) {
                    this.navigate(route);
                }
            });
        });
    }
}
