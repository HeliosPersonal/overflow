'use client';

import {useCallback, useEffect, useRef, useState} from "react";
import {useRouter} from "next/navigation";
import {Spinner, addToast} from "@heroui/react";
import {useRoomWebSocket} from "@/lib/hooks/useRoomWebSocket";
import {createGuestAndSignIn} from "@/lib/auth/create-guest";
import {setActiveRoom} from "@/lib/hooks/useActiveRoom";

import {
    GuestNameGate, RoomHeader, CardPicker,
    CompactEstimationStrip, SidebarPanel,
    PokerTableScene, SpectatorCard, NoticeBar,
    FULL_PAGE_CENTER, useSceneSize,
    parseErrorMessage, generateNextTaskName, generateRandomAvatarJson,
} from "./_components";

export default function RoomClient({roomId, isAuthenticated}: {
    roomId: string;
    isAuthenticated: boolean;
}) {
    const router = useRouter();

    const [needsGuestName, setNeedsGuestName] = useState(false);
    const [guestName, setGuestName] = useState('');
    const [avatarJson, setAvatarJson] = useState<string | null>(generateRandomAvatarJson);
    const [joinLoading, setJoinLoading] = useState(false);
    const [initialLoading, setInitialLoading] = useState(true);
    const [joinedOnce, setJoinedOnce] = useState(false);
    const [actionLoading, setActionLoading] = useState<string | null>(null);
    const [optimisticVote, setOptimisticVote] = useState<string | null | undefined>(undefined);
    const hasLeftRef = useRef(false);
    const [sidebarOpen, setSidebarOpen] = useState(false);
    const [editingTitle, setEditingTitle] = useState(false);
    const [titleDraft, setTitleDraft] = useState('');

    const sceneContainerRef = useRef<HTMLDivElement>(null);
    const scene = useSceneSize(sceneContainerRef);

    const {room, status: wsStatus, updateRoom} = useRoomWebSocket(joinedOnce ? roomId : null);

    // Track active room globally so sign-out can leave before session dies
    useEffect(() => {
        if (joinedOnce) {
            setActiveRoom(roomId);
            return () => setActiveRoom(null);
        }
    }, [joinedOnce, roomId]);

    // When the user tabs back into the room, re-fetch room state so any avatar/
    // display-name changes made in another tab are reflected immediately — even if
    // the WebSocket broadcast was delivered while the tab was hidden or the cache
    // eviction on the backend was delayed.
    useEffect(() => {
        if (!joinedOnce) return;

        async function refreshRoom() {
            try {
                const res = await fetch(`/api/estimation/rooms/${roomId}`);
                if (res.ok) {
                    const data = await res.json();
                    updateRoom(data);
                }
            } catch {
                // Best-effort — WebSocket state is still authoritative
            }
        }

        function handleVisibilityChange() {
            if (document.visibilityState === 'visible') {
                void refreshRoom();
            }
        }

        document.addEventListener('visibilitychange', handleVisibilityChange);
        return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
    }, [joinedOnce, roomId, updateRoom]);

    // Leave on tab close / external navigation
    useEffect(() => {
        if (!joinedOnce) return;
        const leaveUrl = `/api/estimation/rooms/${roomId}/leave`;

        function handlePageLeave() {
            if (hasLeftRef.current) return;
            hasLeftRef.current = true;
            navigator.sendBeacon(leaveUrl);
        }

        window.addEventListener('pagehide', handlePageLeave);
        window.addEventListener('beforeunload', handlePageLeave);
        return () => {
            window.removeEventListener('pagehide', handlePageLeave);
            window.removeEventListener('beforeunload', handlePageLeave);
            if (!hasLeftRef.current) {
                hasLeftRef.current = true;
                navigator.sendBeacon(leaveUrl);
            }
        };
    }, [joinedOnce, roomId]);

    // Bootstrap: try to join room on mount
    useEffect(() => {
        if (!roomId) return;

        async function bootstrap() {
            try {
                if (isAuthenticated) {
                    await fetch('/api/estimation/claim-guest', {method: 'POST'}).catch(() => {});
                }

                const res = await fetch(`/api/estimation/rooms/${roomId}/join`, {
                    method: 'POST',
                    headers: {'Content-Type': 'application/json'},
                    body: JSON.stringify({}),
                });

                if (res.ok) {
                    const data = await res.json();
                    updateRoom(data);
                    setJoinedOnce(true);
                } else if (res.status === 400) {
                    const errBody = await res.json().catch(() => null);
                    const errMsg = parseErrorMessage(errBody, '');
                    const isArchived = typeof errMsg === 'string' && errMsg.toLowerCase().includes('archived');

                    if (isArchived) {
                        const getRes = await fetch(`/api/estimation/rooms/${roomId}`);
                        if (getRes.ok) {
                            updateRoom(await getRes.json());
                            setJoinedOnce(true);
                        } else {
                            addToast({title: 'Room is archived', color: 'warning'});
                            router.push('/planning-poker');
                        }
                    } else if (!isAuthenticated) {
                        setNeedsGuestName(true);
                    } else {
                        addToast({title: errMsg || 'Failed to join room', color: 'danger'});
                    }
                } else if (res.status === 404) {
                    addToast({title: 'Room not found', color: 'danger'});
                    router.push('/planning-poker');
                } else {
                    addToast({title: 'Failed to join room', color: 'danger'});
                }
            } catch {
                addToast({title: 'Network error', color: 'danger'});
            } finally {
                setInitialLoading(false);
            }
        }

        void bootstrap();
    }, [roomId, isAuthenticated, router, updateRoom]);

    async function handleGuestJoin() {
        if (!guestName.trim()) return;
        setJoinLoading(true);
        try {
            const result = await createGuestAndSignIn(guestName, avatarJson ?? undefined);
            if (!result.ok) {
                addToast({title: result.error, color: 'danger'});
                setJoinLoading(false);
                return;
            }

            const joinRes = await fetch(`/api/estimation/rooms/${roomId}/join`, {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({displayName: guestName.trim()}),
            });

            if (joinRes.ok) {
                updateRoom(await joinRes.json());
                setJoinedOnce(true);
                setNeedsGuestName(false);
                router.refresh();
            } else {
                const errBody = await joinRes.json().catch(() => null);
                addToast({title: parseErrorMessage(errBody, 'Failed to join room'), color: 'danger'});
            }
        } catch {
            addToast({title: 'Network error', color: 'danger'});
        } finally {
            setJoinLoading(false);
        }
    }

    // ── Mutation helpers ──────────────────────────────────────────────────

    const performAction = useCallback(async (
        actionName: string, url: string, method: string = 'POST', body?: unknown,
    ) => {
        setActionLoading(actionName);
        try {
            const res = await fetch(url, {
                method,
                headers: {'Content-Type': 'application/json'},
                ...(body !== undefined ? {body: JSON.stringify(body)} : {}),
            });
            if (res.ok) {
                const ct = res.headers.get('content-type');
                if (ct?.includes('application/json')) {
                    updateRoom(await res.json());
                }
            } else {
                const err = await res.json().catch(() => null);
                addToast({title: parseErrorMessage(err, `${actionName} failed`), color: 'danger'});
            }
        } catch {
            addToast({title: `${actionName} failed`, color: 'danger'});
        } finally {
            setActionLoading(null);
        }
    }, [updateRoom]);

    useEffect(() => {
        if (room) setOptimisticVote(undefined);
    }, [room?.viewer?.selectedVote, room?.roundNumber]);

    function updateViewerVoteOptimistically(hasVoted: boolean) {
        if (!room) return;
        updateRoom({
            ...room,
            participants: room.participants.map(p =>
                p.participantId === room.viewer.participantId ? {...p, hasVoted} : p
            ),
        });
    }

    async function handleVote(value: string) {
        setOptimisticVote(value);
        updateViewerVoteOptimistically(true);
        await performAction('Vote', `/api/estimation/rooms/${roomId}/votes`, 'POST', {value});
    }

    async function handleClearVote() {
        setOptimisticVote(null);
        updateViewerVoteOptimistically(false);
        await performAction('Clear vote', `/api/estimation/rooms/${roomId}/votes`, 'DELETE');
    }

    async function handleReveal() {
        await performAction('Reveal', `/api/estimation/rooms/${roomId}/reveal`);
    }

    const hasTasks = room?.tasks && room.tasks.length > 0;

    async function handleReset() {
        if (hasTasks && room && room.roundNumber >= room.tasks!.length) {
            const newTaskName = generateNextTaskName(room.tasks!);
            await saveTasksToServer([...room.tasks!, newTaskName]);
        }
        await performAction('Reset', `/api/estimation/rooms/${roomId}/reset`);
    }

    async function handleRevote(roundNumber?: number) {
        setActionLoading('Revote');
        try {
            const res = await fetch(`/api/estimation/rooms/${roomId}/revote`, {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({roundNumber: roundNumber ?? null}),
            });
            if (res.ok) {
                updateRoom(await res.json());
                setOptimisticVote(undefined);
            } else {
                const err = await res.json().catch(() => null);
                addToast({title: parseErrorMessage(err, 'Revote failed'), color: 'danger'});
            }
        } catch {
            addToast({title: 'Revote failed', color: 'danger'});
        } finally {
            setActionLoading(null);
        }
    }

    // ── Task management ──────────────────────────────────────────────────

    async function saveTasksToServer(tasks: string[]) {
        setActionLoading('Tasks');
        try {
            const res = await fetch(`/api/estimation/rooms/${roomId}/tasks`, {
                method: 'PUT',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({tasks}),
            });
            if (res.ok) {
                updateRoom(await res.json());
            } else {
                const err = await res.json().catch(() => null);
                addToast({title: parseErrorMessage(err, 'Failed to update tasks'), color: 'danger'});
            }
        } catch {
            addToast({title: 'Failed to update tasks', color: 'danger'});
        } finally {
            setActionLoading(null);
        }
    }

    async function handleAddTask() {
        if (!room) return;
        const currentTasks = room.tasks ?? [];
        await saveTasksToServer([...currentTasks, generateNextTaskName(currentTasks)]);
    }

    async function handleDeleteTask(index: number) {
        if (!room?.tasks) return;
        if (index + 1 === room.roundNumber) return;
        await saveTasksToServer(room.tasks.filter((_, i) => i !== index));
    }

    async function handleEditTask(index: number, newName: string) {
        if (!room?.tasks) return;
        const trimmed = newName.trim();
        if (!trimmed || trimmed === room.tasks[index]) return;
        await saveTasksToServer(room.tasks.map((t, i) => i === index ? trimmed : t));
    }

    async function handleEnableTasks() {
        if (!room) return;
        const tasks: string[] = [];
        for (let i = 0; i < room.roundNumber; i++) {
            const h = room.roundHistory.find(h => h.roundNumber === i + 1);
            tasks.push(h?.taskName ?? `Task ${i + 1}`);
        }
        await saveTasksToServer(tasks);
    }

    async function handleDisableTasks() {
        if (room) await saveTasksToServer([]);
    }

    async function handleArchive() {
        await performAction('Archive', `/api/estimation/rooms/${roomId}/archive`);
        hasLeftRef.current = true;
        router.push('/planning-poker');
    }

    async function handleModeToggle() {
        if (!room) return;
        const newMode = !room.viewer.isSpectator;
        if (newMode) {
            updateRoom({
                ...room,
                viewer: {...room.viewer, isSpectator: true, selectedVote: null},
            });
        }
        await performAction('Mode', `/api/estimation/rooms/${roomId}/mode`, 'POST', {isSpectator: newMode});
    }

    async function handleLeave() {
        if (!hasLeftRef.current) {
            hasLeftRef.current = true;
            fetch(`/api/estimation/rooms/${roomId}/leave`, {method: 'POST'}).catch(() => {});
        }
        router.push('/planning-poker');
    }

    const roomLink = typeof window !== 'undefined'
        ? `${window.location.origin}/planning-poker/${roomId}` : room?.canonicalUrl ?? '';

    function copyToClipboard(text: string, label: string) {
        navigator.clipboard.writeText(text).then(() => addToast({title: `${label} copied!`, color: 'success'}));
    }

    function startEditingTitle() {
        if (!room) return;
        setTitleDraft(room.title);
        setEditingTitle(true);
    }

    async function handleRename() {
        const trimmed = titleDraft.trim();
        if (!trimmed || trimmed === room?.title) {
            setEditingTitle(false);
            return;
        }
        await performAction('Rename', `/api/estimation/rooms/${roomId}/title`, 'PUT', {title: trimmed});
        setEditingTitle(false);
    }

    // ── Loading / gate states ────────────────────────────────────────────

    if (initialLoading) {
        return (
            <div className={`${FULL_PAGE_CENTER} py-20`}>
                <Spinner size="lg" label="Loading room..."/>
            </div>
        );
    }

    if (needsGuestName) {
        return (
            <GuestNameGate
                roomId={roomId} guestName={guestName} onGuestNameChange={setGuestName}
                avatarJson={avatarJson} onAvatarChange={setAvatarJson} joinLoading={joinLoading}
                onJoin={handleGuestJoin}
                onNavigateToLogin={() =>
                    router.push(`/login?callbackUrl=${encodeURIComponent(`/planning-poker/${roomId}`)}`)
                }
            />
        );
    }

    if (!room) {
        return (
            <div className={`${FULL_PAGE_CENTER} py-20`}>
                <Spinner size="lg" label="Connecting..."/>
            </div>
        );
    }

    // ── Room UI ──────────────────────────────────────────────────────────

    const isModerator = room.viewer.isModerator;
    const isSpectator = room.viewer.isSpectator;
    const isVoting = room.status === 'Voting';
    const isRevealed = room.status === 'Revealed';
    const isArchived = room.status === 'Archived';

    const viewerInList = room.participants.find(p => p.participantId === room.viewer.participantId);
    const serverSaysNoVote = viewerInList && !viewerInList.hasVoted && !viewerInList.isSpectator && isVoting;
    const effectiveVote = serverSaysNoVote
        ? (optimisticVote !== undefined ? optimisticVote : null)
        : (optimisticVote !== undefined ? optimisticVote : room.viewer.selectedVote);

    const activeParticipants = room.participants.filter(p => !p.isSpectator && p.isPresent);
    const spectators = room.participants.filter(p => p.isSpectator && p.isPresent);
    const votedCount = activeParticipants.filter(p => p.hasVoted).length;
    const hasAnyVotes = votedCount > 0;
    const allVoted = votedCount === activeParticipants.length && activeParticipants.length > 0;
    const showCardPicker = !isArchived && !isSpectator && (isVoting || isRevealed);

    return (
        <div className="h-full bg-content1 flex flex-col overflow-hidden">
            <RoomHeader
                room={room} editingTitle={editingTitle} titleDraft={titleDraft}
                onTitleDraftChange={setTitleDraft} onStartEditingTitle={startEditingTitle}
                onRename={handleRename} onCancelEditTitle={() => setEditingTitle(false)}
                isModerator={isModerator} isSpectator={isSpectator} isArchived={isArchived}
                wsStatus={wsStatus} actionLoading={actionLoading} sidebarOpen={sidebarOpen}
                onCopyLink={() => copyToClipboard(roomLink, 'Room link')}
                onModeToggle={handleModeToggle} onArchive={handleArchive} onLeave={handleLeave}
                onToggleSidebar={() => setSidebarOpen(o => !o)}
            />

            <div className="flex-1 flex overflow-hidden min-h-0">
                <div className="flex-1 flex flex-col transition-all duration-300 relative min-w-0">
                    <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
                        <div className="shrink-0 px-2 sm:px-4 pt-1 flex justify-center">
                            <div className="w-full max-w-[860px]">
                                <CompactEstimationStrip
                                    room={room} hasTasks={!!hasTasks} isVoting={isVoting} isRevealed={isRevealed}
                                    isArchived={isArchived} activeParticipants={activeParticipants}
                                    votedCount={votedCount} allVoted={allVoted}
                                />
                            </div>
                        </div>

                        <div ref={sceneContainerRef} className="flex-1 min-h-0 relative overflow-hidden">
                            {spectators.length > 0 && <SpectatorCard spectators={spectators} scene={scene}/>}
                            <PokerTableScene
                                participants={activeParticipants} viewerParticipantId={room.viewer.participantId}
                                isVoting={isVoting} isRevealed={isRevealed || isArchived}
                                isModerator={isModerator} isArchived={isArchived}
                                hasAnyVotes={hasAnyVotes} allVoted={allVoted} actionLoading={actionLoading}
                                onReveal={handleReveal} onRevote={() => handleRevote()} onReset={handleReset}
                                hasTasks={!!hasTasks} room={room} scene={scene}
                            />
                            <NoticeBar isSpectator={isSpectator} isArchived={isArchived}/>
                        </div>
                    </div>

                    <CardPicker
                        visible={showCardPicker} deckValues={room?.deck.values ?? []}
                        effectiveVote={effectiveVote} isVoting={isVoting}
                        onVote={handleVote} onClearVote={handleClearVote}
                    />
                </div>

                <div className={`hidden sm:block border-l border-content3 bg-content2/50 backdrop-blur-sm transition-all duration-300 overflow-y-auto
                    ${sidebarOpen ? 'w-72 min-w-[280px]' : 'w-0 min-w-0 border-l-0'}`}>
                    {sidebarOpen && (
                        <div className="p-3">
                            <SidebarPanel
                                room={room} isModerator={isModerator} isArchived={isArchived}
                                hasTasks={!!hasTasks} actionLoading={actionLoading}
                                onAddTask={handleAddTask} onDeleteTask={handleDeleteTask}
                                onEditTask={handleEditTask} onEnableTasks={handleEnableTasks}
                                onDisableTasks={handleDisableTasks} onRevoteTask={(rn) => handleRevote(rn)}
                            />
                        </div>
                    )}
                </div>
            </div>

            {sidebarOpen && (
                <div className="sm:hidden fixed inset-0 z-50 flex">
                    <div className="flex-1 bg-black/30" onClick={() => setSidebarOpen(false)}/>
                    <div className="w-72 max-w-[85vw] bg-content2 border-l border-content3 overflow-y-auto">
                        <div className="p-3 pb-6">
                            <SidebarPanel
                                room={room} isModerator={isModerator} isArchived={isArchived}
                                hasTasks={!!hasTasks} actionLoading={actionLoading}
                                onAddTask={handleAddTask} onDeleteTask={handleDeleteTask}
                                onEditTask={handleEditTask} onEnableTasks={handleEnableTasks}
                                onDisableTasks={handleDisableTasks} onRevoteTask={(rn) => handleRevote(rn)}
                            />
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
