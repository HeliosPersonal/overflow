import {Answer} from "@/lib/types";
import VotingButtons from "@/app/(main)/questions/[id]/VotingButtons";
import AnswerFooter from "@/app/(main)/questions/[id]/AnswerFooter";
import {User} from "next-auth";

type Props = {
    answer: Answer;
    askerId: string;
    currentUser?: User | null;
}

export default function AnswerContent({answer, askerId, currentUser}: Props) {
    return (
        <div className='flex border-b border-content3 pb-4 pt-4 px-6'>
            <VotingButtons
                target={answer}
                currentUserId={currentUser?.id}
                askerId={askerId}
            />
            <div className='flex flex-col w-full'>
                <div className='flex-1 mt-4 ml-6 bg-content3 rounded-xl px-5 py-4 prose max-w-none dark:prose-invert'
                    dangerouslySetInnerHTML={{__html: answer.content}}
                />
                <div className='ml-6 mt-0'>
                    <AnswerFooter answer={answer} currentUser={currentUser} />
                </div>
            </div>

        </div>
    );
}