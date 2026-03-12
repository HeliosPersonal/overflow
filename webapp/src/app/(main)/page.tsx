import {getQuestions} from "@/lib/actions/question-actions";
import QuestionCard from "@/app/(main)/questions/QuestionCard";
import QuestionsHeader from "@/app/(main)/questions/QuestionsHeader";
import AppPagination from "@/components/AppPagination";
import {QuestionParams} from "@/lib/types";
import {Suspense} from "react";

export default async function Home({searchParams}: {searchParams?: Promise<QuestionParams>}) {
    const params = await searchParams;
    const {data: questions, error} = await getQuestions(params);

    // Don't hard-crash on backend unavailability — show empty state instead
    if (error) console.warn('[Home] Failed to load questions:', error.message);

    return (
        <>
            <div className='sticky top-0 z-40 bg-white dark:bg-[#18181b] border-b border-neutral-200 dark:border-neutral-800'>
                <Suspense>
                    <QuestionsHeader total={questions?.totalCount ?? 0} tag={params?.tag} />
                </Suspense>
            </div>
            {error && (
                <div className='py-8 text-center text-neutral-500 dark:text-neutral-400 text-sm'>
                    Could not load questions. The backend may be starting up — please refresh in a moment.
                </div>
            )}
            {questions?.items.map(question => (
                <div key={question.id} className='py-4 not-last:border-b border-neutral-200 dark:border-neutral-800 w-full flex'>
                    <QuestionCard key={question.id} question={question} />
                </div>
            ))}
            <Suspense>
                <AppPagination totalCount={questions?.totalCount ?? 0} />
            </Suspense>
        </>
    );
}
