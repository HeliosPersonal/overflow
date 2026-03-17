'use server';

import {fetchClient} from "@/lib/fetchClient";
import {FetchResponse, Profile, TopUser, TopUserWithProfile} from "@/lib/types";
import {revalidatePath} from "next/cache";
import {EditProfileSchema} from "@/lib/schemas/editProfileSchema";
import {auth} from "@/auth";

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
    if (error) return {data: null, error: 
            {message: 'Problem getting users', status: 500}}
    
    const ids = Array.isArray(users) ? [...new Set(users.map(u => u.userId))] : [];
    const qs = encodeURIComponent(ids.join(','));
    
    const {data: profiles, error: profilesError} = await fetchClient<Profile[]>(
        `/profiles/batch?ids=${qs}`, 'GET', {cache: 'force-cache', next: {revalidate: 60}}
    );

    if (profilesError) return {data: null, error:
            {message: 'Problem getting profiles', status: 500}}
    
    const byId = new Map((profiles ?? []).map(p => [p.userId, p]));
    
    return {data: Array.isArray(users) ? users.map(u => ({
            ...u,
            profile: byId.get(u.userId)
        })) as TopUserWithProfile[] : null}
}