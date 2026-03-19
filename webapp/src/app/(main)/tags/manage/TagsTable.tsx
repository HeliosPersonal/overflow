'use client';

import {Tag} from "@/lib/types";
import {useState, useTransition} from "react";
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
import {Pencil, Trash2, Plus, Check, X} from "lucide-react";
import {handleError} from "@/lib/util";

type Props = { tags: Tag[] }

export default function TagsTable({tags: initialTags}: Props) {
    const [tags, setTags] = useState<Tag[]>(initialTags);
    const [editingId, setEditingId] = useState<string | null>(null);
    const [deletingTag, setDeletingTag] = useState<Tag | null>(null);
    const [isPending, startTransition] = useTransition();
    const {isOpen, onOpen, onClose} = useDisclosure();

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

    const handleAdd = (data: TagSchema) => {
        startTransition(async () => {
            const {data: created, error} = await createTag(data);
            if (error) { handleError(error); return; }
            if (created) {
                setTags(prev => [...prev, created].sort((a, b) => a.name.localeCompare(b.name)));
                addForm.reset();
            }
        });
    };

    const startEdit = (tag: Tag) => {
        setEditingId(tag.id);
        editForm.reset({name: tag.name, description: tag.description});
    };

    const cancelEdit = () => setEditingId(null);

    const handleUpdate = (data: UpdateTagSchema, tag: Tag) => {
        startTransition(async () => {
            const {data: updated, error} = await updateTag(tag.id, data);
            if (error) { handleError(error); return; }
            if (updated) {
                setTags(prev => prev.map(t => t.id === tag.id ? updated : t));
                setEditingId(null);
            }
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
            setTags(prev => prev.filter(t => t.id !== deletingTag.id));
            onClose();
            setDeletingTag(null);
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
                                            onSubmit={editForm.handleSubmit(data => handleUpdate(data, tag))}
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
                                                    type='submit'
                                                    form={`edit-${tag.id}`}
                                                    isIconOnly
                                                    size='sm'
                                                    color='success'
                                                    variant='flat'
                                                    isLoading={isPending}
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
                                    No tags yet. Add one above.
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>

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


