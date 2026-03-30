'use client';

import {useMemo} from "react";
import {Button, Chip, Tooltip} from "@heroui/react";
import {Eye, Archive, RefreshCw} from "lucide-react";
import {CheckCircle, Crown} from "lucide-react";
import {motion} from "framer-motion";
import DiceBearAvatar from "@/components/DiceBearAvatar";
import type {PlanningPokerParticipant, PlanningPokerRoom} from "@/lib/types";
import {type SceneDimensions, ICON_SM, SPECTATOR_CARD_W} from "./room-constants";
import {getRoundLabel} from "./room-helpers";

// ── PokerTableScene ──────────────────────────────────────────────────────────

export function PokerTableScene({participants, viewerParticipantId, isVoting, isRevealed, isModerator, isArchived, hasAnyVotes, allVoted, actionLoading, onReveal, onRevote, onReset, hasTasks, room, scene}: {
    participants: PlanningPokerParticipant[];
    viewerParticipantId: string;
    isVoting: boolean;
    isRevealed: boolean;
    isModerator: boolean;
    isArchived: boolean;
    hasAnyVotes: boolean;
    allVoted: boolean;
    actionLoading: string | null;
    onReveal: () => void;
    onRevote: () => void;
    onReset: () => void;
    hasTasks: boolean;
    room: PlanningPokerRoom;
    scene: SceneDimensions;
}) {
    const {orbitRx, orbitRy} = scene;

    const seats = useMemo(() => {
        const count = participants.length;
        if (count === 0) return [];

        return participants.map((p, i) => {
            const deg = count === 1 ? 90 : 90 - (i / count) * 360;
            const rad = (deg * Math.PI) / 180;
            const ox = Math.cos(rad) * orbitRx;
            const oy = -Math.sin(rad) * orbitRy;
            const dist = Math.sqrt(ox * ox + oy * oy) || 1;
            const nx = ox / dist;
            const ny = oy / dist;
            return {participant: p, ox, oy, nx, ny, deg};
        });
    }, [participants, orbitRx, orbitRy]);

    if (participants.length === 0) {
        return (
            <div className="flex flex-col items-center py-6">
                <p className="text-sm text-foreground-400">No active voters yet</p>
            </div>
        );
    }

    return (
        <div className="absolute inset-0">
            <CenterTable
                room={room} hasTasks={hasTasks} isModerator={isModerator} isArchived={isArchived}
                isVoting={isVoting} isRevealed={isRevealed} hasAnyVotes={hasAnyVotes} allVoted={allVoted}
                actionLoading={actionLoading} onReveal={onReveal} onRevote={onRevote} onReset={onReset} scene={scene}
            />
            {seats.map(({participant: p, ox, oy, nx, ny, deg}, i) => (
                <ParticipantSeat
                    key={p.participantId} participant={p}
                    ox={ox} oy={oy} nx={nx} ny={ny} deg={deg} index={i}
                    isViewer={p.participantId === viewerParticipantId}
                    isVoting={isVoting} isRevealed={isRevealed} scene={scene}
                />
            ))}
        </div>
    );
}

// ── CenterTable ──────────────────────────────────────────────────────────────

