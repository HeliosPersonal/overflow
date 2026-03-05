import QuestionForm from "@/app/(main)/questions/ask/QuestionForm";

export default function Page() {
    return (
        <div className='px-6'>
            <h1 className='pb-3'>Ask a public question</h1>
            <QuestionForm />
        </div>
    );
}