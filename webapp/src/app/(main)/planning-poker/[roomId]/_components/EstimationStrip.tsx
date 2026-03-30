'use client';

import {useEffect, useState} from "react";
import {Chip, Tooltip} from "@heroui/react";
import DiceBearAvatar from "@/components/DiceBearAvatar";
import {celebrationColors} from "@/lib/theme/colors";
import confetti from 'canvas-confetti';
import type {PlanningPokerParticipant, PlanningPokerRoom} from "@/lib/types";
import {CONFETTI_DURATION_MS, VOTER_AVATAR_PREVIEW_LIMIT} from "./room-constants";
import {getConsensusInfo, getRoundLabel} from "./room-helpers";

export default function CompactEstimationStrip({room, hasTasks, isVoting, isRevealed, isArchived, activeParticipants, votedCount, allVoted}: {
    room: PlanningPokerRoom; hasTasks: boolean; isVoting: boolean; isRevealed: boolean; isArchived: boolean;
    activeParticipants: PlanningPokerParticipant[]; votedCount: number; allVoted: boolean;
}) {
    const totalActive = activeParticipants.length;
    const votePct = totalActive > 0 ? Math.round((votedCount / totalActive) * 100) : 0;
    const progressBarColor = isRevealed ? 'bg-primary' : isArchived ? 'bg-warning' : 'bg-success';

    const summary = room.roundSummary;
    const hasResults = (isRevealed || isArchived) && summary?.distribution && Object.keys(summary.distribution).length > 0;
    const distribution = hasResults ? summary!.distribution! : {};
    const sorted = Object.entries(distribution).sort(([, a], [, b]) => b - a);
    const maxCount = sorted.length > 0 ? Math.max(...sorted.map(([, c]) => c)) : 0;
    const totalVotes = sorted.reduce((sum, [, c]) => sum + c, 0);
    const uniqueValues = sorted.length;
    const isFullConsensus = uniqueValues === 1 && totalVotes > 1;

    const [resultsVisible, setResultsVisible] = useState(false);
    useEffect(() => {
        if (!hasResults) { setResultsVisible(false); return; }
        const t1 = requestAnimationFrame(() => setResultsVisible(true));
        return () => cancelAnimationFrame(t1);
    }, [hasResults]);

    useEffect(() => {
        if (!isFullConsensus) return;
        const themeColors = [...celebrationColors];
        const end = Date.now() + CONFETTI_DURATION_MS;
        const frame = () => {
            confetti({ particleCount: 3, angle: 55, spread: 60, origin: {x: 0, y: 0.6}, colors: themeColors });
            confetti({ particleCount: 3, angle: 125, spread: 60, origin: {x: 1, y: 0.6}, colors: themeColors });
            if (Date.now() < end) requestAnimationFrame(frame);
        };
        frame();
    }, [isFullConsensus, summary?.roundNumber]);

    const {label: consensusLabel, colorClass: consensusColor} = getConsensusInfo(uniqueValues, sorted[0]?.[1] ?? 0, totalVotes);

    const voteGroups: Record<string, PlanningPokerParticipant[]> = {};
    if (hasResults) {
        for (const p of activeParticipants) {
            const v = p.revealedVote ?? '—';
            if (!voteGroups[v]) voteGroups[v] = [];
            voteGroups[v].push(p);
        }
    }

    const roundLabel = getRoundLabel(room, hasTasks);

    return (
        <div className="rounded-xl bg-content2/80 border border-content3/60 overflow-hidden">
            <div className="px-3 sm:px-4 py-2 sm:py-2.5 flex items-center gap-2 sm:gap-3">
                <div className="min-w-0 flex items-center gap-2 sm:gap-2.5 flex-1">
                    <span className="text-sm sm:text-base font-bold text-foreground-600 truncate">{roundLabel}</span>
                    {hasTasks && (
                        <Chip size="sm" variant="bordered" color={isRevealed ? 'primary' : isArchived ? 'warning' : 'success'}
                              className="font-semibold tabular-nums text-xs h-6 shrink-0">
                            {room.roundHistory.length}/{room.tasks!.length}
                        </Chip>
                    )}
                </div>
                {totalActive > 0 && (
                    <VoteProgressDots participants={activeParticipants} votedCount={votedCount}
                        totalActive={totalActive} allVoted={allVoted}/>
                )}
            </div>

            {totalActive > 0 && (
                <div className="h-1.5 bg-content4">
                    <div className={`h-full transition-all duration-500 ${progressBarColor}`} style={{width: `${votePct}%`}}/>
                </div>
            )}

            <div className="min-h-[56px] sm:min-h-[68px] border-t border-content3/40">
                {hasResults ? (
                    <div className="px-3 sm:px-4 py-2 sm:py-2.5 flex flex-wrap items-center gap-2 sm:gap-3">
                        <div className="flex items-center gap-1.5 sm:gap-2.5 flex-1 flex-wrap">
                            {sorted.map(([value, count], index) => (
                                <DistributionCard key={value} value={value} count={count} totalVotes={totalVotes}
                                    isTop={count === maxCount} voters={voteGroups[value] ?? []}
                                    animationDelay={100 + index * 60} visible={resultsVisible}/>
                            ))}
                        </div>
                        <div className="flex items-center gap-2.5 shrink-0">
                            {summary!.numericAverageDisplay && (
                                <span className="text-2xl font-black tabular-nums text-warning leading-none">
                                    {summary!.numericAverageDisplay}
                                </span>
                            )}
                            <span className={`text-xs font-semibold ${consensusColor}`}>{consensusLabel}</span>
                        </div>
                    </div>
                ) : (
                    <div className="px-4 py-2.5 flex items-center min-h-[48px]">
                        <span className="text-xs text-foreground-300 italic">
                            {isVoting ? 'Results will appear here after reveal' : '\u00A0'}
                        </span>
                    </div>
                )}
            </div>
        </div>
    );
}