function CenterTable({room, hasTasks, isModerator, isArchived, isVoting, isRevealed, hasAnyVotes, allVoted, actionLoading, onReveal, onRevote, onReset, scene}: {
    room: PlanningPokerRoom; hasTasks: boolean; isModerator: boolean; isArchived: boolean;
    isVoting: boolean; isRevealed: boolean; hasAnyVotes: boolean; allVoted: boolean;
    actionLoading: string | null; onReveal: () => void; onRevote: () => void; onReset: () => void;
    scene: SceneDimensions;
}) {
    const {centerRx, centerRy, scale} = scene;
    const showRevealButton = isModerator && !isArchived && isVoting;
    const showRevealedActions = isModerator && !isArchived && isRevealed;
    const showWaitingMessage = (!isModerator || isArchived) && isVoting;

    const btnSize = scale >= 0.85 ? 'lg' as const : 'md' as const;
    const revealClass = scale >= 0.85
        ? 'font-bold px-8 h-12 text-base rounded-xl'
        : 'font-bold px-6 h-10 text-sm rounded-lg';

    return (
        <div
            className="absolute bg-gradient-to-br from-content2 via-content2 to-content3/50
                border border-content3/80
                shadow-[0_0_0_1px_rgba(255,255,255,0.04),0_8px_60px_rgba(0,0,0,0.12),0_2px_12px_rgba(0,0,0,0.06)]
                flex flex-col items-center justify-center gap-3"
            style={{
                left: '50%', top: '50%', transform: 'translate(-50%, -50%)',
                width: `${centerRx * 2}px`, height: `${centerRy * 2}px`, borderRadius: '50%',
            }}
        >
            <div className="text-xs font-semibold uppercase tracking-wider text-foreground-400 text-center leading-snug max-w-[70%] break-words">
                {getRoundLabel(room, hasTasks)}
            </div>

            {showRevealButton && (
                <Tooltip content="No votes yet — at least one participant must vote before revealing"
                         isDisabled={hasAnyVotes} placement="bottom" delay={200}>
                    <span className="inline-block">
                        <Button size={btnSize} color="primary" variant="solid"
                            onPress={onReveal} isDisabled={!hasAnyVotes} isLoading={actionLoading === 'Reveal'}
                            startContent={!actionLoading ? <Eye className="h-5 w-5"/> : undefined}
                            className={`${revealClass}
                                shadow-lg shadow-primary/25 hover:shadow-xl hover:shadow-primary/35 hover:scale-[1.02]
                                active:scale-95 transition-all duration-200
                                ${allVoted ? 'animate-pulse' : ''}`}>
                            Reveal
                        </Button>
                    </span>
                </Tooltip>
            )}

            {showRevealedActions && (
                <div className="flex gap-2">
                    <Button size="md" color="warning" variant="flat" onPress={onRevote}
                        isLoading={actionLoading === 'Revote'}
                        startContent={<RefreshCw className={ICON_SM}/>}
                        className="font-semibold px-4 h-9 text-xs">Revote</Button>
                    <Button size="md" color="secondary" variant="solid" onPress={onReset}
                        isLoading={actionLoading === 'Reset'}
                        startContent={<RefreshCw className={ICON_SM}/>}
                        className="font-semibold px-4 h-9 text-xs shadow-md shadow-secondary/20">Next</Button>
                </div>
            )}

            {showWaitingMessage && (
                <span className="text-xs text-foreground-400">Waiting for reveal…</span>
            )}
        </div>
    );
}

// ── ParticipantSeat ──────────────────────────────────────────────────────────

