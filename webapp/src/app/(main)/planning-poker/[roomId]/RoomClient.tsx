'use client';

import {useCallback, useEffect, useState} from "react";
import {useRouter} from "next/navigation";
import {
    Button, Input, Chip, Divider, Spinner, Tooltip, addToast,
} from "@heroui/react";
import {
    ClipboardDocumentIcon, ArrowLeftOnRectangleIcon, EyeIcon, EyeSlashIcon,
    StarIcon, ArchiveBoxIcon, ArrowPathIcon,
} from "@heroicons/react/24/outline";
import {CheckCircleIcon} from "@heroicons/react/24/solid";
import {useRoomWebSocket} from "@/lib/hooks/useRoomWebSocket";
import {createGuestAndSignIn} from "@/lib/auth/create-guest";
import AvatarPicker from "@/components/AvatarPicker";
import DiceBearAvatar from "@/components/DiceBearAvatar";
import GoogleSignInButton from "@/components/auth/GoogleSignInButton";
import {generateAvatarUrl, AVATAR_EYES, AVATAR_MOUTH} from "@/lib/avatar";
import type {PlanningPokerRoom, PlanningPokerParticipant} from "@/lib/types";

export default function RoomClient({roomId, isAuthenticated}: {roomId: string; isAuthenticated: boolean}) {
    const router = useRouter();

    // Gate: guest name entry before join
    const [needsGuestName, setNeedsGuestName] = useState(false);
    const [guestName, setGuestName] = useState('');
    const [avatarJson, setAvatarJson] = useState<string | null>(() => {
        const eyes = AVATAR_EYES[Math.floor(Math.random() * AVATAR_EYES.length)];
        const mouth = AVATAR_MOUTH[Math.floor(Math.random() * AVATAR_MOUTH.length)];
        return JSON.stringify({ eyes: [eyes], mouth: [mouth] });
    });
    const [joinLoading, setJoinLoading] = useState(false);
    const [initialLoading, setInitialLoading] = useState(true);
    const [joinedOnce, setJoinedOnce] = useState(false);
    const [actionLoading, setActionLoading] = useState<string | null>(null);
    // Optimistic card selection — updated instantly before server confirms
    const [optimisticVote, setOptimisticVote] = useState<string | null | undefined>(undefined);

    const {room, status: wsStatus, updateRoom} = useRoomWebSocket(joinedOnce ? roomId : null);

    // Bootstrap: try to join room on mount
    useEffect(() => {
        if (!roomId) return;
        
        async function bootstrap() {
            try {
                // If the user just logged in with a prior guest cookie, claim guest data
                if (isAuthenticated) {
                    await fetch('/api/estimation/claim-guest', {method: 'POST'}).catch(() => {});
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
                // Hard reload so the entire page (including TopNav layout)
                // re-renders with the new authenticated session
                window.location.reload();
            } else {
                // Fallback: reload anyway
                window.location.reload();
            }
        } catch {
            addToast({title: 'Network error', color: 'danger'});
        } finally {
            setJoinLoading(false);
        }
    }

    // ── Mutation helpers ──────────────────────────────────────────────────

    const doAction = useCallback(async (
        actionName: string,
        url: string,
        method: string = 'POST',
        body?: unknown,
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
        if (room) {
            setOptimisticVote(undefined);
        }
    }, [room?.viewer?.selectedVote, room?.roundNumber]);

    async function handleVote(value: string) {
        // Optimistic: highlight the card instantly
        setOptimisticVote(value);

        // Optimistic: update the participant's hasVoted status immediately
        if (room) {
            updateRoom({
                ...room,
                participants: room.participants.map(p =>
                    p.participantId === room.viewer.participantId
                        ? {...p, hasVoted: true}
                        : p
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
                    p.participantId === room.viewer.participantId
                        ? {...p, hasVoted: false}
                        : p
                ),
            });
        }

        await doAction('Clear vote', `/api/estimation/rooms/${roomId}/votes`, 'DELETE');
    }

    async function handleReveal() {
        await doAction('Reveal', `/api/estimation/rooms/${roomId}/reveal`);
    }

    async function handleReset() {
        await doAction('Reset', `/api/estimation/rooms/${roomId}/reset`);
    }

    async function handleArchive() {
        await doAction('Archive', `/api/estimation/rooms/${roomId}/archive`);
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

    function handleLeave() {
        // Just navigate away — the WS close removes the participant server-side
        router.push('/planning-poker');
    }

    const roomLink = typeof window !== 'undefined'
        ? `${window.location.origin}/planning-poker/${roomId}`
        : room?.canonicalUrl ?? '';

    function copyToClipboard(text: string, label: string) {
        navigator.clipboard.writeText(text).then(() => {
            addToast({title: `${label} copied!`, color: 'success'});
        });
    }

    // ── Loading / gate states ────────────────────────────────────────────

    if (initialLoading) {
        return (
            <div className="min-h-full bg-content1 flex items-center justify-center py-20">
                <Spinner size="lg" label="Loading room..."/>
            </div>
        );
    }

    if (needsGuestName) {
        return (
            <div className="min-h-full bg-content1 flex items-center justify-center">
                <div className="max-w-md w-full px-4 py-16 flex flex-col gap-4">
                    <div className="bg-content2 border border-content3 shadow-raise-sm rounded-2xl overflow-hidden">
                        {/* ── Header ── */}
                        <div className="px-6 pt-6 pb-4">
                            <h2 className="text-xl font-semibold">Set up your profile</h2>
                            <p className="text-sm text-foreground-500 mt-1">
                                Choose a name and avatar to join this room.
                            </p>
                        </div>

                        <Divider />

                        {/* ── Display name ── */}
                        <div className="px-6 py-5">
                            <label className="text-xs font-semibold uppercase tracking-wide text-foreground-400 mb-2 block">
                                Display name
                            </label>
                            <Input
                                placeholder="Jane"
                                value={guestName}
                                onValueChange={setGuestName}
                                autoFocus
                                size="lg"
                                onKeyDown={(e) => e.key === 'Enter' && handleGuestJoin()}
                            />
                        </div>

                        <Divider />

                        {/* ── Avatar ── */}
                        <div className="px-6 py-5">
                            <label className="text-xs font-semibold uppercase tracking-wide text-foreground-400 mb-3 block">
                                Your avatar
                            </label>
                            <AvatarPicker seed={guestName.trim() || 'guest'} value={avatarJson} onChange={setAvatarJson}>
                                {({avatarSrc, onOpen}) => (
                                    <div className="flex flex-col items-center gap-2">
                                        <button type="button" onClick={onOpen} className="group relative">
                                            <img
                                                src={avatarSrc}
                                                alt="Avatar"
                                                className="h-20 w-20 rounded-full ring-2 ring-foreground-200 group-hover:ring-primary transition-all"
                                            />
                                            <span className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity text-white text-xs font-medium">
                                                Edit
                                            </span>
                                        </button>
                                        <button type="button" onClick={onOpen}
                                            className="text-xs text-primary hover:underline">
                                            Customize avatar
                                        </button>
                                    </div>
                                )}
                            </AvatarPicker>
                        </div>

                        <Divider />

                        {/* ── Actions ── */}
                        <div className="px-6 py-5 flex flex-col gap-3">
                            <Button color="primary" size="lg" isLoading={joinLoading} onPress={handleGuestJoin}
                                    isDisabled={!guestName.trim()} className="w-full font-semibold">
                                Join Room
                            </Button>

                            <div className="flex items-center gap-3 my-1">
                                <Divider className="flex-1"/>
                                <span className="text-xs text-foreground-400">or sign in</span>
                                <Divider className="flex-1"/>
                            </div>

                            <GoogleSignInButton
                                callbackUrl={`/planning-poker/${roomId}`}
                                label="Continue with Google"
                            />
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
        return (
            <div className="min-h-full bg-content1 flex items-center justify-center py-20">
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

    // Resolve displayed card selection: optimistic first, then server state.
    // Safety net: if the server's participant list says we haven't voted yet
    // (e.g. after a round reset), discard any stale selection so cards clear.
    const viewerInList = room.participants.find(p => p.participantId === room.viewer.participantId);
    const serverSaysNoVote = viewerInList && !viewerInList.hasVoted && !viewerInList.isSpectator && room.status === 'Voting';
    const effectiveVote = serverSaysNoVote
        ? (optimisticVote !== undefined ? optimisticVote : null)
        : (optimisticVote !== undefined ? optimisticVote : room.viewer.selectedVote);

    const activeParticipants = room.participants.filter(p => !p.isSpectator && p.isPresent);
    const spectators = room.participants.filter(p => p.isSpectator && p.isPresent);
    const votedCount = activeParticipants.filter(p => p.hasVoted).length;

    return (
        <div className="min-h-full bg-content1">
        <div className="max-w-[1536px] mx-auto px-4 py-6">
            {/* ── Header ────────────────────────────────────────────── */}
            <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
                <div className="flex items-center gap-3 min-w-0">
                    <h1 className="text-2xl font-bold truncate">{room.title}</h1>
                    <StatusBadge status={room.status}/>
                </div>
                <div className="flex items-center gap-2 flex-shrink-0">
                    <Tooltip content="Copy room link">
                        <Button size="sm" variant="flat"
                            onPress={() => copyToClipboard(roomLink, 'Room link')}
                            startContent={<ClipboardDocumentIcon className="h-4 w-4"/>}>
                            Copy Link
                        </Button>
                    </Tooltip>
                    {wsStatus !== 'connected' && (
                        <Chip size="sm" color="warning" variant="dot">
                            {wsStatus === 'connecting' ? 'Reconnecting...' : 'Offline'}
                        </Chip>
                    )}
                </div>
            </div>

            {/* ── Round banner ─────────────────────────────────────── */}
            <div className={`bg-content2 border border-content3 shadow-raise-sm rounded-xl px-5 py-3 mb-6 flex items-center justify-between`}>
                <div className="flex items-center gap-3">
                    <span className={`text-2xl font-extrabold tabular-nums
                        ${isRevealed ? 'text-primary' : isArchived ? 'text-warning' : 'text-success'}`}>
                        Round {room.roundNumber}
                    </span>
                    <span className="text-sm text-foreground-500">
                        {isVoting ? 'Voting in progress' : isRevealed ? 'Votes revealed' : 'Archived'}
                    </span>
                </div>
                <div className="flex items-center gap-3">
                    {isVoting && (
                        <div className="flex items-center gap-2">
                            <div className="flex items-center gap-1">
                                {activeParticipants.map(p => (
                                    <div key={p.participantId}
                                        className={`w-2.5 h-2.5 rounded-full transition-colors ${
                                            p.hasVoted ? 'bg-success' : 'bg-default-300'
                                        }`}
                                    />
                                ))}
                            </div>
                            <span className={`text-sm font-semibold tabular-nums ${
                                votedCount === activeParticipants.length && activeParticipants.length > 0
                                    ? 'text-success' : 'text-foreground-500'
                            }`}>
                                {votedCount}/{activeParticipants.length} voted
                            </span>
                        </div>
                    )}
                </div>
            </div>

            <div className="grid gap-6 lg:grid-cols-[1.3fr_3fr_1.3fr]">
                {/* ── Left: Participants ── */}
                <div>
                    <div className="bg-content2 border border-content3 shadow-raise-sm rounded-xl p-4">
                        <div className="flex items-center justify-between mb-3">
                            <h3 className="text-sm font-semibold uppercase tracking-wide text-foreground-500">
                                Participants ({activeParticipants.length})
                            </h3>
                        </div>
                        <div className="flex flex-col gap-2">
                            {activeParticipants.map(p => (
                                <ParticipantRow key={p.participantId} participant={p}
                                    isVoting={isVoting} isRevealed={isRevealed || isArchived}/>
                            ))}
                            {activeParticipants.length === 0 && (
                                <p className="text-sm text-foreground-400 py-2">No active voters</p>
                            )}
                        </div>
                        {spectators.length > 0 && (
                            <>
                                <Divider className="my-3"/>
                                <h3 className="text-sm font-semibold uppercase tracking-wide text-foreground-500 mb-2">
                                    Spectators ({spectators.length})
                                </h3>
                                <div className="flex flex-col gap-2">
                                    {spectators.map(p => (
                                        <div key={p.participantId}
                                             className="flex items-center gap-2 text-sm text-foreground-400">
                                            <EyeIcon className="h-4 w-4 flex-shrink-0"/>
                                            <DiceBearAvatar
                                                userId={p.participantId}
                                                avatarJson={p.avatarUrl}
                                                name={p.displayName}
                                                size="sm"
                                            />
                                            <span className="truncate">{p.displayName}</span>
                                            {p.isModerator && <StarIcon className="h-3.5 w-3.5 text-warning"/>}
                                            {p.isGuest && <Chip size="sm" variant="flat" className="text-xs h-5">Guest</Chip>}
                                        </div>
                                    ))}
                                </div>
                            </>
                        )}
                        <Divider className="my-3"/>
                        <div className="flex flex-col gap-2">
                            {!isArchived && (
                                <Button size="sm" variant="flat" fullWidth
                                    onPress={handleModeToggle} isLoading={actionLoading === 'Mode'}
                                    startContent={isSpectator ? <EyeSlashIcon className="h-4 w-4"/> : <EyeIcon className="h-4 w-4"/>}>
                                    {isSpectator ? 'Switch to Participant' : 'Switch to Spectator'}
                                </Button>
                            )}
                            {isModerator && !isArchived && (
                                <Button size="sm" variant="flat" color="danger" fullWidth
                                    onPress={handleArchive} isLoading={actionLoading === 'Archive'}
                                    startContent={<ArchiveBoxIcon className="h-4 w-4"/>}>
                                    Archive Room
                                </Button>
                            )}
                            <Button size="sm" variant="light" color="danger" fullWidth
                                onPress={handleLeave}
                                startContent={<ArrowLeftOnRectangleIcon className="h-4 w-4"/>}>
                                Leave Room
                            </Button>
                        </div>
                    </div>
                </div>

                {/* ── Center: Deck + controls + results ── */}
                <div className="flex flex-col gap-6">
                    {!isArchived && !isSpectator && (
                        <div className="bg-content2 border border-content3 shadow-raise-sm rounded-xl p-4">
                            <h3 className="text-sm font-semibold uppercase tracking-wide text-foreground-500 mb-3">
                                {isVoting ? 'Pick a card' : 'Cards locked'}
                            </h3>
                            <div className="flex flex-wrap gap-2">
                                {room.deck.values.map(v => {
                                    const isSelected = effectiveVote === v;
                                    return (
                                        <button key={v}
                                            onClick={() => isVoting && handleVote(v)}
                                            disabled={!isVoting}
                                            className={`
                                                w-14 h-20 rounded-lg border-2 text-lg font-bold
                                                flex items-center justify-center transition-all duration-100
                                                ${isSelected
                                                    ? 'border-primary bg-primary/10 text-primary scale-105 shadow-md'
                                                    : 'border-content3 hover:border-primary/50 text-foreground-600'}
                                                ${!isVoting ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer active:scale-95'}
                                            `}>
                                            {v}
                                        </button>
                                    );
                                })}
                            </div>
                            {isVoting && effectiveVote && (
                                <Button size="sm" variant="light" color="danger" className="mt-3"
                                    onPress={handleClearVote}>
                                    Clear my vote
                                </Button>
                            )}
                        </div>
                    )}

                    {isSpectator && !isArchived && (
                        <div className="bg-content2 border border-content3 shadow-raise-sm rounded-xl p-4">
                            <div className="flex items-center gap-2 text-foreground-400">
                                <EyeIcon className="h-5 w-5"/>
                                <p>You are in <strong>Spectator</strong> mode. Switch to Participant to vote.</p>
                            </div>
                        </div>
                    )}

                    {(isRevealed || isArchived) && room.roundSummary && (
                        <div className="bg-content2 border border-content3 shadow-raise-sm rounded-xl p-6">
                            <h3 className="text-sm font-semibold uppercase tracking-wide text-foreground-500 mb-4">
                                Results — Round {room.roundSummary.roundNumber}
                            </h3>
                            {room.roundSummary.distribution && Object.keys(room.roundSummary.distribution).length > 0 ? (
                                <>
                                    {room.roundSummary.numericAverageDisplay && (
                                        <div className="flex flex-col items-center justify-center mb-6 py-4 rounded-xl bg-content3">
                                            <span className="text-xs font-semibold uppercase tracking-wider text-foreground-400 mb-1">Average</span>
                                            <span className="text-5xl font-extrabold text-warning tabular-nums">
                                                {room.roundSummary.numericAverageDisplay}
                                            </span>
                                        </div>
                                    )}
                                    <div className="flex flex-wrap gap-4 justify-center">
                                        {Object.entries(room.roundSummary.distribution)
                                            .sort(([, a], [, b]) => b - a)
                                            .map(([value, count]) => (
                                                <div key={value} className="flex flex-col items-center gap-1">
                                                    <div className="w-14 h-20 rounded-lg border-2 border-primary bg-primary/10 text-primary text-lg font-bold flex items-center justify-center">
                                                        {value}
                                                    </div>
                                                    <span className="text-sm font-medium text-foreground-500">
                                                        {count} vote{count !== 1 ? 's' : ''}
                                                    </span>
                                                </div>
                                            ))}
                                    </div>
                                </>
                            ) : (
                                <p className="text-foreground-400 text-center">No votes were cast this round.</p>
                            )}
                        </div>
                    )}

                    {isModerator && !isArchived && (
                        <div className="flex justify-center gap-4">
                            <Button size="md" color="primary" variant={isVoting ? 'solid' : 'flat'}
                                onPress={handleReveal} isDisabled={!isVoting}
                                isLoading={actionLoading === 'Reveal'} className="font-semibold px-6">
                                Reveal Cards
                            </Button>
                            <Button size="md" color="secondary" variant={isRevealed ? 'solid' : 'flat'}
                                onPress={handleReset} isDisabled={!isRevealed}
                                isLoading={actionLoading === 'Reset'}
                                startContent={<ArrowPathIcon className="h-4 w-4"/>}
                                className="font-semibold px-6">
                                Next Round
                            </Button>
                        </div>
                    )}

                    {isArchived && (
                        <div className="bg-content2 border border-content3 shadow-raise-sm rounded-xl p-4">
                            <div className="flex items-center gap-2 text-warning">
                                <ArchiveBoxIcon className="h-5 w-5"/>
                                <p className="font-medium">This room has been archived and is read-only.</p>
                            </div>
                        </div>
                    )}
                </div>

                {/* ── Right: Voting History ── */}
                <div>
                    <div className="sticky top-4">
                        <div className="bg-content2 border border-content3 shadow-raise-sm rounded-xl p-4">
                            <h3 className="text-sm font-semibold uppercase tracking-wide text-foreground-500 mb-4">
                                Voting History
                            </h3>
                            {room.roundHistory && room.roundHistory.length > 0 ? (
                                <div className="flex flex-col gap-3">
                                    {[...room.roundHistory].reverse().map(h => (
                                        <div key={h.roundNumber}
                                             className="bg-content3 border border-content4 rounded-lg p-3">
                                            <div className="flex items-center justify-between mb-2">
                                                <span className="text-sm font-bold text-foreground-700">Round {h.roundNumber}</span>
                                                {h.numericAverageDisplay && (
                                                    <span className="text-lg font-extrabold text-primary tabular-nums">
                                                        {h.numericAverageDisplay}
                                                    </span>
                                                )}
                                            </div>
                                            <div className="flex items-center gap-1 mb-1.5">
                                                <Chip size="sm" variant="flat" className="text-xs">
                                                    {h.voterCount} vote{h.voterCount !== 1 ? 's' : ''}
                                                </Chip>
                                            </div>
                                            <div className="flex flex-wrap gap-1.5">
                                                {Object.entries(h.distribution)
                                                    .sort(([, a], [, b]) => b - a)
                                                    .map(([value, count]) => (
                                                        <div key={value} className="flex items-center gap-0.5">
                                                            <div className="w-7 h-10 rounded-md border border-primary/40 bg-primary/5 text-primary text-xs font-bold flex items-center justify-center">
                                                                {value}
                                                            </div>
                                                            {count > 1 && (
                                                                <span className="text-xs text-foreground-400 font-medium">x{count}</span>
                                                            )}
                                                        </div>
                                                    ))}
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            ) : (
                                <p className="text-sm text-foreground-400">No rounds completed yet.</p>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </div>
        </div>
    );
}

// ── Sub-components ───────────────────────────────────────────────────────────

function StatusBadge({status}: {status: string}) {
    const colorMap: Record<string, 'success' | 'primary' | 'warning'> = {
        Voting: 'success',
        Revealed: 'primary',
        Archived: 'warning',
    };
    return <Chip size="sm" color={colorMap[status] ?? 'default'} variant="flat">{status}</Chip>;
}

function ParticipantRow({participant: p, isVoting, isRevealed}: {
    participant: PlanningPokerParticipant; isVoting: boolean; isRevealed: boolean;
}) {
    return (
        <div className="flex items-center gap-2 text-sm">
            <div className={`w-10 h-14 rounded-md border flex items-center justify-center text-xs font-bold flex-shrink-0 transition-all
                ${isRevealed && p.revealedVote ? 'border-primary bg-primary/10 text-primary' : 'border-content3'}`}>
                {isRevealed
                    ? (p.revealedVote ?? '—')
                    : (p.hasVoted
                        ? <CheckCircleIcon className="h-5 w-5 text-success"/>
                        : <span className="text-foreground-300">?</span>)
                }
            </div>
            <DiceBearAvatar
                userId={p.participantId}
                avatarJson={p.avatarUrl}
                name={p.displayName}
                size="sm"
            />
            <div className="flex flex-col min-w-0">
                <div className="flex items-center gap-1.5">
                    <span className="truncate font-medium">{p.displayName}</span>
                    {p.isModerator && <Tooltip content="Moderator"><StarIcon className="h-3.5 w-3.5 text-warning flex-shrink-0"/></Tooltip>}
                    {p.isGuest && <Chip size="sm" variant="flat" className="text-xs h-5">Guest</Chip>}
                </div>
                <span className="text-xs text-foreground-400">
                    {isVoting ? (p.hasVoted ? 'Voted' : 'Thinking...') : ''}
                </span>
            </div>
        </div>
    );
}

