'use server';

import {fetchClient} from "@/lib/fetchClient";
import {FetchResponse, Profile, TopUser, TopUserWithProfile} from "@/lib/types";
import {revalidatePath} from "next/cache";
import {EditProfileSchema} from "@/lib/schemas/editProfileSchema";
import {cookies} from "next/headers";
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

    // Signal the JWT callback to re-fetch the profile immediately on next request
    const cookieStore = await cookies();
    cookieStore.set('profile_dirty', '1', { maxAge: 30, path: '/', httpOnly: true });

    // ProfileService is the source of truth for display names.
    // Revalidate broadly so all pages showing the user's name pick up the change.
    revalidatePath(`/profiles/${id}`);
    revalidatePath('/profiles');
    revalidatePath('/questions');
    revalidatePath('/', 'layout');

    // Push updated profile (name + avatar) to all estimation rooms the user is in.
    // Uses raw fetch instead of fetchClient to avoid notFound() throwing in server-action context.
    // Best-effort: failures here must not block the profile edit response.
    try {
        const apiUrl = process.env.API_URL;
        const session = await auth();
        if (apiUrl && session?.accessToken) {
            const refreshRes = await fetch(`${apiUrl}/estimation/refresh-profile`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${session.accessToken}`,
                    'Content-Type': 'application/json',
                },
            });
            if (!refreshRes.ok) {
                console.warn('[editProfile] refresh-profile failed:', refreshRes.status, await refreshRes.text().catch(() => ''));
            }
        }
    } catch (e) {
        console.warn('[editProfile] refresh-profile threw:', e instanceof Error ? e.message : e);
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