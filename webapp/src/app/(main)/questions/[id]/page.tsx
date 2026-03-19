import {getQuestionById} from "@/lib/actions/question-actions";
import {notFound} from "next/navigation";
import QuestionDetailedHeader from "@/app/(main)/questions/[id]/QuestionDetailedHeader";
import QuestionContent from "@/app/(main)/questions/[id]/QuestionContent";
import AnswerContent from "@/app/(main)/questions/[id]/AnswerContent";
import AnswersHeader from "@/app/(main)/questions/[id]/AnswersHeader";
import AnswerForm from "@/app/(main)/questions/[id]/AnswerForm";
import {Answer} from "@/lib/types";

type Params = Promise<{id: string}>
type SearchParams = Promise<{sort?: string}>

export default async function QuestionDetailedPage({params, searchParams}: 
        {params: Params, searchParams: SearchParams}) {
    const {id} = await params;
    const {sort} = await searchParams;
    const {data: question, error} = await getQuestionById(id);
    
    if (error) return (
        <div className='min-h-full bg-content1 px-6 py-4'>
            <p className="text-sm text-foreground-400">{error.message}</p>
        </div>
    );
    if (!question) return notFound();

    const sortMode = sort === 'created' ? 'created' : 'highScore';

    const sortHighScore = (a: Answer, b: Answer) => {
        if (a.accepted !== b.accepted) return a.accepted ? -1 : 1;
        const va = a.votes ?? 0, vb = b.votes ?? 0;
        if (va !== vb) return vb - va;
        return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
    }

    const sortCreated = (a: Answer, b: Answer) => {
        return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
    }

    const answers = [...question.answers].sort(
        sortMode === 'created' ? sortCreated : sortHighScore
    );

    return (
        <div className='min-h-full bg-content1 px-6 py-4 flex flex-col gap-4'>
            {/* Question card */}
            <div className='bg-content2 border border-content3 shadow-raise-sm rounded-2xl overflow-hidden'>
                <QuestionDetailedHeader question={question} />
                <QuestionContent question={question} />
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