import {AVATAR_EYES, AVATAR_MOUTH} from "@/lib/avatar";
import type {PlanningPokerRoom} from "@/lib/types";

export function parseErrorMessage(errBody: unknown, fallback: string): string {
    if (typeof errBody === 'string') return errBody;
    if (errBody && typeof errBody === 'object') {
        const obj = errBody as Record<string, unknown>;
        return (typeof obj.message === 'string' ? obj.message : null)
            ?? (typeof obj.error === 'string' ? obj.error : null)
            ?? fallback;
    }
    return fallback;
}

export function generateNextTaskName(existingTasks: string[]): string {
    const existingNumbers = existingTasks
        .map(t => {
            const m = t.match(/^Task (\d+)$/);
            return m ? parseInt(m[1], 10) : 0;
        })
        .filter(n => n > 0);
    const next = existingNumbers.length > 0 ? Math.max(...existingNumbers) + 1 : 1;
    return `Task ${next}`;
}

export function generateRandomAvatarJson(): string {
    const eyes = AVATAR_EYES[Math.floor(Math.random() * AVATAR_EYES.length)];
    const mouth = AVATAR_MOUTH[Math.floor(Math.random() * AVATAR_MOUTH.length)];
    return JSON.stringify({eyes: [eyes], mouth: [mouth]});
}

export function getConsensusInfo(uniqueValues: number, topCount: number, totalVotes: number) {
    if (uniqueValues === 1) {
        return {label: 'Full consensus!', colorClass: 'text-success'};
    }
    if (uniqueValues === 2 && topCount >= totalVotes * 0.7) {
        return {label: 'Almost aligned', colorClass: 'text-warning'};
    }
    return {label: 'Split opinions', colorClass: 'text-danger'};
}

export function getRoundLabel(room: PlanningPokerRoom, hasTasks: boolean): string {
    return hasTasks
        ? (room.currentTaskName ?? `Task ${room.roundNumber}`)
        : `Round ${room.roundNumber}`;
}

