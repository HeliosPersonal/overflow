import QuestionForm from "@/app/(main)/questions/ask/QuestionForm";

export default function Page() {
    return (
        <div className='min-h-full bg-content1 px-3 sm:px-6 pt-4 pb-6'>
            <h1 className='pb-3'>Ask a public question</h1>
            <QuestionForm />
        </div>
    );
}