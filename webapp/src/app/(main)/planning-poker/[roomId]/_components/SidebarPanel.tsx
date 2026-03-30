'use client';

import {useState} from "react";
import {Button, Chip, Switch, Tooltip} from "@heroui/react";
import {List, Plus, Pencil, Trash2, RefreshCw} from "lucide-react";
import type {PlanningPokerRoom, PlanningPokerRoundHistory} from "@/lib/types";
import {ICON_SM} from "./room-constants";

// ── Timeline row data ────────────────────────────────────────────────────────

type TimelineRowData = {
    key: string;
    roundNum: number;
    label: string;
    isCurrent: boolean;
    isDone: boolean;
    isFuture: boolean;
    taskIndex: number | null;
    history: PlanningPokerRoundHistory | null;
};

function buildTimelineRows(
    room: PlanningPokerRoom, hasTasks: boolean, isArchived: boolean, tasks: string[]
): TimelineRowData[] {
    const rows: TimelineRowData[] = [];

    if (hasTasks) {
        tasks.forEach((task, i) => {
            const roundNum = i + 1;
            const isCurrent = roundNum === room.roundNumber && !isArchived;
            const historyEntry = room.roundHistory.find(h => h.roundNumber === roundNum) ?? null;
            const isDone = !!historyEntry && !isCurrent;
            const isFuture = !isCurrent && !isDone;
            rows.push({key: `task-${i}`, roundNum, label: task, isCurrent, isDone, isFuture, taskIndex: i, history: historyEntry});
        });
    } else {
        room.roundHistory.forEach(h => {
            rows.push({
                key: `done-${h.roundNumber}`, roundNum: h.roundNumber,
                label: h.taskName ?? `Round ${h.roundNumber}`,
                isCurrent: false, isDone: true, isFuture: false, taskIndex: null, history: h,
            });
        });
        if (!isArchived) {
            rows.push({
                key: `current-${room.roundNumber}`, roundNum: room.roundNumber,
                label: `Round ${room.roundNumber}`,
                isCurrent: true, isDone: false, isFuture: false, taskIndex: null, history: null,
            });
        }
    }

    return rows;
}

// ── SidebarPanel ─────────────────────────────────────────────────────────────

