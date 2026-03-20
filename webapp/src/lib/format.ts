import {
    differenceInCalendarDays,
    differenceInCalendarMonths,
    differenceInCalendarWeeks,
    formatDistanceToNow,
    isToday,
    isYesterday,
} from "date-fns";

/** Human-friendly relative date: "Today", "Yesterday", "3 days ago", "2 weeks ago", etc. */
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

/** Precise relative time via date-fns: "3 minutes ago", "about 2 hours ago", etc. */
export function timeAgo(date: string | Date) {
    return formatDistanceToNow(date, {addSuffix: true});
}