function ParticipantSeat({participant: p, ox, oy, nx, ny, deg, index, isViewer, isVoting, isRevealed, scene}: {
    participant: PlanningPokerParticipant;
    ox: number; oy: number; nx: number; ny: number; deg: number; index: number;
    isViewer: boolean; isVoting: boolean; isRevealed: boolean; scene: SceneDimensions;
}) {
    const {cardW, cardH, avatarSize, cardInward, nameLabelWidth} = scene;

    const cardOx = ox - nx * cardInward;
    const cardOy = oy - ny * cardInward;

    const normDeg = ((deg % 360) + 360) % 360;
    const isTopHalf = normDeg > 0 && normDeg < 180;
    const angRad = (normDeg * Math.PI) / 180;
    const sideWeight = Math.abs(Math.cos(angRad));
    const isLeftSide = normDeg > 90 && normDeg < 270;

    const NAME_GAP = 3;
    const nameOy = isTopHalf ? oy - avatarSize / 2 - 16 - NAME_GAP : oy + avatarSize / 2 + NAME_GAP;

    const hShift = sideWeight * (nameLabelWidth * 0.45);
    const nameOx = isLeftSide
        ? ox - nameLabelWidth / 2 + hShift * 0.1
        : ox + nameLabelWidth / 2 - hShift * 0.1;

    const textAlign = sideWeight > 0.4
        ? (isLeftSide ? 'text-right' : 'text-left')
        : 'text-center';

    return (
        <>
            <motion.div className="absolute"
                style={{left: '50%', top: '50%', width: `${cardW}px`, height: `${cardH}px`,
                    x: cardOx - cardW / 2, y: cardOy - cardH / 2, pointerEvents: 'auto'}}
                initial={{opacity: 0, scale: 0.92}} animate={{opacity: 1, scale: 1}}
                transition={{delay: index * 0.04, type: 'spring', stiffness: 340, damping: 26}}>
                <FlipCard hasVoted={p.hasVoted} isVoting={isVoting} isRevealed={isRevealed}
                          revealedVote={p.revealedVote} sizeClass="w-full h-full"/>
            </motion.div>

            <motion.div className="absolute flex items-center justify-center"
                style={{left: '50%', top: '50%', width: `${avatarSize}px`, height: `${avatarSize}px`,
                    x: ox - avatarSize / 2, y: oy - avatarSize / 2, pointerEvents: 'auto'}}
                initial={{opacity: 0, scale: 0.92}} animate={{opacity: 1, scale: 1}}
                transition={{delay: index * 0.04, type: 'spring', stiffness: 340, damping: 26}}>
                <div className="relative w-full h-full">
                    <DiceBearAvatar userId={p.participantId} avatarJson={p.avatarUrl} name={p.displayName}
                        className="w-full h-full" size="sm"
                        borderClass={`transition-all duration-300 ${isViewer ? 'border-2 border-primary/60' : 'border border-content3/60'}`}/>
                    {p.isModerator && (
                        <div className="absolute -top-1 -right-1 bg-warning rounded-full p-[2px] shadow-sm">
                            <Crown className="h-2.5 w-2.5 text-white"/>
                        </div>
                    )}
                    {!p.isPresent && (
                        <div className="absolute -bottom-0.5 -right-0.5 w-3 h-3 rounded-full bg-foreground-300 border-2 border-content1"/>
                    )}
                </div>
            </motion.div>

            <Tooltip content={p.displayName} delay={400}>
                <motion.div className={`absolute ${isTopHalf ? 'items-end' : 'items-start'}`}
                    style={{left: '50%', top: '50%', width: `${nameLabelWidth}px`,
                        x: nameOx - nameLabelWidth / 2, y: nameOy, pointerEvents: 'auto'}}
                    initial={{opacity: 0}} animate={{opacity: 1}} transition={{delay: index * 0.04 + 0.1}}>
                    <span className={`block text-[11px] leading-tight font-medium truncate ${textAlign}
                        ${isViewer ? 'text-primary' : 'text-foreground-500'}`}>
                        {p.displayName}
                    </span>
                </motion.div>
            </Tooltip>
        </>
    );
}

// ── FlipCard ─────────────────────────────────────────────────────────────────

function FlipCard({hasVoted, isVoting, isRevealed, revealedVote, sizeClass}: {
    hasVoted: boolean; isVoting: boolean; isRevealed: boolean;
    revealedVote?: string | null; sizeClass?: string;
}) {
    const showFront = isRevealed;

    return (
        <div className={sizeClass ?? 'w-[54px] h-[74px]'} style={{perspective: '600px'}}>
            <motion.div className="relative w-full h-full" style={{transformStyle: 'preserve-3d'}}
                animate={{rotateY: showFront ? 0 : 180}}
                transition={{duration: 0.45, ease: [0.4, 0, 0.2, 1]}}>
                <div className={`absolute inset-0 rounded-lg border-2 flex items-center justify-center
                    ${isRevealed && revealedVote ? 'border-primary/50 bg-content1 shadow-md' : 'border-content3 bg-content1'}`}
                    style={{backfaceVisibility: 'hidden'}}>
                    <span className="text-lg font-black text-primary tabular-nums">{revealedVote ?? '—'}</span>
                </div>
                <div className={`absolute inset-0 rounded-lg border-2 flex items-center justify-center
                    ${hasVoted ? 'border-success/40 bg-content1 shadow-md' : 'border-content3 bg-content1'}`}
                    style={{backfaceVisibility: 'hidden', transform: 'rotateY(180deg)'}}>
                    {hasVoted ? <CheckCircle className="h-5 w-5 text-success"/> :
                        <span className="text-foreground-300/60 text-sm font-medium">?</span>}
                </div>
            </motion.div>
        </div>
    );
}

