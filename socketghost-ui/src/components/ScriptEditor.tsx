import React from 'react';
import Editor from '@monaco-editor/react';

interface ScriptEditorProps {
    code: string;
    onChange: (value: string | undefined) => void;
}

export const ScriptEditor: React.FC<ScriptEditorProps> = ({ code, onChange }) => {
    return (
        <div className="h-[400px] border border-gray-700 rounded-md overflow-hidden">
            <Editor
                height="400px"
                defaultLanguage="javascript"
                theme="vs-dark"
                value={code}
                onChange={onChange}
                options={{
                    minimap: { enabled: false },
                    fontSize: 14,
                    scrollBeyondLastLine: false,
                }}
            />
        </div>
    );
};
