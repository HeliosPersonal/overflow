'use server';

import {fetchClient} from "@/lib/fetchClient";
import {PaginatedResult, Profile} from "@/lib/types";
import {revalidatePath} from "next/cache";
import {createLogger} from "@/lib/logger";

const logger = createLogger('admin-actions');

export async function getAdminUsers(params: { search?: string; page?: number; pageSize?: number }) {
    const searchParams = new URLSearchParams();
    if (params.search) searchParams.set('search', params.search);
    if (params.page) searchParams.set('page', params.page.toString());
    if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());
    const qs = searchParams.toString();
    return fetchClient<PaginatedResult<Profile>>(`/profiles/admin/users${qs ? `?${qs}` : ''}`, 'GET');
}

export async function adminUpdateUser(userId: string, data: { displayName?: string; description?: string; avatarUrl?: string }) {
    logger.info({userId, data}, 'Admin updating user');
    const result = await fetchClient<Profile>(`/profiles/admin/${userId}`, 'PUT', {body: data});
    if (result.error) {
        logger.warn({userId, error: result.error}, 'Admin user update failed');
    } else {
        logger.info({userId}, 'Admin user updated successfully');
        revalidatePath('/admin/users');
        revalidatePath('/profiles');
    }
    return result;
}

export async function adminDeleteUser(userId: string) {
    logger.info({userId}, 'Admin deleting user');
    const result = await fetchClient<void>(`/profiles/admin/${userId}`, 'DELETE');
    if (result.error) {
        logger.warn({userId, error: result.error}, 'Admin user delete failed');
    } else {
        logger.info({userId}, 'Admin user deletion queued');
        revalidatePath('/admin/users');
    }
    return result;
}

export async function adminBulkDeleteUsers(userIds: string[]) {
    logger.info({count: userIds.length, userIds}, 'Admin bulk-deleting users');
    const result = await fetchClient<void>('/profiles/admin/bulk-delete', 'POST', {body: {userIds}});
    if (result.error) {
        logger.warn({count: userIds.length, error: result.error}, 'Admin bulk delete failed');
    } else {
        logger.info({count: userIds.length}, 'Admin bulk deletion queued');
        revalidatePath('/admin/users');
    }
    return result;
}
