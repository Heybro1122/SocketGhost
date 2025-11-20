import { useState } from 'react';

interface Props {
    selectedPid: number | null;
    selectedProcessName: string;
    enabled: boolean;
    onEnable: (pid: number) => void;
    onDisable: (pid: number) => void;
}

// System-critical PIDs and process names to block
const BLOCKED_PIDS = [4]; // PID 4 is System process on Windows
const BLOCKED_PROCESS_NAMES = ['System', 'services', 'svchost', 'lsass', 'csrss', 'wininit', 'winlogon'];

export default function InterceptorToggle({
    selectedPid,
    selectedProcessName,
    enabled,
    onEnable,
    onDisable,
}: Props) {
    const [showConfirmModal, setShowConfirmModal] = useState(false);

    const isSafePid = (pid: number, processName: string) => {
        // Check PID blocklist
        if (BLOCKED_PIDS.includes(pid)) {
            return false;
        }

        // Check process name blocklist
        const nameLower = processName.toLowerCase();
        if (BLOCKED_PROCESS_NAMES.some((blocked) => nameLower.includes(blocked.toLowerCase()))) {
            return false;
        }

        return true;
    };

    const handleToggle = () => {
        if (!selectedPid) return;

        if (!enabled) {
            // Enabling interceptor
            if (!isSafePid(selectedPid, selectedProcessName)) {
                alert(`⚠️ Cannot intercept system-critical process: ${selectedProcessName} (PID ${selectedPid})\n\nIntercepting system processes may cause system instability.`);
                return;
            }
            setShowConfirmModal(true);
        } else {
            // Disabling interceptor
            onDisable(selectedPid);
        }
    };

    const handleConfirm = () => {
        if (selectedPid) {
            onEnable(selectedPid);
        }
        setShowConfirmModal(false);
    };

    return (
        <>
            <button
                onClick={handleToggle}
                disabled={!selectedPid}
                className={`px-4 py-2 rounded font-semibold transition-colors ${enabled
                        ? 'bg-red-600 hover:bg-red-700 text-white'
                        : selectedPid
                            ? 'bg-blue-600 hover:bg-blue-700 text-white'
                            : 'bg-gray-300 text-gray-500 cursor-not-allowed'
                    }`}
            >
                {enabled ? '⏸ Disable Interceptor' : '▶ Enable Interceptor'}
            </button>

            {/* Confirmation Modal */}
            {showConfirmModal && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
                    <div className="bg-white rounded-lg shadow-xl max-w-md w-full p-6">
                        <h3 className="text-lg font-bold text-gray-800 mb-4">⚠️ Enable Interceptor?</h3>
                        <div className="mb-6">
                            <p className="text-sm text-gray-700 mb-3">
                                Enabling interception will <strong>pause requests</strong> from:
                            </p>
                            <div className="bg-blue-50 border border-blue-200 rounded p-3 mb-3">
                                <p className="text-sm font-semibold text-blue-800">
                                    {selectedProcessName} (PID: {selectedPid})
                                </p>
                            </div>
                            <p className="text-sm text-red-600 font-semibold mb-2">
                                ⚠️ The application may block until flows are forwarded or dropped.
                            </p>
                            <p className="text-xs text-gray-600">
                                Paused flows will auto-forward after 60 seconds to prevent deadlocks.
                            </p>
                        </div>
                        <div className="flex gap-3 justify-end">
                            <button
                                onClick={() => setShowConfirmModal(false)}
                                className="px-4 py-2 bg-gray-200 hover:bg-gray-300 text-gray-800 rounded font-semibold"
                            >
                                Cancel
                            </button>
                            <button
                                onClick={handleConfirm}
                                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded font-semibold"
                            >
                                Enable Interceptor
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
}
