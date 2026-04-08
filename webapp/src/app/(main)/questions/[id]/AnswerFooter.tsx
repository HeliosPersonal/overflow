'use client';

import {Answer} from "@/lib/types";
import AuthorBadge from "@/components/AuthorBadge";
import {handleError} from "@/lib/util";
import { Button } from "@heroui/button";
import {User} from "next-auth";
import {useState, useTransition} from "react";
import {deleteAnswer} from "@/lib/actions/question-actions";
import {useAnswerStore} from "@/lib/hooks/useAnswerStore";

type Props = {
    answer: Answer;
    currentUser?: User | null;
    isAdmin?: boolean;
}

export default function AnswerFooter({ answer, currentUser, isAdmin }: Props) {
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

    const isOwner = currentUser?.id === answer.userId;

    return (
        <div className='flex flex-col-reverse sm:flex-row sm:justify-between gap-3 mt-4'>
            <div className='flex items-center mt-auto'>
                {(isOwner || isAdmin) &&
                <>
                    {isOwner && (
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
                    )}
                    <Button
                        isLoading={pending && answer.id === deleteTarget}
                        onPress={handleDelete}
                        size='sm'
                        variant='light'
                        color='danger'
                    >Delete</Button>
                </>}
            </div>

            <AuthorBadge
                userId={answer.userId}
                author={answer.author}
                verb="answered"
                createdAt={answer.createdAt}
            />
        </div>
    );
}