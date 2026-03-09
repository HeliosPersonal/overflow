'use server';

import {FetchResponse, PlanningPokerRoom} from "@/lib/types";
import {fetchClient} from "@/lib/fetchClient";

export async function createRoom(title: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>('/estimation/rooms', 'POST', {body: {title}});
}

export async function getRoom(code: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${code}`, 'GET');
}

export async function joinRoom(code: string, displayName?: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${code}/join`, 'POST', {
        body: {displayName}
    });
}

export async function submitVote(code: string, value: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${code}/votes`, 'POST', {body: {value}});
}

export async function clearVote(code: string): Promise<FetchResponse<void>> {
    return fetchClient<void>(`/estimation/rooms/${code}/votes/me`, 'DELETE');
}

export async function revealVotes(code: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${code}/reveal`, 'POST');
}

export async function resetRound(code: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${code}/reset`, 'POST');
}

export async function archiveRoom(code: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${code}/archive`, 'POST');
}

export async function changeMode(code: string, isSpectator: boolean): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${code}/mode`, 'POST', {body: {isSpectator}});
}

export async function leaveRoom(code: string): Promise<FetchResponse<void>> {
    return fetchClient<void>(`/estimation/rooms/${code}/leave`, 'POST');
}

