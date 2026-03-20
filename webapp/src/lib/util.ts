/**
 * Re-export barrel — keeps existing `import { … } from '@/lib/util'` working.
 * Prefer importing from the focused module directly in new code:
 *   @/lib/toast   — errorToast, successToast, handleError
 *   @/lib/format  — fuzzyTimeAgo, timeAgo
 *   @/lib/html    — stripHtmlTags, htmlToExcerpt, extractPublicIdsFromHtml
 */
export { errorToast, successToast, handleError } from './toast';
export { fuzzyTimeAgo, timeAgo } from './format';
export { stripHtmlTags, htmlToExcerpt, extractPublicIdsFromHtml } from './html';

