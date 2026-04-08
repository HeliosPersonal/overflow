import {Question} from "@/lib/types";
import {Button} from "@heroui/button";
import Link from "next/link";
import {fuzzyTimeAgo} from "@/lib/util";
import DeleteQuestionButton from "@/app/(main)/questions/[id]/DeleteQuestionButton";

type Props = {
    question: Question;
    currentUserId?: string;
    isAdmin?: boolean;
}

export default function QuestionDetailedHeader({question, currentUserId, isAdmin}: Props) {
    const isOwner = currentUserId === question.askerId;

    return (
        <div className='flex flex-col w-full border-b border-content3 gap-3 sm:gap-4 pb-4 px-4 sm:px-6 pt-4 sm:pt-5'>
            <div className='flex flex-col sm:flex-row sm:justify-between gap-3 sm:gap-4'>
                <h1 className='first-letter:uppercase text-lg sm:text-2xl'>
                    {question.title}
                </h1>
                <Link href='/questions/ask' className='shrink-0 self-start'>
                    <Button color='primary' size='sm'>
                        Ask Question
                    </Button>
                </Link>
            </div>
            <div className='flex flex-col sm:flex-row sm:justify-between gap-2 sm:items-center'>
                <div className='flex flex-wrap items-center gap-x-4 gap-y-1 text-sm'>
                    <div className='flex items-center gap-1.5'>
                        <span className='text-foreground-500'>Asked</span>
                        <span>{fuzzyTimeAgo(question.createdAt)}</span>
                    </div>
                    {question.updatedAt && (
                        <div className='flex items-center gap-1.5'>
                            <span className='text-foreground-500'>Modified</span>
                            <span>{fuzzyTimeAgo(question.updatedAt)}</span>
                        </div>
                    )}
                    <div className='flex items-center gap-1.5'>
                        <span className='text-foreground-500'>Viewed</span>
                        <span>{question.viewCount + 1} times</span>
                    </div>
                </div>

                {(isOwner || isAdmin) &&
                    <div className='flex items-center gap-2 sm:gap-3'>
                        {isOwner && (
                            <Link href={`/questions/${question.id}/edit`}>
                                <Button
                                    size='sm'
                                    variant='faded'
                                    color='primary'
                                >
                                    Edit
                                </Button>
                            </Link>
                        )}
                        <DeleteQuestionButton questionId={question.id}/>
                    </div>}
            </div>

        </div>
    );
}