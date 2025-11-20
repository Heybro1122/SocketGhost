export interface Process {
    pid: number;
    exe: string;
    cmdline: string;
}

export interface FlowData {
    flowId: string;
    pid: number | null;
    method: string;
    url: string;
    headers: Record<string, string>;
    bodyPreview: string;
}

export interface FlowEvent {
    v: string;
    type: string;
    flow: FlowData;
}

export type FilterMode = 'all' | 'targeted' | 'unmapped';
