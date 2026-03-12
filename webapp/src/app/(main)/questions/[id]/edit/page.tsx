import QuestionForm from "@/app/(main)/questions/ask/QuestionForm";
import {getQuestionById} from "@/lib/actions/question-actions";
import {notFound} from "next/navigation";

type Params = Promise<{id: string}>
export default async function EditQuestionPage({params}: {params: Params}) {
    const {id} = await params;
    const {data: question, error} = await getQuestionById(id);

    if (error) {
        return (
            <div className='flex flex-col gap-4 px-6'>
                <h1>Edit your question</h1>
                <p className="text-sm text-neutral-500 dark:text-neutral-400">{error.message}</p>
            </div>
        );
    }

    if (!question) return notFound();

    return (
        <div className='flex flex-col gap-4 px-6'>
            <h1>Edit your question</h1>
            <QuestionForm questionToUpdate={question} />
        </div>
    );
}