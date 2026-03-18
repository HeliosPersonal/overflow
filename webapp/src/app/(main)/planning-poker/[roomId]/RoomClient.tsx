'use client';

import {useCallback, useEffect, useRef, useState} from "react";
import {useRouter} from "next/navigation";
import {
    Button, Input, Chip, Divider, Spinner, Tooltip, Switch, addToast,
} from "@heroui/react";
import {
    ClipboardCopy, LogOut, Eye, EyeOff,
    Archive, RefreshCw, List, Plus, Pencil,
    Trash2, ChevronDown, ChevronUp, Menu,
} from "lucide-react";
import {CheckCircle, Flag} from "lucide-react";
import {useRoomWebSocket} from "@/lib/hooks/useRoomWebSocket";
import {createGuestAndSignIn} from "@/lib/auth/create-guest";
import {setActiveRoom} from "@/lib/hooks/useActiveRoom";
import AvatarPicker from "@/components/AvatarPicker";
import DiceBearAvatar from "@/components/DiceBearAvatar";
import GoogleSignInButton from "@/components/auth/GoogleSignInButton";

import {AVATAR_EYES, AVATAR_MOUTH} from "@/lib/avatar";
import confetti from 'canvas-confetti';
import type {
    PlanningPokerRoom,
    PlanningPokerParticipant,
    PlanningPokerRoundHistory,
    PlanningPokerRoundSummary
} from "@/lib/types";

