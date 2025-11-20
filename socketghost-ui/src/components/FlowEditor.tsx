import { useState, useEffect } from 'react';
import Editor from '@monaco-editor/react';
import { PausedFlow } from '../hooks/usePausedFlows';
import { FlowUpdate, useFlowEditor } from '../hooks/useFlowEditor';

interface Props {
    flow: PausedFlow;
    ws: WebSocket | null;
    onClose: () => void;
    onForward: (flowId: string) => void;
    onDrop: (flowId: string) => void;
}

const MAX_BODY_SIZE = 1024 * 1024; // 1MB
const EDITOR_PREFS_KEY = 'socketghost-editor-prefs';

interface EditorPrefs {
    tabSize: number;
    autoParse: boolean;
}

export default function FlowEditor({ flow, ws, onClose, onForward, onDrop }: Props) {
    const { sendFlowUpdate, validateJson } = useFlowEditor(ws);

    // Load preferences from localStorage
    const loadPrefs = (): EditorPrefs => {
        try {
            const saved = localStorage.getItem(EDITOR_PREFS_KEY);
            return saved ? JSON.parse(saved) : { tabSize: 2, autoParse: true };
        } catch {
            return { tabSize: 2, autoParse: true };
        }
    };

    const [prefs, _setPrefs] = useState<EditorPrefs>(loadPrefs());
    const [headers, setHeaders] = useState<Record<string, string>>(flow.headers || {});
    const [body, setBody] = useState(flow.bodyPreview || '');
    const [isJsonValid, setIsJsonValid] = useState(true);
    const [forceForward, setForceForward] = useState(false);
    const [hasChanges, setHasChanges] = useState(false);

    // Check if content type is binary
    const contentType = headers['Content-Type'] || headers['content-type'] || '';
    const isBinary = contentType.includes('octet-stream') ||
        contentType.includes('image/') ||
        contentType.includes('video/') ||
        contentType.includes('audio/');

    const isBodyTooLarge = body.length > MAX_BODY_SIZE;

    // Detect language mode
    const getLanguageMode = () => {
        if (contentType.includes('json')) return 'json';
        if (contentType.includes('xml')) return 'xml';
        if (contentType.includes('html')) return 'html';
        if (contentType.includes('javascript')) return 'javascript';
        return 'plaintext';
    };

    // Validate JSON on body change
    useEffect(() => {
        const mode = getLanguageMode();
        if (mode === 'json') {
            setIsJsonValid(validateJson(body));
        } else {
            setIsJsonValid(true); // Non-JSON is always valid
        }
    }, [body, validateJson]);

    // Save preferences to localStorage
    useEffect(() => {
        localStorage.setItem(EDITOR_PREFS_KEY, JSON.stringify(prefs));
    }, [prefs]);

    const handleFormatJson = () => {
        try {
            const parsed = JSON.parse(body);
            const formatted = JSON.stringify(parsed, null, prefs.tabSize);
            setBody(formatted);
            setHasChanges(true);
        } catch (err) {
            alert('Cannot format: Invalid JSON');
        }
    };

    const handleAddHeader = () => {
        const key = prompt('Header name:');
        if (!key) return;
        const value = prompt('Header value:');
        if (value === null) return;
        setHeaders({ ...headers, [key]: value });
        setHasChanges(true);
    };

    const handleRemoveHeader = (key: string) => {
        const newHeaders = { ...headers };
        delete newHeaders[key];
        setHeaders(newHeaders);
        setHasChanges(true);
    };

    const handleForwardWithEdits = () => {
        // Check JSON validity
        if (getLanguageMode() === 'json' && !isJsonValid && !forceForward) {
            alert('Invalid JSON. Enable "Force forward invalid JSON" to proceed.');
            return;
        }

        // Send update if there are changes
        if (hasChanges) {
            const update: FlowUpdate = {
                headers,
                body
            };
            sendFlowUpdate(flow.flowId, update);

            // Wait a bit for the update to be processed, then forward
            setTimeout(() => {
                onForward(flow.flowId);
                onClose();
            }, 100);
        } else {
            // No changes, just forward
            onForward(flow.flowId);
            onClose();
        }
    };

    const handleForwardNoEdit = () => {
        onForward(flow.flowId);
        onClose();
    };

    const handleDrop = () => {
        onDrop(flow.flowId);
        onClose();
    };

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center">
            <div className="bg-white w-11/12 h-5/6 rounded-lg shadow-xl flex flex-col">
                {/* Header */}
                <div className="p-4 border-b border-gray-200 flex items-center justify-between">
                    <div>
                        <h2 className="text-xl font-bold text-gray-800">Flow Editor</h2>
                        <p className="text-sm text-gray-600">
                            {flow.method} {flow.url.length > 60 ? flow.url.substring(0, 60) + '...' : flow.url}
                        </p>
                    </div>
                    <button
                        onClick={onClose}
                        className="text-gray-500 hover:text-gray-700 text-2xl font-bold"
                    >
                        ×
                    </button>
                </div>

                {/* Binary content warning */}
                {isBinary && (
                    <div className="p-4 bg-yellow-50 border-b border-yellow-200">
                        <p className="text-sm text-yellow-800">
                            ⚠️ Binary content detected. Editor disabled. You can only Forward (no edits) or Drop.
                        </p>
                    </div>
                )}

                {/* Body too large warning */}
                {isBodyTooLarge && (
                    <div className="p-4 bg-orange-50 border-b border-orange-200">
                        <p className="text-sm text-orange-800">
                            ⚠️ Body too large ({body.length} bytes &gt; {MAX_BODY_SIZE} bytes). Only header edits allowed.
                        </p>
                    </div>
                )}

                {/* Headers Section */}
                <div className="p-4 border-b border-gray-200">
                    <div className="flex items-center justify-between mb-2">
                        <h3 className="text-sm font-semibold text-gray-700">Headers</h3>
                        <button
                            onClick={handleAddHeader}
                            className="px-2 py-1 text-xs bg-blue-600 hover:bg-blue-700 text-white rounded"
                        >
                            + Add Header
                        </button>
                    </div>
                    <div className="max-h-32 overflow-y-auto bg-gray-50 p-2 rounded">
                        {Object.entries(headers).map(([key, value]) => (
                            <div key={key} className="flex items-center gap-2 mb-1">
                                <span className="text-xs font-mono text-gray-700 flex-1">
                                    {key}: {value}
                                </span>
                                <button
                                    onClick={() => handleRemoveHeader(key)}
                                    className="text-xs text-red-600 hover:text-red-800"
                                >
                                    Remove
                                </button>
                            </div>
                        ))}
                        {Object.keys(headers).length === 0 && (
                            <p className="text-xs text-gray-500">No headers</p>
                        )}
                    </div>
                </div>

                {/* Editor Section */}
                <div className="flex-1 flex flex-col">
                    <div className="p-2 bg-gray-100 border-b border-gray-200 flex items-center justify-between">
                        <div className="flex items-center gap-2">
                            <span className="text-xs font-semibold text-gray-700">Body</span>
                            {getLanguageMode() === 'json' && !isJsonValid && (
                                <span className="text-xs text-red-600 font-semibold">❌ Invalid JSON</span>
                            )}
                            {getLanguageMode() === 'json' && isJsonValid && (
                                <span className="text-xs text-green-600 font-semibold">✓ Valid JSON</span>
                            )}
                        </div>
                        {getLanguageMode() === 'json' && (
                            <button
                                onClick={handleFormatJson}
                                className="px-2 py-1 text-xs bg-gray-600 hover:bg-gray-700 text-white rounded"
                            >
                                Format JSON
                            </button>
                        )}
                    </div>

                    {isBinary ? (
                        <div className="flex-1 flex items-center justify-center text-gray-500">
                            <p>Binary content cannot be edited</p>
                        </div>
                    ) : isBodyTooLarge ? (
                        <div className="flex-1 flex items-center justify-center text-gray-500">
                            <p>Body too large to edit ({body.length} bytes)</p>
                        </div>
                    ) : (
                        <Editor
                            height="100%"
                            defaultLanguage={getLanguageMode()}
                            value={body}
                            onChange={(value) => {
                                setBody(value || '');
                                setHasChanges(true);
                            }}
                            options={{
                                minimap: { enabled: false },
                                fontSize: 14,
                                tabSize: prefs.tabSize,
                                wordWrap: 'on',
                                scrollBeyondLastLine: false,
                            }}
                        />
                    )}
                </div>

                {/* Actions Footer */}
                <div className="p-4 border-t border-gray-200 bg-gray-50">
                    <div className="flex items-center justify-between">
                        <div className="flex items-center gap-3">
                            {getLanguageMode() === 'json' && (
                                <label className="flex items-center gap-2 text-sm">
                                    <input
                                        type="checkbox"
                                        checked={forceForward}
                                        onChange={(e) => setForceForward(e.target.checked)}
                                        className="w-4 h-4"
                                    />
                                    <span className="text-gray-700">Force forward invalid JSON</span>
                                </label>
                            )}
                        </div>

                        <div className="flex gap-3">
                            <button
                                onClick={handleForwardNoEdit}
                                className="px-4 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded font-semibold"
                            >
                                Forward (no edits)
                            </button>
                            <button
                                onClick={handleForwardWithEdits}
                                disabled={isBinary}
                                className={`px-4 py-2 rounded font-semibold ${isBinary
                                    ? 'bg-gray-300 text-gray-500 cursor-not-allowed'
                                    : 'bg-green-600 hover:bg-green-700 text-white'
                                    }`}
                            >
                                Forward (with edits)
                            </button>
                            <button
                                onClick={handleDrop}
                                className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded font-semibold"
                            >
                                Drop
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
