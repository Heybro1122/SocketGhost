import { useState } from 'react';
import ProcessPicker from './components/ProcessPicker';
import FlowsList from './components/FlowsList';
import InterceptorToggle from './components/InterceptorToggle';
import PausedFlowsList from './components/PausedFlowsList';
import { ScriptsPage } from './pages/ScriptsPage';
import { SavedFlowsPage } from './pages/SavedFlowsPage';
import { useProcesses } from './hooks/useProcesses';
import { useWebSocket } from './hooks/useWebSocket';
import { useInterceptor } from './hooks/useInterceptor';
import { usePausedFlows } from './hooks/usePausedFlows';

function App() {
    const [demoMode, setDemoMode] = useState(false);
    const [currentView, setCurrentView] = useState<'dashboard' | 'scripts' | 'savedFlows'>('dashboard');

    const {
        processes,
        selectedPid,
        selectProcess,
        loading,
        error: processError,
        retry: retryProcesses,
    } = useProcesses(demoMode);

    const {
        flows,
        isConnected,
        error: wsError,
        retry: retryWebSocket,
        ws,
    } = useWebSocket(demoMode);

    // Interceptor hooks
    const {
        interceptorEnabled,
        enableInterceptor,
        disableInterceptor,
    } = useInterceptor(ws, selectedPid);

    const {
        pausedFlows,
        forwardFlow,
        dropFlow,
    } = usePausedFlows(ws);

    const selectedProcess = processes.find((p) => p.pid === selectedPid);

    return (
        <div className="flex flex-col h-screen bg-gray-100">
            {/* Header */}
            <header className="bg-white border-b border-gray-200 p-4 shadow-sm">
                <div className="flex items-center justify-between">
                    <div className="flex items-center space-x-4">
                        <h1 className="text-xl font-bold text-gray-800">SocketGhost</h1>
                        <nav className="flex space-x-2">
                            <button
                                onClick={() => setCurrentView('dashboard')}
                                className={`px-3 py-1 rounded-md text-sm font-medium ${currentView === 'dashboard' ? 'bg-gray-200 text-gray-900' : 'text-gray-600 hover:bg-gray-100'}`}
                            >
                                Dashboard
                            </button>
                            <button
                                onClick={() => setCurrentView('scripts')}
                                className={`px-3 py-1 rounded-md text-sm font-medium ${currentView === 'scripts' ? 'bg-gray-200 text-gray-900' : 'text-gray-600 hover:bg-gray-100'}`}
                            >
                                Scripts
                            </button>
                            <button
                                onClick={() => setCurrentView('savedFlows')}
                                className={`px-3 py-1 rounded-md text-sm font-medium ${currentView === 'savedFlows' ? 'bg-gray-200 text-gray-900' : 'text-gray-600 hover:bg-gray-100'}`}
                            >
                                Saved Flows
                            </button>
                        </nav>
                    </div>
                    <div className="flex items-center space-x-4">
                        <div className="flex items-center space-x-2">
                            <input
                                type="checkbox"
                                id="demoMode"
                                checked={demoMode}
                                onChange={(e) => setDemoMode(e.target.checked)}
                                className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                            />
                            <label htmlFor="demoMode" className="text-sm text-gray-600">
                                Demo Mode
                            </label>
                        </div>
                        <div className="flex items-center space-x-2">
                            <div
                                className={`w-3 h-3 rounded-full ${isConnected ? 'bg-green-500' : 'bg-red-500'
                                    }`}
                            />
                            <span className="text-sm text-gray-600">
                                {isConnected ? 'Connected' : 'Disconnected'}
                            </span>
                        </div>
                    </div>
                </div>
            </header>

            {/* Main Content */}
            <main className="flex-1 overflow-hidden">
                {currentView === 'scripts' ? (
                    <div className="h-full overflow-auto bg-gray-900">
                        <ScriptsPage />
                    </div>
                ) : currentView === 'savedFlows' ? (
                    <div className="h-full overflow-auto">
                        <SavedFlowsPage />
                    </div>
                ) : (
                    <div className="flex h-full">
                        {/* Sidebar */}
                        <div className="w-80 bg-white border-r border-gray-200 flex flex-col">
                            <ProcessPicker
                                processes={processes}
                                selectedPid={selectedPid}
                                onSelectProcess={selectProcess}
                                loading={loading}
                                error={processError}
                                onRetry={retryProcesses}
                            />
                        </div>

                        {/* Content Area */}
                        <div className="flex-1 flex flex-col bg-gray-50">
                            {selectedPid ? (
                                <>
                                    <div className="bg-white border-b border-gray-200 p-4">
                                        <div className="flex justify-between items-center">
                                            <div>
                                                <h2 className="text-lg font-semibold text-gray-800">
                                                    {selectedProcess?.exe} (PID: {selectedPid})
                                                </h2>
                                                <p className="text-sm text-gray-500">
                                                    {selectedProcess?.cmdline}
                                                </p>
                                            </div>
                                            <InterceptorToggle
                                                selectedPid={selectedPid}
                                                selectedProcessName={selectedProcess?.exe ?? ''}
                                                enabled={interceptorEnabled}
                                                onEnable={enableInterceptor}
                                                onDisable={disableInterceptor}
                                            />
                                        </div>
                                    </div>

                                    <div className="flex-1 overflow-hidden flex flex-col">
                                        <PausedFlowsList
                                            pausedFlows={pausedFlows}
                                            ws={ws}
                                            onForward={forwardFlow}
                                            onDrop={dropFlow}
                                        />
                                        <div className="flex-1 overflow-hidden">
                                            <FlowsList
                                                flows={flows}
                                                selectedPid={selectedPid}
                                                isConnected={isConnected}
                                                error={wsError}
                                                onRetry={retryWebSocket}
                                            />
                                        </div>
                                    </div>
                                </>
                            ) : (
                                <div className="flex-1 flex items-center justify-center text-gray-500">
                                    Select a process to start monitoring
                                </div>
                            )}
                        </div>
                    </div>
                )}
            </main>
        </div>
    );
}

export default App;
