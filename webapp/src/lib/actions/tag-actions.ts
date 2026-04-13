'use server';

import {fetchClient} from "@/lib/fetchClient";
import {PaginatedResult, Tag, TrendingTag} from "@/lib/types";
import {revalidatePath} from "next/cache";
import {createLogger} from "@/lib/logger";

const logger = createLogger('tag-actions');

export async function getTags(sort?: string) {
    let url = '/tags';
    if (sort) url += '?sort=' + sort;
    
    return fetchClient<Tag[]>(url, 'GET')
}

export async function searchTags(params: { search?: string; page?: number; pageSize?: number }) {
    const searchParams = new URLSearchParams();
    if (params.search) searchParams.set('search', params.search);
    if (params.page) searchParams.set('page', params.page.toString());
    if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());
    const qs = searchParams.toString();
    return fetchClient<PaginatedResult<Tag>>(`/tags/search${qs ? `?${qs}` : ''}`, 'GET');
}

export async function getTrendingTags() {
    return fetchClient<TrendingTag[]>('/stats/trending-tags', 'GET')
}

export async function createTag(data: {name: string; slug: string; description: string}) {
    logger.info({data}, 'Creating tag');
    const result = await fetchClient<Tag>('/tags', 'POST', {body: data});
    if (result.error) {
        logger.warn({error: result.error}, 'Create tag failed');
    } else {
        logger.info({tagId: result.data?.id, name: data.name}, 'Tag created');
        revalidatePath('/tags');
    }
    return result;
}

export async function updateTag(id: string, data: {name: string; description: string}) {
    logger.info({tagId: id, data}, 'Updating tag');
    const result = await fetchClient<Tag>(`/tags/${id}`, 'PUT', {body: data});
    if (result.error) {
        logger.warn({tagId: id, error: result.error}, 'Update tag failed');
    } else {
        logger.info({tagId: id, name: data.name}, 'Tag updated');
        revalidatePath('/tags');
    }
    return result;
}

export async function deleteTag(id: string) {
    logger.info({tagId: id}, 'Deleting tag');
    const result = await fetchClient<void>(`/tags/${id}`, 'DELETE');
    if (result.error) {
        logger.warn({tagId: id, error: result.error}, 'Delete tag failed');
    } else {
        logger.info({tagId: id}, 'Tag deleted');
        revalidatePath('/tags');
    }
    return result;
}
