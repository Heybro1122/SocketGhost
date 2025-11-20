import { useState } from 'react';
import { FlowEvent, FilterMode } from '../types';
import FlowDetails from './FlowDetails';

interface Props {
    flows: FlowEvent[];
    selectedPid: number | null;
    isConnected: boolean;
    error: string | null;
    onRetry: () => void;
}

export default function FlowsList({
    flows,
    selectedPid,
    isConnected,
    error,
    onRetry,
}: Props) {
    const [filterMode, setFilterMode] = useState<FilterMode>('all');
    const [selectedFlow, setSelectedFlow] = useState<FlowEvent | null>(null);

    const filteredFlows = flows.filter((flowEvent) => {
        const flow = flowEvent.flow;
        if (filterMode === 'all') return true;
        if (filterMode === 'targeted') return flow.pid === selectedPid;
        if (filterMode === 'unmapped') return flow.pid === null;
        return true;
    });

    const shortenUrl = (url: string, maxLength = 50) => {
        if (url.length <= maxLength) return url;
        return url.substring(0, maxLength) + '...';
    };

    const truncateText = (text: string, maxLength = 40) => {
        if (text.length <= maxLength) return text;
        return text.substring(0, maxLength) + '...';
    };

    return (
        <div className="flex flex-col h-full">
            <div className="p-4 border-b border-gray-200">
                <div className="flex items-center justify-between mb-2">
                    <h2 className="text-lg font-semibold">Live Flows</h2>
                    <div className="flex items-center gap-2">
                        {isConnected ? (
                            <span className="flex items-center gap-1 text-green-600 text-sm">
                                <span className="w-2 h-2 bg-green-600 rounded-full"></span>
                                Connected
                            </span>
                        ) : (
                            <span className="flex items-center gap-1 text-red-600 text-sm">
                                <span className="w-2 h-2 bg-red-600 rounded-full"></span>
                                Disconnected
                            </span>
                        )}
                    </div>
                </div>

                <div className="flex items-center gap-2">
                    <label className="text-sm text-gray-600">Filter:</label>
                    <select
                        value={filterMode}
                        onChange={(e) => setFilterMode(e.target.value as FilterMode)}
                        className="px-3 py-1 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    >
                        <option value="all">All Flows</option>
                        <option value="targeted" disabled={!selectedPid}>
                            Only Targeted PID ({selectedPid || 'none'})
                        </option>
                        <option value="unmapped">Unmapped (pid=null)</option>
                    </select>
                    <span className="text-xs text-gray-500">
                        ({filteredFlows.length} flows)
                    </span>
                </div>
            </div>

            {error && (
                <div className="p-4 bg-red-50 border-b border-red-200">
                    <p className="text-sm text-red-600 mb-2">{error}</p>
                    <button
                        onClick={onRetry}
                        className="px-3 py-1 text-sm bg-red-600 text-white rounded hover:bg-red-700"
                    >
                        Retry Connection
                    </button>
                </div>
            )}

            <div className="flex-1 overflow-y-auto">
                {filteredFlows.length === 0 && (
                    <div className="p-8 text-center text-gray-500">
                        <p>No flows yet...</p>
                        <p className="text-sm mt-2">
                            {isConnected
                                ? 'Waiting for intercepted traffic'
                                : 'Connect to WebSocket to see flows'}
                        </p>
                    </div>
                )}

                {/*  Future: Replace with graph visualization (React Flow) */}
                <div className="divide-y divide-gray-100">
                    {filteredFlows.map((flowEvent) => {
                        const flow = flowEvent.flow;
                        return (
                            <div
                                key={flow.flowId}
                                onClick={() => setSelectedFlow(flowEvent)}
                                className="p-3 hover:bg-gray-50 cursor-pointer transition-colors"
                            >
                                <div className="flex items-center gap-2 mb-1">
                                    <span className="px-2 py-0.5 text-xs font-semibold bg-blue-100 text-blue-800 rounded">
                                        {flow.method}
                                    </span>
                                    <span className="text-xs text-gray-500">
                                        PID: {flow.pid ?? 'unmapped'}
                                    </span>
                                    <span className="text-xs text-gray-400 font-mono">
                                        {flow.flowId.substring(0, 8)}...
                                    </span>
                                </div>
                                <div className="text-sm text-gray-800 font-mono mb-1">
                                    {shortenUrl(flow.url)}
                                </div>
                                {flow.bodyPreview && (
                                    <div className="text-xs text-gray-600 font-mono">
                                        {truncateText(flow.bodyPreview)}
                                    </div>
                                )}
                            </div>
                        );
                    })}
                </div>
            </div>

            {/* TODO: Add Monaco editor for script editing here */}

            {selectedFlow && (
                <FlowDetails
                    flow={selectedFlow}
                    onClose={() => setSelectedFlow(null)}
                />
            )}
        </div>
    );
}
