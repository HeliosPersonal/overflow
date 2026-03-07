import {addToast} from "@heroui/toast";
import {
    differenceInCalendarDays,
    differenceInCalendarMonths,
    differenceInCalendarWeeks, formatDistanceToNow,
    isToday,
    isYesterday
} from "date-fns";

export function errorToast(error: {message: string, status?: number}) {
    return addToast({
        color: 'danger',
        title: error.status || 'Error!',
        description: error.message || 'Something went wrong',
    })
}

export function successToast(message: string, title?: string) {
    return addToast({
        color: 'success',
        title: title|| 'Success!',
        description: message,
    })
}

export function handleError(error: {message: string, status?: number}) {
    if (error.status === 500) {
        throw error
    } else {
        return errorToast(error)
    }
}

export function fuzzyTimeAgo(date: string | Date) {
    const now = new Date();
    if (isToday(date)) return 'Today';
    if (isYesterday(date)) return 'Yesterday';
    
    const days = differenceInCalendarDays(now, date);
    if (days < 7) return `${days} day${days > 1 ? 's' : ''} ago`;

    const weeks = differenceInCalendarWeeks(now, date);
    if (weeks < 4) return `${weeks} week${weeks > 1 ? 's' : ''} ago`;

    const months = differenceInCalendarMonths(now, date);
    return `${months} month${months > 1 ? 's' : ''} ago`;
}

export function timeAgo(date: string | Date) {
    return formatDistanceToNow(date, {addSuffix: true});
}

export function stripHtmlTags(html: string) {
    return html
        .replace(/<[^>]*>/g, ' ')   // replace every tag with a space
        .replace(/\s+/g, ' ')       // collapse runs of whitespace
        .trim();
}

/**
 * Returns a plain-text excerpt from HTML content suitable for question card previews.
 * Skips <pre>/<code> blocks and headings; takes the first paragraph of prose text.
 */
export function htmlToExcerpt(html: string, maxLength = 160): string {
    // Drop code blocks entirely — they look terrible as plain text
    const noCode = html.replace(/<pre[\s\S]*?<\/pre>/gi, '');
    // Drop headings
    const noHeadings = noCode.replace(/<h[1-6][^>]*>[\s\S]*?<\/h[1-6]>/gi, '');
    // Extract first <p> content, fall back to all remaining text
    const pMatch = noHeadings.match(/<p[^>]*>([\s\S]*?)<\/p>/i);
    const raw = pMatch ? pMatch[1] : noHeadings;
    const text = raw.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
    return text.length > maxLength ? text.slice(0, maxLength).trimEnd() + '…' : text;
}

export const extractPublicIdsFromHtml = (html: string) => {
    const matches = [...html.matchAll(/<img[^>]*src="([^"]+)"[^>]*>/g)];

    return matches.map(match => {
        const url = match[1];
        const parts = url.split('/');
        const fileWithExt = parts.pop()!;
        const [publicId] = fileWithExt.split('.');

        const uploadIndex = parts.indexOf('upload');
        let pathParts = parts.slice(uploadIndex + 1);

        if (pathParts[0]?.startsWith('v') && /^\d+$/.test(pathParts[0].slice(1))) {
            pathParts = pathParts.slice(1);
        }

        return [...pathParts, publicId].join('/');
    });
};
