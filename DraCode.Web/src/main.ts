/**
 * DraCode Web Application Entry Point
 * Birko.Web.Shell integration
 */

import './app-shell.js';

// Initialize application when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    console.log('📄 DOM loaded, creating app-shell...');

    // Create app shell element
    const appShell = document.createElement('dracode-app-shell');
    console.log('✅ Created element:', appShell);

    // Append to body
    document.body.appendChild(appShell);
    console.log('✅ Appended to body');

    console.log('✨ DraCode Client initialized with Birko.Web.Shell');
    console.log('📦 Using components:');
    console.log('   - b-app-shell (Birko.Web.Shell)');
    console.log('   - b-button, b-card, b-input, b-select, b-modal (Birko.Web.Components)');
    console.log('   - WsClient (Birko.Web.Core HTTP)');
    console.log('   - Store (Birko.Web.Core State)');
});
