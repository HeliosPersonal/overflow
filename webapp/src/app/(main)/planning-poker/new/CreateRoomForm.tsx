'use client';

import {useState} from "react";
import {Button, Divider, Input, Switch, Tooltip, addToast} from "@heroui/react";
import {ArrowLeft, List} from "lucide-react";
import {Dices} from "@/components/animated-icons";
import {Textarea} from "@heroui/input";
import {createGuestAndSignIn} from "@/lib/auth/create-guest";
import {useRouter} from "next/navigation";
import {generateRoomName} from "@/lib/room-name-generator";
import AvatarPicker from "@/components/AvatarPicker";
import {AVATAR_EYES, AVATAR_MOUTH} from "@/lib/avatar";

export default function CreateRoomForm({isAuthenticated}: { isAuthenticated: boolean }) {
    const router = useRouter();
    const MAX_TITLE_LENGTH = 80;
    const [title, setTitle] = useState('');
    const [deckType, setDeckType] = useState('fibonacci');
    const [guestName, setGuestName] = useState('');
    const [avatarJson, setAvatarJson] = useState<string | null>(() => {
        const eyes = AVATAR_EYES[Math.floor(Math.random() * AVATAR_EYES.length)];
        const mouth = AVATAR_MOUTH[Math.floor(Math.random() * AVATAR_MOUTH.length)];
        return JSON.stringify({ eyes: [eyes], mouth: [mouth] });
    });
    const [creating, setCreating] = useState(false);
    const [hasTaskList, setHasTaskList] = useState(false);
    const [taskListText, setTaskListText] = useState('');

    function rollName() {
        setTitle(generateRoomName());
    }

    async function handleCreate() {
        if (!title.trim()) return;
        if (title.trim().length > MAX_TITLE_LENGTH) return;
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

            const tasks = hasTaskList
                ? taskListText.split('\n').map(t => t.trim()).filter(Boolean)
                : undefined;

            const res = await fetch('/api/estimation/rooms', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({title: title.trim(), deckType, tasks}),
            });

            if (!res.ok) {
                const err = await res.json().catch(() => null);
                const msg = typeof err === 'string' ? err
                    : err?.message || err?.error || 'Failed to create room';
                addToast({title: msg, color: 'danger'});
                return;
            }

            const room = await res.json();
            if (!isAuthenticated) {
                // Guest just signed in — hard navigate so server components
                // (TopNav layout) re-render with the new authenticated session
                window.location.href = `/planning-poker/${room.roomId}`;
            } else {
                // Already authenticated — smooth SPA navigation
                router.push(`/planning-poker/${room.roomId}`);
            }
        } catch {
            addToast({title: 'Failed to create room', color: 'danger'});
        } finally {
            setCreating(false);
        }
    }

    return (
        <div className="min-h-full bg-content1">
            <div className="px-6 py-6 max-w-2xl mx-auto">
                {/* Back link */}
                <button
                    onClick={() => router.push('/planning-poker')}
                    className="flex items-center gap-1.5 text-sm text-foreground-400 hover:text-foreground-600 mb-5 transition-colors"
                >
                    <ArrowLeft className="h-4 w-4"/>
                    Back to Planning Poker
                </button>

                {/* Header */}
                <div className="mb-6">
                    <h1 className="text-3xl font-bold text-foreground-700">New Planning Poker Session</h1>
                    <p className="text-base text-foreground-400 mt-1">
                        Set up your room and share the link with your team.
                    </p>
                </div>

                {/* Form card */}
                <div className="bg-content2 border border-content3 rounded-2xl shadow-raise-md p-6 flex flex-col gap-5">
                    <h2 className="text-xl font-semibold text-foreground-700">Room details</h2>
                    {!isAuthenticated && (
                        <>
                            <div>
                                <label className="text-sm font-semibold uppercase tracking-wide text-foreground-400 mb-2 block">
                                    Your name
                                </label>
                                <Input
                                    size="lg"
                                    placeholder="e.g. Bob"
                                    value={guestName}
                                    onValueChange={setGuestName}
                                    description="You're not signed in — enter a display name to continue as a guest."
                                    autoFocus
                                />
                            </div>
                            <div>
                                <label className="text-sm font-semibold uppercase tracking-wide text-foreground-400 mb-3 block">
                                    Your avatar
                                </label>
                                <AvatarPicker seed={guestName.trim() || 'guest'} value={avatarJson} onChange={setAvatarJson}>
                                    {({avatarSrc, onOpen}) => (
                                        <div className="flex flex-col items-center gap-2">
                                            <button type="button" onClick={onOpen} className="group relative">
                                                <img src={avatarSrc} alt="Avatar"
                                                     className="h-20 w-20 rounded-full border-2 border-foreground-200 group-hover:border-primary transition-all"/>
                                                <span className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity text-white text-sm font-medium">
                                                    Edit
                                                </span>
                                            </button>
                                            <button type="button" onClick={onOpen}
                                                className="text-sm text-primary hover:underline">
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
                        size="lg"
                        label="Room name"
                        placeholder="e.g. Sprint 42 estimation"
                        value={title}
                        onValueChange={v => setTitle(v.slice(0, MAX_TITLE_LENGTH))}
                        onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
                        autoFocus={isAuthenticated}
                        maxLength={MAX_TITLE_LENGTH}
                        isInvalid={title.trim().length > MAX_TITLE_LENGTH}
                        errorMessage={`Room name must be ${MAX_TITLE_LENGTH} characters or fewer`}
                        description={
                            <span className={title.length >= MAX_TITLE_LENGTH ? 'text-danger' : ''}>
                                {title.length}/{MAX_TITLE_LENGTH}{title.length < MAX_TITLE_LENGTH && ' · No ideas? Hit the dice to generate a funny name.'}
                            </span>
                        }
                        endContent={
                            <Tooltip content="Generate a funny name" placement="top">
                                <button
                                    type="button"
                                    onClick={rollName}
                                    className="text-foreground-400 hover:text-primary transition-colors focus:outline-none"
                                    aria-label="Generate random room name"
                                >
                                    <Dices size={28} />
                                </button>
                            </Tooltip>
                        }
                    />
                    <div>
                        <label className="text-sm font-semibold uppercase tracking-wide text-foreground-400 mb-3 block">
                            Deck type
                        </label>
                        <div className="grid grid-cols-2 gap-3">
                            {[
                                {id: 'fibonacci', name: 'Fibonacci', preview: '0, 1, 2, 3, 5, 8, 13, 21…'},
                                {id: 'tshirts', name: 'T-Shirts', preview: 'XS, S, M, L, XL, XXL'},
                            ].map(d => (
                                <button
                                    key={d.id}
                                    type="button"
                                    onClick={() => setDeckType(d.id)}
                                    className={`flex flex-col items-center gap-1.5 p-5 rounded-xl border-2 transition-all text-center
                                        ${deckType === d.id
                                            ? 'border-primary bg-primary/10 shadow-raise-sm'
                                            : 'border-content4 bg-content3 hover:border-foreground-300'}`}
                                >
                                    <span className={`text-base font-semibold ${deckType === d.id ? 'text-primary' : 'text-foreground-700'}`}>
                                        {d.name}
                                    </span>
                                    <span className="text-xs text-foreground-400">{d.preview}</span>
                                </button>
                            ))}
                        </div>
                    </div>
                    <div>
                        <div className="flex items-center justify-between mb-3">
                            <label className="text-sm font-semibold uppercase tracking-wide text-foreground-400 flex items-center gap-1.5">
                                <List className="h-4 w-4"/>
                                Task list
                            </label>
                            <Switch
                                size="sm"
                                isSelected={hasTaskList}
                                onValueChange={setHasTaskList}
                                aria-label="Enable task list"
                            />
                        </div>
                        {hasTaskList && (
                            <div className="flex flex-col gap-2">
                                <Textarea
                                    placeholder={"Login page\nDashboard redesign\nAPI integration\nUser profile"}
                                    value={taskListText}
                                    onValueChange={setTaskListText}
                                    minRows={4}
                                    maxRows={12}
                                    description={(() => {
                                        const count = taskListText.split('\n').map(t => t.trim()).filter(Boolean).length;
                                        return `${count} task${count !== 1 ? 's' : ''} · One task per line. Each task becomes a voting round.`;
                                    })()}
                                />
                            </div>
                        )}
                    </div>
                    <Button
                        color="primary"
                        size="lg"
                        isLoading={creating}
                        onPress={handleCreate}
                        isDisabled={!title.trim() || title.trim().length > MAX_TITLE_LENGTH || (!isAuthenticated && !guestName.trim())}
                        className="w-full font-semibold text-base mt-2"
                    >
                        Create Room
                    </Button>
                </div>
            </div>
        </div>
    );
}
