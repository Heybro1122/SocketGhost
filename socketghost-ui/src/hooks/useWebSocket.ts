import { useState, useEffect, useCallback, useRef } from 'react';
import { FlowEvent } from '../types';

const MOCK_FLOWS: FlowEvent[] = [
    {
        v: '0.1',
        type: 'flow.new',
        flow: {
            flowId: 'mock-flow-1',
            pid: 1234,
            method: 'POST',
            url: 'http://localhost:3000/api/tasks',
            headers: { 'Content-Type': 'application/json' },
            bodyPreview: '{"task":"Demo task 1"}',
        },
    },
    {
        v: '0.1',
        type: 'flow.new',
        flow: {
            flowId: 'mock-flow-2',
            pid: null,
            method: 'GET',
            url: 'http://example.com/data',
            headers: { Accept: 'application/json' },
            bodyPreview: '',
        },
    },
    {
        v: '0.1',
        type: 'flow.new',
        flow: {
            flowId: 'mock-flow-3',
            pid: 5678,
            method: 'PUT',
            url: 'http://localhost:3000/api/users/42',
            headers: { 'Content-Type': 'application/json' },
            bodyPreview: '{"name":"Updated User"}',
        },
    },
];

export function useWebSocket(demoMode: boolean) {
    const [flows, setFlows] = useState<FlowEvent[]>([]);
    const [isConnected, setIsConnected] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const wsRef = useRef<WebSocket | null>(null);
    const reconnectTimeoutRef = useRef<number | null>(null);
    const mockIntervalRef = useRef<number | null>(null);

    const connect = useCallback(() => {
        if (demoMode) {
            setIsConnected(true);
            setError(null);
            return;
        }

        try {
            const ws = new WebSocket('ws://127.0.0.1:9000');
            wsRef.current = ws;

            ws.onopen = () => {
                console.log('[WebSocket] Connected');
                setIsConnected(true);
                setError(null);
            };

            ws.onmessage = (event) => {
                try {
                    const flowEvent = JSON.parse(event.data) as FlowEvent;
                    setFlows((prev) => [flowEvent, ...prev]);
                } catch (err) {
                    console.error('[WebSocket] Parse error:', err);
                }
            };

            ws.onerror = (event) => {
                console.error('[WebSocket] Error:', event);
                setError('WebSocket connection error');
            };

            ws.onclose = () => {
                console.log('[WebSocket] Disconnected');
                setIsConnected(false);
                wsRef.current = null;

                // Auto-reconnect after 3 seconds
                reconnectTimeoutRef.current = window.setTimeout(() => {
                    console.log('[WebSocket] Attempting to reconnect...');
                    connect();
                }, 3000);
            };
        } catch (err) {
            console.error('[WebSocket] Connection failed:', err);
            setError('Failed to connect to WebSocket');
            setIsConnected(false);
        }
    }, [demoMode]);

    const disconnect = useCallback(() => {
        if (reconnectTimeoutRef.current) {
            clearTimeout(reconnectTimeoutRef.current);
            reconnectTimeoutRef.current = null;
        }
        if (mockIntervalRef.current) {
            clearInterval(mockIntervalRef.current);
            mockIntervalRef.current = null;
        }
        if (wsRef.current) {
            wsRef.current.close();
            wsRef.current = null;
        }
    }, []);

    useEffect(() => {
        if (demoMode) {
            // Generate mock flows periodically
            let mockIndex = 0;
            mockIntervalRef.current = window.setInterval(() => {
                const mockFlow = MOCK_FLOWS[mockIndex % MOCK_FLOWS.length];
                setFlows((prev) => [
                    {
                        ...mockFlow,
                        flow: {
                            ...mockFlow.flow,
                            flowId: `mock-flow-${Date.now()}-${mockIndex}`,
                        },
                    },
                    ...prev,
                ]);
                mockIndex++;
            }, 2000);

            return () => {
                if (mockIntervalRef.current) {
                    clearInterval(mockIntervalRef.current);
                }
            };
        } else {
            connect();
            return disconnect;
        }
    }, [demoMode, connect, disconnect]);

    return { flows, isConnected, error, retry: connect, ws: wsRef.current };
}
