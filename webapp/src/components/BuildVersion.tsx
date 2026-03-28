const sha = process.env.NEXT_PUBLIC_COMMIT_SHA;

export default function BuildVersion() {
    if (!sha) return null;

    const short = sha.slice(0, 7);

    return (
        <span
            title={sha}
            className="fixed bottom-2 left-2 z-50 text-[10px] font-mono text-foreground-400 select-none pointer-events-none hidden sm:inline"
        >
            {short}
        </span>
    );
}

