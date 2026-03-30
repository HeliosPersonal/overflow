'use client';

import {motion} from "framer-motion";

export default function CardPicker({visible, deckValues, effectiveVote, isVoting, onVote, onClearVote}: {
    visible: boolean;
    deckValues: string[];
    effectiveVote: string | null | undefined;
    isVoting: boolean;
    onVote: (value: string) => void;
    onClearVote: () => void;
}) {
    return (
        <div className={`shrink-0 z-20 pointer-events-none
            transition-all duration-500 ease-out
            ${visible ? 'translate-y-0 opacity-100' : 'translate-y-full opacity-0'}`}>
            <div className="flex justify-center px-2 pb-2 pt-1 pointer-events-auto">
                <div className="bg-content2/95 backdrop-blur-xl border border-content3/80
                    shadow-[0_-2px_24px_rgba(0,0,0,0.10)] rounded-xl px-2 sm:px-4 py-2 sm:py-2.5 max-w-fit">
                    <div className="flex items-center justify-center gap-1.5 sm:gap-2 flex-wrap">
                        {deckValues.map(v => {
                            const isSelected = effectiveVote === v;
                            return (
                                <motion.button
                                    key={v}
                                    onClick={isVoting ? () => isSelected ? onClearVote() : onVote(v) : undefined}
                                    disabled={!isVoting}
                                    whileHover={isVoting && !isSelected ? {y: -3, scale: 1.05} : {}}
                                    whileTap={isVoting ? {scale: 0.92} : {}}
                                    animate={isSelected ? {y: -6, scale: 1.08} : {y: 0, scale: 1}}
                                    transition={{type: 'spring', stiffness: 400, damping: 25}}
                                    className={`
                                        w-[40px] h-[56px] sm:w-[50px] sm:h-[70px] rounded-lg border-2 text-sm sm:text-base font-bold
                                        flex items-center justify-center select-none
                                        ${isVoting ? 'cursor-pointer' : 'cursor-default opacity-40'}
                                        ${isSelected
                                            ? 'border-primary bg-primary text-white shadow-lg shadow-primary/30'
                                            : isVoting
                                                ? 'border-content4 bg-content1 text-foreground-600 hover:border-primary/40 hover:shadow-md'
                                                : 'border-content4 bg-content1 text-foreground-600'}
                                    `}>
                                    {v}
                                </motion.button>
                            );
                        })}
                    </div>
                </div>
            </div>
        </div>
    );
}