export default function RoomClient({roomId, isAuthenticated}: {
    roomId: string;
    isAuthenticated: boolean;
}) {
    const router = useRouter();

    // Gate: guest name entry before join
    const [needsGuestName, setNeedsGuestName] = useState(false);
    const [guestName, setGuestName] = useState('');
    const [avatarJson, setAvatarJson] = useState<string | null>(() => {
        const eyes = AVATAR_EYES[Math.floor(Math.random() * AVATAR_EYES.length)];
        const mouth = AVATAR_MOUTH[Math.floor(Math.random() * AVATAR_MOUTH.length)];
        return JSON.stringify({eyes: [eyes], mouth: [mouth]});
    });
    const [joinLoading, setJoinLoading] = useState(false);
    const [initialLoading, setInitialLoading] = useState(true);
    const [joinedOnce, setJoinedOnce] = useState(false);
    const [actionLoading, setActionLoading] = useState<string | null>(null);
    const [optimisticVote, setOptimisticVote] = useState<string | null | undefined>(undefined);
    const hasLeftRef = useRef(false);
    const [sidebarOpen, setSidebarOpen] = useState(false);
    const [editingTitle, setEditingTitle] = useState(false);
    const [titleDraft, setTitleDraft] = useState('');

    const {room, status: wsStatus, updateRoom} = useRoomWebSocket(joinedOnce ? roomId : null);

    // ── Track active room globally so sign-out can leave before session dies
    useEffect(() => {
        if (joinedOnce) {
            setActiveRoom(roomId);
            return () => setActiveRoom(null);
        }
    }, [joinedOnce, roomId]);

    // ── Leave on tab close / external navigation ─────────────────────────
    // Uses sendBeacon so the request survives page teardown.
    // pagehide is more reliable than beforeunload on mobile Safari.
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
                // If the user just logged in with a prior guest cookie, claim guest data
                if (isAuthenticated) {
                    await fetch('/api/estimation/claim-guest', {method: 'POST'}).catch(() => {
                    });
                }
                // Attempt join (idempotent for returning participants)
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
                    // Parse the error to distinguish "needs name" from "archived"
                    const errBody = await res.json().catch(() => null);
                    const errMsg = typeof errBody === 'string' ? errBody
                        : errBody?.message ?? errBody?.error ?? '';
                    const isArchived = typeof errMsg === 'string'
                        && errMsg.toLowerCase().includes('archived');
                    if (isArchived) {
                        // Archived room — load read-only view
                        const getRes = await fetch(`/api/estimation/rooms/${roomId}`);
                        if (getRes.ok) {
                            const data = await getRes.json();
                            updateRoom(data);
                            setJoinedOnce(true);
                        } else {
                            addToast({title: 'Room is archived', color: 'warning'});
                            router.push('/planning-poker');
                        }
                    } else if (!isAuthenticated) {
                        // Guest without a display name — show the name gate
                        setNeedsGuestName(true);
                    } else {
                        // Authenticated user got a 400 for some other reason
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
            // Create anonymous Keycloak user and sign in
            const result = await createGuestAndSignIn(guestName, avatarJson ?? undefined);
            if (!result.ok) {
                addToast({title: result.error, color: 'danger'});
                setJoinLoading(false);
                return;
            }
            // Now signed in — join the room (the proxy will attach the Bearer token)
            const joinRes = await fetch(`/api/estimation/rooms/${roomId}/join`, {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({displayName: guestName.trim()}),
            });
            if (joinRes.ok) {
                const data = await joinRes.json();
                updateRoom(data);
                setJoinedOnce(true);
                setNeedsGuestName(false);
                // Soft-refresh so TopNav re-renders with the new authenticated session
                router.refresh();
            } else {
                const errBody = await joinRes.json().catch(() => null);
                const errMsg = typeof errBody === 'string' ? errBody
                    : errBody?.message ?? errBody?.error ?? 'Failed to join room';
                addToast({title: errMsg, color: 'danger'});
            }
        } catch {
            addToast({title: 'Network error', color: 'danger'});
        } finally {
            setJoinLoading(false);
        }
    }

    // ── Mutation helpers ──────────────────────────────────────────────────

    const doAction = useCallback(async (
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
                    const data = await res.json();
                    updateRoom(data);
                }
            } else {
                const err = await res.json().catch(() => null);
                const msg = typeof err === 'string' ? err
                    : err?.message || err?.error || `${actionName} failed`;
                addToast({title: msg, color: 'danger'});
            }
        } catch {
            addToast({title: `${actionName} failed`, color: 'danger'});
        } finally {
            setActionLoading(null);
        }
    }, [updateRoom]);

    // When server state arrives via WebSocket, clear the optimistic override
    useEffect(() => {
        if (room) setOptimisticVote(undefined);
    }, [room?.viewer?.selectedVote, room?.roundNumber]);

    async function handleVote(value: string) {
        // Optimistic: highlight the card instantly
        setOptimisticVote(value);
        // Optimistic: update the participant's hasVoted status immediately
        if (room) {
            updateRoom({
                ...room,
                participants: room.participants.map(p =>
                    p.participantId === room.viewer.participantId ? {...p, hasVoted: true} : p
                ),
            });
        }
        // All mutations go through HTTP
        await doAction('Vote', `/api/estimation/rooms/${roomId}/votes`, 'POST', {value});
    }

    async function handleClearVote() {
        // Optimistic: deselect card instantly
        setOptimisticVote(null);
        // Optimistic: update the participant's hasVoted status immediately
        if (room) {
            updateRoom({
                ...room,
                participants: room.participants.map(p =>
                    p.participantId === room.viewer.participantId ? {...p, hasVoted: false} : p
                ),
            });
        }
        await doAction('Clear vote', `/api/estimation/rooms/${roomId}/votes`, 'DELETE');
    }

    async function handleReveal() {
        await doAction('Reveal', `/api/estimation/rooms/${roomId}/reveal`);
    }

    const hasTasks = room?.tasks && room.tasks.length > 0;

    async function handleReset() {
        // If we're at the end of the task list, auto-add a new task before advancing
        if (hasTasks && room && room.roundNumber >= room.tasks!.length) {
            const newTaskName = generateNextTaskName(room.tasks!);
            const updatedTasks = [...room.tasks!, newTaskName];
            await saveTasksToServer(updatedTasks);
        }
        await doAction('Reset', `/api/estimation/rooms/${roomId}/reset`);
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
                const data = await res.json();
                updateRoom(data);
                setOptimisticVote(undefined);
            } else {
                const err = await res.json().catch(() => null);
                const msg = typeof err === 'string' ? err
                    : err?.message || err?.error || 'Revote failed';
                addToast({title: msg, color: 'danger'});
            }
        } catch {
            addToast({title: 'Revote failed', color: 'danger'});
        } finally {
            setActionLoading(null);
        }
    }

    // ── Task management ──────────────────────────────────────────────────

    function generateNextTaskName(existingTasks: string[]): string {
        const existingNumbers = existingTasks
            .map(t => {
                const m = t.match(/^Task (\d+)$/);
                return m ? parseInt(m[1], 10) : 0;
            })
            .filter(n => n > 0);
        const next = existingNumbers.length > 0 ? Math.max(...existingNumbers) + 1 : 1;
        return `Task ${next}`;
    }

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
                addToast({
                    title: typeof err === 'string' ? err : err?.message || 'Failed to update tasks',
                    color: 'danger'
                });
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
        // Don't allow deleting the task currently being estimated
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
        // Enable tasks mode: create one task for the current round
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
        await doAction('Archive', `/api/estimation/rooms/${roomId}/archive`);
        hasLeftRef.current = true;
        router.push('/planning-poker');
    }

    async function handleModeToggle() {
        if (!room) return;
        const newMode = !room.viewer.isSpectator;
        // Optimistically update: clear card selection & switch mode instantly
        if (newMode) {
            updateRoom({
                ...room,
                viewer: {...room.viewer, isSpectator: true, selectedVote: null},
            });
        }
        await doAction('Mode', `/api/estimation/rooms/${roomId}/mode`, 'POST', {isSpectator: newMode});
    }

    async function handleLeave() {
        if (!hasLeftRef.current) {
            hasLeftRef.current = true;
            // Fire-and-forget — don't block navigation on the API response
            fetch(`/api/estimation/rooms/${roomId}/leave`, {method: 'POST'}).catch(() => {
            });
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
        await doAction('Rename', `/api/estimation/rooms/${roomId}/title`, 'PUT', {title: trimmed});
        setEditingTitle(false);
    }

    // ── Loading / gate states ────────────────────────────────────────────

    if (initialLoading) {
        return <div className="min-h-full bg-content1 flex items-center justify-center py-20"><Spinner size="lg"
                                                                                                       label="Loading room..."/>
        </div>;
    }

    if (needsGuestName) {
        return (
            <div className="min-h-full bg-content1 flex items-center justify-center">
                <div className="max-w-md w-full px-4 py-16 flex flex-col gap-4">
                    <div className="bg-content2 border border-content3 shadow-raise-sm rounded-2xl overflow-hidden">
                        <div className="px-6 pt-6 pb-4">
                            <h2 className="text-xl font-semibold">Set up your profile</h2>
                            <p className="text-sm text-foreground-500 mt-1">Choose a name and avatar to join this
                                room.</p>
                        </div>
                        <Divider/>
                        <div className="px-6 py-5">
                            <label
                                className="text-xs font-semibold uppercase tracking-wide text-foreground-400 mb-2 block">Display
                                name</label>
                            <Input placeholder="Jane" value={guestName} onValueChange={setGuestName} autoFocus size="lg"
                                   onKeyDown={(e) => e.key === 'Enter' && handleGuestJoin()}/>
                        </div>
                        <Divider/>
                        <div className="px-6 py-5">
                            <label
                                className="text-xs font-semibold uppercase tracking-wide text-foreground-400 mb-3 block">Your
                                avatar</label>
                            <AvatarPicker seed={guestName.trim() || 'guest'} value={avatarJson}
                                          onChange={setAvatarJson}>
                                {({avatarSrc, onOpen}) => (
                                    <div className="flex flex-col items-center gap-2">
                                        <button type="button" onClick={onOpen} className="group relative">
                                            <img src={avatarSrc} alt="Avatar"
                                                 className="h-20 w-20 rounded-full ring-2 ring-foreground-200 group-hover:ring-primary transition-all"/>
                                            <span
                                                className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity text-white text-xs font-medium">Edit</span>
                                        </button>
                                        <button type="button" onClick={onOpen}
                                                className="text-xs text-primary hover:underline">Customize avatar
                                        </button>
                                    </div>
                                )}
                            </AvatarPicker>
                        </div>
                        <Divider/>
                        <div className="px-6 py-5 flex flex-col gap-3">
                            <Button color="primary" size="lg" isLoading={joinLoading} onPress={handleGuestJoin}
                                    isDisabled={!guestName.trim()} className="w-full font-semibold">Join Room</Button>
                            <div className="flex items-center gap-3 my-1">
                                <Divider className="flex-1"/><span
                                className="text-xs text-foreground-400">or sign in</span><Divider className="flex-1"/>
                            </div>
                            <GoogleSignInButton callbackUrl={`/planning-poker/${roomId}`} label="Continue with Google"/>
                            <Button variant="flat" className="w-full bg-content3"
                                    onPress={() => router.push(`/login?callbackUrl=${encodeURIComponent(`/planning-poker/${roomId}`)}`)}>
                                Sign In with Email
                            </Button>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    if (!room) {
        return <div className="min-h-full bg-content1 flex items-center justify-center py-20"><Spinner size="lg"
                                                                                                       label="Connecting..."/>
        </div>;
    }

    // ── Room UI ──────────────────────────────────────────────────────────

    const isModerator = room.viewer.isModerator;
    const isSpectator = room.viewer.isSpectator;
    const isVoting = room.status === 'Voting';
    const isRevealed = room.status === 'Revealed';
    const isArchived = room.status === 'Archived';

    const viewerInList = room.participants.find(p => p.participantId === room.viewer.participantId);
    const serverSaysNoVote = viewerInList && !viewerInList.hasVoted && !viewerInList.isSpectator && room.status === 'Voting';
    const effectiveVote = serverSaysNoVote
        ? (optimisticVote !== undefined ? optimisticVote : null)
        : (optimisticVote !== undefined ? optimisticVote : room.viewer.selectedVote);

    const activeParticipants = room.participants.filter(p => !p.isSpectator && p.isPresent);
    const spectators = room.participants.filter(p => p.isSpectator && p.isPresent);
    const votedCount = activeParticipants.filter(p => p.hasVoted).length;
    const allVoted = votedCount === activeParticipants.length && activeParticipants.length > 0;
    const showCardPicker = !isArchived && !isSpectator && (isVoting || isRevealed);

    return (
        <div className="min-h-full bg-content1 flex flex-col">

            {/* ════════════ ROOM INFO BLOCK ════════════ */}
            <div className="border-b border-content3 bg-content2/80 backdrop-blur-md z-10">
                <div className="max-w-[1600px] mx-auto px-4 h-14 flex items-center gap-3">
                    {/* Left: title + status */}
                    <div className="flex items-center gap-2.5 min-w-0 flex-1">
                        {editingTitle ? (
                            <input
                                className="text-lg font-bold bg-content1 border border-primary rounded-md px-2 py-0.5 min-w-0 w-64
                                focus:outline-none focus:ring-2 focus:ring-primary/40"
                                value={titleDraft}
                                onChange={e => setTitleDraft(e.target.value)}
                                onKeyDown={e => {
                                    if (e.key === 'Enter') handleRename();
                                    if (e.key === 'Escape') setEditingTitle(false);
                                }}
                                onBlur={handleRename}
                                maxLength={80}
                                autoFocus
                            />
                        ) : (
                            <div
                                className={`flex items-center gap-1.5 min-w-0 ${isModerator && !isArchived ? 'group cursor-pointer' : ''}`}
                                onClick={isModerator && !isArchived ? startEditingTitle : undefined}>
                                <h1 className="text-lg font-bold truncate">{room.title}</h1>
                                {isModerator && !isArchived && (
                                    <Pencil
                                        className="h-3.5 w-3.5 text-foreground-400 opacity-0 group-hover:opacity-100 transition-opacity shrink-0"/>
                                )}
                            </div>
                        )}
                        <StatusBadge status={room.status}/>
                        {wsStatus !== 'connected' && (
                            <Chip size="sm" color="warning" variant="dot" className="shrink-0">
                                {wsStatus === 'connecting' ? 'Reconnecting…' : 'Offline'}
                            </Chip>
                        )}
                    </div>
                    {/* Right: actions */}
                    <div className="flex items-center gap-2 shrink-0">
                        <Tooltip content="Copy room link">
                            <Button size="md" variant="flat" isIconOnly
                                    onPress={() => copyToClipboard(roomLink, 'Room link')}>
                                <ClipboardCopy className="h-5 w-5"/>
                            </Button>
                        </Tooltip>
                        {!isArchived && (
                            <Tooltip content={isSpectator ? 'Switch to Participant' : 'Switch to Spectator'}>
                                <Button size="md" variant="flat" isIconOnly
                                        onPress={handleModeToggle} isLoading={actionLoading === 'Mode'}>
                                    {isSpectator ? <EyeOff className="h-5 w-5"/> : <Eye className="h-5 w-5"/>}
                                </Button>
                            </Tooltip>
                        )}
                        {isModerator && !isArchived && (
                            <Tooltip content="Archive room">
                                <Button size="md" variant="flat" color="danger" isIconOnly
                                        onPress={handleArchive} isLoading={actionLoading === 'Archive'}>
                                    <Archive className="h-5 w-5"/>
                                </Button>
                            </Tooltip>
                        )}
                        <Tooltip content="Leave room">
                            <Button size="md" variant="flat" color="danger" isIconOnly onPress={handleLeave}>
                                <LogOut className="h-5 w-5"/>
                            </Button>
                        </Tooltip>
                        <div className="w-px h-7 bg-content3 mx-1"/>
                        <Tooltip content="Tasks & History">
                            <Button size="md" variant={sidebarOpen ? 'solid' : 'flat'} isIconOnly
                                    onPress={() => setSidebarOpen(o => !o)}>
                                <Menu className="h-5 w-5"/>
                            </Button>
                        </Tooltip>
                    </div>
                </div>
            </div>

            {/* ════════════ MAIN CONTENT ════════════ */}
            <div className="flex-1 flex overflow-hidden">

                {/* ── Center column ── */}
                <div
                    className="flex-1 flex flex-col transition-all duration-300 relative overflow-y-auto">

                    <div className="max-w-[960px] w-full mx-auto px-4 pt-6 flex flex-col gap-5 relative">

                        {/* ── Spectators — floating block to the left of content ── */}
                        {spectators.length > 0 && (
                            <div className="absolute -left-28 top-1/2 -translate-y-1/2 z-10 hidden xl:block">
                                <div className="rounded-2xl bg-content2/80 backdrop-blur-sm border border-content3 shadow-raise-sm p-4 flex flex-col items-center gap-4 w-20">
                                    <Tooltip content="Spectators" placement="right">
                                        <div className="flex items-center gap-1.5 cursor-default">
                                            <Eye className="h-3.5 w-3.5 text-foreground-400"/>
                                            <span className="text-[10px] font-semibold uppercase tracking-wider text-foreground-400">
                                                {spectators.length}
                                            </span>
                                        </div>
                                    </Tooltip>
                                    <div className="flex flex-col items-center gap-3">
                                        {spectators.map(p => (
                                            <Tooltip key={p.participantId} content={p.displayName} placement="right">
                                                <div className="flex flex-col items-center gap-1 group cursor-default">
                                                    <div className="rounded-full ring-2 ring-content3 group-hover:ring-primary/40 transition-all">
                                                        <DiceBearAvatar userId={p.participantId} avatarJson={p.avatarUrl}
                                                            name={p.displayName}
                                                            className="h-11 w-11 opacity-70 group-hover:opacity-100 transition-opacity"/>
                                                    </div>
                                                    <span className="text-[10px] text-foreground-500 group-hover:text-foreground-700 truncate w-16 text-center leading-tight transition-colors">
                                                        {p.displayName}
                                                    </span>
                                                </div>
                                            </Tooltip>
                                        ))}
                                    </div>
                                </div>
                            </div>
                        )}
                    
                        {/* ── Task / Round banner ── */}
                        {hasTasks ? (
                            <div className="rounded-2xl bg-content2 border border-content3 shadow-raise-sm px-5 py-4">
                                <div className="flex items-start justify-between gap-4 mb-3">
                                    <div className="min-w-0">
                                        <p className="text-xs font-semibold uppercase tracking-wider text-foreground-400 mb-0.5">
                                            Currently estimating
                                        </p>
                                        <h2 className="text-xl font-extrabold text-foreground-800 truncate">
                                            {room.currentTaskName ?? `Task ${room.roundNumber}`}
                                        </h2>
                                    </div>
                                    <Chip size="sm" variant="flat"
                                          color={isRevealed ? 'primary' : isArchived ? 'warning' : 'success'}
                                          className="font-semibold tabular-nums shrink-0 mt-1">
                                        {room.roundHistory.length}/{room.tasks!.length}
                                    </Chip>
                                </div>
                                <div className="w-full h-1.5 rounded-full bg-content4 overflow-hidden">
                                    <div className={`h-full rounded-full transition-all duration-500 ${
                                        isRevealed ? 'bg-primary' : isArchived ? 'bg-warning' : 'bg-success'
                                    }`}
                                         style={{width: `${Math.round((room.roundHistory.length / room.tasks!.length) * 100)}%`}}/>
                                </div>
                            </div>
                        ) : (
                            <div
                                className="rounded-2xl bg-content2 border border-content3 shadow-raise-sm px-5 py-3 flex items-center justify-between">
                                <div className="flex items-center gap-3">
                                <span className={`text-xl font-extrabold tabular-nums ${
                                    isRevealed ? 'text-primary' : isArchived ? 'text-warning' : 'text-success'}`}>
                                    Round {room.roundNumber}
                                </span>
                                    <span className="text-sm text-foreground-500">
                                    {isVoting ? 'Voting in progress' : isRevealed ? 'Votes revealed' : 'Archived'}
                                </span>
                                </div>
                            </div>
                        )}

                        {/* ── Voting progress dots ── */}
                        {isVoting && activeParticipants.length > 0 && (
                            <div className="flex items-center justify-center gap-2">
                                <div className="flex items-center gap-1">
                                    {activeParticipants.map(p => (
                                        <div key={p.participantId}
                                             className={`w-2.5 h-2.5 rounded-full transition-colors duration-300 ${
                                                 p.hasVoted ? 'bg-success' : 'bg-default-300'}`}/>
                                    ))}
                                </div>
                                <span
                                    className={`text-sm font-semibold tabular-nums ${allVoted ? 'text-success' : 'text-foreground-500'}`}>
                                {votedCount}/{activeParticipants.length} voted
                            </span>
                            </div>
                        )}

                        {/* ── Table layout ── */}
                        <div className="flex items-start justify-center">
                            <div className="w-full">
                                {activeParticipants.length === 0 ? (
                                    <p className="text-sm text-foreground-400 py-8 text-center">No active voters yet</p>
                                ) : (() => {
                                    const half = Math.ceil(activeParticipants.length / 2);
                                    const topRow = activeParticipants.slice(0, half);
                                    const bottomRow = activeParticipants.slice(half);

                                    return (
                                        <div className="flex flex-col items-center">
                                            {/* Top row — avatar on outside, card toward center */}
                                            <div className="flex flex-wrap justify-center gap-8">
                                                {topRow.map(p => (
                                                    <ParticipantSeat key={p.participantId} participant={p}
                                                        isVoting={isVoting} isRevealed={isRevealed || isArchived}
                                                        side="top"/>
                                                ))}
                                            </div>

                                            {/* Center action */}
                                            <div className="py-6 flex items-center justify-center">
                                                {isModerator && !isArchived && isVoting && (
                                                    <Button size="lg" color="primary" variant="solid"
                                                        onPress={handleReveal}
                                                        isLoading={actionLoading === 'Reveal'}
                                                        startContent={!actionLoading ? <Eye className="h-7 w-7"/> : undefined}
                                                        className={`font-bold px-16 h-16 text-xl rounded-2xl
                                                            shadow-xl shadow-primary/30
                                                            hover:shadow-2xl hover:shadow-primary/40 hover:scale-105
                                                            active:scale-95 transition-all duration-200
                                                            ${allVoted ? 'animate-pulse' : ''}`}>
                                                        Reveal Cards
                                                    </Button>
                                                )}
                                                {isModerator && !isArchived && isRevealed && (
                                                    <div className="flex gap-3">
                                                        <Button size="lg" color="warning" variant="flat"
                                                            onPress={() => handleRevote()}
                                                            isLoading={actionLoading === 'Revote'}
                                                            startContent={<RefreshCw className="h-5 w-5"/>}
                                                            className="font-semibold px-8 h-12">
                                                            Revote
                                                        </Button>
                                                        <Button size="lg" color="secondary" variant="solid"
                                                            onPress={handleReset}
                                                            isLoading={actionLoading === 'Reset'}
                                                            startContent={<RefreshCw className="h-5 w-5"/>}
                                                            className="font-semibold px-8 h-12 shadow-lg shadow-secondary/20">
                                                            {hasTasks ? 'Next Task' : 'Next Round'}
                                                        </Button>
                                                    </div>
                                                )}
                                                {(!isModerator || isArchived) && isVoting && (
                                                    <span className="text-sm text-foreground-400">Waiting for reveal…</span>
                                                )}
                                            </div>

                                            {/* Bottom row — card toward center, avatar on outside */}
                                            {bottomRow.length > 0 && (
                                                <div className="flex flex-wrap justify-center gap-8">
                                                    {bottomRow.map(p => (
                                                        <ParticipantSeat key={p.participantId} participant={p}
                                                            isVoting={isVoting} isRevealed={isRevealed || isArchived}
                                                            side="bottom"/>
                                                    ))}
                                                </div>
                                            )}
                                        </div>
                                    );
                                })()}
                            </div>
                        </div>

                        {/* Spectator notice */}
                        {isSpectator && !isArchived && (
                            <div className="flex items-center justify-center gap-2 text-foreground-400 text-sm py-2">
                                <Eye className="h-4 w-4"/>
                                <span>You are spectating. Switch to Participant to vote.</span>
                            </div>
                        )}

                        {/* Archived notice */}
                        {isArchived && (
                            <div className="flex items-center justify-center gap-2 text-warning text-sm py-2">
                                <Archive className="h-4 w-4"/>
                                <span className="font-medium">This room has been archived and is read-only.</span>
                            </div>
                        )}
                    </div>

                    {/* Spacer — pushes results + card picker to the bottom */}
                    <div className="flex-1"/>

                    {/* ── Results block (after reveal) — pinned above card picker ── */}
                    {(isRevealed || isArchived) && room.roundSummary && room.roundSummary.distribution &&
                        Object.keys(room.roundSummary.distribution).length > 0 && (
                            <div className="max-w-[960px] w-full mx-auto px-4 mb-5">
                                <ResultsPanel
                                    summary={room.roundSummary}
                                    participants={activeParticipants}
                                />
                            </div>
                        )}

                    {/* ════════════ CARD PICKER — floating bottom ════════════ */}
                    <div className={`sticky bottom-0 z-20 pointer-events-none
                    transition-all duration-500 ease-out
                    ${showCardPicker ? 'translate-y-0 opacity-100' : 'translate-y-full opacity-0'}`}>
                        <div className="flex justify-center px-2 pt-1 pb-1 pointer-events-auto">
                            <div className="bg-content2/95 backdrop-blur-xl border border-content3 shadow-[0_-4px_40px_rgba(0,0,0,0.15)]
                            rounded-xl px-3 py-2 max-w-fit">
                                <div className="flex items-center justify-center gap-2 flex-wrap">
                                    {(room?.deck.values ?? []).map(v => {
                                        const isSelected = effectiveVote === v;
                                        return (
                                            <button key={v}
                                                    onClick={isVoting ? () => isSelected ? handleClearVote() : handleVote(v) : undefined}
                                                    disabled={!isVoting}
                                                    className={`
                                                w-14 h-20 rounded-xl border-2 text-lg font-bold
                                                flex items-center justify-center transition-all duration-150
                                                ${isVoting ? 'cursor-pointer active:scale-90' : 'cursor-default opacity-50'}
                                                ${isSelected
                                                        ? 'border-primary bg-primary text-white scale-110 shadow-lg shadow-primary/40 -translate-y-2'
                                                        : isVoting
                                                            ? 'border-content4 bg-content1 text-foreground-600 hover:border-primary/50 hover:-translate-y-1 hover:shadow-md'
                                                            : 'border-content4 bg-content1 text-foreground-600'}
                                            `}>
                                                {v}
                                            </button>
                                        );
                                    })}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                {/* ── Right sidebar (Tasks + History) ── */}
                <div className={`border-l border-content3 bg-content2/50 backdrop-blur-sm transition-all duration-300 overflow-y-auto
                ${sidebarOpen ? 'w-80 min-w-[320px]' : 'w-0 min-w-0 border-l-0'}`}>
                    {sidebarOpen && (
                        <div className="p-4">
                            <SidebarPanel
                                room={room} isModerator={isModerator} isArchived={isArchived}
                                hasTasks={!!hasTasks} actionLoading={actionLoading}
                                onAddTask={handleAddTask} onDeleteTask={handleDeleteTask}
                                onEditTask={handleEditTask} onEnableTasks={handleEnableTasks}
                                onDisableTasks={handleDisableTasks}
                                onRevoteTask={(rn) => handleRevote(rn)}
                            />
                        </div>
                    )}
                </div>
            </div>

        </div>
    );
}

// ── Sub-components ───────────────────────────────────────────────────────────

function StatusBadge({status}: { status: string }) {
    const colorMap: Record<string, 'success' | 'primary' | 'warning'> = {
        Voting: 'success', Revealed: 'primary', Archived: 'warning',
    };
    return <Chip size="sm" color={colorMap[status] ?? 'default'} variant="flat">{status}</Chip>;
}

/** Results panel — shown after votes are revealed */
function ResultsPanel({summary, participants}: {
    summary: PlanningPokerRoundSummary;
    participants: PlanningPokerParticipant[];
}) {
    const distribution = summary.distribution!;
    const sorted = Object.entries(distribution).sort(([, a], [, b]) => b - a);
    const maxCount = Math.max(...sorted.map(([, c]) => c));
    const totalVotes = sorted.reduce((sum, [, c]) => sum + c, 0);
    const uniqueValues = sorted.length;
    const isFullConsensus = uniqueValues === 1 && totalVotes > 1;

    // Entrance animation: fade-in + slide-up
    const [visible, setVisible] = useState(false);
    useEffect(() => {
        const t1 = requestAnimationFrame(() => setVisible(true));
        return () => {
            cancelAnimationFrame(t1);
        };
    }, []);

    // 🎉 Fire confetti on full consensus
    useEffect(() => {
        if (!isFullConsensus) return;
        // Theme-matched colors: primary purple, warning gold, success green, danger pink + lighter accents
        const themeColors = ['#9573da', '#f5a524', '#f7b750', '#17c964', '#45d483', '#f31260', '#f54180'];
        const end = Date.now() + 1500;
        const frame = () => {
            confetti({
                particleCount: 3,
                angle: 55,
                spread: 60,
                origin: {x: 0, y: 0.6},
                colors: themeColors,
            });
            confetti({
                particleCount: 3,
                angle: 125,
                spread: 60,
                origin: {x: 1, y: 0.6},
                colors: themeColors,
            });
            if (Date.now() < end) requestAnimationFrame(frame);
        };
        frame();
    }, [isFullConsensus, summary.roundNumber]);

    // Consensus label
    const consensusLabel = uniqueValues === 1
        ? 'Full consensus!'
        : uniqueValues === 2 && sorted[0][1] >= totalVotes * 0.7
            ? 'Almost aligned'
            : 'Split opinions';
    const consensusColor = uniqueValues === 1
        ? 'text-success'
        : uniqueValues === 2 && sorted[0][1] >= totalVotes * 0.7
            ? 'text-warning'
            : 'text-danger';

    // Group participants by their revealed vote
    const voteGroups: Record<string, PlanningPokerParticipant[]> = {};
    for (const p of participants) {
        const v = p.revealedVote ?? '—';
        if (!voteGroups[v]) voteGroups[v] = [];
        voteGroups[v].push(p);
    }

    return (
        <div className={`rounded-2xl bg-content2 border border-content3 shadow-raise-sm overflow-hidden
            transition-all duration-500 ease-out
            ${visible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-4'}`}>

            {/* Header row — label + average + consensus inline */}
            <div className={`flex items-center gap-4 px-5 py-3
                transition-all duration-700 delay-150 ease-out
                ${visible ? 'opacity-100 scale-100' : 'opacity-0 scale-75'}`}>
                <p className="text-xs font-semibold uppercase tracking-wider text-foreground-400 shrink-0">
                    Results — {summary.taskName ?? `Round ${summary.roundNumber}`}
                </p>
                <div className="flex-1"/>
                {summary.numericAverageDisplay ? (
                    <div className="flex items-center gap-2">
                        <span className="text-3xl font-black tabular-nums text-warning leading-none">
                            {summary.numericAverageDisplay}
                        </span>
                        <div className="flex flex-col">
                            <span className="text-[10px] font-semibold uppercase tracking-wider text-foreground-400">Avg</span>
                            <span className={`text-xs font-semibold ${consensusColor}`}>{consensusLabel}</span>
                        </div>
                    </div>
                ) : (
                    <span className={`text-xs font-semibold ${consensusColor}`}>{consensusLabel}</span>
                )}
            </div>

            <div className="h-px bg-content3 mx-5"/>

            {/* Distribution — compact card layout */}
            <div className="px-5 py-3">
                <div className="flex flex-wrap justify-center gap-3">
                    {sorted.map(([value, count], index) => {
                        const pct = totalVotes > 0 ? Math.round((count / totalVotes) * 100) : 0;
                        const isTop = count === maxCount;
                        const voters = voteGroups[value] ?? [];

                        return (
                            <div key={value} className="flex flex-col items-center gap-1.5"
                                 style={{
                                     opacity: visible ? 1 : 0,
                                     transform: visible ? 'translateY(0) scale(1)' : 'translateY(12px) scale(0.9)',
                                     transition: `opacity 300ms ease-out ${200 + index * 80}ms, transform 300ms ease-out ${200 + index * 80}ms`,
                                 }}>
                                {/* Card */}
                                <div className={`relative w-12 h-[68px] rounded-lg border-2 flex items-center justify-center
                                    text-lg font-black transition-all duration-500
                                    ${isTop
                                    ? 'border-primary bg-primary/10 text-primary shadow-md shadow-primary/25 scale-105'
                                    : 'border-content4 bg-content3/50 text-foreground-600'}`}>
                                    {value}
                                    {/* Vote count badge */}
                                    <div className={`absolute -top-1.5 -right-1.5 w-5 h-5 rounded-full flex items-center justify-center
                                        text-[10px] font-bold shadow-sm
                                        ${isTop
                                        ? 'bg-primary text-white'
                                        : 'bg-content4 text-foreground-600'}`}>
                                        {count}
                                    </div>
                                </div>

                                {/* Percentage */}
                                <span className={`text-[10px] font-semibold tabular-nums
                                    ${isTop ? 'text-primary' : 'text-foreground-400'}`}>
                                    {pct}%
                                </span>

                                {/* Voter avatars */}
                                {voters.length > 0 && (
                                    <div className="flex items-center justify-center -space-x-1.5">
                                        {voters.map(p => (
                                            <Tooltip key={p.participantId} content={p.displayName}>
                                                <span className="inline-flex ring-2 ring-content2 rounded-full">
                                                    <DiceBearAvatar
                                                        userId={p.participantId}
                                                        avatarJson={p.avatarUrl}
                                                        name={p.displayName}
                                                        className="h-5 w-5"
                                                    />
                                                </span>
                                            </Tooltip>
                                        ))}
                                    </div>
                                )}
                            </div>
                        );
                    })}
                </div>
            </div>
        </div>
    );
}

/** Participant seat — oriented so the card faces the table center */
function ParticipantSeat({participant: p, isVoting, isRevealed, side}: {
    participant: PlanningPokerParticipant; isVoting: boolean; isRevealed: boolean;
    side: 'top' | 'bottom';
}) {
    const voteCard = (
        <div className={`w-16 h-[88px] rounded-xl border-2 flex items-center justify-center text-xl font-bold transition-all duration-300
            ${isRevealed && p.revealedVote
                ? 'border-primary bg-primary/10 text-primary'
                : p.hasVoted && isVoting
                    ? 'border-success/60 bg-success/10'
                    : 'border-content4 bg-content2'}`}>
            {isRevealed
                ? (p.revealedVote ?? '—')
                : (p.hasVoted
                    ? <CheckCircle className="h-6 w-6 text-success"/>
                    : <span className="text-foreground-300 text-base">?</span>)
            }
        </div>
    );

    const avatar = (
        <div className="relative">
            <DiceBearAvatar userId={p.participantId} avatarJson={p.avatarUrl}
                name={p.displayName} className="h-16 w-16"/>
            {p.isModerator && (
                <div className="absolute -bottom-0.5 -right-0.5 bg-warning rounded-full p-1 shadow-sm">
                    <Flag className="h-3 w-3 text-white"/>
                </div>
            )}
        </div>
    );

    const nameLabel = (
        <span className="text-sm font-semibold text-foreground-700 truncate w-full text-center">
            {p.displayName}
        </span>
    );

    const statusLabel = isVoting ? (
        <span className={`text-xs ${p.hasVoted ? 'text-success' : 'text-foreground-400'}`}>
            {p.hasVoted ? 'Voted' : 'Thinking…'}
        </span>
    ) : null;

    // Top side: avatar → name → card (card nearest to center)
    // Bottom side: card → name → avatar (card nearest to center)
    return (
        <div className="flex flex-col items-center gap-2 w-24">
            {side === 'top' ? (
                <>
                    {avatar}
                    {nameLabel}
                    {statusLabel}
                    {voteCard}
                </>
            ) : (
                <>
                    {voteCard}
                    {statusLabel}
                    {nameLabel}
                    {avatar}
                </>
            )}
        </div>
    );
}

/** Unified sidebar — merged Tasks + History timeline */
function SidebarPanel({
                          room,
                          isModerator,
                          isArchived,
                          hasTasks,
                          actionLoading,
                          onAddTask,
                          onDeleteTask,
                          onEditTask,
                          onEnableTasks,
                          onDisableTasks,
                          onRevoteTask
                      }: {
    room: PlanningPokerRoom; isModerator: boolean; isArchived: boolean; hasTasks: boolean;
    actionLoading: string | null; onAddTask: () => void; onDeleteTask: (index: number) => void;
    onEditTask: (index: number, newName: string) => void; onEnableTasks: () => void;
    onDisableTasks: () => void; onRevoteTask: (roundNumber: number) => void;
}) {
    const [editingIndex, setEditingIndex] = useState<number | null>(null);
    const [editValue, setEditValue] = useState('');
    const [expandedRound, setExpandedRound] = useState<number | null>(null);

    function startEdit(index: number, currentName: string) {
        setEditingIndex(index);
        setEditValue(currentName);
    }

    function commitEdit(index: number) {
        onEditTask(index, editValue);
        setEditingIndex(null);
        setEditValue('');
    }

    function cancelEdit() {
        setEditingIndex(null);
        setEditValue('');
    }

    const tasks = room.tasks ?? [];
    const isLoading = actionLoading === 'Tasks';

    // Build unified row list
    type RowData = {
        key: number;
        roundNum: number;
        label: string;
        isCurrent: boolean;
        isDone: boolean;
        isFuture: boolean;
        taskIndex: number | null;       // index in tasks array (null for round-only history)
        history: PlanningPokerRoundHistory | null;
    };

    const rows: RowData[] = [];

    if (hasTasks) {
        tasks.forEach((task, i) => {
            const roundNum = i + 1;
            const isCurrent = roundNum === room.roundNumber && !isArchived;
            const historyEntry = room.roundHistory.find(h => h.roundNumber === roundNum) ?? null;
            const isDone = !!historyEntry && !isCurrent;
            const isFuture = !isCurrent && !isDone;
            rows.push({
                key: roundNum,
                roundNum,
                label: task,
                isCurrent,
                isDone,
                isFuture,
                taskIndex: i,
                history: historyEntry
            });
        });
    } else {
        // Rounds-only mode: show history + current round
        room.roundHistory.forEach(h => {
            rows.push({
                key: h.roundNumber,
                roundNum: h.roundNumber,
                label: h.taskName ?? `Round ${h.roundNumber}`,
                isCurrent: false,
                isDone: true,
                isFuture: false,
                taskIndex: null,
                history: h
            });
        });
        // Current round (if not archived)
        if (!isArchived) {
            rows.push({
                key: room.roundNumber,
                roundNum: room.roundNumber,
                label: `Round ${room.roundNumber}`,
                isCurrent: true,
                isDone: false,
                isFuture: false,
                taskIndex: null,
                history: null
            });
        }
    }

    if (rows.length === 0 && !hasTasks) {
        return (
            <div className="rounded-xl bg-content2 border border-content3 p-4 flex flex-col items-center gap-3">
                <List className="h-8 w-8 text-foreground-300"/>
                <p className="text-sm text-foreground-400 text-center">No rounds yet.</p>
                {isModerator && !isArchived && (
                    <Switch
                        size="sm"
                        isSelected={false}
                        isDisabled={isLoading}
                        onValueChange={() => onEnableTasks()}
                    >
                        <span className="text-xs text-foreground-500">Task list</span>
                    </Switch>
                )}
            </div>
        );
    }

    return (
        <div className="flex flex-col gap-3">
            {/* Header */}
            <div className="flex items-center justify-between px-1">
                <div className="flex items-center gap-2">
                    <List className="h-4 w-4 text-foreground-500"/>
                    <h3 className="text-xs font-semibold uppercase tracking-wider text-foreground-500">
                        {hasTasks ? `Tasks (${tasks.length})` : `Rounds (${rows.length})`}
                    </h3>
                </div>
                {isModerator && !isArchived && (
                    <Tooltip content={hasTasks ? 'Disable task list' : 'Enable task list'}>
                        <div>
                            <Switch
                                size="sm"
                                isSelected={hasTasks}
                                isDisabled={isLoading}
                                onValueChange={(checked) => checked ? onEnableTasks() : onDisableTasks()}
                                aria-label="Toggle task list"
                            />
                        </div>
                    </Tooltip>
                )}
            </div>

            {/* Timeline */}
            <div className="flex flex-col gap-1">
                {rows.map(row => {
                    const canEdit = hasTasks && isModerator && !isArchived && row.taskIndex !== null;
                    const canDelete = hasTasks && isModerator && !isArchived && row.taskIndex !== null && !row.isCurrent;
                    const isEditing = editingIndex !== null && row.taskIndex === editingIndex;
                    const isExpanded = expandedRound === row.roundNum;

                    return (
                        <div key={row.key} className="group">
                            {/* Main row */}
                            <div className={`flex items-center gap-2 px-3 py-2 rounded-lg transition-colors text-sm
                                ${row.isCurrent
                                ? 'bg-primary/10 border border-primary/30'
                                : row.isDone
                                    ? 'hover:bg-content3/40'
                                    : 'hover:bg-content3/40'}`}>

                                {/* Status dot */}
                                <div className={`w-5 h-5 rounded-full flex items-center justify-center shrink-0 text-[10px] font-bold
                                    ${row.isDone
                                    ? 'bg-success/20 text-success'
                                    : row.isCurrent
                                        ? 'bg-primary/20 text-primary'
                                        : 'bg-content4 text-foreground-400'}`}>
                                    {row.isDone ? '✓' : row.roundNum}
                                </div>

                                {/* Label */}
                                {isEditing ? (
                                    <input
                                        className="flex-1 min-w-0 bg-content1 border border-content4 rounded-md px-2 py-1 text-sm text-foreground-700 focus:outline-none focus:border-primary"
                                        value={editValue} onChange={e => setEditValue(e.target.value)}
                                        onKeyDown={e => {
                                            if (e.key === 'Enter') commitEdit(row.taskIndex!);
                                            if (e.key === 'Escape') cancelEdit();
                                        }}
                                        onBlur={() => commitEdit(row.taskIndex!)} autoFocus/>
                                ) : (
                                    <span className={`flex-1 min-w-0 truncate
                                        ${row.isCurrent ? 'font-semibold text-primary' : row.isDone ? 'text-foreground-600' : 'text-foreground-700'}
                                        ${canEdit ? 'cursor-pointer hover:underline decoration-foreground-300' : ''}
                                        ${row.isDone && row.history ? 'cursor-pointer' : ''}`}
                                          onClick={() => {
                                              if (isEditing) return;
                                              if (row.isDone && row.history) setExpandedRound(isExpanded ? null : row.roundNum);
                                              else if (canEdit) startEdit(row.taskIndex!, row.label);
                                          }}>
                                        {row.label}
                                    </span>
                                )}

                                {/* Estimate badge */}
                                {row.isDone && row.history?.numericAverageDisplay && !isEditing && (
                                    <span className="text-sm font-extrabold text-warning tabular-nums shrink-0">
                                        {row.history.numericAverageDisplay}
                                    </span>
                                )}

                                {/* Current chip */}
                                {row.isCurrent && !isEditing && (
                                    <Chip size="sm" variant="flat" color="primary"
                                          className="text-[10px] h-5 shrink-0">Now</Chip>
                                )}

                                {/* Action buttons */}
                                {!isEditing && (
                                    <div
                                        className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
                                        {row.isDone && isModerator && !isArchived && (
                                            <Tooltip content="Re-estimate">
                                                <button type="button" onClick={e => {
                                                    e.stopPropagation();
                                                    onRevoteTask(row.roundNum);
                                                }}
                                                        className="p-0.5 text-foreground-400 hover:text-warning">
                                                    <RefreshCw className="h-3.5 w-3.5"/>
                                                </button>
                                            </Tooltip>
                                        )}
                                        {canEdit && (
                                            <Tooltip content="Rename">
                                                <button type="button" onClick={e => {
                                                    e.stopPropagation();
                                                    startEdit(row.taskIndex!, row.label);
                                                }}
                                                        className="p-0.5 text-foreground-400 hover:text-primary">
                                                    <Pencil className="h-3.5 w-3.5"/>
                                                </button>
                                            </Tooltip>
                                        )}
                                        {canDelete && (
                                            <Tooltip content="Delete task">
                                                <button type="button" onClick={e => {
                                                    e.stopPropagation();
                                                    onDeleteTask(row.taskIndex!);
                                                }}
                                                        className="p-0.5 text-foreground-400 hover:text-danger">
                                                    <Trash2 className="h-3.5 w-3.5"/>
                                                </button>
                                            </Tooltip>
                                        )}
                                    </div>
                                )}
                            </div>

                            {/* Expanded detail — vote distribution */}
                            {isExpanded && row.history && (
                                <div
                                    className="ml-9 mr-3 mt-1 mb-2 rounded-lg bg-content3/60 border border-content4 p-3">
                                    <div className="flex flex-wrap gap-2">
                                        {Object.entries(row.history.distribution)
                                            .sort(([, a], [, b]) => b - a)
                                            .map(([value, count]) => {
                                                const total = Object.values(row.history!.distribution).reduce((s, c) => s + c, 0);
                                                const pct = total > 0 ? Math.round((count / total) * 100) : 0;
                                                return (
                                                    <div key={value}
                                                         className="flex items-center gap-1.5 bg-content2 rounded-md px-2 py-1">
                                                        <span className="text-xs font-bold text-primary">{value}</span>
                                                        <span className="text-[10px] text-foreground-400">
                                                            ×{count} · {pct}%
                                                        </span>
                                                    </div>
                                                );
                                            })}
                                    </div>
                                    {row.history.voterCount > 0 && (
                                        <p className="text-[10px] text-foreground-400 mt-2">{row.history.voterCount} voter{row.history.voterCount !== 1 ? 's' : ''}</p>
                                    )}
                                </div>
                            )}
                        </div>
                    );
                })}
            </div>

            {/* Bottom actions */}
            {isModerator && !isArchived && hasTasks && (
                <div className="flex items-center gap-2 px-1">
                    <Button size="sm" variant="flat" className="flex-1" onPress={onAddTask}
                            isLoading={isLoading} startContent={<Plus className="h-4 w-4"/>}>
                        Add Task
                    </Button>
                </div>
            )}
        </div>
    );
}
