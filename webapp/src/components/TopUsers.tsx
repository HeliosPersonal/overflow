'use client';

import {getTopUsers} from "@/lib/actions/profile-actions";
import {useEffect, useState} from "react";
import {TopUserWithProfile} from "@/lib/types";
import {Trophy, TrendingUp} from "@/components/animated-icons";
import {motion} from "framer-motion";
import Link from "next/link";
import DiceBearAvatar from "@/components/DiceBearAvatar";

const RANK_STYLES = [
    {ring: 'ring-yellow-200', bg: 'bg-yellow-50/80 dark:bg-yellow-900/15', text: 'text-yellow-400 dark:text-yellow-300/70', medal: '🥇'},
    {ring: 'ring-slate-200', bg: 'bg-slate-50/80 dark:bg-slate-800/20', text: 'text-slate-400/80', medal: '🥈'},
    {ring: 'ring-amber-300/50', bg: 'bg-amber-50/70 dark:bg-amber-900/10', text: 'text-amber-500/70 dark:text-amber-400/60', medal: '🥉'},
];

const BAR_COLORS = [
    'bg-linear-to-r from-yellow-200/70 to-amber-300/60',
    'bg-linear-to-r from-slate-200/70 to-slate-300/60',
    'bg-linear-to-r from-amber-300/60 to-orange-300/60',
    'bg-linear-to-r from-blue-300/60 to-indigo-300/60',
    'bg-linear-to-r from-emerald-300/60 to-teal-300/60',
];

function getInitials(name: string) {
    return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2);
}

function SkeletonRow() {
    return (
        <div className="flex items-center gap-3 animate-pulse">
            <div className="w-10 h-10 rounded-full bg-default-200 shrink-0"/>
            <div className="flex-1 flex flex-col gap-2">
                <div className="flex justify-between">
                    <div className="h-3.5 w-24 rounded bg-default-200"/>
                    <div className="h-3.5 w-16 rounded bg-default-200"/>
                </div>
                <div className="h-2.5 w-full rounded-full bg-default-200"/>
            </div>
        </div>
    );
}

export default function TopUsers() {
    const [users, setUsers] = useState<TopUserWithProfile[] | null>(null);
    const [error, setError] = useState<boolean>(false);

    useEffect(() => {
        getTopUsers().then(result => {
            if (result.error) setError(true);
            else setUsers(result.data);
        });
    }, []);

    const filteredUsers = Array.isArray(users) ? users.filter(u => u.profile) : [];
    const maxDelta = filteredUsers.length > 0 ? Math.max(...filteredUsers.map(u => u.delta)) : 1;

    return (
        <div className="rounded-2xl bg-content2 border border-content3 shadow-raise-sm overflow-hidden">
            {/* Header */}
            <div className="px-6 pt-6 pb-4 flex items-center gap-2.5 border-b border-content3">
                <span className="flex items-center justify-center w-8 h-8 rounded-lg bg-yellow-100/60 dark:bg-yellow-900/20">
                    <Trophy size={20} className="text-yellow-300 dark:text-yellow-400/60"/>
                </span>
                <h3 className="text-base font-semibold text-foreground tracking-wide">Most points this week</h3>
            </div>

            {/* Body */}
            <div className="px-6 py-5 flex flex-col gap-4">
                {error ? (
                    <p className="text-sm text-default-400 text-center py-2">Could not load data</p>
                ) : !users ? (
                    Array.from({length: 5}).map((_, i) => <SkeletonRow key={i}/>)
                ) : filteredUsers.length === 0 ? (
                    <p className="text-sm text-default-400 text-center py-2">No data yet</p>
                ) : (
                    filteredUsers.map((u, index) => {
                        const pct = (u.delta / maxDelta) * 100;
                        const rank = RANK_STYLES[index] ?? null;
                        const barColor = BAR_COLORS[index % BAR_COLORS.length];
                        const initials = getInitials(u.profile!.displayName);

                        return (
                            <div key={u.userId} className="flex items-center gap-3 group">
                                {/* Avatar */}
                                <Link href={`/profiles/${u.userId}`} className="shrink-0">
                                    <div className={`
                                        relative w-10 h-10 rounded-full
                                        ring-2 ring-offset-1 ring-offset-content2
                                        ${rank ? `${rank.ring}` : 'ring-default-300'}
                                        transition-transform group-hover:scale-105
                                    `}>
                                        <DiceBearAvatar
                                            userId={u.userId}
                                            avatarJson={u.profile!.avatarUrl}
                                            className="w-10 h-10"
                                            name={initials}
                                        />
                                        {index < 3 && (
                                            <span className="absolute -bottom-1 -right-1 text-xs leading-none">{rank!.medal}</span>
                                        )}
                                    </div>
                                </Link>

                                {/* Info */}
                                <div className="flex-1 min-w-0">
                                    <div className="flex items-center justify-between mb-2">
                                        <Link
                                            href={`/profiles/${u.userId}`}
                                            className="text-sm font-semibold text-foreground-600 hover:text-primary transition-colors truncate"
                                        >
                                            {u.profile!.displayName}
                                        </Link>
                                        <span className="flex items-center gap-0.5 text-xs font-semibold text-emerald-400/70 dark:text-emerald-400/50 tabular-nums ml-2 shrink-0">
                                            <TrendingUp size={14} />
                                            +{u.delta.toLocaleString()}
                                        </span>
                                    </div>
                                    {/* Bar track */}
                                    <div className="h-2 w-full rounded-full bg-content3 shadow-inset-sm overflow-hidden">
                                        <motion.div
                                            className={`h-full rounded-full ${barColor}`}
                                            initial={{width: 0}}
                                            animate={{width: `${pct}%`}}
                                            transition={{duration: 0.7, delay: index * 0.08, ease: [0.25, 1, 0.5, 1]}}
                                        />
                                    </div>
                                </div>
                            </div>
                        );
                    })
                )}
            </div>
        </div>
    );
}