export default function SidebarPanel({room, isModerator, isArchived, hasTasks, actionLoading, onAddTask, onDeleteTask, onEditTask, onEnableTasks, onDisableTasks, onRevoteTask}: {
    room: PlanningPokerRoom; isModerator: boolean; isArchived: boolean; hasTasks: boolean;
    actionLoading: string | null; onAddTask: () => void; onDeleteTask: (index: number) => void;
    onEditTask: (index: number, newName: string) => void; onEnableTasks: () => void;
    onDisableTasks: () => void; onRevoteTask: (roundNumber: number) => void;
}) {
    const [editingIndex, setEditingIndex] = useState<number | null>(null);
    const [editValue, setEditValue] = useState('');
    const [expandedRound, setExpandedRound] = useState<number | null>(null);

    function startEdit(index: number, currentName: string) { setEditingIndex(index); setEditValue(currentName); }
    function commitEdit(index: number) { onEditTask(index, editValue); setEditingIndex(null); setEditValue(''); }
    function cancelEdit() { setEditingIndex(null); setEditValue(''); }

    const tasks = room.tasks ?? [];
    const isLoading = actionLoading === 'Tasks';
    const rows = buildTimelineRows(room, hasTasks, isArchived, tasks);

    if (rows.length === 0 && !hasTasks) {
        return (
            <div className="rounded-xl bg-content2 border border-content3 p-4 flex flex-col items-center gap-3">
                <List className="h-8 w-8 text-foreground-300"/>
                <p className="text-sm text-foreground-400 text-center">No rounds yet.</p>
                {isModerator && !isArchived && (
                    <Switch size="sm" isSelected={false} isDisabled={isLoading}
                            onValueChange={() => onEnableTasks()}>
                        <span className="text-xs text-foreground-500">Task list</span>
                    </Switch>
                )}
            </div>
        );
    }

    return (
        <div className="flex flex-col gap-3">
            <div className="flex items-center justify-between px-1">
                <div className="flex items-center gap-2">
                    <List className="h-4 w-4 text-foreground-500"/>
                    <h3 className="text-xs font-semibold uppercase tracking-wider text-foreground-500">
                        {hasTasks ? `Tasks (${tasks.length})` : `Rounds (${rows.length})`}
                    </h3>
                </div>
                {isModerator && !isArchived && (
                    <Tooltip content={hasTasks ? 'Disable task list' : 'Enable task list'}>
                        <div>
                            <Switch size="sm" isSelected={hasTasks} isDisabled={isLoading}
                                onValueChange={(checked) => checked ? onEnableTasks() : onDisableTasks()}
                                aria-label="Toggle task list"/>
                        </div>
                    </Tooltip>
                )}
            </div>

            <div className="flex flex-col gap-1">
                {rows.map(row => (
                    <TaskRow key={row.key} row={row} hasTasks={hasTasks} isModerator={isModerator}
                        isArchived={isArchived} isEditing={editingIndex !== null && row.taskIndex === editingIndex}
                        isExpanded={expandedRound === row.roundNum} editValue={editValue}
                        onEditValueChange={setEditValue} onStartEdit={startEdit} onCommitEdit={commitEdit}
                        onCancelEdit={cancelEdit}
                        onToggleExpand={(roundNum) => setExpandedRound(expandedRound === roundNum ? null : roundNum)}
                        onRevoteTask={onRevoteTask} onDeleteTask={onDeleteTask}/>
                ))}
            </div>

            {isModerator && !isArchived && hasTasks && (
                <div className="flex items-center gap-2 px-1">
                    <Button size="sm" variant="flat" className="flex-1" onPress={onAddTask}
                            isLoading={isLoading} startContent={<Plus className="h-4 w-4"/>}>Add Task</Button>
                </div>
            )}
        </div>
    );
}

// ── TaskRow ──────────────────────────────────────────────────────────────────

