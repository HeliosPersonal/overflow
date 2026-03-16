'use client';

import {useEffect, useState} from "react";
import {useRouter} from "next/navigation";
import {Button, Chip, Spinner, Tooltip} from "@heroui/react";
import {SparklesIcon, UserGroupIcon, HashtagIcon, ClockIcon} from "@heroicons/react/24/outline";
import {FlagIcon} from "@heroicons/react/24/solid";
import type {PlanningPokerRoomSummary} from "@/lib/types";
import {timeAgo} from "@/lib/util";
import {differenceInDays} from "date-fns";

function roomAge(r: PlanningPokerRoomSummary): string {
    const created = `Created ${timeAgo(r.createdAtUtc)}`;

    if (r.status === 'Archived' && r.archivedAtUtc) {
        const daysLeft = r.retentionDays - differenceInDays(new Date(), new Date(r.archivedAtUtc));
        if (daysLeft <= 0) return `${created} · expires soon`;
        return `${created} · auto-deletes in ${daysLeft}d`;
    }

    return created;
}

export default function PlanningPokerLanding({isAuthenticated}: {isAuthenticated: boolean}) {
    const router = useRouter();
    const [recentSessions, setRecentSessions] = useState<PlanningPokerRoomSummary[]>([]);
    const [sessionsLoading, setSessionsLoading] = useState(false);

    useEffect(() => {
        if (!isAuthenticated) return;

        async function loadRooms() {
            await fetch('/api/estimation/claim-guest', {method: 'POST'}).catch(() => {});
            setSessionsLoading(true);
            try {
                const res = await fetch('/api/estimation/rooms');
                const data: PlanningPokerRoomSummary[] = res.ok ? await res.json() : [];
                setRecentSessions(data.slice(0, 10));
            } catch {
                // ignore
            } finally {
                setSessionsLoading(false);
            }
        }

        void loadRooms();
    }, [isAuthenticated]);

    return (
        <div className="min-h-full bg-content1">
        <div className="px-6 py-8 max-w-4xl mx-auto flex flex-col gap-6">

            {/* ── Page header ───────────────────────────────────────── */}
            <div className="flex items-center gap-3">
                <div>
                    <h1 className="text-3xl font-bold text-foreground-600">Dashboard</h1>
                    <p className="text-foreground-500 text-sm">
                        Estimate as a team using Planning Poker — Fibonacci cards, live voting, instant results.
                    </p>
                </div>
            </div>

            {/* ── Start Planning Poker CTA ──────────────────────────── */}
            <div className="p-6 rounded-2xl bg-content2 border border-content3 shadow-raise-sm flex flex-col sm:flex-row sm:items-center gap-4">
                <div className="flex-1">
                    <h2 className="text-xl font-semibold text-foreground-700 mb-1">Ready to estimate?</h2>
                    <p className="text-sm text-foreground-500">
                        Create a new room, share the link with your team and start voting on story points.
                    </p>
                </div>
                <Button
                    color="primary"
                    size="lg"
                    startContent={<SparklesIcon className="h-5 w-5"/>}
                    onPress={() => router.push('/planning-poker/new')}
                    className="shrink-0 font-semibold"
                >
                    Start Planning Poker
                </Button>
            </div>

            {/* ── Recent Sessions ───────────────────────────────────── */}
            <div className="p-6 rounded-2xl bg-content2 border border-content3 shadow-raise-sm">
                <h2 className="text-xl font-semibold text-foreground-600 mb-1">Recent Sessions</h2>
                <p className="text-xs text-foreground-400 mb-4">
                    Archived rooms are automatically deleted after {recentSessions[0]?.retentionDays ?? 30} days.
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
                    <div className="flex flex-col gap-3">
                        {recentSessions.map(r => (
                            <div
                                key={r.roomId}
                                className="flex items-center gap-4 p-4 rounded-xl bg-content3 border border-content4 hover:bg-content4 transition-colors duration-150 cursor-pointer"
                                onClick={() => router.push(`/planning-poker/${r.roomId}`)}
                            >
                                {/* Title + status */}
                                <div className="flex-1 min-w-0">
                                    <div className="flex items-center gap-2 mb-1">
                                        <span className="font-semibold text-2xl text-foreground-700 truncate">{r.title}</span>
                                        {r.isModerator && (
                                            <Tooltip content="You are the moderator">
                                                <FlagIcon className="h-4 w-4 text-warning flex-shrink-0"/>
                                            </Tooltip>
                                        )}
                                    </div>
                                    <RoomStatusChip status={r.status}/>
                                    <span className="text-xs text-foreground-400 flex items-center gap-1 mt-1">
                                        <ClockIcon className="h-3.5 w-3.5"/>
                                        {roomAge(r)}
                                    </span>
                                </div>

                                {/* Stat blocks */}
                                <div className="flex items-center gap-3 shrink-0">
                                    <div className="flex flex-col items-center justify-center w-18 h-18 rounded-lg bg-content2 border border-content4">
                                        <UserGroupIcon className="h-5 w-5 text-foreground-400 mb-0.5"/>
                                        <span className="text-xl font-bold text-foreground-700">{r.participantCount}</span>
                                        <span className="text-x text-foreground-400 leading-none">players</span>
                                    </div>
                                    <div className="flex flex-col items-center justify-center w-18 h-18 rounded-lg bg-content2 border border-content4">
                                        <HashtagIcon className="h-5 w-5 text-foreground-400 mb-0.5"/>
                                        <span className="text-xl font-bold text-foreground-700">{r.completedRounds}</span>
                                        <span className="text-xs text-foreground-400 leading-none">rounds</span>
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
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
    return <Chip size="sm" color={colorMap[status] ?? 'default'} variant="flat">{status}</Chip>;
}

