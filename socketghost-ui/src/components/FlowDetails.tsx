import { FlowEvent } from '../types';

interface Props {
    flow: FlowEvent;
    onClose: () => void;
}

export default function FlowDetails({ flow, onClose }: Props) {
    const prettyJson = JSON.stringify(flow, null, 2);

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-white rounded-lg shadow-xl max-w-3xl w-full max-h-[80vh] overflow-hidden flex flex-col">
                <div className="p-4 border-b border-gray-200 flex items-center justify-between">
                    <h3 className="text-lg font-semibold">Flow Details</h3>
                    <button
                        onClick={onClose}
                        className="text-gray-500 hover:text-gray-700 text-2xl leading-none"
                    >
                        ×
                    </button>
                </div>

                <div className="flex-1 overflow-y-auto p-4">
                    <pre className="text-sm font-mono bg-gray-50 p-4 rounded border border-gray-200 overflow-x-auto">
                        {prettyJson}
                    </pre>
                </div>

                <div className="p-4 border-t border-gray-200 bg-gray-50">
                    <p className="text-xs text-gray-500">
                        TODO: Flow modification UI will attach here
                    </p>
                </div>
            </div>
        </div>
    );
}
