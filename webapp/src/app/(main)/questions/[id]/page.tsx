import {getQuestionById} from "@/lib/actions/question-actions";
import {notFound} from "next/navigation";
import QuestionDetailedHeader from "@/app/(main)/questions/[id]/QuestionDetailedHeader";
import QuestionContent from "@/app/(main)/questions/[id]/QuestionContent";
import AnswerContent from "@/app/(main)/questions/[id]/AnswerContent";
import AnswersHeader from "@/app/(main)/questions/[id]/AnswersHeader";
import AnswerForm from "@/app/(main)/questions/[id]/AnswerForm";
import {Answer} from "@/lib/types";
import {getCurrentUser} from "@/lib/actions/auth-actions";

type Params = Promise<{id: string}>
type SearchParams = Promise<{sort?: string}>

function byHighScore(a: Answer, b: Answer) {
    if (a.accepted !== b.accepted) return a.accepted ? -1 : 1;
    if (a.votes !== b.votes) return (b.votes ?? 0) - (a.votes ?? 0);
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
}

function byCreated(a: Answer, b: Answer) {
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
}

export default async function QuestionDetailedPage({params, searchParams}: 
        {params: Params, searchParams: SearchParams}) {
    const [{id}, {sort}, currentUser] = await Promise.all([
        params, searchParams, getCurrentUser(),
    ]);
    const {data: question, error} = await getQuestionById(id);
    
    if (error) return (
        <div className='min-h-full bg-content1 px-6 py-4'>
            <p className="text-sm text-foreground-400">{error.message}</p>
        </div>
    );
    if (!question) return notFound();

    const answers = [...question.answers].sort(sort === 'created' ? byCreated : byHighScore);

    return (
        <div className='min-h-full bg-content1 px-6 py-4 flex flex-col gap-4'>
            {/* Question card */}
            <div className='bg-content2 border border-content3 shadow-raise-sm rounded-2xl overflow-hidden'>
                <QuestionDetailedHeader question={question} currentUserId={currentUser?.id} />
                <QuestionContent question={question} currentUserId={currentUser?.id} />
            </div>

            {/* Answers */}
            {question.answers.length > 0 && (
                <div className='bg-content2 border border-content3 shadow-raise-sm rounded-2xl overflow-hidden'>
                    <AnswersHeader answerCount={question.answers.length} />
                    {answers.map(answer => (
                        <AnswerContent
                            answer={answer}
                            key={answer.id}
                            askerId={question.askerId}
                            currentUser={currentUser}
                        />
                    ))}
                </div>
            )}

            {/* Answer form */}
            <div className='bg-content2 border border-content3 shadow-raise-sm rounded-2xl overflow-hidden'>
                <AnswerForm questionId={question.id} />
            </div>
        </div>
    );
}