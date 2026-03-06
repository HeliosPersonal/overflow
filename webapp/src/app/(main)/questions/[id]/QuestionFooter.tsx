import {Question} from "@/lib/types";
import {Chip} from "@heroui/chip";
import Link from "next/link";
import {Avatar} from "@heroui/avatar";
import {timeAgo} from "@/lib/util";

type Props = {
    question: Question
}

export default function QuestionFooter({question}: Props) {
    return (
        <div className='flex justify-between mt-2'>
            <div className='flex flex-col self-end'>
                <div className='flex gap-2'>
                    {question.tagSlugs.map(tag => (
                        <Link href={`/questions?tag=${tag}`} key={tag}>
                            <Chip
                                variant='bordered'
                            >
                                {tag}
                            </Chip>
                        </Link>
                    ))}
                </div>
            </div>

            <div className='flex items-center gap-2 bg-primary/10 px-3 py-2 rounded-lg text-sm'>
                <Avatar className='h-8 w-8 shrink-0' color='primary'
                        name={question.author?.displayName.charAt(0)}/>
                <div className='flex flex-col'>
                    <span className='font-extralight text-xs'>asked {timeAgo(question.createdAt)}</span>
                    <div className='flex items-center gap-1'>
                        <span className='font-medium text-sm'>{question.author?.displayName}</span>
                        <span className='text-xs text-default-400 font-semibold'>{question.author?.reputation}</span>
                    </div>
                </div>
            </div>
        </div>
    );
}