import { useState, useEffect } from 'react';

const FLOW_API_URL = 'http://127.0.0.1:9300';

export interface FlowMetadata {
    id: string;
    capturedAt: string;
    pid: number;
    method: string;
    url: string;
    statusCode: number;
    sizeBytes: number;
    viaUpdate: boolean;
    viaManualResend: boolean;
}

export interface StoredFlow {
    id: string;
    capturedAt: string;
    pid: number;
    method: string;
    url: string;
    request: {
        headers: Record<string, string>;
        bodyPreview?: string;
        fullBodyPath?: string;
    };
    response: {
        statusCode: number;
        headers: Record<string, string>;
        bodyPreview?: string;
        fullBodyPath?: string;
    };
    viaUpdate: boolean;
    viaManualResend: boolean;
    scriptApplied: string[];
    notes?: string;
    sizeBytes: number;
}

export function useFlowHistory() {
    const [flows, setFlows] = useState<FlowMetadata[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const fetchFlows = async (limit = 50, offset = 0, filters?: {
        pid?: number;
        method?: string;
        query?: string;
    }) => {
        setLoading(true);
        setError(null);
        try {
            const params = new URLSearchParams({ limit: String(limit), offset: String(offset) });
            if (filters?.pid) params.append('pid', String(filters.pid));
            if (filters?.method) params.append('method', filters.method);
            if (filters?.query) params.append('query', filters.query);

            const res = await fetch(`${FLOW_API_URL}/flows?${params}`);
            if (!res.ok) throw new Error(`Failed to fetch flows: ${res.statusText}`);

            const data: FlowMetadata[] = await res.json();
            setFlows(data);
        } catch (err: any) {
            setError(err.message);
        } finally {
            setLoading(false);
        }
    };

    const getFlow = async (id: string): Promise<StoredFlow | null> => {
        try {
            const res = await fetch(`${FLOW_API_URL}/flows/${id}`);
            if (!res.ok) throw new Error(`Failed to fetch flow: ${res.statusText}`);
            return await res.json();
        } catch (err) {
            console.error(err);
            return null;
        }
    };

    const deleteFlow = async (id: string) => {
        try {
            const res = await fetch(`${FLOW_API_URL}/flows/${id}`, { method: 'DELETE' });
            if (!res.ok) throw new Error(`Failed to delete flow: ${res.statusText}`);
            await fetchFlows(); // Refresh
        } catch (err: any) {
            setError(err.message);
        }
    };

    const replayFlow = async (id: string, edits?: {
        method?: string;
        url?: string;
        headers?: Record<string, string>;
        body?: string;
    }) => {
        try {
            const res = await fetch(`${FLOW_API_URL}/flows/${id}/replay`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-SocketGhost-Confirm': 'true'
                },
                body: JSON.stringify({ confirm: true, ...edits })
            });

            if (!res.ok) throw new Error(`Failed to replay flow: ${res.statusText}`);
            return await res.json();
        } catch (err: any) {
            setError(err.message);
            throw err;
        }
    };

    const exportFlow = async (id: string) => {
        const flow = await getFlow(id);
        if (!flow) return;

        const blob = new Blob([JSON.stringify(flow, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `flow-${id}.json`;
        a.click();
        URL.revokeObjectURL(url);
    };

    const importFlows = async (file: File) => {
        try {
            const text = await file.text();
            const res = await fetch(`${FLOW_API_URL}/flows/import`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: text
            });

            if (!res.ok) throw new Error(`Failed to import flows: ${res.statusText}`);
            await fetchFlows(); // Refresh
        } catch (err: any) {
            setError(err.message);
            throw err;
        }
    };

    useEffect(() => {
        fetchFlows();
    }, []);

    return {
        flows,
        loading,
        error,
        fetchFlows,
        getFlow,
        deleteFlow,
        replayFlow,
        exportFlow,
        importFlows
    };
}
