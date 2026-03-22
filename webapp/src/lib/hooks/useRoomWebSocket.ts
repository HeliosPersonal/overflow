import {useCallback, useEffect, useRef, useState} from "react";
import {PlanningPokerRoom} from "@/lib/types";
import {createClientLogger} from "@/lib/client-logger";

const log = createClientLogger('RoomWebSocket');

type ConnectionStatus = 'connecting' | 'connected' | 'disconnected' | 'error';

export function useRoomWebSocket(roomId: string | null) {
    const [room, setRoom] = useState<PlanningPokerRoom | null>(null);
    const [status, setStatus] = useState<ConnectionStatus>('disconnected');
    const wsRef = useRef<WebSocket | null>(null);

    const connect = useCallback(() => {
        if (!roomId) return;
        
        let wsUrl: string;
        if (typeof window !== 'undefined') {
            const isDev = window.location.port === '3000' || window.location.port === '4000';
            if (isDev) {
                wsUrl = `ws://localhost:8001/estimation/rooms/${roomId}/ws`;
            } else {
                const proto = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
                wsUrl = `${proto}//${window.location.host}/api/estimation/rooms/${roomId}/ws`;
            }
        } else {
            return;
        }
        
        setStatus('connecting');
        
        const ws = new WebSocket(wsUrl);
        wsRef.current = ws;

        ws.onopen = () => {
            setStatus('connected');
        };

        ws.onmessage = (event) => {
            try {
                const data = JSON.parse(event.data) as PlanningPokerRoom;
                // Ignore error messages from the server
                if ((data as unknown as {type?: string})?.type === 'error') {
                    log.warn('Server error', data);
                    return;
                }
                // The WS connection may resolve to a different viewer identity
                // than the HTTP requests (e.g. guest vs authenticated). When the
                // viewer identity doesn't match, preserve the viewer object from
                // the last HTTP-sourced state so isModerator, selectedVote,
                // isSpectator etc. aren't wiped by WS broadcasts.
                // However, on round change clear selectedVote so stale picks
                // from the previous round don't carry over.
                setRoom(prev => {
                    if (prev?.viewer && data.viewer
                        && prev.viewer.participantId !== data.viewer.participantId) {
                        const roundChanged = prev.roundNumber !== data.roundNumber;
                        const statusChanged = prev.status !== data.status;
                        const shouldClearVote = roundChanged || (statusChanged && data.status === 'Voting');
                        return {
                            ...data,
                            viewer: {
                                ...prev.viewer,
                                selectedVote: shouldClearVote ? null : prev.viewer.selectedVote,
                            },
                        };
                    }
                    return data;
                });
            } catch (e) {
                log.error('Failed to parse message', e);
            }
        };

        ws.onclose = () => {
            setStatus('disconnected');
            wsRef.current = null;
            // Do NOT auto-reconnect — the server marks the participant as absent on WS close.
            // Reconnect only happens when the user re-opens the page (re-join flow).
        };

        ws.onerror = () => {
            setStatus('error');
        };
    }, [roomId]);

    useEffect(() => {
        connect();
        return () => {
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
