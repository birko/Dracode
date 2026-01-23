import { DraCodeClient } from './client.js';

// Initialize application when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    const client = new DraCodeClient();
    
    // Expose client to window for inline event handlers (temporary)
    (window as any).draCodeClient = client;
    
    // Setup global functions for manual provider form
    (window as any).connectManualProvider = () => {
        const provider = (document.getElementById('manualProvider') as HTMLSelectElement).value;
        const apiKey = (document.getElementById('manualApiKey') as HTMLInputElement).value;
        const model = (document.getElementById('manualModel') as HTMLInputElement).value;
        const workingDir = (document.getElementById('manualWorkingDir') as HTMLInputElement).value;
        
        client.connectManualProvider(provider, apiKey, model || undefined, workingDir || undefined);
        
        // Clear form
        (document.getElementById('manualApiKey') as HTMLInputElement).value = '';
        (document.getElementById('manualModel') as HTMLInputElement).value = '';
        (document.getElementById('manualWorkingDir') as HTMLInputElement).value = '';
        
        client.toggleManualConfig();
    };
    
    (window as any).toggleManualConfig = () => {
        client.toggleManualConfig();
    };
    
    // Add toggle function for collapsible sections
    (window as any).toggleSection = (sectionId: string) => {
        const content = document.getElementById(sectionId);
        const icon = document.getElementById(`${sectionId}Icon`);
        const header = icon?.closest('.collapsible-header');
        
        if (content && icon && header) {
            const isCollapsed = content.classList.contains('collapsed');
            
            if (isCollapsed) {
                // Expanding
                content.classList.remove('collapsed');
                icon.classList.remove('collapsed');
                header.classList.remove('collapsed');
                
                // Remove count badge when expanded
                const badge = header.querySelector('.provider-count-badge');
                if (badge) {
                    badge.remove();
                }
                
                // Show provider filter if this is the providers section
                if (sectionId === 'providersContent') {
                    const providerFilter = document.querySelector('.provider-filter') as HTMLElement;
                    if (providerFilter) {
                        providerFilter.style.display = 'flex';
                    }
                }
            } else {
                // Collapsing
                content.classList.add('collapsed');
                icon.classList.add('collapsed');
                header.classList.add('collapsed');
                
                // Hide provider filter if this is the providers section
                if (sectionId === 'providersContent') {
                    const providerFilter = document.querySelector('.provider-filter') as HTMLElement;
                    if (providerFilter) {
                        providerFilter.style.display = 'none';
                    }
                }
                
                // Add count badge for providers section
                if (sectionId === 'providersContent') {
                    const providersGrid = document.getElementById('providersGrid');
                    const providerCards = providersGrid?.querySelectorAll('.provider-card:not([style*="display: none"])');
                    const count = providerCards?.length || 0;
                    
                    if (count > 0) {
                        const badge = document.createElement('span');
                        badge.className = 'provider-count-badge';
                        badge.textContent = `${count}`;
                        badge.title = `${count} provider(s) available`;
                        header.appendChild(badge);
                    }
                }
            }
            
            // Store state in localStorage
            localStorage.setItem(`section_${sectionId}_collapsed`, (!isCollapsed).toString());
        }
    };
    
    // Restore collapsed states from localStorage
    const restoreCollapsedStates = () => {
        ['serverConnection', 'providersContent'].forEach(sectionId => {
            const isCollapsed = localStorage.getItem(`section_${sectionId}_collapsed`) === 'true';
            if (isCollapsed) {
                const content = document.getElementById(sectionId);
                const icon = document.getElementById(`${sectionId}Icon`);
                const header = icon?.closest('.collapsible-header');
                if (content && icon && header) {
                    content.classList.add('collapsed');
                    icon.classList.add('collapsed');
                    header.classList.add('collapsed');
                    
                    // Hide provider filter if this is the providers section
                    if (sectionId === 'providersContent') {
                        const providerFilter = document.querySelector('.provider-filter') as HTMLElement;
                        if (providerFilter) {
                            providerFilter.style.display = 'none';
                        }
                    }
                }
            }
        });
    };
    
    // Restore states after DOM is ready
    setTimeout(restoreCollapsedStates, 100);
    
    // Add debug helper functions
    (window as any).debugShowProviders = () => {
        const providersSection = document.getElementById('providersSection');
        if (providersSection) {
            providersSection.style.display = 'block';
            console.log('✅ Forced provider section to display: block');
        }
    };
    
    (window as any).debugCheckElements = () => {
        console.log('=== DOM Element Check ===');
        console.log('providersSection:', document.getElementById('providersSection'));
        console.log('providersGrid:', document.getElementById('providersGrid'));
        console.log('providersGrid children:', document.getElementById('providersGrid')?.children.length);
        console.log('=========================');
    };
    
    console.log('✨ DraCode Client initialized');
    console.log('💡 Debug commands available:');
    console.log('   debugShowProviders() - Force show provider section');
    console.log('   debugCheckElements() - Check DOM elements');
    console.log('   draCodeClient - Access client instance');
});
