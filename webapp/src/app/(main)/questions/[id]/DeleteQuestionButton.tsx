'use client';

import {Button} from "@heroui/button";
import {useTransition} from "react";
import {useRouter} from "next/navigation";
import {deleteQuestion} from "@/lib/actions/question-actions";
import {handleError} from "@/lib/util";

type Props = {
    questionId: string;
    size?: 'sm' | 'md' | 'lg';
}

export default function DeleteQuestionButton({ questionId, size = 'sm' }: Props) {
    const [pending, startTransition] = useTransition();
    const router = useRouter();
    
    const handleDelete = () => {
        startTransition(async () => {
            const {error} = await deleteQuestion(questionId);
            if (error) handleError(error);
            router.push('/questions');
        })
    }
    
    return (
        <Button
            size={size}
            variant='faded'
            color='danger'
            isLoading={pending}
            onPress={handleDelete}
        >
            Delete
        </Button>
    );
}