import {Answer} from "@/lib/types";
import VotingButtons from "@/app/(main)/questions/[id]/VotingButtons";
import AnswerFooter from "@/app/(main)/questions/[id]/AnswerFooter";
import {User} from "next-auth";

type Props = {
    answer: Answer;
    askerId: string;
    currentUser?: User | null;
    isAdmin?: boolean;
}

export default function AnswerContent({answer, askerId, currentUser, isAdmin}: Props) {
    return (
        <div className='flex border-b border-content3 pb-4 pt-4 px-3 sm:px-6'>
            <VotingButtons
                target={answer}
                currentUserId={currentUser?.id}
                askerId={askerId}
            />
            <div className='flex flex-col w-full min-w-0'>
                <div className='flex-1 mt-4 ml-2 sm:ml-6 bg-content3 rounded-xl px-3 sm:px-5 py-3 sm:py-4 prose max-w-none dark:prose-invert
                    prose-sm sm:prose-base overflow-x-auto'
                    dangerouslySetInnerHTML={{__html: answer.content}}
                />
                <div className='ml-2 sm:ml-6 mt-0'>
                    <AnswerFooter answer={answer} currentUser={currentUser} isAdmin={isAdmin} />
                </div>
            </div>

        </div>
    );
}