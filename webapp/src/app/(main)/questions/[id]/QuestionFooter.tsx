import {Question} from "@/lib/types";
import {Chip} from "@heroui/chip";
import Link from "next/link";
import AuthorBadge from "@/components/AuthorBadge";

type Props = {
    question: Question
}

export default function QuestionFooter({question}: Props) {
    return (
        <div className='flex flex-col sm:flex-row sm:justify-between gap-3 mt-2 ml-2 sm:ml-0'>
            <div className='flex flex-col self-end'>
                <div className='flex flex-wrap gap-1.5 sm:gap-2'>
                    {question.tagSlugs.map(tag => (
                        <Link href={`/questions?tag=${tag}`} key={tag}>
                            <Chip variant='flat' size='sm' className='bg-content3 text-foreground-600 hover:bg-content4 transition-colors'>
                                {tag}
                            </Chip>
                        </Link>
                    ))}
                </div>
            </div>

            <AuthorBadge
                userId={question.askerId}
                author={question.author}
                verb="asked"
                createdAt={question.createdAt}
            />
        </div>
    );
}