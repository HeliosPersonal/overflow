import {Question} from "@/lib/types";
import VotingButtons from "@/app/(main)/questions/[id]/VotingButtons";
import QuestionFooter from "@/app/(main)/questions/[id]/QuestionFooter";

type Props = {
    question: Question;
    currentUserId?: string;
}

export default function QuestionContent({question, currentUserId}: Props) {
    return (
        <div className='flex border-b border-content3 pb-4 pt-4 px-6'>
            <VotingButtons
                target={question} 
                askerId={question.askerId}
                currentUserId={currentUserId}
            />
            <div className='flex flex-col w-full'>
                <div className='flex-1 mt-4 ml-6 bg-content3 rounded-xl px-5 py-4 prose dark:prose-invert max-w-none'
                    dangerouslySetInnerHTML={{ __html: question.content }}
                />
                <QuestionFooter question={question} />
            </div>

        </div>
    );
}