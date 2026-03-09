'use client';

import {useCallback, useEffect, useState} from "react";
import {useRouter} from "next/navigation";
import {
    Button, Input, Card, CardBody, Chip, Divider, Spinner, Tooltip, addToast,
} from "@heroui/react";
import {
    ClipboardDocumentIcon, ArrowLeftOnRectangleIcon, EyeIcon, EyeSlashIcon,
    StarIcon, ArchiveBoxIcon, ArrowPathIcon,
} from "@heroicons/react/24/outline";
import {CheckCircleIcon} from "@heroicons/react/24/solid";
import {useRoomWebSocket} from "@/lib/hooks/useRoomWebSocket";
import type {PlanningPokerRoom, PlanningPokerParticipant} from "@/lib/types";

export default function RoomClient({code, isAuthenticated}: {code: string; isAuthenticated: boolean}) {
    const router = useRouter();

    // Gate: guest name entry before join
    const [needsGuestName, setNeedsGuestName] = useState(false);
    const [guestName, setGuestName] = useState('');
    const [joinLoading, setJoinLoading] = useState(false);
    const [initialLoading, setInitialLoading] = useState(true);
    const [joinedOnce, setJoinedOnce] = useState(false);
    const [actionLoading, setActionLoading] = useState<string | null>(null);

    const {room, status: wsStatus, updateRoom} = useRoomWebSocket(joinedOnce ? code : null);

    // Bootstrap: try to join room on mount
    useEffect(() => {
        if (!code) return;
        
        async function bootstrap() {
            try {
                // Attempt join (idempotent for returning participants)
                const res = await fetch(`/api/estimation/rooms/${code}/join`, {
                    method: 'POST',
                    headers: {'Content-Type': 'application/json'},
                    body: JSON.stringify({}),
                });

                if (res.ok) {
                    const data = await res.json();
                    updateRoom(data);
                    setJoinedOnce(true);
                } else if (res.status === 400) {
                    // Likely needs guest name
                    if (!isAuthenticated) {
                        setNeedsGuestName(true);
                    } else {
                        addToast({title: 'Failed to join room', color: 'danger'});
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
    }, [code, isAuthenticated, router, updateRoom]);

    async function handleGuestJoin() {
        if (!guestName.trim()) return;
        setJoinLoading(true);
        try {
            const res = await fetch(`/api/estimation/rooms/${code}/join`, {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({displayName: guestName.trim()}),
            });
            if (res.ok) {
                const data = await res.json();
                updateRoom(data);
                setNeedsGuestName(false);
                setJoinedOnce(true);
            } else {
                addToast({title: 'Failed to join room', color: 'danger'});
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
                addToast({
                    title: typeof err === 'string' ? err : err?.error || `${actionName} failed`,
                    color: 'danger',
                });
            }
        } catch {
            addToast({title: `${actionName} failed`, color: 'danger'});
        } finally {
            setActionLoading(null);
        }
    }, [updateRoom]);

    async function handleVote(value: string) {
        await doAction('Vote', `/api/estimation/rooms/${code}/votes`, 'POST', {value});
    }

    async function handleClearVote() {
        await doAction('Clear vote', `/api/estimation/rooms/${code}/votes`, 'DELETE');
    }

    async function handleReveal() {
        await doAction('Reveal', `/api/estimation/rooms/${code}/reveal`);
    }

    async function handleReset() {
        await doAction('Reset', `/api/estimation/rooms/${code}/reset`);
    }

    async function handleArchive() {
        await doAction('Archive', `/api/estimation/rooms/${code}/archive`);
    }

    async function handleModeToggle() {
        if (!room) return;
        const newMode = !room.viewer.isSpectator;
        await doAction('Mode', `/api/estimation/rooms/${code}/mode`, 'POST', {isSpectator: newMode});
    }

    async function handleLeave() {
        await doAction('Leave', `/api/estimation/rooms/${code}/leave`);
        router.push('/planning-poker');
    }

    function copyToClipboard(text: string, label: string) {
        navigator.clipboard.writeText(text).then(() => {
            addToast({title: `${label} copied!`, color: 'success'});
        });
    }

    // ── Loading / gate states ────────────────────────────────────────────

    if (initialLoading) {
        return (
            <div className="flex items-center justify-center py-20">
                <Spinner size="lg" label="Loading room..."/>
            </div>
        );
    }

    if (needsGuestName) {
        return (
            <div className="max-w-md mx-auto px-4 py-16">
                <Card shadow="sm">
                    <CardBody className="flex flex-col gap-4 p-6">
                        <h2 className="text-xl font-semibold">Join as Guest</h2>
                        <p className="text-sm text-foreground-500">
                            Enter your display name to join this Planning Poker room.
                        </p>
                        <Input
                            label="Display name"
                            placeholder="Jane"
                            value={guestName}
                            onValueChange={setGuestName}
                            autoFocus
                            onKeyDown={(e) => e.key === 'Enter' && handleGuestJoin()}
                        />
                        <Button
                            color="primary"
                            isLoading={joinLoading}
                            onPress={handleGuestJoin}
                            isDisabled={!guestName.trim()}
                            className="w-full"
                        >
                            Join Room
                        </Button>
                    </CardBody>
                </Card>
            </div>
        );
    }

    if (!room) {
        return (
            <div className="flex items-center justify-center py-20">
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

    const activeParticipants = room.participants.filter(p => !p.isSpectator && p.isPresent);
    const spectators = room.participants.filter(p => p.isSpectator && p.isPresent);
    const votedCount = activeParticipants.filter(p => p.hasVoted).length;

    return (
        <div className="max-w-5xl mx-auto px-4 py-6">
            {/* ── Header ────────────────────────────────────────────── */}
            <div className="flex flex-wrap items-center justify-between gap-3 mb-6">
                <div className="flex items-center gap-3 min-w-0">
                    <h1 className="text-2xl font-bold truncate">{room.title}</h1>
                    <StatusBadge status={room.status}/>
                    <Chip size="sm" variant="flat">Round {room.roundNumber}</Chip>
                </div>
                <div className="flex items-center gap-2 flex-shrink-0">
                    <Tooltip content="Copy room code">
                        <Button
                            size="sm" variant="flat"
                            onPress={() => copyToClipboard(room.code, 'Room code')}
                            startContent={<ClipboardDocumentIcon className="h-4 w-4"/>}
                        >
                            {room.code}
                        </Button>
                    </Tooltip>
                    <Tooltip content="Copy room link">
                        <Button
                            size="sm" variant="flat"
                            onPress={() => copyToClipboard(room.canonicalUrl, 'Room link')}
                        >
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

            <div className="grid gap-6 lg:grid-cols-3">
                {/* ── Left: Participants ──────────────────────────────── */}
                <div className="lg:col-span-1">
                    <Card shadow="sm">
                        <CardBody className="p-4">
                            <div className="flex items-center justify-between mb-3">
                                <h3 className="text-sm font-semibold uppercase tracking-wide text-foreground-500">
                                    Participants ({activeParticipants.length})
                                </h3>
                                {isVoting && (
                                    <span className="text-xs text-foreground-400">
                                        {votedCount}/{activeParticipants.length} voted
                                    </span>
                                )}
                            </div>
                            <div className="flex flex-col gap-2">
                                {activeParticipants.map(p => (
                                    <ParticipantRow
                                        key={p.participantId} participant={p}
                                        isVoting={isVoting} isRevealed={isRevealed || isArchived}
                                    />
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
                                                <span className="truncate">{p.displayName}</span>
                                                {p.isModerator && <StarIcon className="h-3.5 w-3.5 text-warning"/>}
                                                {p.isGuest && (
                                                    <Chip size="sm" variant="flat" className="text-xs h-5">Guest</Chip>
                                                )}
                                            </div>
                                        ))}
                                    </div>
                                </>
                            )}

                            <Divider className="my-3"/>
                            {/* Mode toggle + Leave */}
                            <div className="flex flex-col gap-2">
                                {!isArchived && (
                                    <Button
                                        size="sm" variant="flat" fullWidth
                                        onPress={handleModeToggle}
                                        isLoading={actionLoading === 'Mode'}
                                        startContent={isSpectator
                                            ? <EyeSlashIcon className="h-4 w-4"/>
                                            : <EyeIcon className="h-4 w-4"/>}
                                    >
                                        {isSpectator ? 'Switch to Participant' : 'Switch to Spectator'}
                                    </Button>
                                )}
                                <Button
                                    size="sm" variant="light" color="danger" fullWidth
                                    onPress={handleLeave}
                                    isLoading={actionLoading === 'Leave'}
                                    startContent={<ArrowLeftOnRectangleIcon className="h-4 w-4"/>}
                                >
                                    Leave Room
                                </Button>
                            </div>
                        </CardBody>
                    </Card>
                </div>

                {/* ── Right: Deck + controls + results ────────────────── */}
                <div className="lg:col-span-2 flex flex-col gap-6">
                    {/* Card deck */}
                    {!isArchived && !isSpectator && (
                        <Card shadow="sm">
                            <CardBody className="p-4">
                                <h3 className="text-sm font-semibold uppercase tracking-wide text-foreground-500 mb-3">
                                    {isVoting ? 'Pick a card' : 'Cards locked'}
                                </h3>
                                <div className="flex flex-wrap gap-2">
                                    {room.deck.values.map(v => {
                                        const isSelected = room.viewer.selectedVote === v;
                                        return (
                                            <button
                                                key={v}
                                                onClick={() => isVoting && handleVote(v)}
                                                disabled={!isVoting || actionLoading === 'Vote'}
                                                className={`
                                                    w-14 h-20 rounded-lg border-2 text-lg font-bold
                                                    flex items-center justify-center transition-all
                                                    ${isSelected
                                                    ? 'border-primary bg-primary/10 text-primary scale-105 shadow-md'
                                                    : 'border-default-200 dark:border-default-100 hover:border-primary/50 text-foreground-600'}
                                                    ${!isVoting ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}
                                                `}
                                            >
                                                {v}
                                            </button>
                                        );
                                    })}
                                </div>
                                {isVoting && room.viewer.selectedVote && (
                                    <Button
                                        size="sm" variant="light" color="danger"
                                        className="mt-3"
                                        onPress={handleClearVote}
                                        isLoading={actionLoading === 'Clear vote'}
                                    >
                                        Clear my vote
                                    </Button>
                                )}
                            </CardBody>
                        </Card>
                    )}

                    {isSpectator && !isArchived && (
                        <Card shadow="sm">
                            <CardBody className="p-4">
                                <div className="flex items-center gap-2 text-foreground-400">
                                    <EyeIcon className="h-5 w-5"/>
                                    <p>You are in <strong>Spectator</strong> mode. Switch to Participant to vote.</p>
                                </div>
                            </CardBody>
                        </Card>
                    )}

                    {/* Moderator controls */}
                    {isModerator && !isArchived && (
                        <Card shadow="sm">
                            <CardBody className="p-4">
                                <h3 className="text-sm font-semibold uppercase tracking-wide text-foreground-500 mb-3">
                                    Moderator Controls
                                </h3>
                                <div className="flex flex-wrap gap-3">
                                    <Button
                                        color="primary"
                                        onPress={handleReveal}
                                        isDisabled={!isVoting}
                                        isLoading={actionLoading === 'Reveal'}
                                    >
                                        Reveal Cards
                                    </Button>
                                    <Button
                                        color="secondary" variant="flat"
                                        onPress={handleReset}
                                        isLoading={actionLoading === 'Reset'}
                                        startContent={<ArrowPathIcon className="h-4 w-4"/>}
                                    >
                                        Next Round
                                    </Button>
                                    <Button
                                        color="danger" variant="flat"
                                        onPress={handleArchive}
                                        isLoading={actionLoading === 'Archive'}
                                        startContent={<ArchiveBoxIcon className="h-4 w-4"/>}
                                    >
                                        Archive Room
                                    </Button>
                                </div>
                            </CardBody>
                        </Card>
                    )}

                    {/* Results panel */}
                    {(isRevealed || isArchived) && (
                        <Card shadow="sm">
                            <CardBody className="p-4">
                                <h3 className="text-sm font-semibold uppercase tracking-wide text-foreground-500 mb-3">
                                    Results — Round {room.roundSummary.roundNumber}
                                </h3>

                                {/* Distribution */}
                                {room.roundSummary.distribution && Object.keys(room.roundSummary.distribution).length > 0 ? (
                                    <>
                                        <div className="flex flex-wrap gap-4 mb-4">
                                            {Object.entries(room.roundSummary.distribution)
                                                .sort(([, a], [, b]) => b - a)
                                                .map(([value, count]) => (
                                                    <div key={value}
                                                         className="flex flex-col items-center gap-1">
                                                        <div className="w-14 h-20 rounded-lg border-2 border-primary bg-primary/10 text-primary text-lg font-bold flex items-center justify-center">
                                                            {value}
                                                        </div>
                                                        <span className="text-sm font-medium text-foreground-500">
                                                            {count} vote{count !== 1 ? 's' : ''}
                                                        </span>
                                                    </div>
                                                ))}
                                        </div>

                                        {room.roundSummary.numericAverageDisplay && (
                                            <div className="flex items-center gap-2 text-lg">
                                                <span className="text-foreground-500">Average:</span>
                                                <span className="font-bold text-primary">
                                                    {room.roundSummary.numericAverageDisplay}
                                                </span>
                                            </div>
                                        )}
                                    </>
                                ) : (
                                    <p className="text-foreground-400">No votes were cast this round.</p>
                                )}
                            </CardBody>
                        </Card>
                    )}

                    {/* Archived state */}
                    {isArchived && (
                        <Card shadow="sm" className="border-warning/30 border">
                            <CardBody className="p-4">
                                <div className="flex items-center gap-2 text-warning">
                                    <ArchiveBoxIcon className="h-5 w-5"/>
                                    <p className="font-medium">
                                        This room has been archived and is read-only.
                                    </p>
                                </div>
                            </CardBody>
                        </Card>
                    )}
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

function ParticipantRow({
    participant: p,
    isVoting,
    isRevealed,
}: {
    participant: PlanningPokerParticipant;
    isVoting: boolean;
    isRevealed: boolean;
}) {
    return (
        <div className="flex items-center gap-2 text-sm">
            {/* Vote indicator */}
            <div className={`w-10 h-14 rounded-md border flex items-center justify-center text-xs font-bold flex-shrink-0 transition-all
                ${isRevealed && p.revealedVote ? 'border-primary bg-primary/10 text-primary' : 'border-default-200 dark:border-default-100'}`}>
                {isRevealed
                    ? (p.revealedVote ?? '—')
                    : (p.hasVoted
                        ? <CheckCircleIcon className="h-5 w-5 text-success"/>
                        : <span className="text-foreground-300">?</span>)
                }
            </div>
            <div className="flex flex-col min-w-0">
                <div className="flex items-center gap-1.5">
                    <span className="truncate font-medium">{p.displayName}</span>
                    {p.isModerator && (
                        <Tooltip content="Moderator">
                            <StarIcon className="h-3.5 w-3.5 text-warning flex-shrink-0"/>
                        </Tooltip>
                    )}
                    {p.isGuest && (
                        <Chip size="sm" variant="flat" className="text-xs h-5">Guest</Chip>
                    )}
                </div>
                <span className="text-xs text-foreground-400">
                    {isVoting ? (p.hasVoted ? 'Voted' : 'Thinking...') : ''}
                </span>
            </div>
        </div>
    );
}

