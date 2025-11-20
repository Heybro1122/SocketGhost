# Ghost Scripting Security

SocketGhost allows you to run JavaScript scripts to automatically modify network traffic. This feature is powerful but comes with security considerations.

## Sandbox Limitations

Scripts are executed in a **Jint** sandbox with the following constraints:

1.  **No CLR Access**: Scripts cannot access .NET types or the host system. They cannot read files, open sockets, or launch processes.
2.  **Timeout**: Scripts have a strict execution timeout (default 50ms) to prevent infinite loops or hanging the proxy.
3.  **Memory Limit**: Scripts have a soft memory limit (~4MB) to prevent denial-of-service attacks via memory exhaustion.
4.  **API Surface**: Scripts only have access to the `flow` object and specific helper functions (`setRequestBody`, `setResponseBody`, etc.).

## Risks

Despite the sandbox, enabling untrusted scripts can be dangerous:

*   **Traffic Modification**: Scripts can modify *any* traffic passing through the proxy, including sensitive data (passwords, tokens) if SSL interception is enabled.
*   **Logic Errors**: A buggy script could corrupt data or break application flows.

## Best Practices

1.  **Only enable scripts you trust.** Do not copy-paste scripts from unknown sources.
2.  **Review code carefully.** Ensure the script only targets the specific URLs or hosts you intend to modify.
3.  **Disable unused scripts.** Keep your active script list clean to avoid unexpected side effects.
