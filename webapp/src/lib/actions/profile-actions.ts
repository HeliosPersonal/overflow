'use server';

import {fetchClient} from "@/lib/fetchClient";
import {FetchResponse, Profile, ThemePreference, TopUser, TopUserWithProfile} from "@/lib/types";
import {revalidatePath} from "next/cache";
import {EditProfileSchema} from "@/lib/schemas/editProfileSchema";
import {auth} from "@/auth";
import {fetchProfileMap} from "@/lib/profiles";
import { createLogger } from "@/lib/logger";

const logger = createLogger('profile-actions');

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
            const evictionRes = await fetch(`${apiUrl}/estimation/profile-cache`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${session.accessToken}`,
                },
            });
            if (!evictionRes.ok) {
                logger.warn(
                    { status: evictionRes.status, userId: id },
                    'profile-cache eviction returned non-OK — room avatars may be stale until cache TTL expires'
                );
            }
        } else {
            logger.warn({ userId: id }, 'profile-cache eviction skipped — no session access token');
        }
    } catch (e) {
        logger.warn({ err: e }, 'profile-cache eviction threw');
    }

    return result;
}

export async function updateThemePreference(themePreference: ThemePreference) {
    return fetchClient<void>('/profiles/theme', 'PUT', {body: {themePreference}});
}

export async function getMyThemePreference(): Promise<ThemePreference | null> {
    const {data} = await fetchClient<Profile>('/profiles/me', 'GET');
    return data?.themePreference ?? null;
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