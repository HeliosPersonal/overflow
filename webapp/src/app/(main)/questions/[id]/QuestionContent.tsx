import {Question} from "@/lib/types";
import VotingButtons from "@/app/(main)/questions/[id]/VotingButtons";
import QuestionFooter from "@/app/(main)/questions/[id]/QuestionFooter";
import {getCurrentUser} from "@/lib/actions/auth-actions";

type Props = {
    question: Question;
}

export default async function QuestionContent({question}: Props) {
    const currentUser = await getCurrentUser();
    return (
        <div className='flex border-b border-neutral-200 dark:border-neutral-800 pb-3 px-6'>
            <VotingButtons
                target={question} 
                askerId={question.askerId}
                currentUserId={currentUser?.id}
            />
            <div className='flex flex-col w-full'>
                <div
                    className='flex-1 mt-4 ml-6 prose dark:prose-invert max-w-none'
                    dangerouslySetInnerHTML={{ __html: question.content }}
                />
                <QuestionFooter question={question} />
            </div>

        </div>
    );
}