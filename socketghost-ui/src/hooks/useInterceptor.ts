import { useState, useEffect, useCallback } from 'react';

const STORAGE_KEY_PREFIX = 'socketghost-interceptor-';

interface UseInterceptorReturn {
    interceptorEnabled: boolean;
    enableInterceptor: (pid: number) => void;
    disableInterceptor: (pid: number) => void;
}

export function useInterceptor(
    ws: WebSocket | null,
    selectedPid: number | null
): UseInterceptorReturn {
    const [interceptorEnabled, setInterceptorEnabled] = useState(false);

    // Load interceptor state from localStorage on mount or when PID changes
    useEffect(() => {
        if (!selectedPid) {
            setInterceptorEnabled(false);
            return;
        }

        const storageKey = `${STORAGE_KEY_PREFIX}${selectedPid}`;
        const saved = localStorage.getItem(storageKey);
        setInterceptorEnabled(saved === 'true');
    }, [selectedPid]);

    const enableInterceptor = useCallback((pid: number) => {
        if (!ws || ws.readyState !== WebSocket.OPEN) {
            console.error('[useInterceptor] WebSocket not connected');
            return;
        }

        // Send interceptor.set message to core
        ws.send(JSON.stringify({
            v: '0.1',
            type: 'interceptor.set',
            pid,
            enabled: true
        }));

        setInterceptorEnabled(true);
        localStorage.setItem(`${STORAGE_KEY_PREFIX}${pid}`, 'true');
        console.log(`[useInterceptor] Enabled for PID ${pid}`);
    }, [ws]);

    const disableInterceptor = useCallback((pid: number) => {
        if (!ws || ws.readyState !== WebSocket.OPEN) {
            console.error('[useInterceptor] WebSocket not connected');
            return;
        }

        // Send interceptor.set message to core
        ws.send(JSON.stringify({
            v: '0.1',
            type: 'interceptor.set',
            pid,
            enabled: false
        }));

        setInterceptorEnabled(false);
        localStorage.removeItem(`${STORAGE_KEY_PREFIX}${pid}`);
        console.log(`[useInterceptor] Disabled for PID ${pid}`);
    }, [ws]);

    return {
        interceptorEnabled,
        enableInterceptor,
        disableInterceptor,
    };
}
