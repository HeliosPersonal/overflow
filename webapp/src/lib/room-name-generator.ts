const ADJECTIVES = [
    'Confused', 'Caffeinated', 'Sleepy', 'Chaotic', 'Legendary', 'Agile',
    'Speedy', 'Haunted', 'Grumpy', 'Overengineered', 'Quantum', 'Async',
    'Blazing', 'Infinite', 'Yolo', 'Verbose', 'Recursive', 'Stubborn',
    'Panicking', 'Heroic', 'Mystery', 'Spaghetti', 'Rubber-Duck', 'Spooky',
    'Unstoppable', 'Vintage', 'Nuclear', 'Cursed', 'Fuzzy', 'Sneaky',
    'Hyper', 'Lazy', 'Frantic', 'Philosophical', 'Immortal', 'Enchanted',
    'Turbo', 'Rusty', 'Passive-Aggressive', 'Optimistic', 'Pessimistic',
    'Distracted', 'Midnight', 'Caffeinated', 'Semi-Functional', 'Rogue',
    'Sentient', 'Over-Caffeinated', 'Delirious', 'Paranoid', 'Catastrophic',
    'Wholesome', 'Awkward', 'Suspicious', 'Unhinged', 'Elite', 'Budget',
    'Premium', 'Enterprise', 'Serverless', 'Distributed', 'Cloud-Native',
    'On-Call', 'Sleep-Deprived', 'Highly-Available', 'Containerized', 'Forked',
    'Deprecated', 'Legacy', 'Beta', 'Experimental', 'Refactored', 'Monolithic',
    'Sarcastic', 'Caffeinated', 'Terrified', 'Overcommitted', 'Hypnotized',
    'Galactic', 'Invisible', 'Glorified', 'Underestimated', 'Overpromised',
    'Ninja-Level', 'Copy-Pasted', 'Stack-Traced', 'Rubber-Stamped', 'Hard-Coded',
    'Bikeshedding', 'Over-Scoped', 'Secretly-Brilliant', 'Barely-Tested',
    'Technically-Correct', 'Works-On-My-Machine', 'Aggressively-Mediocre',
    'Surprisingly-Functional', 'Absolutely-Not-Production-Ready', 'Vibes-Based',
];

const NOUNS = [
    'Unicorns', 'Devs', 'Sprinters', 'Estimators', 'Architects', 'Backloggers',
    'Pirates', 'Ninjas', 'Wizards', 'Penguins', 'Robots', 'Hamsters',
    'Debuggers', 'Rubber Ducks', 'Story Points', 'Stakeholders', 'Pivots',
    'Standups', 'Retros', 'Vikings', 'Ticket Crushers', 'Merge Conflicts',
    'Pull Requests', 'Hotfixes', 'Scrum Lords', 'Tech Debtors', 'Pod People',
    'Deploys', 'Firefighters', 'Ship-It Crew',
    'Jira Tickets', 'Sprint Goals', 'Velocity Charts', 'Burndown Curves',
    'On-Call Heroes', 'Stack Tracers', 'Pipeline Breakers', 'Code Reviewers',
    'Feature Flags', 'Kanban Monks', 'Backlog Groomers', 'Release Blockers',
    'Coffee Drinkers', 'Keyboard Warriors', 'Terminal Jockeys', 'Tab Hoarders',
    'Microservices', 'Monoliths', 'Docker Whales', 'Kubernetes Cowboys',
    'API Consumers', 'Cache Invalidators', 'Race Conditions', 'Deadlocks',
    'Null Pointers', 'Stack Overflows', 'Memory Leaks', 'Off-By-One Errors',
    'Dark Patterns', 'Edge Cases', 'Regression Gremlins', 'Flaky Tests',
    '10x Engineers', 'Pair Programmers', 'Senior Juniors', 'Junior Seniors',
    'Scope Creepers', 'Deadline Chasers', 'Estimation Wizards', 'Velocity Killers',
    'Ticket Farmers', 'Story Point Hoarders', 'Grooming Survivors', 'Daily Standuppers',
    'Definition-of-Done Deniers', 'Acceptance Criteria Writers', 'PM Whisperers',
    'Infinite Loopers', 'Git Blame Avoiders', 'Force Pushers', 'Rebase Resisters',
    'LGTM Clickers', 'Comment Resolvers', 'Todo Leavers', 'Console Loggers',
    'Environment Variables', 'YAML Engineers', 'Config Wranglers', 'Secret Rotators',
    'Incident Responders', 'Postmortem Writers', 'Runbook Followers', 'Alert Fatiguers',
    'Dependency Updaters', 'Breaking Changers', 'Semver Abusers', 'Changelog Skippers',
    'Fibonacci Believers', 'T-Shirt Sizers', 'Infinity-Pointers', 'Zero-Pointers',
];

export function generateRoomName(): string {
    const adj = ADJECTIVES[Math.floor(Math.random() * ADJECTIVES.length)];
    const noun = NOUNS[Math.floor(Math.random() * NOUNS.length)];
    return `${adj} ${noun}`;
}