import { useState, useEffect } from 'react';
import { Process } from '../types';

const STORAGE_KEY = 'socketghost-selected-pid';

const MOCK_PROCESSES: Process[] = [
    { pid: 1234, exe: 'node', cmdline: 'node examples/todo/client.js' },
    { pid: 5678, exe: 'chrome', cmdline: 'chrome.exe --type=renderer' },
    { pid: 9012, exe: 'vscode', cmdline: 'C:\\Program Files\\VS Code\\Code.exe' },
];

export function useProcesses(demoMode: boolean) {
    const [processes, setProcesses] = useState<Process[]>([]);
    const [selectedPid, setSelectedPid] = useState<number | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Load last selected PID from localStorage
    useEffect(() => {
        const saved = localStorage.getItem(STORAGE_KEY);
        if (saved) {
            const pid = parseInt(saved, 10);
            if (!isNaN(pid)) {
                setSelectedPid(pid);
            }
        }
    }, []);

    // Fetch processes
    const fetchProcesses = async () => {
        if (demoMode) {
            setProcesses(MOCK_PROCESSES);
            setError(null);
            return;
        }

        setLoading(true);
        setError(null);

        try {
            const response = await fetch('http://127.0.0.1:9100/processes');
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            const data = await response.json();
            setProcesses(data);
        } catch (err) {
            console.error('[ProcessAPI] Fetch error:', err);
            setError('Failed to fetch processes. Is the core running?');
            setProcesses([]);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchProcesses();
    }, [demoMode]);

    const selectProcess = (pid: number) => {
        setSelectedPid(pid);
        localStorage.setItem(STORAGE_KEY, pid.toString());
    };

    return {
        processes,
        selectedPid,
        selectProcess,
        loading,
        error,
        retry: fetchProcesses,
    };
}
