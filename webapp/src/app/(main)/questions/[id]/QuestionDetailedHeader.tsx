import {Question} from "@/lib/types";
import {Button} from "@heroui/button";
import Link from "next/link";
import {fuzzyTimeAgo} from "@/lib/util";
import DeleteQuestionButton from "@/app/(main)/questions/[id]/DeleteQuestionButton";

type Props = {
    question: Question;
    currentUserId?: string;
}

export default function QuestionDetailedHeader({question, currentUserId}: Props) {

    return (
        <div className='flex flex-col w-full border-b border-content3 gap-4 pb-4 px-6 pt-5'>
            <div className='flex justify-between gap-4'>
                <h1 className='first-letter:uppercase'>
                    {question.title}
                </h1>
                <Link href='/questions/ask'>
                    <Button color='primary'>
                        Ask Question
                    </Button>
                </Link>
            </div>
            <div className='flex justify-between items-center'>
                <div className='flex items-center gap-6'>
                    <div className='flex items-center gap-3'>
                        <span className='text-foreground-500'>Asked</span>
                        <span>{fuzzyTimeAgo(question.createdAt)}</span>
                    </div>
                    {question.updatedAt && (
                        <div className='flex items-center gap-3'>
                            <span className='text-foreground-500'>Modified</span>
                            <span>{fuzzyTimeAgo(question.updatedAt)}</span>
                        </div>
                    )}
                    <div className='flex items-center gap-3'>
                        <span className='text-foreground-500'>Viewed</span>
                        <span>{question.viewCount + 1} times</span>
                    </div>
                </div>

                {currentUserId === question.askerId &&
                    <div className='flex items-center gap-3'>
                        <Link href={`/questions/${question.id}/edit`}>
                            <Button
                                size='sm'
                                variant='faded'
                                color='primary'
                            >
                                Edit
                            </Button>
                        </Link>
                        <DeleteQuestionButton questionId={question.id}/>
                    </div>}
            </div>

        </div>
    );
}