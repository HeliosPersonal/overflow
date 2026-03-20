import {fetchClient} from "@/lib/fetchClient";
import {Profile} from "@/lib/types";

const PROFILE_CACHE_TTL = 60; // seconds

/**
 * Fetch profiles for a set of user IDs and return them as a lookup map.
 * Uses force-cache with 60s revalidation to avoid redundant ProfileService calls.
 * Returns an empty map (not an error) when the input set is empty.
 */
export async function fetchProfileMap(userIds: Iterable<string>): Promise<Map<string, Profile>> {
    const ids = [...new Set(userIds)].sort();
    if (ids.length === 0) return new Map();

    const url = '/profiles/batch?' + new URLSearchParams({ids: ids.join(',')});
    const {data: profiles, error} = await fetchClient<Profile[]>(url, 'GET', {
        cache: 'force-cache',
        next: {revalidate: PROFILE_CACHE_TTL},
    });

    if (error) throw new Error('Failed to fetch profiles');

    return new Map(
        Array.isArray(profiles) ? profiles.map(p => [p.userId, p]) : []
    );
}

