import {getQuestions} from "@/lib/actions/question-actions";
import QuestionCard from "@/app/(main)/questions/QuestionCard";
import QuestionsHeader from "@/app/(main)/questions/QuestionsHeader";
import AppPagination from "@/components/AppPagination";
import {QuestionParams} from "@/lib/types";
import {Suspense} from "react";

export default async function QuestionsPage({searchParams}: {searchParams?: Promise<QuestionParams>}) {
    const params = await searchParams;
    const {data: questions, error} = await getQuestions(params);

    // Don't hard-crash on backend unavailability — show empty state instead
    if (error) console.warn('[QuestionsPage] Failed to load questions:', error.message);

    return (
        <div className='flex flex-col min-h-full bg-content1'>
            <div className='sticky top-0 z-40 bg-content1 border-b border-content2'>
                <Suspense>
                    <QuestionsHeader total={questions?.totalCount ?? 0} tag={params?.tag} />
                </Suspense>
            </div>
            {error && (
                <div className='py-8 text-center text-foreground-400 text-sm'>
                    Could not load questions. The backend may be starting up — please refresh in a moment.
                </div>
            )}
            <div className='flex flex-col gap-2 p-4 flex-1'>
                {questions?.items.map(question => (
                    <div key={question.id} className='w-full flex bg-content2 border border-content3 rounded-xl shadow-raise-sm hover:shadow-raise-lg transition-shadow duration-200'>
                        <QuestionCard key={question.id} question={question} />
                    </div>
                ))}
            </div>
            <Suspense>
                <AppPagination totalCount={questions?.totalCount ?? 0} />
            </Suspense>
        </div>
    );
}