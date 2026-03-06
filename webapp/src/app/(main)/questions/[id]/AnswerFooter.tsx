'use client';

import {Answer} from "@/lib/types";
import {Avatar} from "@heroui/avatar";
import {handleError, timeAgo} from "@/lib/util";
import { Button } from "@heroui/button";
import {User} from "next-auth";
import {useState, useTransition} from "react";
import {deleteAnswer} from "@/lib/actions/question-actions";
import {useAnswerStore} from "@/lib/hooks/useAnswerStore";

type Props = {
    answer: Answer;
    currentUser?: User | null;
}

export default function AnswerFooter({ answer, currentUser }: Props) {
    const [pending, startTransition] = useTransition();
    const [deleteTarget, setDeleteTarget] = useState<string>('');
    const setAnswer = useAnswerStore(state => state.setAnswer);
    const editableAnswer = useAnswerStore(state => state.answer);

    const handleDelete = () => {
        setDeleteTarget(answer.id);
        startTransition(async () => {
            const {error} = await deleteAnswer(answer.id, answer.questionId);
            if (error) handleError(error);
            setDeleteTarget('');
        })
    }
    
    return (
        <div className='flex justify-between mt-4'>
            <div className='flex items-center mt-auto'>
                {currentUser?.id === answer.userId &&
                <>
                    <Button
                        isDisabled={!!editableAnswer}
                        onPress={() => {
                            setAnswer(answer);
                            setTimeout(() => {
                                document.getElementById('answer-form')?.scrollIntoView({
                                    behavior: 'smooth' });
                            }, 100);
                        }}
                        size='sm'
                        variant='light'
                        color='primary'
                    >Edit</Button>
                    <Button
                        isLoading={pending && answer.id === deleteTarget}
                        onPress={handleDelete}
                        size='sm'
                        variant='light'
                        color='danger'
                    >Delete</Button>
                </>}
            </div>

            <div className='flex items-center gap-2 bg-primary/10 px-3 py-2 rounded-lg text-sm'>
                <Avatar className='h-8 w-8 shrink-0' color='primary'
                        name={answer.author?.displayName.charAt(0)} />
                <div className='flex flex-col'>
                    <span className='font-extralight text-xs'>answered {timeAgo(answer.createdAt)}</span>
                    <div className='flex items-center gap-1'>
                        <span className='font-medium text-sm'>{answer.author?.displayName}</span>
                        <span className='text-xs text-default-400 font-semibold'>{answer.author?.reputation}</span>
                    </div>
                </div>
            </div>
        </div>
    );
}