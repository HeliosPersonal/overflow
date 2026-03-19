'use client';

import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {useRouter} from "next/navigation";
import {
    Button, Input, Chip, Divider, Spinner, Tooltip, Switch, addToast,
} from "@heroui/react";
import {
    ClipboardCopy, LogOut, Eye, EyeOff,
    Archive, RefreshCw, List, Plus, Pencil,
    Trash2, Menu,
} from "lucide-react";
import {CheckCircle, Crown} from "lucide-react";
import {motion} from "framer-motion";
import {useRoomWebSocket} from "@/lib/hooks/useRoomWebSocket";
import {createGuestAndSignIn} from "@/lib/auth/create-guest";
import {celebrationColors} from "@/lib/theme/colors";
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
} from "@/lib/types";

// ── Constants ────────────────────────────────────────────────────────────────

const FULL_PAGE_CENTER = "min-h-full bg-content1 flex items-center justify-center";
const ICON_SM = "h-3.5 w-3.5";
const ICON_MD = "h-5 w-5";
const SECTION_LABEL = "text-xs font-semibold uppercase tracking-wide text-foreground-400";
const CONFETTI_DURATION_MS = 1500;
const VOTER_AVATAR_PREVIEW_LIMIT = 3;
const TITLE_MAX_LENGTH = 80;
const NAME_LABEL_WIDTH = 96; // px — width of the name label below each avatar seat

// PokerTableScene layout
const SCENE_W = 900;
const SCENE_H = 440;
const CENTER_RX = 250;
const CENTER_RY = 170;
const ORBIT_R = 340;
const CX = SCENE_W / 2;
const CY = SCENE_H * 0.42;
const CARD_W = 64;
const CARD_H = 88;
const AVATAR_SIZE = 48;
const CARD_INWARD = 48;

// Arc layout
const ARC_START_DEG = 200;
const ARC_END_DEG = 340;
const ARC_MAX_SPAN = ARC_END_DEG - ARC_START_DEG;
const ARC_MID_DEG = (ARC_START_DEG + ARC_END_DEG) / 2;

// ── Helpers ──────────────────────────────────────────────────────────────────

/** Extract a user-friendly message from an API error response body. */
function parseErrorMessage(errBody: unknown, fallback: string): string {
    if (typeof errBody === 'string') return errBody;
    if (errBody && typeof errBody === 'object') {
        const obj = errBody as Record<string, unknown>;
        return (typeof obj.message === 'string' ? obj.message : null)
            ?? (typeof obj.error === 'string' ? obj.error : null)
            ?? fallback;
    }
    return fallback;
}

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

function generateRandomAvatarJson(): string {
    const eyes = AVATAR_EYES[Math.floor(Math.random() * AVATAR_EYES.length)];
    const mouth = AVATAR_MOUTH[Math.floor(Math.random() * AVATAR_MOUTH.length)];
    return JSON.stringify({eyes: [eyes], mouth: [mouth]});
}

function getConsensusInfo(uniqueValues: number, topCount: number, totalVotes: number) {
    if (uniqueValues === 1) {
        return {label: 'Full consensus!', colorClass: 'text-success'};
    }
    if (uniqueValues === 2 && topCount >= totalVotes * 0.7) {
        return {label: 'Almost aligned', colorClass: 'text-warning'};
    }
    return {label: 'Split opinions', colorClass: 'text-danger'};
}

function getRoundLabel(room: PlanningPokerRoom, hasTasks: boolean): string {
    return hasTasks
        ? (room.currentTaskName ?? `Task ${room.roundNumber}`)
        : `Round ${room.roundNumber}`;
}

// ── Main Component ───────────────────────────────────────────────────────────

