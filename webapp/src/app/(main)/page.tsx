import {getQuestions} from "@/lib/actions/question-actions";
import QuestionCard from "@/app/(main)/questions/QuestionCard";
import QuestionsHeader from "@/app/(main)/questions/QuestionsHeader";
import AppPagination from "@/components/AppPagination";
import {QuestionParams} from "@/lib/types";
import {Suspense} from "react";

export default async function Home({searchParams}: {searchParams?: Promise<QuestionParams>}) {
    const params = await searchParams;
    const {data: questions, error} = await getQuestions(params);

    if (error) throw error;

    return (
        <>
            <Suspense>
                <QuestionsHeader total={questions?.totalCount ?? 0} tag={params?.tag} />
            </Suspense>
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
