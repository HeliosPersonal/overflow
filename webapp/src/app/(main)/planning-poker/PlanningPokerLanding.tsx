'use client';

import {useEffect, useState} from "react";
import {useRouter} from "next/navigation";
import {
    AvatarGroup,
    Button,
    Chip,
    Dropdown,
    DropdownItem,
    DropdownMenu,
    DropdownTrigger,
    Spinner,
    Tooltip,
} from "@heroui/react";
import {Dices} from "@/components/animated-icons";
import {MoreVertical, Archive, Trash2} from "lucide-react";
import type {PlanningPokerRoomSummary} from "@/lib/types";
import {timeAgo} from "@/lib/util";
import {differenceInDays} from "date-fns";
import DiceBearAvatar from "@/components/DiceBearAvatar";

function retentionLabel(r: PlanningPokerRoomSummary): {text: string; color: 'warning' | 'danger' | 'default'} | null {
    if (r.status === 'Archived' && r.archivedAtUtc) {
        const daysLeft = r.archivedDaysBeforeDelete - differenceInDays(new Date(), new Date(r.archivedAtUtc));
        if (daysLeft <= 0) return {text: 'expires soon', color: 'danger'};
        return {text: `deletes in ${daysLeft}d`, color: 'warning'};
    }
    if (r.status !== 'Archived' && r.updatedAtUtc) {
        const inactiveDays = differenceInDays(new Date(), new Date(r.updatedAtUtc));
        const daysLeft = r.inactiveDaysBeforeArchive - inactiveDays;
        if (daysLeft <= 3 && daysLeft > 0) return {text: `auto-archives in ${daysLeft}d`, color: 'default'};
        if (daysLeft <= 0) return {text: 'archiving soon', color: 'warning'};
    }
    return null;
}

