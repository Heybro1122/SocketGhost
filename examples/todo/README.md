# SocketGhost To-Do Example

This example demonstrates a simple client-server interaction intercepted by SocketGhost.

## Prerequisites
- Node.js

## How to Run

1.  **Install Dependencies:**
    ```bash
    npm install express body-parser
    ```

2.  **Start the Server:**
    ```bash
    node server.js
    ```

3.  **Run the Client (via Proxy):**
    Ensure `socketghost-core` is running on port 8080.
    ```bash
    node client.js
    ```

## Expected Output

**Server Console:**
```
Received request: { task: 'buy milk' }
```

**Client Console:**
```
STATUS: 200
BODY: {"status":"success","message":"Task added","task":"buy milk"}
```

**SocketGhost Core Console:**
```json
{"v":"0.1","type":"flow.new","flow":{"flowId":"...","pid":1234,...}}
```

**Verify PID Mapping:**
1.  Run `curl http://127.0.0.1:9100/processes` to see the list of processes.
2.  Find the PID of your `node` process.
3.  Check that the `pid` in the SocketGhost console log matches the Node PID.
