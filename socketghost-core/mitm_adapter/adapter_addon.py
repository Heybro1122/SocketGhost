"""
SocketGhost mitmproxy Adapter Addon

This addon integrates mitmproxy with SocketGhost Core by translating
mitmproxy flow events into SocketGhost's WebSocket message format.

Usage:
    mitmdump -s adapter_addon.py --listen-host 127.0.0.1 --listen-port 8081

Environment Variables:
    SOCKETGHOST_INGEST_URL: Core ingestion endpoint (default: http://127.0.0.1:9001/ingest)
"""

import os
import json
import base64
from typing import Optional
from mitmproxy import http, ctx
import requests


class SocketGhostAdapter:
    def __init__(self):
        self.ingest_url = os.getenv("SOCKETGHOST_INGEST_URL", "http://127.0.0.1:9001/ingest")
        self.max_body_preview = 128 * 1024  # 128KB inline preview limit
        
        ctx.log.info(f"SocketGhost Adapter initialized. Ingesting to: {self.ingest_url}")
    
    def request(self, flow: http.HTTPFlow):
        """Called when request is complete"""
        # Store flowId in flow for correlation with response
        flow.metadata["socketghost_id"] = flow.id
        
        ctx.log.info(f"[Adapter] Request: {flow.request.method} {flow.request.pretty_url}")
    
    def response(self, flow: http.HTTPFlow):
        """Called when response is complete - send flow to SocketGhost core"""
        try:
            # Extract request data
            request_headers = dict(flow.request.headers)
            request_body = flow.request.content if flow.request.content else b""
            request_body_preview = self._get_body_preview(request_body)
            
            # Extract response data
            response_headers = dict(flow.response.headers)
            response_body = flow.response.content if flow.response.content else b""
            response_body_preview = self._get_body_preview(response_body)
            
            # Build SocketGhost flow.new message
            message = {
                "v": "0.1",
                "type": "flow.new",
                "flow": {
                    "flowId": flow.id,
                    "pid": None,  # mitmproxy cannot get client PID
                    "method": flow.request.method,
                    "url": flow.request.pretty_url,
                    "headers": request_headers,
                    "bodyPreview": request_body_preview,
                    "responseStatusCode": flow.response.status_code,
                    "responseHeaders": response_headers,
                    "responseBody": response_body_preview,
                    "scriptApplied": [],
                    "_adapter": {
                        "source": "mitmproxy",
                        "clientAddress": str(flow.client_conn.peername) if flow.client_conn.peername else None,
                     "serverAddress": str(flow.server_conn.address) if flow.server_conn and flow.server_conn.address else None
                    }
                }
            }
            
            # Send to SocketGhost core
            self._send_to_core(message)
            
            ctx.log.info(f"[Adapter] Response sent: {flow.request.method} {flow.request.pretty_url} -> {flow.response.status_code}")
            
        except Exception as e:
            ctx.log.error(f"[Adapter] Error processing flow: {e}")
    
    def _get_body_preview(self, body: bytes) -> str:
        """Get body preview, truncate if too large"""
        if not body:
            return ""
        
        # Try to decode as UTF-8 text
        try:
            text = body.decode('utf-8', errors='ignore')
            if len(text) > self.max_body_preview:
                return text[:self.max_body_preview] + f"\n... (truncated, {len(body)} bytes total)"
            return text
        except:
            # Binary content - return base64 preview
            b64 = base64.b64encode(body[:self.max_body_preview]).decode('ascii')
            if len(body) > self.max_body_preview:
                return f"base64:{b64}... (truncated, {len(body)} bytes total)"
            return f"base64:{b64}"
    
    def _send_to_core(self, message: dict):
        """Send message to SocketGhost core ingestion endpoint"""
        try:
            response = requests.post(
                self.ingest_url,
                json=message,
                headers={"Content-Type": "application/json"},
                timeout=5
            )
            response.raise_for_status()
        except requests.exceptions.RequestException as e:
            ctx.log.warn(f"[Adapter] Failed to send to core: {e}")
            # Don't crash - core might not be ready yet


# mitmproxy addon entrypoint
addons = [SocketGhostAdapter()]
