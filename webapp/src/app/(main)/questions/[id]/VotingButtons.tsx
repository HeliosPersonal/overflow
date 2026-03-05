'use client';

import {Button} from "@heroui/button";
import {ArrowDownIcon, ArrowUpIcon, CheckIcon} from "@heroicons/react/20/solid";
import {Answer, Question, Vote} from "@/lib/types";
import {useState, useTransition} from "react";
import {acceptAnswer, addVote} from "@/lib/actions/question-actions";
import {handleError, successToast} from "@/lib/util";

type Props = {
    target: Question | Answer
    currentUserId?: string;
    askerId?: string;
}

const isTargetAnswer = (target: Question | Answer): target is Answer => {
    return "questionId" in target;
}

export default function VotingButtons({target, currentUserId, askerId}: Props) {
    const [pending, startTransition] = useTransition();
    const [targetBtn, setTargetBtn] = useState<{type: 'up' | 'down', id: string} | null>(null);
    const isAnswer = isTargetAnswer(target);
    
    const canVote = isAnswer
        ? target.userId !== currentUserId && target.userVoted === 0
        : target.askerId !== currentUserId && target.userVoted === 0
    
    const handleAddVote = (value: 1 | -1) => {
        setTargetBtn(value === 1 
            ? {type: 'up', id: target.id}
            : {type: 'down', id: target.id}
        );
        
        startTransition(async () => {
            const vote: Vote = {
                targetId: target.id,
                targetType: isAnswer ? 'Answer' : 'Question',
                targetUserId: isAnswer ? target.userId : target.askerId,
                voteValue: value,
                questionId: isAnswer ? target.questionId : target.id
            }
            const {error} = await addVote(vote);
            if (error) handleError(error);
            else {
                successToast('Vote has been registered', 'Success');
                setTargetBtn(null);
            }
        })
    }
    
    const handleAcceptAnswer = () => {
        if (!isAnswer || askerId !== currentUserId) return;
        startTransition(async () => {
            const {error} = await acceptAnswer(target.id, target.questionId);
            if (error) handleError(error);
            else successToast('Answer has been accepted.', 'Success');
        })
    }
    
    return (
        <div className='shrink-0 flex flex-col gap-3 items-center justify-start mt-4'>
            <Button
                isIconOnly
                variant='light'
                isLoading={pending && targetBtn?.type === 'up' && targetBtn.id === target.id}
                isDisabled={!canVote}
                onPress={() => handleAddVote(1)}
            >
                <ArrowUpIcon className='w-8' />
            </Button>
            <span className='text-xl font-semibold'>{target.votes}</span>
            <Button
                isIconOnly
                variant='light'
                isLoading={pending && targetBtn?.type === 'down' && targetBtn.id === target.id}
                isDisabled={!canVote}
                onPress={() => handleAddVote(-1)}
            >
                <ArrowDownIcon className='w-8' />
            </Button>
            {isAnswer && (
                <Button
                    isIconOnly
                    variant='light'
                    className='disabled:opacity-100'
                    isDisabled={target.accepted || askerId !== currentUserId}
                    isLoading={pending && !targetBtn}
                    onPress={handleAcceptAnswer}
                >
                    {target.accepted ? (
                        <CheckIcon className='size-6 text-success' />
                    ) : (
                        <CheckIcon className='size-6 text-default-500' />
                    )}
                    
                </Button>
            )}
        </div>
    );
}