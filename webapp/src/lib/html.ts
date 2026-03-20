/** Strip all HTML tags, collapse whitespace. */
export function stripHtmlTags(html: string) {
    return html
        .replace(/<[^>]*>/g, ' ')
        .replace(/\s+/g, ' ')
        .trim();
}

/**
 * Plain-text excerpt from HTML content for question card previews.
 * Skips code blocks and headings; takes the first paragraph of prose.
 */
export function htmlToExcerpt(html: string, maxLength = 160): string {
    const noCode = html.replace(/<pre[\s\S]*?<\/pre>/gi, '');
    const noHeadings = noCode.replace(/<h[1-6][^>]*>[\s\S]*?<\/h[1-6]>/gi, '');
    const pMatch = noHeadings.match(/<p[^>]*>([\s\S]*?)<\/p>/i);
    const raw = pMatch ? pMatch[1] : noHeadings;
    const text = raw.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
    return text.length > maxLength ? text.slice(0, maxLength).trimEnd() + '…' : text;
}

/** Extract Cloudinary public IDs from `<img>` tags in HTML. */
export function extractPublicIdsFromHtml(html: string) {
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
}

