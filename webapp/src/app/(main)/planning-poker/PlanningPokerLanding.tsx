'use client';

import {useEffect, useState} from "react";
import {useRouter} from "next/navigation";
import {Button, Chip, Spinner, Table, TableBody, TableCell, TableColumn, TableHeader, TableRow} from "@heroui/react";
import {SparklesIcon, UserGroupIcon, HashtagIcon} from "@heroicons/react/24/outline";
import type {PlanningPokerRoomSummary} from "@/lib/types";

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
                <SparklesIcon className="h-8 w-8 text-primary shrink-0"/>
                <div>
                    <h1 className="text-3xl font-bold text-foreground-600">Dashboard</h1>
                    <p className="text-foreground-500 text-sm">
                        Estimate as a team using Planning Poker — Fibonacci cards, live voting, instant results.
                    </p>
                </div>
            </div>

            {/* ── Start Planning Poker CTA ──────────────────────────── */}
            <div className="p-6 rounded-2xl bg-content2 border border-content3 shadow-raise-md flex flex-col sm:flex-row sm:items-center gap-4">
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
                <p className="text-xs text-foreground-400 mb-4">Archived rooms are automatically deleted after 30 days.</p>

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
                    <div className="rounded-xl overflow-hidden border border-content4">
                    <Table
                        aria-label="Recent planning poker sessions"
                        selectionMode="single"
                        onRowAction={(key) => router.push(`/planning-poker/${key}`)}
                        classNames={{
                            base: 'bg-content3',
                            tr: 'cursor-pointer bg-content3 hover:bg-content4 transition-colors duration-150',
                            th: 'bg-content4 text-foreground-500 uppercase text-xs tracking-wide border-b border-content4',
                            td: 'text-foreground-600 border-b border-content4/60',
                        }}
                        removeWrapper
                    >
                        <TableHeader>
                            <TableColumn>TITLE</TableColumn>
                            <TableColumn>STATUS</TableColumn>
                            <TableColumn>
                                <span className="flex items-center gap-1">
                                    <UserGroupIcon className="h-4 w-4"/>PARTICIPANTS
                                </span>
                            </TableColumn>
                            <TableColumn>
                                <span className="flex items-center gap-1">
                                    <HashtagIcon className="h-4 w-4"/>ROUNDS
                                </span>
                            </TableColumn>
                        </TableHeader>
                        <TableBody>
                            {recentSessions.map(r => (
                                <TableRow key={r.roomId}>
                                    <TableCell>
                                        <span className="font-medium">{r.title}</span>
                                    </TableCell>
                                    <TableCell>
                                        <RoomStatusChip status={r.status}/>
                                    </TableCell>
                                    <TableCell>{r.participantCount}</TableCell>
                                    <TableCell>{r.completedRounds}</TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                    </div>
                )}
            </div>
        </div>
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

