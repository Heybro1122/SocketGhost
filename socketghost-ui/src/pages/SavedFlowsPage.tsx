import { useState } from 'react';
import { useFlowHistory, StoredFlow } from '../hooks/useFlowHistory';

export function SavedFlowsPage() {
    const { flows, loading, error, getFlow, replayFlow, exportFlow, deleteFlow } = useFlowHistory();
    const [selectedFlow, setSelectedFlow] = useState<StoredFlow | null>(null);
    const [showReplayModal, setShowReplayModal] = useState(false);
    const [replayConfirm, setReplayConfirm] = useState('');

    const handleViewFlow = async (id: string) => {
        const flow = await getFlow(id);
        if (flow) setSelectedFlow(flow);
    };

    const handleReplay = async () => {
        if (!selectedFlow || replayConfirm !== 'REPLAY') return;

        try {
            await replayFlow(selectedFlow.id);
            alert('Replay initiated! Check console for logs.');
            setShowReplayModal(false);
            setReplayConfirm('');
        } catch (err) {
            alert(`Replay failed: ${err}`);
        }
    };

    return (
        <div style={{ padding: '20px', fontFamily: 'Arial, sans-serif' }}>
            <h2>Saved Flows</h2>

            {loading && <p>Loading flows...</p>}
            {error && <p style={{ color: 'red' }}>Error: {error}</p>}

            <div style={{ display: 'flex', gap: '20px' }}>
                {/* Flow List */}
                <div style={{ flex: 1 }}>
                    <h3>Flow History</h3>
                    {flows.length === 0 && !loading && <p>No flows saved yet.</p>}
                    <div style={{ maxHeight: '600px', overflowY: 'auto' }}>
                        {flows.map(flow => (
                            <div
                                key={flow.id}
                                onClick={() => handleViewFlow(flow.id)}
                                style={{
                                    padding: '10px',
                                    margin: '5px 0',
                                    border: '1px solid #ccc',
                                    borderRadius: '4px',
                                    cursor: 'pointer',
                                    background: selectedFlow?.id === flow.id ? '#e6f7ff' : '#fff'
                                }}
                            >
                                <div style={{ fontWeight: 'bold' }}>
                                    {flow.method} {flow.url.length > 50 ? flow.url.substring(0, 50) + '...' : flow.url}
                                </div>
                                <div style={{ fontSize: '12px', color: '#666' }}>
                                    PID: {flow.pid} | Status: {flow.statusCode} | {new Date(flow.capturedAt).toLocaleString()}
                                </div>
                                {flow.viaUpdate && <span style={{ fontSize: '10px', background: '#ff9800', padding: '2px 4px', borderRadius: '2px', color: '#fff' }}>UPDATED</span>}
                                {flow.viaManualResend && <span style={{ fontSize: '10px', background: '#2196f3', padding: '2px 4px', borderRadius: '2px', color: '#fff', marginLeft: '4px' }}>MANUAL_RESEND</span>}
                            </div>
                        ))}
                    </div>
                </div>

                {/* Flow Detail */}
                {selectedFlow && (
                    <div style={{ flex: 2, border: '1px solid #ccc', borderRadius: '4px', padding: '20px' }}>
                        <h3>Flow Details</h3>
                        <div style={{ marginBottom: '10px' }}>
                            <strong>ID:</strong> {selectedFlow.id}
                        </div>
                        <div style={{ marginBottom: '10px' }}>
                            <strong>Method:</strong> {selectedFlow.method} <strong>URL:</strong> {selectedFlow.url}
                        </div>
                        <div style={{ marginBottom: '10px' }}>
                            <strong>PID:</strong> {selectedFlow.pid} | <strong>Status:</strong> {selectedFlow.response.statusCode}
                        </div>
                        <div style={{ marginBottom: '10px' }}>
                            <strong>Captured:</strong> {new Date(selectedFlow.capturedAt).toLocaleString()}
                        </div>

                        <h4>Request Headers</h4>
                        <pre style={{ background: '#f5f5f5', padding: '10px', overflow: 'auto', maxHeight: '150px', fontSize: '12px' }}>
                            {JSON.stringify(selectedFlow.request.headers, null, 2)}
                        </pre>

                        <h4>Request Body</h4>
                        <pre style={{ background: '#f5f5f5', padding: '10px', overflow: 'auto', maxHeight: '150px', fontSize: '12px' }}>
                            {selectedFlow.request.bodyPreview || '(empty)'}
                        </pre>

                        <h4>Response Headers</h4>
                        <pre style={{ background: '#f5f5f5', padding: '10px', overflow: 'auto', maxHeight: '150px', fontSize: '12px' }}>
                            {JSON.stringify(selectedFlow.response.headers, null, 2)}
                        </pre>

                        <h4>Response Body</h4>
                        <pre style={{ background: '#f5f5f5', padding: '10px', overflow: 'auto', maxHeight: '200px', fontSize: '12px' }}>
                            {selectedFlow.response.bodyPreview || '(empty)'}
                        </pre>

                        <div style={{ marginTop: '20px', display: 'flex', gap: '10px' }}>
                            <button onClick={() => exportFlow(selectedFlow.id)} style={{ padding: '8px 16px', background: '#4caf50', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}>
                                Export
                            </button>
                            <button onClick={() => setShowReplayModal(true)} style={{ padding: '8px 16px', background: '#ff9800', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}>
                                Replay
                            </button>
                            <button onClick={() => { if (confirm('Delete this flow?')) deleteFlow(selectedFlow.id); setSelectedFlow(null); }} style={{ padding: '8px 16px', background: '#f44336', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}>
                                Delete
                            </button>
                        </div>
                    </div>
                )}
            </div>

            {/* Replay Confirmation Modal */}
            {showReplayModal && (
                <div style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    background: 'rgba(0,0,0,0.5)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center'
                }}>
                    <div style={{ background: '#fff', padding: '30px', borderRadius: '8px', maxWidth: '500px' }}>
                        <h3>⚠️ Confirm Replay</h3>
                        <p>You are about to replay this flow: <code>{selectedFlow?.method} {selectedFlow?.url}</code></p>
                        <p style={{ color: '#f44336', fontWeight: 'bold' }}>WARNING: This will send a real HTTP request with potential side effects!</p>
                        <p>Type <strong>REPLAY</strong> to confirm:</p>
                        <input
                            type="text"
                            value={replayConfirm}
                            onChange={(e) => setReplayConfirm(e.target.value)}
                            style={{ width: '100%', padding: '8px', marginBottom: '20px', border: '1px solid #ccc', borderRadius: '4px' }}
                        />
                        <div style={{ display: 'flex', gap: '10px' }}>
                            <button onClick={handleReplay} disabled={replayConfirm !== 'REPLAY'} style={{ padding: '8px 16px', background: replayConfirm === 'REPLAY' ? '#ff9800' : '#ccc', color: '#fff', border: 'none', borderRadius: '4px', cursor: replayConfirm === 'REPLAY' ? 'pointer' : 'not-allowed' }}>
                                Replay
                            </button>
                            <button onClick={() => { setShowReplayModal(false); setReplayConfirm(''); }} style={{ padding: '8px 16px', background: '#9e9e9e', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}>
                                Cancel
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
