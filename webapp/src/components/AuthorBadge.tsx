import DiceBearAvatar from "@/components/DiceBearAvatar";
import {timeAgo} from "@/lib/format";
import type {Profile} from "@/lib/types";

type Props = {
    userId: string;
    author?: Profile;
    /** e.g. "asked", "answered" */
    verb: string;
    createdAt: string;
}

/** Compact author attribution badge used in question/answer footers. */
export default function AuthorBadge({userId, author, verb, createdAt}: Props) {
    return (
        <div className='flex items-center gap-2 bg-content4 px-3 py-2 rounded-lg text-sm'>
            <DiceBearAvatar
                className='h-8 w-8 shrink-0'
                borderClass='border-2 border-primary'
                userId={userId}
                avatarJson={author?.avatarUrl}
                name={author?.displayName?.charAt(0)}
            />
            <div className='flex flex-col'>
                <span className='font-extralight text-xs'>{verb} {timeAgo(createdAt)}</span>
                <div className='flex items-center gap-1'>
                    <span className='font-medium text-sm'>{author?.displayName}</span>
                    <span className='text-xs text-default-400 font-semibold'>{author?.reputation}</span>
                </div>
            </div>
        </div>
    );
}

