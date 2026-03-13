'use client';

import {useState} from "react";
import {Button, Divider, Input, Tooltip, addToast} from "@heroui/react";
import {SparklesIcon, ArrowLeftIcon} from "@heroicons/react/24/outline";
import {createGuestAndSignIn} from "@/lib/auth/create-guest";
import {useRouter} from "next/navigation";
import {generateRoomName} from "@/lib/room-name-generator";
import AvatarPicker from "@/components/AvatarPicker";
import {AVATAR_EYES, AVATAR_MOUTH} from "@/lib/avatar";

export default function CreateRoomForm({isAuthenticated}: { isAuthenticated: boolean }) {
    const router = useRouter();
    const [title, setTitle] = useState('');
    const [guestName, setGuestName] = useState('');
    const [avatarJson, setAvatarJson] = useState<string | null>(() => {
        const eyes = AVATAR_EYES[Math.floor(Math.random() * AVATAR_EYES.length)];
        const mouth = AVATAR_MOUTH[Math.floor(Math.random() * AVATAR_MOUTH.length)];
        return JSON.stringify({ eyes: [eyes], mouth: [mouth] });
    });
    const [creating, setCreating] = useState(false);

    function rollName() {
        setTitle(generateRoomName());
    }

    async function handleCreate() {
        if (!title.trim()) return;
        if (!isAuthenticated && !guestName.trim()) return;
        setCreating(true);
        try {
            if (!isAuthenticated) {
                const guestResult = await createGuestAndSignIn(guestName, avatarJson ?? undefined);
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
            // Hard navigate so TopNav re-renders with the updated session
            window.location.href = `/planning-poker/${room.roomId}`;
        } catch {
            addToast({title: 'Failed to create room', color: 'danger'});
        } finally {
            setCreating(false);
        }
    }

    return (
        <div className="min-h-full bg-content1">
            <div className="px-6 py-8 max-w-lg mx-auto">
                {/* Back link */}
                <button
                    onClick={() => router.push('/planning-poker')}
                    className="flex items-center gap-1 text-sm text-foreground-400 hover:text-foreground-600 mb-6 transition-colors"
                >
                    <ArrowLeftIcon className="h-4 w-4"/>
                    Back to Dashboard
                </button>

                {/* Header */}
                <div className="flex items-center gap-3 mb-8">
                    <SparklesIcon className="h-8 w-8 text-primary shrink-0"/>
                    <div>
                        <h1 className="text-2xl font-bold text-foreground-700">New Planning Poker Session</h1>
                        <p className="text-sm text-foreground-400 mt-0.5">
                            Set up your room and share the link with your team.
                        </p>
                    </div>
                </div>

                {/* Form card — no border, bg-content2 is the separator */}
                <div className="bg-content2 border border-content3 rounded-2xl shadow-raise-md p-5 flex flex-col gap-4">
                    <h2 className="text-lg font-semibold text-foreground-700">Room details</h2>
                    {!isAuthenticated && (
                        <>
                            <div>
                                <label className="text-xs font-semibold uppercase tracking-wide text-foreground-400 mb-2 block">
                                    Your name
                                </label>
                                <Input
                                    placeholder="e.g. Bob"
                                    value={guestName}
                                    onValueChange={setGuestName}
                                    description="You're not signed in — enter a display name to continue as a guest."
                                    autoFocus
                                />
                            </div>
                            <div>
                                <label className="text-xs font-semibold uppercase tracking-wide text-foreground-400 mb-3 block">
                                    Your avatar
                                </label>
                                <AvatarPicker seed={guestName.trim() || 'guest'} value={avatarJson} onChange={setAvatarJson}>
                                    {({avatarSrc, onOpen}) => (
                                        <div className="flex flex-col items-center gap-2">
                                            <button type="button" onClick={onOpen} className="group relative">
                                                <img src={avatarSrc} alt="Avatar"
                                                     className="h-16 w-16 rounded-full ring-2 ring-foreground-200 group-hover:ring-primary transition-all"/>
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
                        </>
                    )}
                    <Input
                        label="Room name"
                        placeholder="e.g. Sprint 42 estimation"
                        value={title}
                        onValueChange={setTitle}
                        onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
                        autoFocus={isAuthenticated}
                        description="No ideas? Hit 🎲 to generate a funny name."
                        endContent={
                            <Tooltip content="Generate a funny name" placement="top">
                                <button
                                    type="button"
                                    onClick={rollName}
                                    className="text-foreground-400 hover:text-primary transition-colors focus:outline-none"
                                    aria-label="Generate random room name"
                                >
                                    🎲
                                </button>
                            </Tooltip>
                        }
                    />
                    <Button
                        color="primary"
                        size="lg"
                        isLoading={creating}
                        onPress={handleCreate}
                        isDisabled={!title.trim() || (!isAuthenticated && !guestName.trim())}
                        className="w-full font-semibold"
                        startContent={!creating && <SparklesIcon className="h-5 w-5"/>}
                    >
                        Create Room
                    </Button>
                </div>
            </div>
        </div>
    );
}
