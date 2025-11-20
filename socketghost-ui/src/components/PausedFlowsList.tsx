import { useState, useEffect } from 'react';
import { PausedFlow } from '../hooks/usePausedFlows';
import FlowEditor from './FlowEditor';

interface Props {
    pausedFlows: PausedFlow[];
    ws: WebSocket | null;
    onForward: (flowId: string) => void;
    onDrop: (flowId: string) => void;
}

export default function PausedFlowsList({ pausedFlows, ws, onForward, onDrop }: Props) {
    const [expandedFlowId, setExpandedFlowId] = useState<string | null>(null);
    const [editingFlow, setEditingFlow] = useState<PausedFlow | null>(null);

    const getAge = (receivedAt: string) => {
        const received = new Date(receivedAt);
        const now = new Date();
        const seconds = Math.floor((now.getTime() - received.getTime()) / 1000);

        if (seconds < 60) return `${seconds}s`;
        const minutes = Math.floor(seconds / 60);
        if (minutes < 60) return `${minutes}m`;
        const hours = Math.floor(minutes / 60);
        return `${hours}h`;
    };

    const shortenUrl = (url: string, maxLength = 50) => {
        if (url.length <= maxLength) return url;
        return url.substring(0, maxLength) + '...';
    };

    const truncateText = (text: string, maxLength = 100) => {
        if (text.length <= maxLength) return text;
        return text.substring(0, maxLength) + '...';
    };

    // Update ages every second
    const [, setTick] = useState(0);
    useEffect(() => {
        const interval = setInterval(() => {
            setTick((t) => t + 1);
        }, 1000);
        return () => clearInterval(interval);
    }, []);

    if (pausedFlows.length === 0) {
        return null;
    }

    return (
        <>
            <div className="border-t border-gray-200 bg-yellow-50">
                <div className="p-3 bg-yellow-100 border-b border-yellow-200">
                    <h3 className="text-sm font-semibold text-yellow-900">
                        ⏸ Paused Flows ({pausedFlows.length})
                    </h3>
                    <p className="text-xs text-yellow-700 mt-1">
                        Flows waiting for action (will auto-forward after 60s)
                    </p>
                </div>

                <div className="max-h-64 overflow-y-auto">
                    {pausedFlows.map((flow) => (
                        <div
                            key={flow.flowId}
                            className="p-3 border-b border-yellow-200 hover:bg-yellow-100 transition-colors"
                        >
                            <div className="flex items-start justify-between mb-2">
                                <div className="flex-1">
                                    <div className="flex items-center gap-2 mb-1">
                                        <span className="px-2 py-0.5 text-xs font-semibold bg-blue-100 text-blue-800 rounded">
                                            {flow.method}
                                        </span>
                                        <span className="text-xs text-gray-500">
                                            PID: {flow.pid ?? 'unmapped'}
                                        </span>
                                        <span className="text-xs text-yellow-700 font-semibold">
                                            Age: {getAge(flow.receivedAt)}
                                        </span>
                                    </div>
                                    <div className="text-sm font-mono text-gray-800 mb-1">
                                        {shortenUrl(flow.url)}
                                    </div>
                                    {flow.bodyPreview && (
                                        <div className="text-xs text-gray-600 font-mono">
                                            {truncateText(flow.bodyPreview)}
                                        </div>
                                    )}
                                </div>

                                <div className="flex gap-2 ml-3">
                                    <button
                                        onClick={() => setEditingFlow(flow)}
                                        className="px-3 py-1 text-xs bg-blue-600 hover:bg-blue-700 text-white rounded font-semibold"
                                        title="Edit request headers and body"
                                    >
                                        ✏️ Edit
                                    </button>
                                    <button
                                        onClick={() => onForward(flow.flowId)}
                                        className="px-3 py-1 text-xs bg-green-600 hover:bg-green-700 text-white rounded font-semibold"
                                        title="Forward this request to the server"
                                    >
                                        ✓ Forward
                                    </button>
                                    <button
                                        onClick={() => onDrop(flow.flowId)}
                                        className="px-3 py-1 text-xs bg-red-600 hover:bg-red-700 text-white rounded font-semibold"
                                        title="Drop this request (respond with 502 error)"
                                    >
                                        ✗ Drop
                                    </button>
                                </div>
                            </div>

                            <button
                                onClick={() => setExpandedFlowId(expandedFlowId === flow.flowId ? null : flow.flowId)}
                                className="text-xs text-blue-600 hover:text-blue-800"
                            >
                                {expandedFlowId === flow.flowId ? '▼ Hide Details' : '▶ Show Details'}
                            </button>

                            {expandedFlowId === flow.flowId && (
                                <div className="mt-2 p-2 bg-white border border-gray-200 rounded">
                                    <pre className="text-xs font-mono overflow-x-auto">
                                        {JSON.stringify(flow, null, 2)}
                                    </pre>
                                </div>
                            )}
                        </div>
                    ))}
                </div>
            </div>

            {/* Flow Editor Modal */}
            {editingFlow && (
                <FlowEditor
                    flow={editingFlow}
                    ws={ws}
                    onClose={() => setEditingFlow(null)}
                    onForward={onForward}
                    onDrop={onDrop}
                />
            )}
        </>
    );
}
