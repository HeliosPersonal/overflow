'use server';

import {FetchResponse, PlanningPokerRoom} from "@/lib/types";
import {fetchClient} from "@/lib/fetchClient";

export async function createRoom(title: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>('/estimation/rooms', 'POST', {body: {title}});
}

export async function getRoom(roomId: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${roomId}`, 'GET');
}

export async function joinRoom(roomId: string, displayName?: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${roomId}/join`, 'POST', {
        body: {displayName}
    });
}

export async function submitVote(roomId: string, value: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${roomId}/votes`, 'POST', {body: {value}});
}

export async function clearVote(roomId: string): Promise<FetchResponse<void>> {
    return fetchClient<void>(`/estimation/rooms/${roomId}/votes/me`, 'DELETE');
}

export async function revealVotes(roomId: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${roomId}/reveal`, 'POST');
}

export async function resetRound(roomId: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${roomId}/reset`, 'POST');
}

export async function archiveRoom(roomId: string): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${roomId}/archive`, 'POST');
}

export async function changeMode(roomId: string, isSpectator: boolean): Promise<FetchResponse<PlanningPokerRoom>> {
    return fetchClient<PlanningPokerRoom>(`/estimation/rooms/${roomId}/mode`, 'POST', {body: {isSpectator}});
}

export async function leaveRoom(roomId: string): Promise<FetchResponse<void>> {
    return fetchClient<void>(`/estimation/rooms/${roomId}/leave`, 'POST');
}
