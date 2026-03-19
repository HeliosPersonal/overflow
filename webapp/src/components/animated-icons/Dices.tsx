'use client';

import type {Variants} from "framer-motion";
import {motion, useAnimation} from "framer-motion";
import type {HTMLAttributes} from "react";
import {forwardRef, useCallback, useImperativeHandle, useRef} from "react";
import clsx from "clsx";

export interface DicesIconHandle {
    startAnimation: () => void;
    stopAnimation: () => void;
}

const DICE_LEFT_VARIANTS: Variants = {
    normal: {rotate: 0, x: 0},
    animate: {
        rotate: [0, -12, 3, 0],
        x: [0, -2, 0],
        transition: {duration: 0.5, ease: "easeInOut"},
    },
};

const DICE_RIGHT_VARIANTS: Variants = {
    normal: {rotate: 0, x: 0},
    animate: {
        rotate: [0, 10, -3, 0],
        x: [0, 2, 0],
        transition: {duration: 0.5, ease: "easeInOut", delay: 0.08},
    },
};

const DOT_VARIANTS: Variants = {
    normal: {scale: 1, opacity: 1},
    animate: (i: number) => ({
        scale: [0.3, 1.2, 1],
        opacity: [0, 1],
        transition: {delay: 0.2 + i * 0.06, duration: 0.3, ease: "easeOut"},
    }),
};

const Dices = forwardRef<DicesIconHandle, HTMLAttributes<HTMLDivElement> & { size?: number }>(
    ({onMouseEnter, onMouseLeave, className, size = 24, ...props}, ref) => {
        const controls = useAnimation();
        const isControlledRef = useRef(false);

        useImperativeHandle(ref, () => {
            isControlledRef.current = true;
            return {
                startAnimation: () => controls.start("animate"),
                stopAnimation: () => controls.start("normal"),
            };
        });

        const handleMouseEnter = useCallback(
            (e: React.MouseEvent<HTMLDivElement>) => {
                if (!isControlledRef.current) controls.start("animate");
                onMouseEnter?.(e);
            },
            [controls, onMouseEnter],
        );

        const handleMouseLeave = useCallback(
            (e: React.MouseEvent<HTMLDivElement>) => {
                if (!isControlledRef.current) controls.start("normal");
                onMouseLeave?.(e);
            },
            [controls, onMouseLeave],
        );

        return (
            <div
                className={clsx("inline-flex items-center justify-center", className)}
                onMouseEnter={handleMouseEnter}
                onMouseLeave={handleMouseLeave}
                {...props}
            >
                <svg
                    xmlns="http://www.w3.org/2000/svg"
                    width={size}
                    height={size}
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                >
                    {/* Left die body */}
                    <motion.rect
                        width="12" height="12" x="2" y="10" rx="2" ry="2"
                        animate={controls}
                        variants={DICE_LEFT_VARIANTS}
                    />
                    {/* Right die body */}
                    <motion.path
                        d="m17.92 14 3.5-3.5a2.24 2.24 0 0 0 0-3l-5-4.92a2.24 2.24 0 0 0-3 0L10 6"
                        animate={controls}
                        variants={DICE_RIGHT_VARIANTS}
                    />
                    {/* Left die dots */}
                    <motion.path d="M6 18h.01" animate={controls} variants={DOT_VARIANTS} custom={0}/>
                    <motion.path d="M10 14h.01" animate={controls} variants={DOT_VARIANTS} custom={1}/>
                    {/* Right die dots */}
                    <motion.path d="M15 6h.01" animate={controls} variants={DOT_VARIANTS} custom={2}/>
                    <motion.path d="M18 9h.01" animate={controls} variants={DOT_VARIANTS} custom={3}/>
                </svg>
            </div>
        );
    },
);

Dices.displayName = "Dices";
export {Dices};

