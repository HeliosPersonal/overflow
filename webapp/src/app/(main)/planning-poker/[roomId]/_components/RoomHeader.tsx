'use client';

import {
    Button, Chip, Tooltip,
    Dropdown, DropdownTrigger, DropdownMenu, DropdownItem,
} from "@heroui/react";
import {
    ClipboardCopy, LogOut, Eye, EyeOff, Archive, Pencil, Menu, MoreVertical,
} from "lucide-react";
import type {PlanningPokerRoom} from "@/lib/types";
import {ICON_SM, TITLE_MAX_LENGTH} from "./room-constants";

const STATUS_COLOR_MAP: Record<string, 'success' | 'primary' | 'warning'> = {
    Voting: 'success', Revealed: 'primary', Archived: 'warning',
};

function StatusBadge({status}: { status: string }) {
    return <Chip size="sm" color={STATUS_COLOR_MAP[status] ?? 'default'} variant="bordered">{status}</Chip>;
}

export default function RoomHeader({room, editingTitle, titleDraft, onTitleDraftChange, onStartEditingTitle, onRename, onCancelEditTitle, isModerator, isSpectator, isArchived, wsStatus, actionLoading, sidebarOpen, onCopyLink, onModeToggle, onArchive, onLeave, onToggleSidebar}: {
    room: PlanningPokerRoom;
    editingTitle: boolean;
    titleDraft: string;
    onTitleDraftChange: (v: string) => void;
    onStartEditingTitle: () => void;
    onRename: () => void;
    onCancelEditTitle: () => void;
    isModerator: boolean;
    isSpectator: boolean;
    isArchived: boolean;
    wsStatus: string;
    actionLoading: string | null;
    sidebarOpen: boolean;
    onCopyLink: () => void;
    onModeToggle: () => void;
    onArchive: () => void;
    onLeave: () => void;
    onToggleSidebar: () => void;
}) {
    const canEditTitle = isModerator && !isArchived;

    return (
        <div className="border-b border-content3 bg-content2/80 backdrop-blur-md z-10 shrink-0">
            <div className="max-w-[1600px] mx-auto px-2 sm:px-3 h-12 flex items-center gap-1 sm:gap-2">
                {/* Left: title + status */}
                <div className="flex items-center justify-center gap-1.5 sm:gap-2 min-w-0 flex-1">
                    {editingTitle ? (
                        <input
                            className="text-base sm:text-lg font-bold bg-content1 border border-primary rounded-md px-2 py-0.5 min-w-0 w-full sm:w-96 text-center
                            focus:outline-none focus:ring-2 focus:ring-primary/40"
                            value={titleDraft}
                            onChange={e => onTitleDraftChange(e.target.value)}
                            onKeyDown={e => {
                                if (e.key === 'Enter') onRename();
                                if (e.key === 'Escape') onCancelEditTitle();
                            }}
                            onBlur={onRename}
                            maxLength={TITLE_MAX_LENGTH}
                            autoFocus
                        />
                    ) : (
                        <div
                            className={`flex items-center gap-1 sm:gap-1.5 min-w-0 max-w-[60%] sm:max-w-[50%] ${canEditTitle ? 'group cursor-pointer' : ''}`}
                            onClick={canEditTitle ? onStartEditingTitle : undefined}>
                            <h1 className="text-sm sm:text-lg font-bold truncate text-center">{room.title}</h1>
                            {canEditTitle && (
                                <Pencil className={`${ICON_SM} text-foreground-400 opacity-0 group-hover:opacity-100 transition-opacity shrink-0`}/>
                            )}
                        </div>
                    )}
                    <StatusBadge status={room.status}/>
                    {wsStatus !== 'connected' && (
                        <Chip size="sm" color="warning" variant="dot" className="shrink-0 hidden sm:inline-flex">
                            {wsStatus === 'connecting' ? 'Reconnecting…' : 'Offline'}
                        </Chip>
                    )}
                </div>

                {/* Right: actions */}
                <div className="flex items-center gap-1 sm:gap-1.5 shrink-0">
                    <Tooltip content="Copy room link">
                        <Button size="sm" variant="flat" isIconOnly onPress={onCopyLink}>
                            <ClipboardCopy className={ICON_SM}/>
                        </Button>
                    </Tooltip>
                    {!isArchived && (
                        <span className="hidden sm:inline-flex">
                            <Tooltip content={isSpectator ? 'Switch to Participant' : 'Switch to Spectator'}>
                                <Button size="sm" variant="flat" isIconOnly
                                        onPress={onModeToggle} isLoading={actionLoading === 'Mode'}>
                                    {isSpectator ? <EyeOff className={ICON_SM}/> : <Eye className={ICON_SM}/>}
                                </Button>
                            </Tooltip>
                        </span>
                    )}
                    {canEditTitle && (
                        <span className="hidden sm:inline-flex">
                            <Tooltip content="Archive room">
                                <Button size="sm" variant="flat" color="danger" isIconOnly
                                        onPress={onArchive} isLoading={actionLoading === 'Archive'}>
                                    <Archive className={ICON_SM}/>
                                </Button>
                            </Tooltip>
                        </span>
                    )}
                    <Tooltip content="Leave room">
                        <Button size="sm" variant="flat" color="danger" isIconOnly onPress={onLeave}>
                            <LogOut className={ICON_SM}/>
                        </Button>
                    </Tooltip>
                    <div className="w-px h-6 bg-content3 mx-0.5 hidden sm:block"/>
                    <Tooltip content="Tasks & History">
                        <Button size="sm" variant={sidebarOpen ? 'solid' : 'flat'} isIconOnly onPress={onToggleSidebar}>
                            <Menu className={ICON_SM}/>
                        </Button>
                    </Tooltip>
                    <span className="sm:hidden">
                        <Dropdown placement="bottom-end">
                            <DropdownTrigger>
                                <Button size="sm" variant="flat" isIconOnly aria-label="More actions">
                                    <MoreVertical className={ICON_SM}/>
                                </Button>
                            </DropdownTrigger>
                            <DropdownMenu aria-label="More actions">
                                {!isArchived ? (
                                    <DropdownItem
                                        key="mode"
                                        startContent={isSpectator ? <EyeOff className={ICON_SM}/> : <Eye className={ICON_SM}/>}
                                        onPress={onModeToggle}
                                    >
                                        {isSpectator ? 'Switch to Participant' : 'Switch to Spectator'}
                                    </DropdownItem>
                                ) : null}
                                {canEditTitle ? (
                                    <DropdownItem
                                        key="archive"
                                        startContent={<Archive className={ICON_SM}/>}
                                        className="text-danger"
                                        color="danger"
                                        onPress={onArchive}
                                    >
                                        Archive room
                                    </DropdownItem>
                                ) : null}
                            </DropdownMenu>
                        </Dropdown>
                    </span>
                </div>
            </div>
        </div>
    );
}

