'use server';

import {fetchClient} from "@/lib/fetchClient";
import {FetchResponse, Profile, TopUser, TopUserWithProfile} from "@/lib/types";
import {revalidatePath} from "next/cache";
import {EditProfileSchema} from "@/lib/schemas/editProfileSchema";
import {auth} from "@/auth";
import {fetchProfileMap} from "@/lib/profiles";

export async function getUserProfiles(sortBy?: string) {
    const effectiveSort = sortBy ?? 'reputation';
    return fetchClient<Profile[]>(`/profiles?sortBy=${effectiveSort}`, 'GET');
}

export async function getProfileById(id: string) {
    return fetchClient<Profile>(`/profiles/${id}`, 'GET');
}

export async function editProfile(id: string, profile: EditProfileSchema) {
    const result = await fetchClient<Profile>(`/profiles/edit`, 'PUT', {body: profile});

    // ProfileService is the source of truth for display names + avatars.
    // Revalidate broadly so all pages (including TopNav which fetches /profiles/me) pick up changes.
    revalidatePath(`/profiles/${id}`);
    revalidatePath('/profiles');
    revalidatePath('/questions');
    revalidatePath('/', 'layout');

    // Evict the cached profile in EstimationService so WebSocket broadcasts and
    // room listings fetch fresh avatar/display name from ProfileService.
    // This is lightweight — just a cache eviction, no DB writes.
    try {
        const apiUrl = process.env.API_URL;
        const session = await auth();
        if (apiUrl && session?.accessToken) {
            await fetch(`${apiUrl}/estimation/profile-cache`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${session.accessToken}`,
                },
            });
        }
    } catch (e) {
        console.warn('[editProfile] profile-cache eviction threw:', e instanceof Error ? e.message : e);
    }

    return result;
}

export async function getTopUsers(): Promise<FetchResponse<TopUserWithProfile[]>> {
    const {data: users, error} = await fetchClient<TopUser[]>('/stats/top-users', 'GET');
    if (error) return {data: null, error: {message: 'Problem getting users', status: 500}};
    
    if (!Array.isArray(users) || users.length === 0) return {data: []};

    let profileMap: Map<string, Profile>;
    try {
        profileMap = await fetchProfileMap(users.map(u => u.userId));
    } catch {
        return {data: null, error: {message: 'Problem getting profiles', status: 500}};
    }
    
    return {
        data: users.map(u => ({...u, profile: profileMap.get(u.userId)})),
    };
}