export default function RoomClient({roomId, isAuthenticated}: {
    roomId: string;
    isAuthenticated: boolean;
}) {
    const router = useRouter();

    // Gate: guest name entry before join
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

    const {room, status: wsStatus, updateRoom} = useRoomWebSocket(joinedOnce ? roomId : null);

    // ── Track active room globally so sign-out can leave before session dies
    useEffect(() => {
        if (joinedOnce) {
            setActiveRoom(roomId);
            return () => setActiveRoom(null);
        }
    }, [joinedOnce, roomId]);

    // ── Leave on tab close / external navigation ─────────────────────────
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

    // When server state arrives via WebSocket, clear the optimistic override
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
                roomId={roomId}
                guestName={guestName}
                onGuestNameChange={setGuestName}
                avatarJson={avatarJson}
                onAvatarChange={setAvatarJson}
                joinLoading={joinLoading}
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
        <div className="min-h-full bg-content1 flex flex-col">

            {/* ════════════ ROOM HEADER ════════════ */}
            <RoomHeader
                room={room}
                editingTitle={editingTitle}
                titleDraft={titleDraft}
                onTitleDraftChange={setTitleDraft}
                onStartEditingTitle={startEditingTitle}
                onRename={handleRename}
                onCancelEditTitle={() => setEditingTitle(false)}
                isModerator={isModerator}
                isSpectator={isSpectator}
                isArchived={isArchived}
                wsStatus={wsStatus}
                actionLoading={actionLoading}
                sidebarOpen={sidebarOpen}
                onCopyLink={() => copyToClipboard(roomLink, 'Room link')}
                onModeToggle={handleModeToggle}
                onArchive={handleArchive}
                onLeave={handleLeave}
                onToggleSidebar={() => setSidebarOpen(o => !o)}
            />

            {/* ════════════ MAIN CONTENT ════════════ */}
            <div className="flex-1 flex overflow-hidden">

                {/* ── Center column ── */}
                <div className="flex-1 flex flex-col transition-all duration-300 relative overflow-y-auto">

                    {/* ── Room scene: banner + table + participants ── */}
                    <div className="flex-1 flex flex-col items-center px-6 pt-2">

                        {/* ── Estimation strip ── */}
                        <div className="w-full max-w-[860px] mb-1">
                            <CompactEstimationStrip
                                room={room}
                                hasTasks={!!hasTasks}
                                isVoting={isVoting}
                                isRevealed={isRevealed}
                                isArchived={isArchived}
                                activeParticipants={activeParticipants}
                                votedCount={votedCount}
                                allVoted={allVoted}
                            />
                        </div>

                        {/* Spacer pushes table scene down toward the card deck */}
                        <div className="flex-1 min-h-0"/>

                        {/* ── The poker table scene ── */}
                        <div className="relative w-full max-w-[960px]">

                            {/* ── Spectators — floating left panel ── */}
                            {spectators.length > 0 && (
                                <SpectatorPanel spectators={spectators}/>
                            )}

                            {/* ── Table + participants ── */}
                            <PokerTableScene
                                participants={activeParticipants}
                                viewerParticipantId={room.viewer.participantId}
                                isVoting={isVoting}
                                isRevealed={isRevealed || isArchived}
                                isModerator={isModerator}
                                isArchived={isArchived}
                                hasAnyVotes={hasAnyVotes}
                                allVoted={allVoted}
                                actionLoading={actionLoading}
                                onReveal={handleReveal}
                                onRevote={() => handleRevote()}
                                onReset={handleReset}
                                hasTasks={!!hasTasks}
                                room={room}
                            />

                            {/* Spectator / archive notices */}
                            <NoticeBar isSpectator={isSpectator} isArchived={isArchived}/>
                        </div>
                    </div>

                    {/* ════════════ CARD PICKER — floating bottom ════════════ */}
                    <CardPicker
                        visible={showCardPicker}
                        deckValues={room?.deck.values ?? []}
                        effectiveVote={effectiveVote}
                        isVoting={isVoting}
                        onVote={handleVote}
                        onClearVote={handleClearVote}
                    />
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

// ── GuestNameGate ────────────────────────────────────────────────────────────

function GuestNameGate({roomId, guestName, onGuestNameChange, avatarJson, onAvatarChange, joinLoading, onJoin, onNavigateToLogin}: {
    roomId: string;
    guestName: string;
    onGuestNameChange: (name: string) => void;
    avatarJson: string | null;
    onAvatarChange: (json: string | null) => void;
    joinLoading: boolean;
    onJoin: () => void;
    onNavigateToLogin: () => void;
}) {
    return (
        <div className={FULL_PAGE_CENTER}>
            <div className="max-w-md w-full px-4 py-16 flex flex-col gap-4">
                <div className="bg-content2 border border-content3 shadow-raise-sm rounded-2xl overflow-hidden">
                    {/* Header */}
                    <div className="px-6 pt-6 pb-4">
                        <h2 className="text-xl font-semibold">Set up your profile</h2>
                        <p className="text-sm text-foreground-500 mt-1">Choose a name and avatar to join this room.</p>
                    </div>
                    <Divider/>

                    {/* Display name */}
                    <div className="px-6 py-5">
                        <label className={`${SECTION_LABEL} mb-2 block`}>Display name</label>
                        <Input placeholder="Jane" value={guestName} onValueChange={onGuestNameChange} autoFocus
                               size="lg" onKeyDown={(e) => e.key === 'Enter' && onJoin()}/>
                    </div>
                    <Divider/>

                    {/* Avatar */}
                    <div className="px-6 py-5">
                        <label className={`${SECTION_LABEL} mb-3 block`}>Your avatar</label>
                        <AvatarPicker seed={guestName.trim() || 'guest'} value={avatarJson} onChange={onAvatarChange}>
                            {({avatarSrc, onOpen}) => (
                                <div className="flex flex-col items-center gap-2">
                                    <button type="button" onClick={onOpen} className="group relative">
                                        <img src={avatarSrc} alt="Avatar"
                                             className="h-20 w-20 rounded-full ring-2 ring-foreground-200 group-hover:ring-primary transition-all"/>
                                        <span className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity text-white text-xs font-medium">
                                            Edit
                                        </span>
                                    </button>
                                    <button type="button" onClick={onOpen}
                                            className="text-xs text-primary hover:underline">Customize avatar</button>
                                </div>
                            )}
                        </AvatarPicker>
                    </div>
                    <Divider/>

                    {/* Actions */}
                    <div className="px-6 py-5 flex flex-col gap-3">
                        <Button color="primary" size="lg" isLoading={joinLoading} onPress={onJoin}
                                isDisabled={!guestName.trim()} className="w-full font-semibold">Join Room</Button>
                        <div className="flex items-center gap-3 my-1">
                            <Divider className="flex-1"/>
                            <span className="text-xs text-foreground-400">or sign in</span>
                            <Divider className="flex-1"/>
                        </div>
                        <GoogleSignInButton callbackUrl={`/planning-poker/${roomId}`}
                                            label="Continue with Google"/>
                        <Button variant="flat" className="w-full bg-content3" onPress={onNavigateToLogin}>
                            Sign In with Email
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    );
}

// ── RoomHeader ───────────────────────────────────────────────────────────────

function RoomHeader({room, editingTitle, titleDraft, onTitleDraftChange, onStartEditingTitle, onRename, onCancelEditTitle, isModerator, isSpectator, isArchived, wsStatus, actionLoading, sidebarOpen, onCopyLink, onModeToggle, onArchive, onLeave, onToggleSidebar}: {
    room: PlanningPokerRoom;
    editingTitle: boolean;
    titleDraft: string;
    onTitleDraftChange: (v: string) => void;
    onStartEditingTitle: () => void;
    onRename: () => void;
    onCancelEditTitle: () => void;
    isModerator: boolean;
    isSpectator: boolean;
    isArchived: boolean;
    wsStatus: string;
    actionLoading: string | null;
    sidebarOpen: boolean;
    onCopyLink: () => void;
    onModeToggle: () => void;
    onArchive: () => void;
    onLeave: () => void;
    onToggleSidebar: () => void;
}) {
    const canEditTitle = isModerator && !isArchived;

    return (
        <div className="border-b border-content3 bg-content2/80 backdrop-blur-md z-10">
            <div className="max-w-[1600px] mx-auto px-4 h-14 flex items-center gap-3">
                {/* Left: title + status */}
                <div className="flex items-center gap-2.5 min-w-0 flex-1">
                    {editingTitle ? (
                        <input
                            className="text-lg font-bold bg-content1 border border-primary rounded-md px-2 py-0.5 min-w-0 w-64
                            focus:outline-none focus:ring-2 focus:ring-primary/40"
                            value={titleDraft}
                            onChange={e => onTitleDraftChange(e.target.value)}
                            onKeyDown={e => {
                                if (e.key === 'Enter') onRename();
                                if (e.key === 'Escape') onCancelEditTitle();
                            }}
                            onBlur={onRename}
                            maxLength={TITLE_MAX_LENGTH}
                            autoFocus
                        />
                    ) : (
                        <div
                            className={`flex items-center gap-1.5 min-w-0 ${canEditTitle ? 'group cursor-pointer' : ''}`}
                            onClick={canEditTitle ? onStartEditingTitle : undefined}>
                            <h1 className="text-lg font-bold truncate">{room.title}</h1>
                            {canEditTitle && (
                                <Pencil className={`${ICON_SM} text-foreground-400 opacity-0 group-hover:opacity-100 transition-opacity shrink-0`}/>
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
                        <Button size="md" variant="flat" isIconOnly onPress={onCopyLink}>
                            <ClipboardCopy className={ICON_MD}/>
                        </Button>
                    </Tooltip>
                    {!isArchived && (
                        <Tooltip content={isSpectator ? 'Switch to Participant' : 'Switch to Spectator'}>
                            <Button size="md" variant="flat" isIconOnly
                                    onPress={onModeToggle} isLoading={actionLoading === 'Mode'}>
                                {isSpectator ? <EyeOff className={ICON_MD}/> : <Eye className={ICON_MD}/>}
                            </Button>
                        </Tooltip>
                    )}
                    {canEditTitle && (
                        <Tooltip content="Archive room">
                            <Button size="md" variant="flat" color="danger" isIconOnly
                                    onPress={onArchive} isLoading={actionLoading === 'Archive'}>
                                <Archive className={ICON_MD}/>
                            </Button>
                        </Tooltip>
                    )}
                    <Tooltip content="Leave room">
                        <Button size="md" variant="flat" color="danger" isIconOnly onPress={onLeave}>
                            <LogOut className={ICON_MD}/>
                        </Button>
                    </Tooltip>
                    <div className="w-px h-7 bg-content3 mx-1"/>
                    <Tooltip content="Tasks & History">
                        <Button size="md" variant={sidebarOpen ? 'solid' : 'flat'} isIconOnly onPress={onToggleSidebar}>
                            <Menu className={ICON_MD}/>
                        </Button>
                    </Tooltip>
                </div>
            </div>
        </div>
    );
}

// ── StatusBadge ──────────────────────────────────────────────────────────────

const STATUS_COLOR_MAP: Record<string, 'success' | 'primary' | 'warning'> = {
    Voting: 'success', Revealed: 'primary', Archived: 'warning',
};

function StatusBadge({status}: { status: string }) {
    return <Chip size="sm" color={STATUS_COLOR_MAP[status] ?? 'default'} variant="bordered">{status}</Chip>;
}

// ── SpectatorPanel ───────────────────────────────────────────────────────────

function SpectatorPanel({spectators}: { spectators: PlanningPokerParticipant[] }) {
    return (
        <div className="absolute -left-24 top-1/2 -translate-y-1/2 z-10 hidden xl:block">
            <div className="rounded-2xl bg-content2/60 backdrop-blur-sm border border-content3/60 p-3 flex flex-col items-center gap-3 w-[80px]">
                <div className="flex items-center gap-1 cursor-default">
                    <Eye className={`${ICON_SM} text-foreground-400`}/>
                    <span className="text-[11px] font-semibold text-foreground-400">
                        {spectators.length}
                    </span>
                </div>
                <div className="flex flex-col items-center gap-2.5">
                    {spectators.map(p => (
                        <Tooltip key={p.participantId} content={p.displayName} placement="right">
                            <div className="flex flex-col items-center gap-0.5 group cursor-default">
                                <div className="rounded-full ring-1 ring-content3 group-hover:ring-primary/40 transition-all">
                                    <DiceBearAvatar userId={p.participantId} avatarJson={p.avatarUrl}
                                        name={p.displayName}
                                        className="h-10 w-10 opacity-60 group-hover:opacity-100 transition-opacity"/>
                                </div>
                                <span className="text-[10px] text-foreground-500 truncate w-16 text-center leading-tight">
                                    {p.displayName}
                                </span>
                            </div>
                        </Tooltip>
                    ))}
                </div>
            </div>
        </div>
    );
}

// ── NoticeBar ────────────────────────────────────────────────────────────────

function NoticeBar({isSpectator, isArchived}: { isSpectator: boolean; isArchived: boolean }) {
    if (isSpectator && !isArchived) {
        return (
            <div className="flex items-center justify-center gap-2 text-foreground-400 text-xs mt-3">
                <Eye className={ICON_SM}/>
                <span>You are spectating. Switch to Participant to vote.</span>
            </div>
        );
    }
    if (isArchived) {
        return (
            <div className="flex items-center justify-center gap-2 text-warning text-xs mt-3">
                <Archive className={ICON_SM}/>
                <span className="font-medium">This room has been archived and is read-only.</span>
            </div>
        );
    }
    return null;
}

// ── CardPicker ───────────────────────────────────────────────────────────────

function CardPicker({visible, deckValues, effectiveVote, isVoting, onVote, onClearVote}: {
    visible: boolean;
    deckValues: string[];
    effectiveVote: string | null | undefined;
    isVoting: boolean;
    onVote: (value: string) => void;
    onClearVote: () => void;
}) {
    return (
        <div className={`sticky bottom-0 z-20 pointer-events-none
            transition-all duration-500 ease-out
            ${visible ? 'translate-y-0 opacity-100' : 'translate-y-full opacity-0'}`}>
            <div className="flex justify-center px-2 pb-3 pt-1 pointer-events-auto">
                <div className="bg-content2/95 backdrop-blur-xl border border-content3/80
                    shadow-[0_-2px_32px_rgba(0,0,0,0.12)] rounded-2xl px-5 py-3.5 max-w-fit">
                    <div className="flex items-center justify-center gap-2.5 flex-wrap">
                        {deckValues.map(v => {
                            const isSelected = effectiveVote === v;
                            return (
                                <motion.button
                                    key={v}
                                    onClick={isVoting ? () => isSelected ? onClearVote() : onVote(v) : undefined}
                                    disabled={!isVoting}
                                    whileHover={isVoting && !isSelected ? {y: -4, scale: 1.05} : {}}
                                    whileTap={isVoting ? {scale: 0.92} : {}}
                                    animate={isSelected ? {y: -8, scale: 1.1} : {y: 0, scale: 1}}
                                    transition={{type: 'spring', stiffness: 400, damping: 25}}
                                    className={`
                                        w-[60px] h-[84px] rounded-xl border-2 text-lg font-bold
                                        flex items-center justify-center select-none
                                        ${isVoting ? 'cursor-pointer' : 'cursor-default opacity-40'}
                                        ${isSelected
                                            ? 'border-primary bg-primary text-white shadow-lg shadow-primary/30'
                                            : isVoting
                                                ? 'border-content4 bg-content1 text-foreground-600 hover:border-primary/40 hover:shadow-md'
                                                : 'border-content4 bg-content1 text-foreground-600'}
                                    `}>
                                    {v}
                                </motion.button>
                            );
                        })}
                    </div>
                </div>
            </div>
        </div>
    );
}

// ── CompactEstimationStrip ───────────────────────────────────────────────────

function CompactEstimationStrip({room, hasTasks, isVoting, isRevealed, isArchived, activeParticipants, votedCount, allVoted}: {
    room: PlanningPokerRoom;
    hasTasks: boolean;
    isVoting: boolean;
    isRevealed: boolean;
    isArchived: boolean;
    activeParticipants: PlanningPokerParticipant[];
    votedCount: number;
    allVoted: boolean;
}) {
    const totalActive = activeParticipants.length;
    const votePct = totalActive > 0 ? Math.round((votedCount / totalActive) * 100) : 0;
    const progressBarColor = isRevealed ? 'bg-primary' : isArchived ? 'bg-warning' : 'bg-success';

    // Results data
    const summary = room.roundSummary;
    const hasResults = (isRevealed || isArchived) && summary?.distribution && Object.keys(summary.distribution).length > 0;
    const distribution = hasResults ? summary!.distribution! : {};
    const sorted = Object.entries(distribution).sort(([, a], [, b]) => b - a);
    const maxCount = sorted.length > 0 ? Math.max(...sorted.map(([, c]) => c)) : 0;
    const totalVotes = sorted.reduce((sum, [, c]) => sum + c, 0);
    const uniqueValues = sorted.length;
    const isFullConsensus = uniqueValues === 1 && totalVotes > 1;

    const [resultsVisible, setResultsVisible] = useState(false);
    useEffect(() => {
        if (!hasResults) { setResultsVisible(false); return; }
        const t1 = requestAnimationFrame(() => setResultsVisible(true));
        return () => cancelAnimationFrame(t1);
    }, [hasResults]);

    // Confetti on consensus
    useEffect(() => {
        if (!isFullConsensus) return;
        const themeColors = [...celebrationColors];
        const end = Date.now() + CONFETTI_DURATION_MS;
        const frame = () => {
            confetti({ particleCount: 3, angle: 55, spread: 60, origin: {x: 0, y: 0.6}, colors: themeColors });
            confetti({ particleCount: 3, angle: 125, spread: 60, origin: {x: 1, y: 0.6}, colors: themeColors });
            if (Date.now() < end) requestAnimationFrame(frame);
        };
        frame();
    }, [isFullConsensus, summary?.roundNumber]);

    const {label: consensusLabel, colorClass: consensusColor} = getConsensusInfo(uniqueValues, sorted[0]?.[1] ?? 0, totalVotes);

    // Group participants by their revealed vote
    const voteGroups: Record<string, PlanningPokerParticipant[]> = {};
    if (hasResults) {
        for (const p of activeParticipants) {
            const v = p.revealedVote ?? '—';
            if (!voteGroups[v]) voteGroups[v] = [];
            voteGroups[v].push(p);
        }
    }

    const roundLabel = getRoundLabel(room, hasTasks);

    return (
        <div className="rounded-2xl bg-content2/80 border border-content3/60 overflow-hidden">
            {/* Single-line header */}
            <div className="px-6 py-4 flex items-center gap-4">
                <div className="min-w-0 flex items-center gap-3 flex-1">
                    <span className="text-lg font-bold text-foreground-600 truncate">
                        {roundLabel}
                    </span>
                    {hasTasks && (
                        <Chip size="sm" variant="bordered" color={isRevealed ? 'primary' : isArchived ? 'warning' : 'success'}
                              className="font-semibold tabular-nums text-xs h-6 shrink-0">
                            {room.roundHistory.length}/{room.tasks!.length}
                        </Chip>
                    )}
                </div>
                {totalActive > 0 && (
                    <VoteProgressDots
                        participants={activeParticipants}
                        votedCount={votedCount}
                        totalActive={totalActive}
                        allVoted={allVoted}
                    />
                )}
            </div>

            {/* Progress bar */}
            {totalActive > 0 && (
                <div className="h-1.5 bg-content4">
                    <div className={`h-full transition-all duration-500 ${progressBarColor}`}
                         style={{width: `${votePct}%`}}/>
                </div>
            )}

            {/* Results (revealed) */}
            {hasResults && (
                <div className="px-5 py-3 flex items-center gap-4 border-t border-content3/40">
                    {/* Distribution cards */}
                    <div className="flex items-center gap-3 flex-1">
                        {sorted.map(([value, count], index) => (
                            <DistributionCard
                                key={value}
                                value={value}
                                count={count}
                                totalVotes={totalVotes}
                                isTop={count === maxCount}
                                voters={voteGroups[value] ?? []}
                                animationDelay={100 + index * 60}
                                visible={resultsVisible}
                            />
                        ))}
                    </div>
                    {/* Average + consensus */}
                    <div className="flex items-center gap-2.5 shrink-0">
                        {summary!.numericAverageDisplay && (
                            <span className="text-3xl font-black tabular-nums text-warning leading-none">
                                {summary!.numericAverageDisplay}
                            </span>
                        )}
                        <span className={`text-xs font-semibold ${consensusColor}`}>{consensusLabel}</span>
                    </div>
                </div>
            )}
        </div>
    );
}

// ── VoteProgressDots ─────────────────────────────────────────────────────────

function VoteProgressDots({participants, votedCount, totalActive, allVoted}: {
    participants: PlanningPokerParticipant[];
    votedCount: number;
    totalActive: number;
    allVoted: boolean;
}) {
    return (
        <div className="flex items-center gap-3 shrink-0">
            <div className="flex items-center gap-1.5">
                {participants.map(p => (
                    <div key={p.participantId}
                         className={`w-2.5 h-2.5 rounded-full transition-colors duration-300 ${
                             p.hasVoted ? 'bg-success' : 'bg-default-300'}`}/>
                ))}
            </div>
            <span className={`text-base font-semibold tabular-nums ${allVoted ? 'text-success' : 'text-foreground-500'}`}>
                {votedCount}/{totalActive}
            </span>
        </div>
    );
}

// ── DistributionCard ─────────────────────────────────────────────────────────

function DistributionCard({value, count, totalVotes, isTop, voters, animationDelay, visible}: {
    value: string;
    count: number;
    totalVotes: number;
    isTop: boolean;
    voters: PlanningPokerParticipant[];
    animationDelay: number;
    visible: boolean;
}) {
    const pct = totalVotes > 0 ? Math.round((count / totalVotes) * 100) : 0;

    return (
        <div className="flex items-center gap-2"
             style={{
                 opacity: visible ? 1 : 0,
                 transition: `opacity 250ms ease-out ${animationDelay}ms`,
             }}>
            <div className={`w-10 h-14 rounded-lg border-2 flex items-center justify-center text-base font-black
                ${isTop ? 'border-primary/60 bg-primary/10 text-primary' : 'border-content4 bg-content3/40 text-foreground-600'}`}>
                {value}
            </div>
            <div className="flex flex-col gap-0.5">
                <span className={`text-xs font-bold tabular-nums leading-none ${isTop ? 'text-primary' : 'text-foreground-500'}`}>
                    {pct}%
                </span>
                {voters.length > 0 && (
                    <VoterAvatars voters={voters}/>
                )}
            </div>
        </div>
    );
}

// ── VoterAvatars ─────────────────────────────────────────────────────────────

function VoterAvatars({voters}: { voters: PlanningPokerParticipant[] }) {
    const visible = voters.slice(0, VOTER_AVATAR_PREVIEW_LIMIT);
    const overflow = voters.length - VOTER_AVATAR_PREVIEW_LIMIT;

    return (
        <div className="flex -space-x-1">
            {visible.map(p => (
                <Tooltip key={p.participantId} content={p.displayName}>
                    <span className="inline-flex ring-1 ring-content2 rounded-full">
                        <DiceBearAvatar userId={p.participantId} avatarJson={p.avatarUrl}
                            name={p.displayName} className="h-5 w-5"/>
                    </span>
                </Tooltip>
            ))}
            {overflow > 0 && (
                <span className="text-[10px] text-foreground-400 pl-1">+{overflow}</span>
            )}
        </div>
    );
}

// ── PokerTableScene ──────────────────────────────────────────────────────────

function PokerTableScene({participants, viewerParticipantId, isVoting, isRevealed, isModerator, isArchived, hasAnyVotes, allVoted, actionLoading, onReveal, onRevote, onReset, hasTasks, room}: {
    participants: PlanningPokerParticipant[];
    viewerParticipantId: string;
    isVoting: boolean;
    isRevealed: boolean;
    isModerator: boolean;
    isArchived: boolean;
    hasAnyVotes: boolean;
    allVoted: boolean;
    actionLoading: string | null;
    onReveal: () => void;
    onRevote: () => void;
    onReset: () => void;
    hasTasks: boolean;
    room: PlanningPokerRoom;
}) {
    const seats = useMemo(() => {
        const count = participants.length;
        if (count === 0) return [];

        const span = count <= 2 ? 80
            : count <= 3 ? 100
            : count <= 5 ? 120
            : ARC_MAX_SPAN;

        const startDeg = ARC_MID_DEG - span / 2;
        const endDeg = ARC_MID_DEG + span / 2;

        const orbitBase = count <= 2 ? ORBIT_R * 0.88
            : count <= 3 ? ORBIT_R * 0.90
            : count <= 5 ? ORBIT_R * 0.94
            : ORBIT_R;

        const rx = orbitBase;
        const ry = orbitBase * 0.65;

        return participants.map((p, i) => {
            const deg = count === 1
                ? 270
                : startDeg + (i / (count - 1)) * (endDeg - startDeg);
            const rad = (deg * Math.PI) / 180;
            const x = CX + Math.cos(rad) * rx;
            const y = CY - Math.sin(rad) * ry;
            const dx = x - CX;
            const dy = y - CY;
            const dist = Math.sqrt(dx * dx + dy * dy) || 1;
            const nx = dx / dist;
            const ny = dy / dist;
            return {participant: p, x, y, nx, ny, deg};
        });
    }, [participants]);

    if (participants.length === 0) {
        return (
            <div className="flex flex-col items-center py-8">
                <p className="text-sm text-foreground-400">No active voters yet</p>
            </div>
        );
    }

    return (
        <div className="flex justify-center w-full">
            <div
                className="relative"
                style={{width: `${SCENE_W}px`, height: `${SCENE_H}px`, maxWidth: '100%'}}
            >
                {/* ── Center table ── */}
                <CenterTable
                    room={room}
                    hasTasks={hasTasks}
                    isModerator={isModerator}
                    isArchived={isArchived}
                    isVoting={isVoting}
                    isRevealed={isRevealed}
                    hasAnyVotes={hasAnyVotes}
                    allVoted={allVoted}
                    actionLoading={actionLoading}
                    onReveal={onReveal}
                    onRevote={onRevote}
                    onReset={onReset}
                />

                {/* ── Participant seats ── */}
                {seats.map(({participant: p, x, y, nx, ny}, i) => (
                    <ParticipantSeat
                        key={p.participantId}
                        participant={p}
                        x={x} y={y} nx={nx} ny={ny}
                        index={i}
                        isViewer={p.participantId === viewerParticipantId}
                        isVoting={isVoting}
                        isRevealed={isRevealed}
                    />
                ))}
            </div>
        </div>
    );
}

// ── CenterTable ──────────────────────────────────────────────────────────────

function CenterTable({room, hasTasks, isModerator, isArchived, isVoting, isRevealed, hasAnyVotes, allVoted, actionLoading, onReveal, onRevote, onReset}: {
    room: PlanningPokerRoom;
    hasTasks: boolean;
    isModerator: boolean;
    isArchived: boolean;
    isVoting: boolean;
    isRevealed: boolean;
    hasAnyVotes: boolean;
    allVoted: boolean;
    actionLoading: string | null;
    onReveal: () => void;
    onRevote: () => void;
    onReset: () => void;
}) {
    const showRevealButton = isModerator && !isArchived && isVoting;
    const showRevealedActions = isModerator && !isArchived && isRevealed;
    const showWaitingMessage = (!isModerator || isArchived) && isVoting;

    return (
        <div
            className="absolute bg-gradient-to-br from-content2 via-content2 to-content3/50
                border border-content3/80
                shadow-[0_0_0_1px_rgba(255,255,255,0.04),0_8px_60px_rgba(0,0,0,0.12),0_2px_12px_rgba(0,0,0,0.06)]
                flex flex-col items-center justify-center gap-4"
            style={{
                left: `${CX - CENTER_RX}px`,
                top: `${CY - CENTER_RY}px`,
                width: `${CENTER_RX * 2}px`,
                height: `${CENTER_RY * 2}px`,
                borderRadius: '50%',
            }}
        >
            {/* Task label */}
            <div className="text-[13px] font-semibold uppercase tracking-wider text-foreground-400 text-center leading-snug max-w-[70%] break-words">
                {getRoundLabel(room, hasTasks)}
            </div>

            {/* Reveal button */}
            {showRevealButton && (
                <Tooltip
                    content="No votes yet — at least one participant must vote before revealing"
                    isDisabled={hasAnyVotes}
                    placement="bottom"
                    delay={200}
                >
                    <span className="inline-block">
                        <Button size="lg" color="primary" variant="solid"
                            onPress={onReveal}
                            isDisabled={!hasAnyVotes}
                            isLoading={actionLoading === 'Reveal'}
                            startContent={!actionLoading ? <Eye className="h-6 w-6"/> : undefined}
                            className={`font-bold px-10 h-14 text-lg rounded-xl
                                shadow-lg shadow-primary/25
                                hover:shadow-xl hover:shadow-primary/35 hover:scale-[1.02]
                                active:scale-95 transition-all duration-200
                                ${allVoted ? 'animate-pulse' : ''}`}>
                            Reveal
                        </Button>
                    </span>
                </Tooltip>
            )}

            {/* Revote / Next buttons */}
            {showRevealedActions && (
                <div className="flex gap-3">
                    <Button size="lg" color="warning" variant="flat"
                        onPress={onRevote}
                        isLoading={actionLoading === 'Revote'}
                        startContent={<RefreshCw className={ICON_MD}/>}
                        className="font-semibold px-6 h-12 text-sm">
                        Revote
                    </Button>
                    <Button size="lg" color="secondary" variant="solid"
                        onPress={onReset}
                        isLoading={actionLoading === 'Reset'}
                        startContent={<RefreshCw className={ICON_MD}/>}
                        className="font-semibold px-6 h-12 text-sm shadow-md shadow-secondary/20">
                        Next
                    </Button>
                </div>
            )}

            {/* Waiting message for non-moderators */}
            {showWaitingMessage && (
                <span className="text-sm text-foreground-400">Waiting for reveal…</span>
            )}
        </div>
    );
}

// ── ParticipantSeat ──────────────────────────────────────────────────────────

function ParticipantSeat({participant: p, x, y, nx, ny, index, isViewer, isVoting, isRevealed}: {
    participant: PlanningPokerParticipant;
    x: number; y: number; nx: number; ny: number;
    index: number;
    isViewer: boolean;
    isVoting: boolean;
    isRevealed: boolean;
}) {
    const cardX = x - nx * CARD_INWARD - CARD_W / 2;
    const cardY = y - ny * CARD_INWARD - CARD_H / 2;
    const avatarX = x - AVATAR_SIZE / 2;
    const avatarY = y - AVATAR_SIZE / 2;

    return (
        <motion.div
            className="absolute"
            style={{left: 0, top: 0, width: `${SCENE_W}px`, height: `${SCENE_H}px`, pointerEvents: 'none'}}
            initial={{opacity: 0, scale: 0.92}}
            animate={{opacity: 1, scale: 1}}
            transition={{delay: index * 0.04, type: 'spring', stiffness: 340, damping: 26}}
        >
            {/* Card */}
            <div
                className="absolute"
                style={{
                    left: `${cardX}px`,
                    top: `${cardY}px`,
                    width: `${CARD_W}px`,
                    height: `${CARD_H}px`,
                    pointerEvents: 'auto',
                }}
            >
                <FlipCard
                    hasVoted={p.hasVoted}
                    isVoting={isVoting}
                    isRevealed={isRevealed}
                    revealedVote={p.revealedVote}
                    sizeClass="w-full h-full"
                />
            </div>

            {/* Avatar */}
            <div
                className="absolute"
                style={{
                    left: `${avatarX}px`,
                    top: `${avatarY}px`,
                    width: `${AVATAR_SIZE}px`,
                    height: `${AVATAR_SIZE}px`,
                    pointerEvents: 'auto',
                }}
            >
                <div className="relative">
                    <div className={`rounded-full transition-all duration-300
                        ${isViewer
                            ? 'ring-2 ring-primary/60 ring-offset-2 ring-offset-content1'
                            : 'ring-1 ring-content3/60'}`}>
                        <DiceBearAvatar
                            userId={p.participantId}
                            avatarJson={p.avatarUrl}
                            name={p.displayName}
                            className="h-12 w-12"
                            size="sm"
                        />
                    </div>
                    {p.isModerator && (
                        <div className="absolute -top-1 -right-1 bg-warning rounded-full p-[3px] shadow-sm">
                            <Crown className="h-3 w-3 text-white"/>
                        </div>
                    )}
                    {!p.isPresent && (
                        <div className="absolute -bottom-0.5 -right-0.5 w-3.5 h-3.5 rounded-full bg-foreground-300 border-2 border-content1"/>
                    )}
                </div>
            </div>

            {/* Name — centered below avatar */}
            <Tooltip content={p.displayName} delay={400}>
                <div
                    className="absolute flex items-start justify-center"
                    style={{
                        left: `${x - NAME_LABEL_WIDTH / 2}px`,
                        top: `${avatarY + AVATAR_SIZE + 4}px`,
                        width: `${NAME_LABEL_WIDTH}px`,
                        pointerEvents: 'auto',
                    }}
                >
                    <span className={`text-xs leading-tight font-medium truncate text-center
                        ${isViewer ? 'text-primary' : 'text-foreground-500'}`}>
                        {p.displayName}
                    </span>
                </div>
            </Tooltip>
        </motion.div>
    );
}

// ── FlipCard ─────────────────────────────────────────────────────────────────

function FlipCard({hasVoted, isVoting, isRevealed, revealedVote, sizeClass}: {
    hasVoted: boolean;
    isVoting: boolean;
    isRevealed: boolean;
    revealedVote?: string | null;
    sizeClass?: string;
}) {
    const showFront = isRevealed;

    return (
        <div className={sizeClass ?? 'w-[64px] h-[88px]'} style={{perspective: '600px'}}>
            <motion.div
                className="relative w-full h-full"
                style={{transformStyle: 'preserve-3d'}}
                animate={{rotateY: showFront ? 0 : 180}}
                transition={{duration: 0.45, ease: [0.4, 0, 0.2, 1]}}
            >
                {/* Front face — revealed vote */}
                <div
                    className={`absolute inset-0 rounded-xl border-2 flex items-center justify-center
                        ${isRevealed && revealedVote
                            ? 'border-primary/50 bg-content1 shadow-md'
                            : 'border-content3 bg-content1'}`}
                    style={{backfaceVisibility: 'hidden'}}
                >
                    <span className="text-xl font-black text-primary tabular-nums">
                        {revealedVote ?? '—'}
                    </span>
                </div>

                {/* Back face — hidden card or empty */}
                <div
                    className={`absolute inset-0 rounded-xl border-2 flex items-center justify-center
                        ${hasVoted
                            ? 'border-success/40 bg-content1 shadow-md'
                            : 'border-content3 bg-content1'}`}
                    style={{backfaceVisibility: 'hidden', transform: 'rotateY(180deg)'}}
                >
                    {hasVoted ? (
                        <CheckCircle className="h-6 w-6 text-success"/>
                    ) : (
                        <span className="text-foreground-300/60 text-base font-medium">?</span>
                    )}
                </div>
            </motion.div>
        </div>
    );
}

// ── SidebarPanel ─────────────────────────────────────────────────────────────

function SidebarPanel({room, isModerator, isArchived, hasTasks, actionLoading, onAddTask, onDeleteTask, onEditTask, onEnableTasks, onDisableTasks, onRevoteTask}: {
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

    const rows = buildTimelineRows(room, hasTasks, isArchived, tasks);

    if (rows.length === 0 && !hasTasks) {
        return (
            <div className="rounded-xl bg-content2 border border-content3 p-4 flex flex-col items-center gap-3">
                <List className="h-8 w-8 text-foreground-300"/>
                <p className="text-sm text-foreground-400 text-center">No rounds yet.</p>
                {isModerator && !isArchived && (
                    <Switch size="sm" isSelected={false} isDisabled={isLoading}
                            onValueChange={() => onEnableTasks()}>
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
                {rows.map(row => (
                    <TaskRow
                        key={row.key}
                        row={row}
                        hasTasks={hasTasks}
                        isModerator={isModerator}
                        isArchived={isArchived}
                        isEditing={editingIndex !== null && row.taskIndex === editingIndex}
                        isExpanded={expandedRound === row.roundNum}
                        editValue={editValue}
                        onEditValueChange={setEditValue}
                        onStartEdit={startEdit}
                        onCommitEdit={commitEdit}
                        onCancelEdit={cancelEdit}
                        onToggleExpand={(roundNum) => setExpandedRound(expandedRound === roundNum ? null : roundNum)}
                        onRevoteTask={onRevoteTask}
                        onDeleteTask={onDeleteTask}
                    />
                ))}
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

// ── Timeline row data ────────────────────────────────────────────────────────

type TimelineRowData = {
    key: number;
    roundNum: number;
    label: string;
    isCurrent: boolean;
    isDone: boolean;
    isFuture: boolean;
    taskIndex: number | null;
    history: PlanningPokerRoundHistory | null;
};

function buildTimelineRows(
    room: PlanningPokerRoom, hasTasks: boolean, isArchived: boolean, tasks: string[]
): TimelineRowData[] {
    const rows: TimelineRowData[] = [];

    if (hasTasks) {
        tasks.forEach((task, i) => {
            const roundNum = i + 1;
            const isCurrent = roundNum === room.roundNumber && !isArchived;
            const historyEntry = room.roundHistory.find(h => h.roundNumber === roundNum) ?? null;
            const isDone = !!historyEntry && !isCurrent;
            const isFuture = !isCurrent && !isDone;
            rows.push({key: roundNum, roundNum, label: task, isCurrent, isDone, isFuture, taskIndex: i, history: historyEntry});
        });
    } else {
        room.roundHistory.forEach(h => {
            rows.push({
                key: h.roundNumber, roundNum: h.roundNumber,
                label: h.taskName ?? `Round ${h.roundNumber}`,
                isCurrent: false, isDone: true, isFuture: false, taskIndex: null, history: h,
            });
        });
        if (!isArchived) {
            rows.push({
                key: room.roundNumber, roundNum: room.roundNumber,
                label: `Round ${room.roundNumber}`,
                isCurrent: true, isDone: false, isFuture: false, taskIndex: null, history: null,
            });
        }
    }

    return rows;
}

// ── TaskRow ──────────────────────────────────────────────────────────────────

function TaskRow({row, hasTasks, isModerator, isArchived, isEditing, isExpanded, editValue, onEditValueChange, onStartEdit, onCommitEdit, onCancelEdit, onToggleExpand, onRevoteTask, onDeleteTask}: {
    row: TimelineRowData;
    hasTasks: boolean;
    isModerator: boolean;
    isArchived: boolean;
    isEditing: boolean;
    isExpanded: boolean;
    editValue: string;
    onEditValueChange: (v: string) => void;
    onStartEdit: (index: number, name: string) => void;
    onCommitEdit: (index: number) => void;
    onCancelEdit: () => void;
    onToggleExpand: (roundNum: number) => void;
    onRevoteTask: (roundNumber: number) => void;
    onDeleteTask: (index: number) => void;
}) {
    const canEdit = hasTasks && isModerator && !isArchived && row.taskIndex !== null;
    const canDelete = canEdit && !row.isCurrent;

    const rowBgClass = row.isCurrent
        ? 'bg-primary/10 border border-primary/30'
        : 'hover:bg-content3/40';

    const statusDotClass = row.isDone
        ? 'bg-success/20 text-success'
        : row.isCurrent
            ? 'bg-primary/20 text-primary'
            : 'bg-content4 text-foreground-400';

    const labelClass = row.isCurrent
        ? 'font-semibold text-primary'
        : row.isDone
            ? 'text-foreground-600'
            : 'text-foreground-700';

    function handleLabelClick() {
        if (isEditing) return;
        if (row.isDone && row.history) onToggleExpand(row.roundNum);
        else if (canEdit) onStartEdit(row.taskIndex!, row.label);
    }

    return (
        <div className="group">
            {/* Main row */}
            <div className={`flex items-center gap-2 px-3 py-2 rounded-lg transition-colors text-sm ${rowBgClass}`}>

                {/* Status dot */}
                <div className={`w-5 h-5 rounded-full flex items-center justify-center shrink-0 text-[10px] font-bold ${statusDotClass}`}>
                    {row.isDone ? '✓' : row.roundNum}
                </div>

                {/* Label */}
                {isEditing ? (
                    <input
                        className="flex-1 min-w-0 bg-content1 border border-content4 rounded-md px-2 py-1 text-sm text-foreground-700 focus:outline-none focus:border-primary"
                        value={editValue} onChange={e => onEditValueChange(e.target.value)}
                        onKeyDown={e => {
                            if (e.key === 'Enter') onCommitEdit(row.taskIndex!);
                            if (e.key === 'Escape') onCancelEdit();
                        }}
                        onBlur={() => onCommitEdit(row.taskIndex!)} autoFocus/>
                ) : (
                    <span
                        className={`flex-1 min-w-0 truncate ${labelClass}
                            ${canEdit ? 'cursor-pointer hover:underline decoration-foreground-300' : ''}
                            ${row.isDone && row.history ? 'cursor-pointer' : ''}`}
                        onClick={handleLabelClick}>
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
                    <Chip size="sm" variant="flat" color="primary" className="text-[10px] h-5 shrink-0">Now</Chip>
                )}

                {/* Action buttons */}
                {!isEditing && (
                    <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
                        {row.isDone && isModerator && !isArchived && (
                            <Tooltip content="Re-estimate">
                                <button type="button" onClick={e => { e.stopPropagation(); onRevoteTask(row.roundNum); }}
                                        className="p-0.5 text-foreground-400 hover:text-warning">
                                    <RefreshCw className={ICON_SM}/>
                                </button>
                            </Tooltip>
                        )}
                        {canEdit && (
                            <Tooltip content="Rename">
                                <button type="button" onClick={e => { e.stopPropagation(); onStartEdit(row.taskIndex!, row.label); }}
                                        className="p-0.5 text-foreground-400 hover:text-primary">
                                    <Pencil className={ICON_SM}/>
                                </button>
                            </Tooltip>
                        )}
                        {canDelete && (
                            <Tooltip content="Delete task">
                                <button type="button" onClick={e => { e.stopPropagation(); onDeleteTask(row.taskIndex!); }}
                                        className="p-0.5 text-foreground-400 hover:text-danger">
                                    <Trash2 className={ICON_SM}/>
                                </button>
                            </Tooltip>
                        )}
                    </div>
                )}
            </div>

            {/* Expanded detail — vote distribution */}
            {isExpanded && row.history && (
                <ExpandedRoundDetail history={row.history}/>
            )}
        </div>
    );
}

// ── ExpandedRoundDetail ──────────────────────────────────────────────────────

function ExpandedRoundDetail({history}: { history: PlanningPokerRoundHistory }) {
    const total = Object.values(history.distribution).reduce((s, c) => s + c, 0);

    return (
        <div className="ml-9 mr-3 mt-1 mb-2 rounded-lg bg-content3/60 border border-content4 p-3">
            <div className="flex flex-wrap gap-2">
                {Object.entries(history.distribution)
                    .sort(([, a], [, b]) => b - a)
                    .map(([value, count]) => {
                        const pct = total > 0 ? Math.round((count / total) * 100) : 0;
                        return (
                            <div key={value} className="flex items-center gap-1.5 bg-content2 rounded-md px-2 py-1">
                                <span className="text-xs font-bold text-primary">{value}</span>
                                <span className="text-[10px] text-foreground-400">
                                    ×{count} · {pct}%
                                </span>
                            </div>
                        );
                    })}
            </div>
            {history.voterCount > 0 && (
                <p className="text-[10px] text-foreground-400 mt-2">
                    {history.voterCount} voter{history.voterCount !== 1 ? 's' : ''}
                </p>
            )}
        </div>
    );
}
