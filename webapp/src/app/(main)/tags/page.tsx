import {getTags} from "@/lib/actions/tag-actions";
import TagCard from "@/app/(main)/tags/TagCard";
import TagHeader from "@/app/(main)/tags/TagsHeader";
import {auth} from "@/auth";
import {redirect} from "next/navigation";

type SearchParams = Promise<{sort?: string}>
export default async function Page({searchParams}: {searchParams: SearchParams }) {
    const {sort} = await searchParams;
    const session = await auth();
    const isAdmin = session?.user?.roles?.includes('admin') ?? false;

    if (!isAdmin) redirect('/questions');
    
    const {data: tags, error} = await getTags(sort);
    
    if (error) return (
        <div className='min-h-full bg-content1 w-full px-6'>
            <TagHeader isAdmin={isAdmin} />
            <p className="text-sm text-foreground-400 pt-4">{error.message}</p>
        </div>
    );

    return (
        <div className='min-h-full bg-content1 w-full px-6 pb-6'>
            <TagHeader isAdmin={isAdmin} />
            <div className='grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4'>
                {Array.isArray(tags) && tags.map(tag => (
                    <TagCard tag={tag} key={tag.id} />
                ))}
            </div>
        </div>
    );
}