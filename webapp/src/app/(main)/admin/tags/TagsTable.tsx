'use client';

import {PaginatedResult, Tag} from "@/lib/types";
import {useState, useRef, useTransition} from "react";
import {createTag, deleteTag, updateTag} from "@/lib/actions/tag-actions";
import {useForm} from "react-hook-form";
import {zodResolver} from "@hookform/resolvers/zod";
import {tagSchema, TagSchema, updateTagSchema, UpdateTagSchema} from "@/lib/schemas/tagSchema";
import {Input} from "@heroui/input";
import {Button} from "@heroui/button";
import {Textarea} from "@heroui/input";
import {Chip} from "@heroui/chip";
import {
    Modal, ModalBody, ModalContent, ModalFooter, ModalHeader, useDisclosure
} from "@heroui/modal";
import {Pencil, Trash2, Plus, Check, X, Search} from "lucide-react";
import {handleError, successToast} from "@/lib/util";
import {useRouter, useSearchParams} from "next/navigation";
import AppPagination from "@/components/AppPagination";

type Props = { data: PaginatedResult<Tag> }

export default function TagsTable({data}: Props) {
    const tags = data.items;
    const [editingId, setEditingId] = useState<string | null>(null);
    const [deletingTag, setDeletingTag] = useState<Tag | null>(null);
    const [isPending, startTransition] = useTransition();
    const {isOpen, onOpen, onClose} = useDisclosure();
    const router = useRouter();
    const searchParams = useSearchParams();

    // ── Search ──
    const searchFormRef = useRef<HTMLFormElement>(null);

    const handleSearch = (e: React.FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        const formData = new FormData(e.currentTarget);
        const search = (formData.get('search') as string)?.trim() ?? '';
        const params = new URLSearchParams(searchParams.toString());
        if (search) {
            params.set('search', search);
        } else {
            params.delete('search');
        }
        params.set('page', '1');
        router.push(`?${params.toString()}`, {scroll: false});
    };

    const clearSearch = () => {
        searchFormRef.current?.reset();
        const params = new URLSearchParams(searchParams.toString());
        params.delete('search');
        params.set('page', '1');
        router.push(`?${params.toString()}`, {scroll: false});
    };

    // ── Add form ──
    const addForm = useForm<TagSchema>({
        resolver: zodResolver(tagSchema),
        mode: 'onTouched',
        defaultValues: {name: '', slug: '', description: ''}
    });

    // ── Edit form ──
    const editForm = useForm<UpdateTagSchema>({
        resolver: zodResolver(updateTagSchema),
        mode: 'onTouched',
    });

    const handleAdd = (formData: TagSchema) => {
        startTransition(async () => {
            const {error} = await createTag(formData);
            if (error) { handleError(error); return; }
            successToast('Tag created');
            addForm.reset();
            router.refresh();
        });
    };

    const startEdit = (tag: Tag) => {
        setEditingId(tag.id);
        editForm.reset({name: tag.name, description: tag.description});
    };

    const cancelEdit = () => setEditingId(null);

    const handleUpdate = (formData: UpdateTagSchema, tag: Tag) => {
        startTransition(async () => {
            const {error} = await updateTag(tag.id, formData);
            if (error) { handleError(error); return; }
            successToast('Tag updated');
            setEditingId(null);
            router.refresh();
        });
    };

    const confirmDelete = (tag: Tag) => {
        setDeletingTag(tag);
        onOpen();
    };

    const handleDelete = () => {
        if (!deletingTag) return;
        startTransition(async () => {
            const {error} = await deleteTag(deletingTag.id);
            if (error) { handleError(error); return; }
            successToast('Tag deleted');
            onClose();
            setDeletingTag(null);
            router.refresh();
        });
    };

    return (
        <div className='flex flex-col gap-6'>
            {/* ── Add new tag form ── */}
            <form onSubmit={addForm.handleSubmit(handleAdd)}
                  className='flex flex-col gap-3 p-5 rounded-xl border border-default-200 bg-default-50'>
                <h2 className='text-lg font-semibold'>Add new tag</h2>
                <div className='grid grid-cols-2 gap-3'>
                    <Input
                        {...addForm.register('name')}
                        label='Name'
                        labelPlacement='outside'
                        placeholder='e.g. TypeScript'
                        isInvalid={!!addForm.formState.errors.name}
                        errorMessage={addForm.formState.errors.name?.message}
                    />
                    <Input
                        {...addForm.register('slug')}
                        label='Slug'
                        labelPlacement='outside'
                        placeholder='e.g. typescript'
                        isInvalid={!!addForm.formState.errors.slug}
                        errorMessage={addForm.formState.errors.slug?.message}
                    />
                </div>
                <Textarea
                    {...addForm.register('description')}
                    label='Description'
                    labelPlacement='outside'
                    placeholder='A brief description of the tag...'
                    minRows={2}
                    isInvalid={!!addForm.formState.errors.description}
                    errorMessage={addForm.formState.errors.description?.message}
                />
                <div className='flex justify-end'>
                    <Button
                        type='submit'
                        color='primary'
                        startContent={<Plus className='h-4 w-4'/>}
                        isLoading={isPending}
                        isDisabled={!addForm.formState.isValid}
                    >
                        Add tag
                    </Button>
                </div>
            </form>

            {/* ── Search bar ── */}
            <form ref={searchFormRef} onSubmit={handleSearch} className="flex items-center gap-2 max-w-md">
                <Input
                    name="search"
                    placeholder="Search by name or slug..."
                    size="sm"
                    defaultValue={searchParams.get('search') ?? ''}
                    startContent={<Search className="h-4 w-4 text-default-400"/>}
                    isClearable
                    onClear={clearSearch}
                />
                <Button type="submit" size="sm" color="primary">
                    Search
                </Button>
            </form>

            {/* ── Tags count ── */}
            <p className="text-sm text-foreground-500">{data.totalCount} tag(s)</p>

            {/* ── Tags table ── */}
            <div className='rounded-xl border border-default-200 overflow-hidden'>
                <table className='w-full text-sm'>
                    <thead className='bg-default-100 text-default-600'>
                        <tr>
                            <th className='text-left px-4 py-3 font-medium w-36'>Slug</th>
                            <th className='text-left px-4 py-3 font-medium w-40'>Name</th>
                            <th className='text-left px-4 py-3 font-medium'>Description</th>
                            <th className='text-right px-4 py-3 font-medium w-24'>Questions</th>
                            <th className='px-4 py-3 w-24'></th>
                        </tr>
                    </thead>
                    <tbody className='divide-y divide-default-100'>
                        {tags.map(tag => (
                            <tr key={tag.id} className='hover:bg-default-50 transition-colors'>
                                {editingId === tag.id ? (
                                    <td colSpan={4} className='px-4 py-3'>
                                        <form
                                            id={`edit-${tag.id}`}
                                            onSubmit={editForm.handleSubmit(formData => handleUpdate(formData, tag))}
                                            className='flex gap-3 items-start'
                                        >
                                            <Input
                                                {...editForm.register('name')}
                                                label='Name'
                                                size='sm'
                                                className='w-40'
                                                isInvalid={!!editForm.formState.errors.name}
                                                errorMessage={editForm.formState.errors.name?.message}
                                            />
                                            <Textarea
                                                {...editForm.register('description')}
                                                label='Description'
                                                size='sm'
                                                className='flex-1'
                                                minRows={1}
                                                isInvalid={!!editForm.formState.errors.description}
                                                errorMessage={editForm.formState.errors.description?.message}
                                            />
                                        </form>
                                    </td>
                                ) : (
                                    <>
                                        <td className='px-4 py-3'>
                                            <Chip variant='bordered' size='sm'>{tag.slug}</Chip>
                                        </td>
                                        <td className='px-4 py-3 font-medium'>{tag.name}</td>
                                        <td className='px-4 py-3 text-default-600 line-clamp-2'>{tag.description}</td>
                                        <td className='px-4 py-3 text-right text-default-500'>{tag.usageCount}</td>
                                    </>
                                )}
                                <td className='px-4 py-3'>
                                    <div className='flex items-center justify-end gap-1'>
                                        {editingId === tag.id ? (
                                            <>
                                                <Button
                                                    isIconOnly
                                                    size='sm'
                                                    color='success'
                                                    variant='flat'
                                                    isLoading={isPending}
                                                    onPress={() => editForm.handleSubmit(formData => handleUpdate(formData, tag))()}
                                                >
                                                    <Check className='h-4 w-4'/>
                                                </Button>
                                                <Button
                                                    isIconOnly size='sm' variant='flat'
                                                    onPress={cancelEdit}
                                                >
                                                    <X className='h-4 w-4'/>
                                                </Button>
                                            </>
                                        ) : (
                                            <>
                                                <Button
                                                    isIconOnly size='sm' variant='flat'
                                                    onPress={() => startEdit(tag)}
                                                >
                                                    <Pencil className='h-4 w-4'/>
                                                </Button>
                                                <Button
                                                    isIconOnly size='sm' variant='flat' color='danger'
                                                    onPress={() => confirmDelete(tag)}
                                                >
                                                    <Trash2 className='h-4 w-4'/>
                                                </Button>
                                            </>
                                        )}
                                    </div>
                                </td>
                            </tr>
                        ))}
                        {tags.length === 0 && (
                            <tr>
                                <td colSpan={5} className='px-4 py-8 text-center text-default-400'>
                                    No tags found.
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>

            {/* ── Pagination ── */}
            <AppPagination totalCount={data.totalCount}/>

            {/* ── Delete confirmation modal ── */}
            <Modal isOpen={isOpen} onClose={onClose}>
                <ModalContent>
                    <ModalHeader>Delete tag</ModalHeader>
                    <ModalBody>
                        <p>Are you sure you want to delete the <strong>{deletingTag?.name}</strong> tag?
                            This cannot be undone.</p>
                    </ModalBody>
                    <ModalFooter>
                        <Button variant='flat' onPress={onClose}>Cancel</Button>
                        <Button color='danger' isLoading={isPending} onPress={handleDelete}>Delete</Button>
                    </ModalFooter>
                </ModalContent>
            </Modal>
        </div>
    );
}
