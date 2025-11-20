import { useState, useEffect, useCallback } from 'react';

export interface PausedFlow {
    flowId: string;
    pid: number | null;
    method: string;
    url: string;
    headers: Record<string, string>;
    bodyPreview: string;
    receivedAt: string;
}

interface UsePausedFlowsReturn {
    pausedFlows: PausedFlow[];
    forwardFlow: (flowId: string) => void;
    dropFlow: (flowId: string) => void;
}

export function usePausedFlows(ws: WebSocket | null): UsePausedFlowsReturn {
    const [pausedFlows, setPausedFlows] = useState<PausedFlow[]>([]);

    useEffect(() => {
        if (!ws) return;

        const handleMessage = (event: MessageEvent) => {
            try {
                const msg = JSON.parse(event.data);

                if (msg.type === 'flow.paused' && msg.flow) {
                    // Add paused flow to list
                    setPausedFlows((prev) => {
                        // Avoid duplicates
                        if (prev.some((f) => f.flowId === msg.flow.flowId)) {
                            return prev;
                        }
                        return [msg.flow, ...prev];
                    });
                    console.log(`[usePausedFlows] Flow paused: ${msg.flow.flowId}`);
                } else if (msg.type === 'flow.forwarded' || msg.type === 'flow.dropped') {
                    // Remove flow from paused list
                    setPausedFlows((prev) => prev.filter((f) => f.flowId !== msg.flowId));
                    console.log(`[usePausedFlows] Flow ${msg.type}: ${msg.flowId}`);
                } else if (msg.type === 'flow.auto_forwarded') {
                    // Remove auto-forwarded flow
                    setPausedFlows((prev) => prev.filter((f) => f.flowId !== msg.flowId));
                    console.log(`[usePausedFlows] Flow auto-forwarded (timeout): ${msg.flowId}`);

                    // TODO: Show toast notification to user
                }
            } catch (err) {
                console.error('[usePausedFlows] Error processing message:', err);
            }
        };

        ws.addEventListener('message', handleMessage);
        return () => ws.removeEventListener('message', handleMessage);
    }, [ws]);

    const forwardFlow = useCallback((flowId: string) => {
        if (!ws || ws.readyState !== WebSocket.OPEN) {
            console.error('[usePausedFlows] WebSocket not connected');
            return;
        }

        ws.send(JSON.stringify({
            v: '0.1',
            type: 'flow.action',
            action: 'forward',
            flowId
        }));

        console.log(`[usePausedFlows] Forwarding flow: ${flowId}`);
    }, [ws]);

    const dropFlow = useCallback((flowId: string) => {
        if (!ws || ws.readyState !== WebSocket.OPEN) {
            console.error('[usePausedFlows] WebSocket not connected');
            return;
        }

        ws.send(JSON.stringify({
            v: '0.1',
            type: 'flow.action',
            action: 'drop',
            flowId
        }));

        console.log(`[usePausedFlows] Dropping flow: ${flowId}`);
    }, [ws]);

    return {
        pausedFlows,
        forwardFlow,
        dropFlow,
    };
}
