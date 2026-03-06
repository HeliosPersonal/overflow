const sha = process.env.NEXT_PUBLIC_COMMIT_SHA;

export default function BuildVersion() {
    if (!sha) return null;

    const short = sha.slice(0, 7);

    return (
        <span
            title={sha}
            className="fixed bottom-2 left-2 z-50 text-[10px] font-mono text-neutral-400 dark:text-neutral-600 select-none pointer-events-none"
        >
            {short}
        </span>
    );
}

