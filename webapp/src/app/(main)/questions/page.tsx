import {redirect} from "next/navigation";
import {QuestionParams} from "@/lib/types";

export default async function QuestionsPage({searchParams}: {searchParams?: Promise<QuestionParams>}) {
    const params = await searchParams;
    const query = params ? new URLSearchParams(params as Record<string, string>).toString() : '';
    redirect(query ? `/?${query}` : '/');
}