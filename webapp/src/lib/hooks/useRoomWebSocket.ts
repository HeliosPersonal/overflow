import {useCallback, useEffect, useRef, useState} from "react";
import {PlanningPokerRoom} from "@/lib/types";

type ConnectionStatus = 'connecting' | 'connected' | 'disconnected' | 'error';

export function useRoomWebSocket(code: string | null) {
    const [room, setRoom] = useState<PlanningPokerRoom | null>(null);
    const [status, setStatus] = useState<ConnectionStatus>('disconnected');
    const wsRef = useRef<WebSocket | null>(null);
    const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
    const retriesRef = useRef(0);
    const maxRetries = 10;

    const connect = useCallback(() => {
        if (!code) return;
        
        // In production / staging, WebSocket goes through the same-origin ingress path
        // which rewrites /api/estimation/* to /estimation/*
        // In local dev, we connect directly to the YARP gateway on port 8001
        let wsUrl: string;
        if (typeof window !== 'undefined') {
            const isDev = window.location.port === '3000' || window.location.port === '4000';
            if (isDev) {
                // Local dev: connect directly to the Aspire YARP gateway
                wsUrl = `ws://localhost:8001/estimation/rooms/${code}/ws`;
            } else {
                // Production / staging: same origin, ingress rewrites /api/estimation → /estimation
                const proto = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
                wsUrl = `${proto}//${window.location.host}/api/estimation/rooms/${code}/ws`;
            }
        } else {
            return; // SSR — no WebSocket
        }
        
        setStatus('connecting');
        
        const ws = new WebSocket(wsUrl);
        wsRef.current = ws;

        ws.onopen = () => {
            setStatus('connected');
            retriesRef.current = 0;
        };

        ws.onmessage = (event) => {
            try {
                const data = JSON.parse(event.data) as PlanningPokerRoom;
                setRoom(data);
            } catch (e) {
                console.error('[WS] Failed to parse message:', e);
            }
        };

        ws.onclose = () => {
            setStatus('disconnected');
            wsRef.current = null;
            
            // Auto-reconnect with exponential backoff
            if (retriesRef.current < maxRetries) {
                const delay = Math.min(1000 * Math.pow(2, retriesRef.current), 30000);
                retriesRef.current++;
                reconnectTimeoutRef.current = setTimeout(connect, delay);
            }
        };

        ws.onerror = () => {
            setStatus('error');
        };
    }, [code]);

    useEffect(() => {
        connect();
        return () => {
            clearTimeout(reconnectTimeoutRef.current);
            if (wsRef.current) {
                wsRef.current.close();
                wsRef.current = null;
            }
        };
    }, [connect]);

    /** Force update room state from a mutation response */
    const updateRoom = useCallback((updated: PlanningPokerRoom) => {
        setRoom(updated);
    }, []);

    return {room, status, updateRoom};
}

