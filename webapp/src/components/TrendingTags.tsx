'use client';

import {getTrendingTags} from "@/lib/actions/tag-actions";
import Link from "next/link";
import {useEffect, useState} from "react";
import {TrendingTag} from "@/lib/types";
import {Flame} from "@/components/animated-icons/Flame";
import {Hash} from "lucide-react";
import {motion} from "framer-motion";

const BAR_COLORS = [
    'bg-linear-to-r from-violet-300/60 to-fuchsia-300/60',
    'bg-linear-to-r from-blue-300/60 to-sky-300/60',
    'bg-linear-to-r from-emerald-300/60 to-teal-300/60',
    'bg-linear-to-r from-orange-300/60 to-amber-200/60',
    'bg-linear-to-r from-rose-300/60 to-pink-300/60',
];

function SkeletonRow() {
    return (
        <div className="flex items-center gap-3 animate-pulse">
            <div className="w-6 h-4 rounded bg-default-200"/>
            <div className="flex-1 flex flex-col gap-2">
                <div className="flex justify-between">
                    <div className="h-3.5 w-20 rounded bg-default-200"/>
                    <div className="h-3.5 w-12 rounded bg-default-200"/>
                </div>
                <div className="h-2.5 w-full rounded-full bg-default-200"/>
            </div>
        </div>
    );
}

export default function TrendingTags() {
    const [tags, setTags] = useState<TrendingTag[] | null>(null);
    const [error, setError] = useState<boolean>(false);

    useEffect(() => {
        getTrendingTags().then(result => {
            if (result.error) setError(true);
            else setTags(result.data);
        });
    }, []);

    const maxCount = Array.isArray(tags) && tags.length > 0
        ? Math.max(...tags.map(t => t.count))
        : 1;

    return (
        <div className="rounded-2xl bg-content2 border border-content3 shadow-raise-sm overflow-hidden">
            {/* Header */}
            <div className="px-4 pt-4 pb-3 flex items-center gap-2.5 border-b border-content3">
                <span
                    className="flex items-center justify-center w-7 h-7 rounded-lg bg-orange-100/60 dark:bg-orange-900/20">
                    <Flame size={18} className="text-orange-300 dark:text-orange-400/70"/>
                </span>
                <h3 className="text-sm font-semibold text-foreground tracking-wide">Trending this week</h3>
            </div>

            {/* Body */}
            <div className="px-4 py-4 flex flex-col gap-3.5">
                {error ? (
                    <p className="text-sm text-default-400 text-center py-2">Could not load tags</p>
                ) : !tags ? (
                    Array.from({length: 5}).map((_, i) => <SkeletonRow key={i}/>)
                ) : tags.length === 0 ? (
                    <p className="text-sm text-default-400 text-center py-2">No data yet</p>
                ) : (
                    tags.map((tag, index) => {
                        const pct = (tag.count / maxCount) * 100;
                        const color = BAR_COLORS[index % BAR_COLORS.length];
                        return (
                            <div key={tag.tag} className="flex items-center gap-3 group">
                                {/* Rank */}
                                <span
                                    className="w-6 text-center text-sm font-bold text-default-400 shrink-0 tabular-nums">
                                    {index + 1}
                                </span>
                                <div className="flex-1 min-w-0">
                                    {/* Label row */}
                                    <div className="flex items-center justify-between mb-2">
                                        <Link
                                            href={`/questions?tag=${tag.tag}`}
                                            className="flex items-center gap-1 text-sm font-semibold text-foreground-600 hover:text-primary transition-colors truncate"
                                        >
                                            <Hash className="w-3.5 h-3.5 opacity-50 shrink-0"/>
                                            <span className="truncate">{tag.tag}</span>
                                        </Link>
                                        <span
                                            className="text-xs font-medium text-default-400 tabular-nums ml-2 shrink-0">
                                            {tag.count.toLocaleString()}
                                        </span>
                                    </div>
                                    {/* Bar track */}
                                    <div className="h-2 w-full rounded-full bg-content3 shadow-inset-sm overflow-hidden">
                                        <motion.div
                                            className={`h-full rounded-full ${color}`}
                                            initial={{width: 0}}
                                            animate={{width: `${pct}%`}}
                                            transition={{duration: 0.7, delay: index * 0.07, ease: [0.25, 1, 0.5, 1]}}
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