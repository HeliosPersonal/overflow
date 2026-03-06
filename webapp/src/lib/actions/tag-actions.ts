'use server';

import {fetchClient} from "@/lib/fetchClient";
import {Tag, TrendingTag} from "@/lib/types";
import {revalidatePath} from "next/cache";

export async function getTags(sort?: string) {
    let url = '/tags';
    if (sort) url += '?sort=' + sort;
    
    return fetchClient<Tag[]>(url, 'GET', 
        {cache: 'force-cache', next: {revalidate: 3600}})
}

export async function getTrendingTags() {
    return fetchClient<TrendingTag[]>('/stats/trending-tags', 'GET', 
        {cache: 'force-cache', next: {revalidate: 3600}})
}

export async function createTag(data: {name: string; slug: string; description: string}) {
    const result = await fetchClient<Tag>('/tags', 'POST', {body: data});
    if (!result.error) revalidatePath('/tags');
    return result;
}

export async function updateTag(id: string, data: {name: string; description: string}) {
    const result = await fetchClient<Tag>(`/tags/${id}`, 'PUT', {body: data});
    if (!result.error) revalidatePath('/tags');
    return result;
}

export async function deleteTag(id: string) {
    const result = await fetchClient<void>(`/tags/${id}`, 'DELETE');
    if (!result.error) revalidatePath('/tags');
    return result;
}

