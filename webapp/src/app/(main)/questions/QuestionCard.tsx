import {Question} from "@/lib/types";
import Link from "next/link";
import {Chip} from "@heroui/chip";
import {Avatar} from "@heroui/avatar";
import clsx from "clsx";
import {CheckIcon, HandThumbUpIcon, EyeIcon} from "@heroicons/react/24/outline";
import {htmlToExcerpt, timeAgo} from "@/lib/util";

type Props = {
    question: Question;
}

export default function QuestionCard({question}: Props) {
    return (
        <div className='flex gap-0 w-full rounded-xl overflow-hidden'>
            <div className='flex flex-col items-end justify-start text-sm gap-3 min-w-16 bg-content3 px-4 py-4 text-foreground-500'>
                <div className='flex items-center gap-1'>
                    <HandThumbUpIcon className='h-4 w-4' />
                    <span>{question.votes}</span>
                </div>
                <div className='flex items-center gap-1'>
                    <CheckIcon
                        className={clsx('h-4 w-4', {
                            'text-success': question.hasAcceptedAnswer,
                            'text-foreground-300': !question.hasAcceptedAnswer
                        })}
                        strokeWidth={3}
                    />
                    <span>{question.answerCount}</span>
                </div>
                <div className='flex items-center gap-1'>
                    <EyeIcon className='h-4 w-4' />
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
                            <Avatar 
                                className='h-6 w-6'
                                color='primary'
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