// ── SpectatorCard ────────────────────────────────────────────────────────────

export function SpectatorCard({spectators, scene}: {
    spectators: PlanningPokerParticipant[]; scene: SceneDimensions;
}) {
    const orbitLeftFromCenter = scene.orbitRx + scene.avatarSize / 2 + scene.nameLabelWidth * 0.5 + 16 * scene.scale;
    const cardW = SPECTATOR_CARD_W * scene.scale;
    const rawLeft = `calc(50% - ${orbitLeftFromCenter + cardW}px)`;

    return (
        <>
            <div className="absolute z-10 flex-col max-h-[60%] hidden sm:flex
                    rounded-xl bg-content2/80 backdrop-blur-sm border border-content3/60 shadow-sm overflow-hidden"
                style={{width: `${cardW}px`, top: '50%', left: `max(8px, ${rawLeft})`, transform: 'translateY(-50%)'}}>
                <div className="flex items-center gap-2 px-3 py-2 border-b border-content3/40 shrink-0">
                    <Eye className="h-3.5 w-3.5 text-foreground-400 shrink-0"/>
                    <span className="text-[11px] font-semibold text-foreground-400 uppercase tracking-wide">Spectators</span>
                    <span className="text-[11px] font-bold text-foreground-500 ml-auto tabular-nums">{spectators.length}</span>
                </div>
                <div className="overflow-y-auto py-1.5 px-1.5 flex flex-col gap-0.5">
                    {spectators.map(p => (
                        <div key={p.participantId}
                             className="flex items-center gap-2 px-1.5 py-1 rounded-lg hover:bg-content3/40 transition-colors">
                            <div className="relative shrink-0">
                                <DiceBearAvatar userId={p.participantId} avatarJson={p.avatarUrl}
                                    name={p.displayName} className="h-6 w-6 opacity-70" size="sm"
                                    borderClass="border border-content3"/>
                                {p.isModerator && (
                                    <div className="absolute -top-0.5 -right-0.5 bg-warning rounded-full p-[1px]">
                                        <Crown className="h-2 w-2 text-white"/>
                                    </div>
                                )}
                            </div>
                            <span className="text-[11px] text-foreground-500 truncate leading-tight">{p.displayName}</span>
                        </div>
                    ))}
                </div>
            </div>

            <div className="absolute z-10 bottom-1 left-2 right-2 sm:hidden
                flex items-center gap-2 px-2.5 py-1.5
                rounded-lg bg-content2/80 backdrop-blur-sm border border-content3/60 shadow-sm">
                <Eye className="h-3 w-3 text-foreground-400 shrink-0"/>
                <span className="text-[10px] font-semibold text-foreground-400 uppercase shrink-0">Spectators</span>
                <div className="flex items-center -space-x-1.5 overflow-hidden flex-1 min-w-0">
                    {spectators.slice(0, 6).map(p => (
                        <DiceBearAvatar key={p.participantId} userId={p.participantId} avatarJson={p.avatarUrl}
                            name={p.displayName} className="h-5 w-5 shrink-0 opacity-70" size="sm"
                            borderClass="border border-content2"/>
                    ))}
                    {spectators.length > 6 && (
                        <span className="text-[10px] text-foreground-400 pl-1.5 shrink-0">+{spectators.length - 6}</span>
                    )}
                </div>
            </div>
        </>
    );
}

// ── NoticeBar ────────────────────────────────────────────────────────────────

export function NoticeBar({isSpectator, isArchived}: { isSpectator: boolean; isArchived: boolean }) {
    if (isSpectator && !isArchived) {
        return (
            <div className="absolute bottom-1 left-0 right-0 flex items-center justify-center gap-2 text-foreground-400 text-xs">
                <Eye className={ICON_SM}/><span>You are spectating. Switch to Participant to vote.</span>
            </div>
        );
    }
    if (isArchived) {
        return (
            <div className="absolute bottom-1 left-0 right-0 flex items-center justify-center gap-2 text-warning text-xs">
                <Archive className={ICON_SM}/>
                <span className="font-medium">This room has been archived and is read-only. It will be automatically deleted after the retention period.</span>
            </div>
        );
    }
    return null;
}