function TaskRow({row, hasTasks, isModerator, isArchived, isEditing, isExpanded, editValue, onEditValueChange, onStartEdit, onCommitEdit, onCancelEdit, onToggleExpand, onRevoteTask, onDeleteTask}: {
    row: TimelineRowData; hasTasks: boolean; isModerator: boolean; isArchived: boolean;
    isEditing: boolean; isExpanded: boolean; editValue: string;
    onEditValueChange: (v: string) => void; onStartEdit: (index: number, name: string) => void;
    onCommitEdit: (index: number) => void; onCancelEdit: () => void;
    onToggleExpand: (roundNum: number) => void; onRevoteTask: (roundNumber: number) => void;
    onDeleteTask: (index: number) => void;
}) {
    const canEdit = hasTasks && isModerator && !isArchived && row.taskIndex !== null;
    const canDelete = canEdit && !row.isCurrent;

    const rowBgClass = row.isCurrent ? 'bg-primary/10 border border-primary/30' : 'hover:bg-content3/40';
    const statusDotClass = row.isDone ? 'bg-success/20 text-success'
        : row.isCurrent ? 'bg-primary/20 text-primary' : 'bg-content4 text-foreground-400';
    const labelClass = row.isCurrent ? 'font-semibold text-primary'
        : row.isDone ? 'text-foreground-600' : 'text-foreground-700';

    function handleLabelClick(e: React.MouseEvent) {
        if (isEditing) return;
        if (canEdit) { e.stopPropagation(); onStartEdit(row.taskIndex!, row.label); }
    }

    function handleRowClick() {
        if (isEditing) return;
        if (row.isDone && row.history) onToggleExpand(row.roundNum);
    }

    return (
        <div className="group">
            <div className={`flex items-center gap-2 px-3 py-2.5 sm:py-2 rounded-lg transition-colors text-sm ${rowBgClass}
                    ${row.isDone && row.history ? 'cursor-pointer active:bg-content3/60' : ''}`}
                onClick={handleRowClick}>

                <div className={`w-5 h-5 rounded-full flex items-center justify-center shrink-0 text-[10px] font-bold ${statusDotClass}`}>
                    {row.isDone ? '✓' : row.roundNum}
                </div>

                {isEditing ? (
                    <input className="flex-1 min-w-0 bg-content1 border border-content4 rounded-md px-2 py-1 text-sm text-foreground-700 focus:outline-none focus:border-primary"
                        value={editValue} onChange={e => onEditValueChange(e.target.value)}
                        onKeyDown={e => { if (e.key === 'Enter') onCommitEdit(row.taskIndex!); if (e.key === 'Escape') onCancelEdit(); }}
                        onBlur={() => onCommitEdit(row.taskIndex!)} autoFocus/>
                ) : (
                    <span className={`flex-1 min-w-0 truncate ${labelClass}
                            ${canEdit ? 'cursor-pointer hover:underline decoration-foreground-300' : ''}`}
                        onClick={handleLabelClick}>{row.label}</span>
                )}

                {row.isDone && row.history?.numericAverageDisplay && !isEditing && (
                    <span className="text-sm font-extrabold text-warning tabular-nums shrink-0">
                        {row.history.numericAverageDisplay}
                    </span>
                )}

                {row.isCurrent && !isEditing && (
                    <Chip size="sm" variant="flat" color="primary" className="text-[10px] h-5 shrink-0">Now</Chip>
                )}

                {!isEditing && (
                    <div className="flex items-center gap-0.5 sm:opacity-0 sm:group-hover:opacity-100 transition-opacity shrink-0">
                        {row.isDone && isModerator && !isArchived && (
                            <Tooltip content="Re-estimate">
                                <button type="button" onClick={e => { e.stopPropagation(); onRevoteTask(row.roundNum); }}
                                        className="p-0.5 text-foreground-400 hover:text-warning">
                                    <RefreshCw className={ICON_SM}/></button>
                            </Tooltip>
                        )}
                        {canEdit && (
                            <Tooltip content="Rename">
                                <button type="button" onClick={e => { e.stopPropagation(); onStartEdit(row.taskIndex!, row.label); }}
                                        className="p-0.5 text-foreground-400 hover:text-primary">
                                    <Pencil className={ICON_SM}/></button>
                            </Tooltip>
                        )}
                        {canDelete && (
                            <Tooltip content="Delete task">
                                <button type="button" onClick={e => { e.stopPropagation(); onDeleteTask(row.taskIndex!); }}
                                        className="p-0.5 text-foreground-400 hover:text-danger">
                                    <Trash2 className={ICON_SM}/></button>
                            </Tooltip>
                        )}
                    </div>
                )}
            </div>

            {isExpanded && row.history && <ExpandedRoundDetail history={row.history}/>}
        </div>
    );
}

// ── ExpandedRoundDetail ──────────────────────────────────────────────────────

function ExpandedRoundDetail({history}: { history: PlanningPokerRoundHistory }) {
    const total = Object.values(history.distribution).reduce((s, c) => s + c, 0);
    return (
        <div className="ml-9 mr-3 mt-1 mb-2 rounded-lg bg-content3/60 border border-content4 p-3">
            <div className="flex flex-wrap gap-2">
                {Object.entries(history.distribution)
                    .sort(([, a], [, b]) => b - a)
                    .map(([value, count]) => {
                        const pct = total > 0 ? Math.round((count / total) * 100) : 0;
                        return (
                            <div key={value} className="flex items-center gap-1.5 bg-content2 rounded-md px-2 py-1">
                                <span className="text-xs font-bold text-primary">{value}</span>
                                <span className="text-[10px] text-foreground-400">×{count} · {pct}%</span>
                            </div>
                        );
                    })}
            </div>
            {history.voterCount > 0 && (
                <p className="text-[10px] text-foreground-400 mt-2">
                    {history.voterCount} voter{history.voterCount !== 1 ? 's' : ''}
                </p>
            )}
        </div>
    );
}

