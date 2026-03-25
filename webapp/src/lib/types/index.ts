export type PaginatedResult<T> = {
    items: T[];
    totalCount: number;
    page: number;
    pageSize: number;
}

export type QuestionParams = {
    tag?: string;
    page?: number;
    pageSize?: number;
    sort?: string;
}

export type Question = {
    id: string
    title: string
    content: string
    askerId: string
    author?: Profile
    createdAt: string
    updatedAt?: string
    viewCount: number
    tagSlugs: string[]
    hasAcceptedAnswer: boolean
    votes: number
    answerCount: number
    answers: Answer[]
    userVoted: number
}

export type Answer = {
    id: string
    content: string
    userId: string
    author?: Profile
    createdAt: string
    updatedAt?: string
    accepted: boolean
    questionId: string
    votes: number
    userVoted: number
}

export type Tag = {
    id: string
    name: string
    slug: string
    description: string
    usageCount: number
}

export type TrendingTag = {
    tag: string
    count: number
}

export type Profile = {
    userId: string
    displayName: string
    description?: string
    reputation: number
    avatarUrl?: string | null
    joinedAt?: string
}

export type FetchResponse<T> = {
    data: T | null
    error?: {message: string, status: number}
}

export type VoteRecord = {
    targetId: string
    targetType: 'Question' | 'Answer'
    voteValue: number
}

export type Vote = {
    targetId: string
    targetType: 'Question' | 'Answer'
    targetUserId: string
    questionId: string
    voteValue: 1 | -1
}

export type TopUser = {
    userId: string
    delta: number
}

export type TopUserWithProfile = TopUser & {profile?: Profile}

// ── Planning Poker ──────────────────────────────────────────────────────

export type PlanningPokerStatus = 'Voting' | 'Revealed' | 'Archived'

export type PlanningPokerDeck = {
    id: string
    name: string
    values: string[]
}

export type PlanningPokerViewer = {
    participantId: string
    userId?: string
    guestId?: string
    displayName: string
    isGuest: boolean
    isModerator: boolean
    isSpectator: boolean
    selectedVote?: string | null
}

export type PlanningPokerParticipant = {
    participantId: string
    displayName: string
    avatarUrl?: string | null
    isGuest: boolean
    isModerator: boolean
    isSpectator: boolean
    hasVoted: boolean
    revealedVote?: string | null
    isPresent: boolean
}

export type PlanningPokerRoundSummary = {
    roundNumber: number
    taskName?: string | null
    status: PlanningPokerStatus
    votesRevealed: boolean
    distribution?: Record<string, number> | null
    numericAverage?: number | null
    numericAverageDisplay?: string | null
    activeVoterCount: number
    spectatorCount: number
    availableDeck: PlanningPokerDeck
}

export type PlanningPokerRoundHistory = {
    roundNumber: number
    taskName?: string | null
    voterCount: number
    distribution: Record<string, number>
    numericAverage?: number | null
    numericAverageDisplay?: string | null
}

export type PlanningPokerParticipantSummary = {
    displayName: string
    avatarUrl?: string | null
}

export type PlanningPokerRoomSummary = {
    roomId: string
    title: string
    status: PlanningPokerStatus
    roundNumber: number
    participantCount: number
    completedRounds: number
    createdAtUtc: string
    updatedAtUtc: string
    archivedAtUtc?: string | null
    isModerator?: boolean
    archivedDaysBeforeDelete: number
    inactiveDaysBeforeArchive: number
    creatorDisplayName: string
    creatorAvatarUrl?: string | null
    participants: PlanningPokerParticipantSummary[]
}

export type PlanningPokerRoom = {
    roomId: string
    title: string
    canonicalUrl: string
    status: PlanningPokerStatus
    roundNumber: number
    deck: PlanningPokerDeck
    isArchived: boolean
    isReadOnly: boolean
    viewer: PlanningPokerViewer
    participants: PlanningPokerParticipant[]
    roundSummary: PlanningPokerRoundSummary
    roundHistory: PlanningPokerRoundHistory[]
    tasks?: string[] | null
    currentTaskName?: string | null
}

