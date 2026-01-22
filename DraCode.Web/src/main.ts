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
