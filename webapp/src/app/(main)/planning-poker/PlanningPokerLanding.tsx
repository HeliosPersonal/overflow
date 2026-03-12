'use client';

import {useEffect, useState} from "react";
import {useRouter} from "next/navigation";
import {Button, Input, Card, CardBody, CardHeader, Chip, Divider, Spinner, addToast} from "@heroui/react";
import {HandRaisedIcon, ArchiveBoxIcon, UserGroupIcon, ClockIcon} from "@heroicons/react/24/outline";
import {createGuestAndSignIn} from "@/lib/auth/create-guest";
import type {PlanningPokerRoomSummary} from "@/lib/types";

export default function PlanningPokerLanding({isAuthenticated}: {isAuthenticated: boolean}) {
    const router = useRouter();
    const [title, setTitle] = useState('');
    const [guestName, setGuestName] = useState('');
    const [creating, setCreating] = useState(false);
    const [myRooms, setMyRooms] = useState<PlanningPokerRoomSummary[]>([]);
    const [roomsLoading, setRoomsLoading] = useState(false);

    useEffect(() => {
        if (!isAuthenticated) return;

        async function loadRooms() {
            // Claim any guest participation from before sign-in (idempotent)
            await fetch('/api/estimation/claim-guest', {method: 'POST'}).catch(() => {});

            setRoomsLoading(true);
            try {
                const res = await fetch('/api/estimation/rooms');
                const data: PlanningPokerRoomSummary[] = res.ok ? await res.json() : [];
                setMyRooms(data);
            } catch {
                // ignore
            } finally {
                setRoomsLoading(false);
            }
        }

        void loadRooms();
    }, [isAuthenticated]);

    async function handleCreate() {
        if (!title.trim()) return;
        if (!isAuthenticated && !guestName.trim()) return;
        setCreating(true);
        try {
            // If not authenticated, create an anonymous Keycloak user and sign in first
            if (!isAuthenticated) {
                const guestResult = await createGuestAndSignIn(guestName);
                if (!guestResult.ok) {
                    addToast({title: guestResult.error, color: 'danger'});
                    return;
                }
            }

            const res = await fetch('/api/estimation/rooms', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({title: title.trim()}),
            });
            if (!res.ok) {
                const err = await res.json().catch(() => null);
                const msg = typeof err === 'string' ? err
                    : err?.message || err?.error || 'Failed to create room';
                addToast({title: msg, color: 'danger'});
                return;
            }
            const room = await res.json();
            // Hard navigate so TopNav re-renders with the session
            window.location.href = `/planning-poker/${room.roomId}`;
        } catch {
            addToast({title: 'Failed to create room', color: 'danger'});
        } finally {
            setCreating(false);
        }
    }

    return (
        <div className="max-w-2xl mx-auto px-4 py-10">
            <div className="flex items-center gap-3 mb-2">
                <HandRaisedIcon className="h-8 w-8 text-primary"/>
                <h1 className="text-3xl font-bold text-foreground-600">Planning Poker</h1>
            </div>
            <p className="text-foreground-500 mb-8">
                Estimate as a team. Create a room, share the link, and vote on story points
                using Fibonacci cards. The moderator shares their screen during a call — the app handles
                room state and voting.
            </p>

            {/* Create room */}
            <Card shadow="md" className="border-2 border-default-300 dark:border-default-200 max-w-md">
                <CardHeader className="flex gap-2 px-5 pt-5">
                    <h2 className="text-xl font-semibold">Create a Room</h2>
                </CardHeader>
                <Divider/>
                <CardBody className="flex flex-col gap-4 px-5 pb-5">
                    {!isAuthenticated && (
                        <Input
                            label="Your name"
                            placeholder="Bob"
                            value={guestName}
                            onValueChange={setGuestName}
                        />
                    )}
                    <Input
                        label="Room title"
                        placeholder="Sprint 42 estimation"
                        value={title}
                        onValueChange={setTitle}
                        onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
                    />
                    <Button
                        color="primary"
                        isLoading={creating}
                        onPress={handleCreate}
                        isDisabled={!title.trim() || (!isAuthenticated && !guestName.trim())}
                        className="w-full"
                    >
                        Create Room
                    </Button>
                </CardBody>
            </Card>

            {/* ── My Rooms ─────────────────────────────────────────── */}
            {isAuthenticated && (
                <div className="mt-10">
                    <h2 className="text-xl font-semibold text-foreground-600 mb-4">My Rooms</h2>
                    {roomsLoading ? (
                        <div className="flex justify-center py-8">
                            <Spinner size="md" label="Loading rooms..."/>
                        </div>
                    ) : myRooms.length === 0 ? (
                        <p className="text-foreground-400 text-sm">
                            You haven&apos;t participated in any rooms yet. Create one above!
                        </p>
                    ) : (
                        <div className="flex flex-col gap-3">
                            {myRooms.map(r => (
                                <Card
                                    key={r.roomId}
                                    shadow="sm"
                                    isPressable
                                    onPress={() => router.push(`/planning-poker/${r.roomId}`)}
                                    className="border border-default-200 dark:border-default-100"
                                >
                                    <CardBody className="flex flex-row items-center gap-4 px-4 py-3">
                                        <div className="flex-1 min-w-0">
                                            <div className="flex items-center gap-2 mb-1">
                                                <span className="font-semibold truncate">{r.title}</span>
                                                <RoomStatusChip status={r.status}/>
                                            </div>
                                            <div className="flex items-center gap-4 text-xs text-foreground-400">
                                                <span className="flex items-center gap-1">
                                                    <UserGroupIcon className="h-3.5 w-3.5"/>
                                                    {r.participantCount} participant{r.participantCount !== 1 ? 's' : ''}
                                                </span>
                                                <span className="flex items-center gap-1">
                                                    <ClockIcon className="h-3.5 w-3.5"/>
                                                    {r.completedRounds} round{r.completedRounds !== 1 ? 's' : ''} completed
                                                </span>
                                                {r.archivedAtUtc && (
                                                    <span className="flex items-center gap-1">
                                                        <ArchiveBoxIcon className="h-3.5 w-3.5"/>
                                                        Archived {formatRelativeDate(r.archivedAtUtc)}
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                    </CardBody>
                                </Card>
                            ))}
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}

function RoomStatusChip({status}: {status: string}) {
    const colorMap: Record<string, 'success' | 'primary' | 'warning'> = {
        Voting: 'success',
        Revealed: 'primary',
        Archived: 'warning',
    };
    return <Chip size="sm" color={colorMap[status] ?? 'default'} variant="flat">{status}</Chip>;
}

function formatRelativeDate(isoDate: string): string {
    const date = new Date(isoDate);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 1) return 'just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 30) return `${diffDays}d ago`;
    return date.toLocaleDateString();
}
