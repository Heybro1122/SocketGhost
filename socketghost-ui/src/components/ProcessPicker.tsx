import { useState } from 'react';
import { Process } from '../types';

interface Props {
    processes: Process[];
    selectedPid: number | null;
    onSelectProcess: (pid: number) => void;
    loading: boolean;
    error: string | null;
    onRetry: () => void;
}

export default function ProcessPicker({
    processes,
    selectedPid,
    onSelectProcess,
    loading,
    error,
    onRetry,
}: Props) {
    const [searchTerm, setSearchTerm] = useState('');

    const filteredProcesses = processes.filter(
        (p) =>
            p.exe.toLowerCase().includes(searchTerm.toLowerCase()) ||
            p.pid.toString().includes(searchTerm) ||
            p.cmdline.toLowerCase().includes(searchTerm.toLowerCase())
    );

    const truncateCmdline = (cmdline: string, maxLength = 80) => {
        if (cmdline.length <= maxLength) return cmdline;
        return cmdline.substring(0, maxLength) + '...';
    };

    return (
        <div className="flex flex-col h-full">
            <div className="p-4 border-b border-gray-200">
                <h2 className="text-lg font-semibold mb-2">Process Picker</h2>
                <input
                    type="text"
                    placeholder="Search processes..."
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
            </div>

            {error && (
                <div className="p-4 bg-red-50 border-b border-red-200">
                    <p className="text-sm text-red-600 mb-2">{error}</p>
                    <button
                        onClick={onRetry}
                        className="px-3 py-1 text-sm bg-red-600 text-white rounded hover:bg-red-700"
                    >
                        Retry
                    </button>
                </div>
            )}

            {loading && (
                <div className="p-4 text-center text-gray-500">
                    Loading processes...
                </div>
            )}

            <div className="flex-1 overflow-y-auto">
                {!loading && filteredProcesses.length === 0 && (
                    <div className="p-4 text-center text-gray-500">
                        {searchTerm ? 'No processes match your search' : 'No processes found'}
                    </div>
                )}

                {filteredProcesses.map((process) => (
                    <div
                        key={process.pid}
                        onClick={() => onSelectProcess(process.pid)}
                        className={`p-3 border-b border-gray-100 cursor-pointer hover:bg-blue-50 transition-colors ${selectedPid === process.pid ? 'bg-blue-100' : ''
                            }`}
                    >
                        <div className="flex items-center justify-between mb-1">
                            <span className="font-semibold text-gray-800">{process.exe}</span>
                            <span className="text-sm text-gray-500">PID: {process.pid}</span>
                        </div>
                        {process.cmdline && (
                            <div className="text-xs text-gray-600 font-mono">
                                {truncateCmdline(process.cmdline)}
                            </div>
                        )}
                    </div>
                ))}
            </div>

            {/* Future: Attach pause/intercept UI here */}
            {selectedPid && (
                <div className="p-3 border-t border-gray-200 bg-gray-50">
                    <p className="text-xs text-gray-500">
                        TODO: Pause/Intercept controls will attach here
                    </p>
                </div>
            )}
        </div>
    );
}
