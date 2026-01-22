// Type definitions for WebSocket messages
export interface WebSocketMessage {
    command: 'list' | 'connect' | 'disconnect' | 'reset' | 'send' | 'prompt_response';
    agentId?: string;
    data?: string;
    config?: AgentConfig;
    promptId?: string;
}

export interface WebSocketResponse {
    Status: 'success' | 'connected' | 'disconnected' | 'processing' | 'completed' | 'error' | 'reset' | 'stream' | 'prompt';
    Message?: string;
    Data?: string;
    Error?: string;
    AgentId?: string;
    MessageType?: string;  // Type of streaming message
    PromptId?: string;     // ID for interactive prompts
}

export interface AgentConfig {
    provider: string;
    apiKey?: string;
    model?: string;
    workingDirectory?: string;
    verbose?: string;
}

export interface Provider {
    name: string;
    type: string;
    model?: string;
    configured: boolean;
    deployment?: string;
}

export interface Agent {
    provider: string;
    name: string;
    tabElement: HTMLButtonElement;
    contentElement: HTMLDivElement;
}

export type LogLevel = 'success' | 'error' | 'info' | 'warning';

