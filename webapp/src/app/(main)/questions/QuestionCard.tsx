import {Question} from "@/lib/types";
import Link from "next/link";
import {Chip} from "@heroui/chip";
import DiceBearAvatar from "@/components/DiceBearAvatar";
import clsx from "clsx";
import {Check, ThumbsUp, Eye} from "lucide-react";
import {htmlToExcerpt, timeAgo} from "@/lib/util";

type Props = {
    question: Question;
}

export default function QuestionCard({question}: Props) {
    return (
        <div className='flex gap-0 w-full rounded-xl overflow-hidden'>
            <div className='flex flex-col items-end justify-start text-sm gap-3 min-w-16 bg-content3 px-4 py-4 text-foreground-500'>
                <div className='flex items-center gap-1'>
                    <ThumbsUp className='h-4 w-4' />
                    <span>{question.votes}</span>
                </div>
                <div className='flex items-center gap-1'>
                    <Check
                        className={clsx('h-4 w-4', {
                            'text-success': question.hasAcceptedAnswer,
                            'text-foreground-300': !question.hasAcceptedAnswer
                        })}
                        strokeWidth={3}
                    />
                    <span>{question.answerCount}</span>
                </div>
                <div className='flex items-center gap-1'>
                    <Eye className='h-4 w-4' />
                    <span>{question.viewCount}</span>
                </div>
            </div>
            <div className='flex flex-1 justify-between min-h-32 px-6 py-4'>
                <div className='flex flex-col gap-2 w-full'>
                    <Link
                        href={`/questions/${question.id}`}
                        className='text-primary font-semibold hover:underline first-letter:uppercase'
                    >
                        {question.title}
                    </Link>
                    <div className='line-clamp-2 text-sm text-foreground-500'>
                        {htmlToExcerpt(question.content)}
                    </div>
                    <div className='flex justify-between pt-2'>
                        <div className='flex gap-2'>
                            {question.tagSlugs.map(slug => (
                                <Link href={`/questions?tag=${slug}`} key={slug}>
                                    <Chip variant='flat' size='sm' className='bg-content3 text-foreground-600 hover:bg-content4 transition-colors'>
                                        {slug}
                                    </Chip>
                                </Link>

                            ))}
                        </div>
                        
                        <div className='text-sm flex items-center gap-2'>
                            <DiceBearAvatar 
                                className='h-6 w-6'
                                borderClass='border-2 border-primary'
                                userId={question.askerId}
                                avatarJson={question.author?.avatarUrl}
                                name={question.author?.displayName?.charAt(0)}
                            />
                            <Link href={`/profiles/${question.askerId}`}>
                                {question.author?.displayName}
                            </Link>
                            <span>asked {timeAgo(question.createdAt)}</span>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}