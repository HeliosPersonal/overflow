'use client';

import {PaginatedResult, Profile} from "@/lib/types";
import {useState, useRef, useTransition} from "react";
import {adminDeleteUser, adminBulkDeleteUsers, adminUpdateUser} from "@/lib/actions/admin-actions";
import {useForm} from "react-hook-form";
import {zodResolver} from "@hookform/resolvers/zod";
import {z} from "zod";
import {Input} from "@heroui/input";
import {Textarea} from "@heroui/input";
import {Button} from "@heroui/button";
import {Checkbox} from "@heroui/checkbox";
import {Chip} from "@heroui/chip";
import {
    Modal, ModalBody, ModalContent, ModalFooter, ModalHeader, useDisclosure
} from "@heroui/modal";
import {Pencil, Trash2, Check, X, Search} from "lucide-react";
import {handleError, successToast} from "@/lib/util";
import DiceBearAvatar from "@/components/DiceBearAvatar";
import AvatarPicker from "@/components/AvatarPicker";
import {useRouter, useSearchParams} from "next/navigation";
import AppPagination from "@/components/AppPagination";

const updateSchema = z.object({
    displayName: z.string().trim().min(1, 'Required').max(200),
    description: z.string().max(1000).optional(),
});

type UpdateSchema = z.infer<typeof updateSchema>;
type Props = { data: PaginatedResult<Profile> };

