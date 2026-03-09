'use client';

import {useState} from "react";
import {useRouter} from "next/navigation";
import {Button, Input, Card, CardBody, CardHeader, Divider, addToast} from "@heroui/react";
import {HandRaisedIcon} from "@heroicons/react/24/outline";

export default function PlanningPokerLanding({isAuthenticated}: {isAuthenticated: boolean}) {
    const router = useRouter();
    const [title, setTitle] = useState('');
    const [joinCode, setJoinCode] = useState('');
    const [guestName, setGuestName] = useState('');
    const [creating, setCreating] = useState(false);
    const [joining, setJoining] = useState(false);

    async function handleCreate() {
        if (!title.trim()) return;
        setCreating(true);
        try {
            const res = await fetch('/api/estimation/rooms', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({title: title.trim()}),
            });
            if (!res.ok) {
                const err = await res.json().catch(() => ({error: 'Failed to create room'}));
                addToast({title: err.error || 'Failed to create room', color: 'danger'});
                return;
            }
            const room = await res.json();
            router.push(`/planning-poker/${room.code}`);
        } catch {
            addToast({title: 'Failed to create room', color: 'danger'});
        } finally {
            setCreating(false);
        }
    }

    async function handleJoin() {
        const code = joinCode.trim().toUpperCase();
        if (!code) return;
        if (!isAuthenticated && !guestName.trim()) {
            addToast({title: 'Please enter your display name', color: 'warning'});
            return;
        }
        setJoining(true);
        try {
            const res = await fetch(`/api/estimation/rooms/${code}/join`, {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({displayName: isAuthenticated ? undefined : guestName.trim()}),
            });
            if (!res.ok) {
                const err = await res.json().catch(() => ({error: 'Room not found'}));
                addToast({title: typeof err === 'string' ? err : err.error || 'Failed to join room', color: 'danger'});
                return;
            }
            router.push(`/planning-poker/${code}`);
        } catch {
            addToast({title: 'Failed to join room', color: 'danger'});
        } finally {
            setJoining(false);
        }
    }

    return (
        <div className="max-w-2xl mx-auto px-4 py-10">
            <div className="flex items-center gap-3 mb-2">
                <HandRaisedIcon className="h-8 w-8 text-primary"/>
                <h1 className="text-3xl font-bold text-foreground-600">Planning Poker</h1>
            </div>
            <p className="text-foreground-500 mb-8">
                Estimate as a team. Create a room, share the code or link, and vote on story points
                using Fibonacci cards. The moderator shares their screen during a call — the app handles
                room state and voting.
            </p>

            <div className="grid gap-6 md:grid-cols-2">
                {/* Create room */}
                <Card shadow="md" className="border-2 border-default-300 dark:border-default-200">
                    <CardHeader className="flex gap-2 px-5 pt-5">
                        <h2 className="text-xl font-semibold">Create a Room</h2>
                    </CardHeader>
                    <Divider/>
                    <CardBody className="flex flex-col gap-4 px-5 pb-5">
                        {!isAuthenticated && (
                            <p className="text-sm text-foreground-400">
                                Sign in to create a room.
                            </p>
                        )}
                        <Input
                            label="Room title"
                            placeholder="Sprint 42 estimation"
                            value={title}
                            onValueChange={setTitle}
                            isDisabled={!isAuthenticated}
                            onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
                        />
                        <Button
                            color="primary"
                            isLoading={creating}
                            onPress={handleCreate}
                            isDisabled={!isAuthenticated || !title.trim()}
                            className="w-full"
                        >
                            Create Room
                        </Button>
                    </CardBody>
                </Card>

                {/* Join room */}
                <Card shadow="md" className="border-2 border-default-300 dark:border-default-200">
                    <CardHeader className="flex gap-2 px-5 pt-5">
                        <h2 className="text-xl font-semibold">Join a Room</h2>
                    </CardHeader>
                    <Divider/>
                    <CardBody className="flex flex-col gap-4 px-5 pb-5">
                        <Input
                            label="Room code"
                            placeholder="e.g. XK7P3N"
                            value={joinCode}
                            onValueChange={(v) => setJoinCode(v.toUpperCase())}
                            maxLength={6}
                            onKeyDown={(e) => e.key === 'Enter' && handleJoin()}
                        />
                        {!isAuthenticated && (
                            <Input
                                label="Your display name"
                                placeholder="Jane"
                                value={guestName}
                                onValueChange={setGuestName}
                                onKeyDown={(e) => e.key === 'Enter' && handleJoin()}
                            />
                        )}
                        <Button
                            color="secondary"
                            variant="flat"
                            isLoading={joining}
                            onPress={handleJoin}
                            isDisabled={!joinCode.trim()}
                            className="w-full"
                        >
                            Join Room
                        </Button>
                    </CardBody>
                </Card>
            </div>
        </div>
    );
}