function VoteProgressDots({participants, votedCount, totalActive, allVoted}: {
    participants: PlanningPokerParticipant[]; votedCount: number; totalActive: number; allVoted: boolean;
}) {
    return (
        <div className="flex items-center gap-2.5 shrink-0">
            <div className="flex items-center gap-1">
                {participants.map(p => (
                    <div key={p.participantId}
                         className={`w-2 h-2 rounded-full transition-colors duration-300 ${p.hasVoted ? 'bg-success' : 'bg-default-300'}`}/>
                ))}
            </div>
            <span className={`text-sm font-semibold tabular-nums ${allVoted ? 'text-success' : 'text-foreground-500'}`}>
                {votedCount}/{totalActive}
            </span>
        </div>
    );
}

function DistributionCard({value, count, totalVotes, isTop, voters, animationDelay, visible}: {
    value: string; count: number; totalVotes: number; isTop: boolean;
    voters: PlanningPokerParticipant[]; animationDelay: number; visible: boolean;
}) {
    const pct = totalVotes > 0 ? Math.round((count / totalVotes) * 100) : 0;
    return (
        <div className="flex items-center gap-2"
             style={{opacity: visible ? 1 : 0, transition: `opacity 250ms ease-out ${animationDelay}ms`}}>
            <div className={`w-9 h-12 rounded-lg border-2 flex items-center justify-center text-sm font-black
                ${isTop ? 'border-primary/60 bg-primary/10 text-primary' : 'border-content4 bg-content3/40 text-foreground-600'}`}>
                {value}
            </div>
            <div className="flex flex-col gap-0.5">
                <span className={`text-xs font-bold tabular-nums leading-none ${isTop ? 'text-primary' : 'text-foreground-500'}`}>
                    {pct}%
                </span>
                {voters.length > 0 && <VoterAvatars voters={voters}/>}
            </div>
        </div>
    );
}

function VoterAvatars({voters}: { voters: PlanningPokerParticipant[] }) {
    const visible = voters.slice(0, VOTER_AVATAR_PREVIEW_LIMIT);
    const overflow = voters.length - VOTER_AVATAR_PREVIEW_LIMIT;
    return (
        <div className="flex -space-x-1">
            {visible.map(p => (
                <Tooltip key={p.participantId} content={p.displayName}>
                    <span className="inline-flex">
                        <DiceBearAvatar userId={p.participantId} avatarJson={p.avatarUrl}
                            name={p.displayName} className="h-5 w-5" borderClass="border border-content2"/>
                    </span>
                </Tooltip>
            ))}
            {overflow > 0 && <span className="text-[10px] text-foreground-400 pl-1">+{overflow}</span>}
        </div>
    );
}