export default function PlanningPokerLanding({isAuthenticated}: {isAuthenticated: boolean}) {
    const router = useRouter();
    const [recentSessions, setRecentSessions] = useState<PlanningPokerRoomSummary[]>([]);
    const [sessionsLoading, setSessionsLoading] = useState(false);

    async function loadRooms() {
        await fetch('/api/estimation/claim-guest', {method: 'POST'}).catch(() => {});
        setSessionsLoading(true);
        try {
            // cache: 'no-store' prevents the browser from serving a stale response
            // after the user updates their avatar on the profile page.
            const res = await fetch('/api/estimation/rooms', {cache: 'no-store'});
            const data: PlanningPokerRoomSummary[] = res.ok ? await res.json() : [];
            setRecentSessions(data.slice(0, 10));
        } catch {
            // ignore
        } finally {
            setSessionsLoading(false);
        }
    }

    // Initial load on mount.
    useEffect(() => {
        if (!isAuthenticated) return;
        void loadRooms();
    }, [isAuthenticated]);

    // Re-fetch when the tab regains focus so avatars/display names are always
    // fresh after the user edits their profile in another tab or window.
    useEffect(() => {
        if (!isAuthenticated) return;

        const handleVisibilityChange = () => {
            if (!document.hidden) void loadRooms();
        };

        document.addEventListener('visibilitychange', handleVisibilityChange);
        return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
    }, [isAuthenticated]);

    async function handleArchive(roomId: string) {
        await fetch(`/api/estimation/rooms/${roomId}/archive`, {method: 'POST'});
        setRecentSessions(prev =>
            prev.map(r => r.roomId === roomId ? {...r, status: 'Archived', archivedAtUtc: new Date().toISOString()} : r)
        );
    }

    async function handleDelete(roomId: string) {
        await fetch(`/api/estimation/rooms/${roomId}`, {method: 'DELETE'});
        setRecentSessions(prev => prev.filter(r => r.roomId !== roomId));
    }

    return (
        <div className="min-h-full bg-content1">
        <div className="px-3 sm:px-6 py-4 sm:py-6 max-w-5xl mx-auto flex flex-col gap-5">

            {/* ── Page header ───────────────────────────────────────── */}
            <div className="flex items-center gap-3">
                <div>
                    <h1 className="text-2xl sm:text-3xl font-bold text-foreground-600">Planning Poker</h1>
                    <p className="text-foreground-500 text-sm">
                        Estimate as a team using Planning Poker — Fibonacci cards, live voting, instant results.
                    </p>
                </div>
            </div>

            {/* ── Start Planning Poker CTA ──────────────────────────── */}
            <div className="p-4 sm:p-5 rounded-2xl bg-content2 border border-content3 shadow-raise-sm flex flex-col sm:flex-row sm:items-center gap-4">
                <div className="flex-1">
                    <h2 className="text-lg sm:text-xl font-semibold text-foreground-700 mb-1">Ready to estimate?</h2>
                    <p className="text-sm text-foreground-500">
                        Create a new room, share the link with your team and start voting on story points.
                    </p>
                </div>
                <Button
                    color="primary"
                    size="lg"
                    startContent={<Dices size={20}/>}
                    onPress={() => router.push('/planning-poker/new')}
                    className="shrink-0 font-semibold w-full sm:w-auto"
                >
                    Start Planning Poker
                </Button>
            </div>

            {/* ── Recent Sessions ───────────────────────────────────── */}
            <div className="p-4 sm:p-5 rounded-2xl bg-content2 border border-content3 shadow-raise-sm">
                <h2 className="text-lg sm:text-xl font-semibold text-foreground-600 mb-1">Recent Sessions</h2>
                <p className="text-xs text-foreground-400 mb-4">
                    Inactive rooms are auto-archived after {recentSessions[0]?.inactiveDaysBeforeArchive ?? 10} days.
                    Archived rooms are permanently deleted after {recentSessions[0]?.archivedDaysBeforeDelete ?? 10} days.
                </p>

                {!isAuthenticated ? (
                    <p className="text-foreground-400 text-sm">
                        Sign in to see your recent sessions.
                    </p>
                ) : sessionsLoading ? (
                    <div className="flex justify-center py-10">
                        <Spinner size="md" label="Loading sessions…"/>
                    </div>
                ) : recentSessions.length === 0 ? (
                    <p className="text-foreground-400 text-sm">
                        No sessions yet. Create your first room above!
                    </p>
                ) : (
                    <>
                        {/* ── Desktop table (hidden on mobile) ── */}
                        <div className="hidden sm:block overflow-x-auto">
                            <table className="w-full text-sm">
                                <thead>
                                    <tr className="border-b border-content3 text-left text-xs text-foreground-400 uppercase tracking-wide">
                                        <th className="pb-2 pr-4 font-medium whitespace-nowrap">Created</th>
                                        <th className="pb-2 pr-4 font-medium">Creator</th>
                                        <th className="pb-2 pr-4 font-medium">Title</th>
                                        <th className="pb-2 pr-4 font-medium">Participants</th>
                                        <th className="pb-2 font-medium text-right">Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {recentSessions.map(r => {
                                        const label = retentionLabel(r);
                                        return (
                                            <tr
                                                key={r.roomId}
                                                className="border-b border-content3 last:border-0 hover:bg-content3 transition-colors cursor-pointer"
                                                onClick={() => router.push(`/planning-poker/${r.roomId}`)}
                                            >
                                                <td className="py-3 pr-4 whitespace-nowrap text-foreground-500">
                                                    <div>{timeAgo(r.createdAtUtc)}</div>
                                                    {label && (
                                                        <div className={`text-xs ${label.color === 'danger' ? 'text-danger' : label.color === 'warning' ? 'text-warning-500' : 'text-foreground-400'}`}>
                                                            {label.text}
                                                        </div>
                                                    )}
                                                </td>
                                                <td className="py-3 pr-4">
                                                    <Tooltip content={r.isModerator ? 'You' : r.creatorDisplayName} placement="top">
                                                        <span className="inline-flex">
                                                            <DiceBearAvatar avatarJson={r.creatorAvatarUrl} name={r.creatorDisplayName} size="sm"/>
                                                        </span>
                                                    </Tooltip>
                                                </td>
                                                <td className="py-3 pr-4">
                                                    <div className="flex items-center gap-2">
                                                        <span className="font-semibold text-foreground-700 truncate max-w-[220px]">{r.title}</span>
                                                        <RoomStatusChip status={r.status}/>
                                                    </div>
                                                </td>
                                                <td className="py-3 pr-4">
                                                    <AvatarGroup max={5} size="sm">
                                                        {r.participants.map((p, i) => (
                                                            <Tooltip key={i} content={p.displayName} placement="top">
                                                                <span className="inline-flex">
                                                                    <DiceBearAvatar avatarJson={p.avatarUrl} name={p.displayName} size="sm"/>
                                                                </span>
                                                            </Tooltip>
                                                        ))}
                                                    </AvatarGroup>
                                                </td>
                                                <td className="py-3 text-right" onClick={e => e.stopPropagation()}>
                                                    <Dropdown placement="bottom-end">
                                                        <DropdownTrigger>
                                                            <Button isIconOnly size="sm" variant="light" aria-label="Room actions">
                                                                <MoreVertical className="h-4 w-4 text-foreground-400"/>
                                                            </Button>
                                                        </DropdownTrigger>
                                                        <DropdownMenu aria-label="Room actions">
                                                            <DropdownItem key="archive" startContent={<Archive className="h-4 w-4"/>}
                                                                isDisabled={r.status === 'Archived'} onPress={() => handleArchive(r.roomId)}>
                                                                Archive
                                                            </DropdownItem>
                                                            <DropdownItem key="delete" startContent={<Trash2 className="h-4 w-4"/>}
                                                                className="text-danger" color="danger" onPress={() => handleDelete(r.roomId)}>
                                                                Delete
                                                            </DropdownItem>
                                                        </DropdownMenu>
                                                    </Dropdown>
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>
                        </div>

                        {/* ── Mobile card list (hidden on desktop) ── */}
                        <div className="sm:hidden flex flex-col gap-3">
                            {recentSessions.map(r => {
                                const label = retentionLabel(r);
                                return (
                                    <div
                                        key={r.roomId}
                                        className="bg-content3/50 border border-content3 rounded-xl p-3 cursor-pointer active:bg-content3 transition-colors"
                                        onClick={() => router.push(`/planning-poker/${r.roomId}`)}
                                    >
                                        <div className="flex items-start justify-between gap-2 mb-2">
                                            <div className="min-w-0 flex-1">
                                                <div className="flex items-center gap-2 mb-0.5">
                                                    <span className="font-semibold text-foreground-700 truncate">{r.title}</span>
                                                    <RoomStatusChip status={r.status}/>
                                                </div>
                                                <div className="text-xs text-foreground-400">
                                                    {timeAgo(r.createdAtUtc)}
                                                    {label && (
                                                        <span className={`ml-2 ${label.color === 'danger' ? 'text-danger' : label.color === 'warning' ? 'text-warning-500' : ''}`}>
                                                            · {label.text}
                                                        </span>
                                                    )}
                                                </div>
                                            </div>
                                            <div onClick={e => e.stopPropagation()}>
                                                <Dropdown placement="bottom-end">
                                                    <DropdownTrigger>
                                                        <Button isIconOnly size="sm" variant="light" aria-label="Room actions">
                                                            <MoreVertical className="h-4 w-4 text-foreground-400"/>
                                                        </Button>
                                                    </DropdownTrigger>
                                                    <DropdownMenu aria-label="Room actions">
                                                        <DropdownItem key="archive" startContent={<Archive className="h-4 w-4"/>}
                                                            isDisabled={r.status === 'Archived'} onPress={() => handleArchive(r.roomId)}>
                                                            Archive
                                                        </DropdownItem>
                                                        <DropdownItem key="delete" startContent={<Trash2 className="h-4 w-4"/>}
                                                            className="text-danger" color="danger" onPress={() => handleDelete(r.roomId)}>
                                                            Delete
                                                        </DropdownItem>
                                                    </DropdownMenu>
                                                </Dropdown>
                                            </div>
                                        </div>
                                        <div className="flex items-center gap-2 mt-1 ml-1">
                                            <AvatarGroup max={4} size="sm">
                                                {r.participants.map((p, i) => (
                                                    <Tooltip key={i} content={p.displayName} placement="top">
                                                        <span className="inline-flex">
                                                            <DiceBearAvatar avatarJson={p.avatarUrl} name={p.displayName} size="sm"/>
                                                        </span>
                                                    </Tooltip>
                                                ))}
                                            </AvatarGroup>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    </>
                )}
            </div>
        </div>
        </div>
    );
}

function RoomStatusChip({status}: {status: string}) {
    const colorMap: Record<string, 'success' | 'secondary' | 'warning'> = {
        Voting: 'success',
        Revealed: 'secondary',
        Archived: 'warning',
    };
    return <Chip size="sm" color={colorMap[status] ?? 'default'} variant="bordered">{status}</Chip>;
}