export default function UsersTable({data}: Props) {
    const profiles = data.items;
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [editingId, setEditingId] = useState<string | null>(null);
    const [editingAvatar, setEditingAvatar] = useState<string | null>(null);
    const [deletingUser, setDeletingUser] = useState<Profile | null>(null);
    const [bulkDeleting, setBulkDeleting] = useState(false);
    const [isPending, startTransition] = useTransition();
    const {isOpen, onOpen, onClose} = useDisclosure();
    const {isOpen: isBulkOpen, onOpen: onBulkOpen, onClose: onBulkClose} = useDisclosure();
    const router = useRouter();
    const searchParams = useSearchParams();

    const editForm = useForm<UpdateSchema>({
        resolver: zodResolver(updateSchema),
        mode: 'onTouched',
    });

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

    const toggleSelect = (userId: string) => {
        setSelected(prev => {
            const next = new Set(prev);
            next.has(userId) ? next.delete(userId) : next.add(userId);
            return next;
        });
    };

    const toggleAll = () => {
        if (selected.size === profiles.length) {
            setSelected(new Set());
        } else {
            setSelected(new Set(profiles.map(p => p.userId)));
        }
    };

    // ── Edit ──
    const startEdit = (profile: Profile) => {
        setEditingId(profile.userId);
        setEditingAvatar(null);
        editForm.reset({displayName: profile.displayName, description: profile.description ?? ''});
    };

    const handleUpdate = (formData: UpdateSchema, userId: string) => {
        startTransition(async () => {
            const payload: { displayName?: string; description?: string; avatarUrl?: string } = {...formData};
            if (editingAvatar) payload.avatarUrl = editingAvatar;
            const {error} = await adminUpdateUser(userId, payload);
            if (error) {
                handleError(error);
                return;
            }
            successToast('User updated');
            setEditingId(null);
            setEditingAvatar(null);
            router.refresh();
        });
    };

    // ── Delete ──
    const confirmDelete = (profile: Profile) => {
        setDeletingUser(profile);
        onOpen();
    };

    const handleDelete = () => {
        if (!deletingUser) return;
        startTransition(async () => {
            const {error} = await adminDeleteUser(deletingUser.userId);
            if (error) {
                handleError(error);
                return;
            }
            successToast('User deletion queued');
            setSelected(prev => {
                const next = new Set(prev);
                next.delete(deletingUser.userId);
                return next;
            });
            onClose();
            setDeletingUser(null);
            router.refresh();
        });
    };

    // ── Bulk Delete ──
    const handleBulkDelete = () => {
        setBulkDeleting(true);
        startTransition(async () => {
            const {error} = await adminBulkDeleteUsers([...selected]);
            if (error) {
                handleError(error);
                setBulkDeleting(false);
                return;
            }
            successToast(`${selected.size} user deletion(s) queued`);
            setSelected(new Set());
            setBulkDeleting(false);
            onBulkClose();
            router.refresh();
        });
    };

    return (
        <div className="flex flex-col gap-4">
            {/* ── Toolbar ── */}
            <div className="flex items-center justify-between gap-3">
                <form ref={searchFormRef} onSubmit={handleSearch} className="flex items-center gap-2 flex-1 max-w-md">
                    <Input
                        name="search"
                        placeholder="Search by name or email..."
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
                <div className="flex items-center gap-3">
                    <p className="text-sm text-foreground-500">{data.totalCount} user(s)</p>
                    {selected.size > 0 && (
                        <Button
                            color="danger"
                            size="sm"
                            variant="flat"
                            startContent={<Trash2 className="h-4 w-4"/>}
                            onPress={onBulkOpen}
                        >
                            Delete {selected.size} selected
                        </Button>
                    )}
                </div>
            </div>

            {/* ── Table ── */}
            <div className="rounded-xl border border-default-200 overflow-hidden">
                <table className="w-full text-sm">
                    <thead className="bg-default-100 text-default-600">
                    <tr>
                        <th className="px-4 py-3 w-10">
                            <Checkbox
                                isSelected={profiles.length > 0 && selected.size === profiles.length}
                                isIndeterminate={selected.size > 0 && selected.size < profiles.length}
                                onValueChange={toggleAll}
                                size="sm"
                            />
                        </th>
                        <th className="text-left px-4 py-3 font-medium w-14">Avatar</th>
                        <th className="text-left px-4 py-3 font-medium">Display Name</th>
                        <th className="text-left px-4 py-3 font-medium">Email</th>
                        <th className="text-left px-4 py-3 font-medium">Description</th>
                        <th className="text-right px-4 py-3 font-medium w-28">Reputation</th>
                        <th className="text-right px-4 py-3 font-medium w-32">Joined</th>
                        <th className="px-4 py-3 w-24"/>
                    </tr>
                    </thead>
                    <tbody className="divide-y divide-default-100">
                    {profiles.map(profile => (
                        <tr key={profile.userId} className="hover:bg-default-50 transition-colors">
                            <td className="px-4 py-3">
                                <Checkbox
                                    isSelected={selected.has(profile.userId)}
                                    onValueChange={() => toggleSelect(profile.userId)}
                                    size="sm"
                                />
                            </td>
                            <td className="px-4 py-3">
                                {editingId === profile.userId ? (
                                    <AvatarPicker
                                        seed={profile.userId}
                                        value={editingAvatar ?? profile.avatarUrl}
                                        onChange={setEditingAvatar}
                                    >
                                        {({avatarSrc, onOpen}) => (
                                            <button type="button" onClick={onOpen} className="group relative shrink-0">
                                                <img
                                                    src={avatarSrc}
                                                    alt="Avatar"
                                                    className="h-8 w-8 rounded-full border-2 border-primary/40 group-hover:border-primary transition-all"
                                                />
                                                <span className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity text-white text-[8px] font-medium">
                                                    Edit
                                                </span>
                                            </button>
                                        )}
                                    </AvatarPicker>
                                ) : (
                                    <DiceBearAvatar
                                        userId={profile.userId}
                                        avatarJson={profile.avatarUrl}
                                        size="sm"
                                        name={profile.displayName.charAt(0)}
                                    />
                                )}
                            </td>
                            {editingId === profile.userId ? (
                                <td colSpan={4} className="px-4 py-3">
                                    <form
                                        id={`edit-${profile.userId}`}
                                        onSubmit={editForm.handleSubmit(formData => handleUpdate(formData, profile.userId))}
                                        className="flex gap-3 items-start"
                                    >
                                        <Input
                                            {...editForm.register('displayName')}
                                            label="Name"
                                            size="sm"
                                            className="w-48"
                                            isInvalid={!!editForm.formState.errors.displayName}
                                            errorMessage={editForm.formState.errors.displayName?.message}
                                        />
                                        <Textarea
                                            {...editForm.register('description')}
                                            label="Description"
                                            size="sm"
                                            className="flex-1"
                                            minRows={1}
                                            isInvalid={!!editForm.formState.errors.description}
                                            errorMessage={editForm.formState.errors.description?.message}
                                        />
                                    </form>
                                </td>
                            ) : (
                                <>
                                    <td className="px-4 py-3">
                                        <button
                                            className="text-left hover:underline text-foreground-800 font-medium"
                                            onClick={() => router.push(`/profiles/${profile.userId}`)}
                                        >
                                            {profile.displayName}
                                        </button>
                                    </td>
                                    <td className="px-4 py-3 text-default-500 text-xs truncate max-w-45">
                                        {profile.email || '—'}
                                    </td>
                                    <td className="px-4 py-3 text-default-500 line-clamp-1 max-w-xs">
                                        {profile.description || '—'}
                                    </td>
                                    <td className="px-4 py-3 text-right">
                                        <Chip size="sm" variant="flat">{profile.reputation}</Chip>
                                    </td>
                                </>
                            )}
                            <td className="px-4 py-3 text-right text-default-500 text-xs">
                                {profile.joinedAt
                                    ? new Date(profile.joinedAt).toLocaleDateString()
                                    : '—'}
                            </td>
                            <td className="px-4 py-3">
                                <div className="flex items-center justify-end gap-1">
                                    {editingId === profile.userId ? (
                                        <>
                                            <Button
                                                isIconOnly
                                                size="sm"
                                                color="success"
                                                variant="flat"
                                                isLoading={isPending}
                                                onPress={() => editForm.handleSubmit(formData => handleUpdate(formData, profile.userId))()}
                                            >
                                                <Check className="h-4 w-4"/>
                                            </Button>
                                            <Button
                                                isIconOnly size="sm" variant="flat"
                                                onPress={() => { setEditingId(null); setEditingAvatar(null); }}
                                            >
                                                <X className="h-4 w-4"/>
                                            </Button>
                                        </>
                                    ) : (
                                        <>
                                            <Button
                                                isIconOnly size="sm" variant="flat"
                                                onPress={() => startEdit(profile)}
                                            >
                                                <Pencil className="h-4 w-4"/>
                                            </Button>
                                            <Button
                                                isIconOnly size="sm" variant="flat" color="danger"
                                                onPress={() => confirmDelete(profile)}
                                            >
                                                <Trash2 className="h-4 w-4"/>
                                            </Button>
                                        </>
                                    )}
                                </div>
                            </td>
                        </tr>
                    ))}
                    {profiles.length === 0 && (
                        <tr>
                            <td colSpan={8} className="px-4 py-8 text-center text-default-400">
                                No users found.
                            </td>
                        </tr>
                    )}
                    </tbody>
                </table>
            </div>

            {/* ── Pagination ── */}
            <AppPagination totalCount={data.totalCount}/>

            {/* ── Delete Confirmation ── */}
            <Modal isOpen={isOpen} onClose={onClose}>
                <ModalContent>
                    <ModalHeader>Delete user</ModalHeader>
                    <ModalBody>
                        <p>Are you sure you want to delete <strong>{deletingUser?.displayName}</strong>?
                            This will remove the user from Keycloak, delete their profile, questions, answers, and votes.
                            This cannot be undone.</p>
                    </ModalBody>
                    <ModalFooter>
                        <Button variant="flat" onPress={onClose}>Cancel</Button>
                        <Button color="danger" isLoading={isPending} onPress={handleDelete}>Delete</Button>
                    </ModalFooter>
                </ModalContent>
            </Modal>

            {/* ── Bulk Delete Confirmation ── */}
            <Modal isOpen={isBulkOpen} onClose={onBulkClose}>
                <ModalContent>
                    <ModalHeader>Delete {selected.size} user(s)</ModalHeader>
                    <ModalBody>
                        <p>Are you sure you want to delete <strong>{selected.size}</strong> user(s)?
                            All their data (profiles, questions, answers, votes) will be permanently removed.
                            This cannot be undone.</p>
                    </ModalBody>
                    <ModalFooter>
                        <Button variant="flat" onPress={onBulkClose}>Cancel</Button>
                        <Button color="danger" isLoading={bulkDeleting} onPress={handleBulkDelete}>
                            Delete {selected.size} user(s)
                        </Button>
                    </ModalFooter>
                </ModalContent>
            </Modal>
        </div>
    );
}
