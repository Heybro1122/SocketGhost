import { useState, useEffect } from 'react';

export interface Script {
    Id: string;
    Name: string;
    Enabled: boolean;
    Code: string;
    CreatedAt: string;
    UpdatedAt: string;
}

export function useScripts() {
    const [scripts, setScripts] = useState<Script[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const fetchScripts = async () => {
        try {
            const res = await fetch('http://127.0.0.1:9200/scripts');
            if (!res.ok) throw new Error('Failed to fetch scripts');
            const data = await res.json();
            setScripts(data);
        } catch (err: any) {
            setError(err.message);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchScripts();
    }, []);

    const createScript = async (script: Partial<Script>) => {
        await fetch('http://127.0.0.1:9200/scripts', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(script),
        });
        fetchScripts();
    };

    const updateScript = async (id: string, script: Partial<Script>) => {
        await fetch(`http://127.0.0.1:9200/scripts/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(script),
        });
        fetchScripts();
    };

    const deleteScript = async (id: string) => {
        await fetch(`http://127.0.0.1:9200/scripts/${id}`, {
            method: 'DELETE',
        });
        fetchScripts();
    };

    const toggleScript = async (id: string, enabled: boolean) => {
        await fetch(`http://127.0.0.1:9200/scripts/${id}/${enabled ? 'enable' : 'disable'}`, {
            method: 'POST',
        });
        fetchScripts();
    };

    return { scripts, loading, error, fetchScripts, createScript, updateScript, deleteScript, toggleScript };
}
