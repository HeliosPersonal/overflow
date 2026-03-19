import Link from "next/link";
import {Tag} from "@/lib/types";
import {Chip} from "@heroui/chip";

type Props = {
    tag: Tag;
}

export default function TagCard({tag}: Props) {
    return (
        <Link href={`/questions?tag=${tag.slug}`}>
            <div className="bg-content2 border border-content3 rounded-xl shadow-raise-sm hover:shadow-raise-lg transition-shadow duration-200 p-4 flex flex-col gap-2 h-full">
                <Chip variant='flat' size='sm' className='bg-content3 text-foreground-600'>
                    {tag.slug}
                </Chip>
                <p className='line-clamp-3 text-sm text-foreground-500 flex-1'>{tag.description}</p>
                <p className='text-xs text-foreground-400'>{tag.usageCount} {tag.usageCount === 1 ? 'question' : 'questions'}</p>
            </div>
        </Link>
    );
}