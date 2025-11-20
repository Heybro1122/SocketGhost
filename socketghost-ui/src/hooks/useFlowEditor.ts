import { useCallback } from 'react';

export interface FlowUpdate {
    headers?: Record<string, string>;
    body?: string;
}

interface UseFlowEditorReturn {
    sendFlowUpdate: (flowId: string, update: FlowUpdate) => void;
    validateJson: (text: string) => boolean;
}

export function useFlowEditor(ws: WebSocket | null): UseFlowEditorReturn {
    const sendFlowUpdate = useCallback((flowId: string, update: FlowUpdate) => {
        if (!ws || ws.readyState !== WebSocket.OPEN) {
            console.error('[useFlowEditor] WebSocket not connected');
            return;
        }

        ws.send(JSON.stringify({
            v: '0.1',
            type: 'flow.update',
            flowId,
            update
        }));

        console.log(`[useFlowEditor] Sent flow.update for ${flowId}`);
    }, [ws]);

    const validateJson = useCallback((text: string): boolean => {
        if (!text || text.trim() === '') {
            return true; // Empty is valid
        }

        try {
            JSON.parse(text);
            return true;
        } catch {
            return false;
        }
    }, []);

    return {
        sendFlowUpdate,
        validateJson,
    };
}
