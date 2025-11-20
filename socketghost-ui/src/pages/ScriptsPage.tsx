import React, { useState } from 'react';
import { useScripts, Script } from '../hooks/useScripts';
import { ScriptEditor } from '../components/ScriptEditor';

export const ScriptsPage: React.FC = () => {
    const { scripts, loading, error, createScript, updateScript, deleteScript, toggleScript } = useScripts();
    const [editingScript, setEditingScript] = useState<Script | null>(null);
    const [isCreating, setIsCreating] = useState(false);
    const [newScriptName, setNewScriptName] = useState('');
    const [newScriptCode, setNewScriptCode] = useState('function onRequest(flow) {\n  // ...\n}');

    if (loading) return <div className="p-4 text-gray-400">Loading scripts...</div>;
    if (error) return <div className="p-4 text-red-400">Error: {error}</div>;

    const handleSave = async () => {
        if (isCreating) {
            await createScript({ Name: newScriptName, Code: newScriptCode, Enabled: false });
            setIsCreating(false);
        } else if (editingScript) {
            await updateScript(editingScript.Id, { ...editingScript, Code: newScriptCode, Name: newScriptName });
            setEditingScript(null);
        }
        setNewScriptName('');
        setNewScriptCode('');
    };

    const startEdit = (script: Script) => {
        setEditingScript(script);
        setNewScriptName(script.Name);
        setNewScriptCode(script.Code);
        setIsCreating(false);
    };

    const startCreate = () => {
        setIsCreating(true);
        setEditingScript(null);
        setNewScriptName('New Script');
        setNewScriptCode('function onRequest(flow) {\n  // Modify flow.request\n}\n\nfunction onResponse(flow) {\n  // Modify flow.response\n}');
    };

    return (
        <div className="p-6 text-gray-200">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-2xl font-bold">Ghost Scripts</h1>
                <button
                    onClick={startCreate}
                    className="bg-blue-600 hover:bg-blue-700 px-4 py-2 rounded-md text-sm font-medium"
                >
                    New Script
                </button>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {/* List */}
                <div className="space-y-4">
                    {scripts.map(script => (
                        <div key={script.Id} className="bg-gray-800 p-4 rounded-lg border border-gray-700 flex justify-between items-center">
                            <div>
                                <h3 className="font-semibold text-lg">{script.Name}</h3>
                                <p className="text-xs text-gray-500 font-mono">{script.Id}</p>
                            </div>
                            <div className="flex items-center space-x-3">
                                <button
                                    onClick={() => toggleScript(script.Id, !script.Enabled)}
                                    className={`px-3 py-1 rounded text-xs font-bold ${script.Enabled ? 'bg-green-900 text-green-300' : 'bg-gray-700 text-gray-400'}`}
                                >
                                    {script.Enabled ? 'ENABLED' : 'DISABLED'}
                                </button>
                                <button onClick={() => startEdit(script)} className="text-blue-400 hover:text-blue-300 text-sm">Edit</button>
                                <button onClick={() => deleteScript(script.Id)} className="text-red-400 hover:text-red-300 text-sm">Delete</button>
                            </div>
                        </div>
                    ))}
                    {scripts.length === 0 && <p className="text-gray-500 italic">No scripts found.</p>}
                </div>

                {/* Editor */}
                {(isCreating || editingScript) && (
                    <div className="bg-gray-800 p-6 rounded-lg border border-gray-700">
                        <h2 className="text-xl font-bold mb-4">{isCreating ? 'Create Script' : 'Edit Script'}</h2>
                        <div className="mb-4">
                            <label className="block text-sm font-medium text-gray-400 mb-1">Name</label>
                            <input
                                type="text"
                                value={newScriptName}
                                onChange={e => setNewScriptName(e.target.value)}
                                className="w-full bg-gray-900 border border-gray-700 rounded p-2 text-gray-200 focus:outline-none focus:border-blue-500"
                            />
                        </div>
                        <div className="mb-4">
                            <label className="block text-sm font-medium text-gray-400 mb-1">Code</label>
                            <ScriptEditor code={newScriptCode} onChange={val => setNewScriptCode(val || '')} />
                        </div>
                        <div className="flex justify-end space-x-3">
                            <button
                                onClick={() => { setIsCreating(false); setEditingScript(null); }}
                                className="px-4 py-2 text-gray-400 hover:text-white"
                            >
                                Cancel
                            </button>
                            <button
                                onClick={handleSave}
                                className="bg-blue-600 hover:bg-blue-700 px-4 py-2 rounded text-white font-medium"
                            >
                                Save Script
                            </button>
                        </div>
                        <div className="mt-4 p-3 bg-yellow-900/30 border border-yellow-700/50 rounded text-xs text-yellow-200">
                            <strong>Warning:</strong> Scripts run arbitrary code. Only enable scripts you trust.
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};
