'use client';

import type {Variants} from "framer-motion";
import {motion, useAnimation} from "framer-motion";
import type {HTMLAttributes} from "react";
import {forwardRef, useCallback, useImperativeHandle, useRef} from "react";
import clsx from "clsx";

export interface TrophyIconHandle {
    startAnimation: () => void;
    stopAnimation: () => void;
}

const CUP_VARIANTS: Variants = {
    normal: {rotate: 0},
    animate: {
        rotate: [0, -10, 10, -5, 5, 0],
        transition: {duration: 0.5, ease: "easeInOut"},
    },
};

const SPARKLE_VARIANTS: Variants = {
    normal: {opacity: 1},
    animate: (i: number) => ({
        opacity: [0, 1],
        transition: {delay: 0.1 + i * 0.12, duration: 0.3},
    }),
};

const Trophy = forwardRef<TrophyIconHandle, HTMLAttributes<HTMLDivElement> & { size?: number }>(
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
                <motion.svg
                    xmlns="http://www.w3.org/2000/svg"
                    width={size}
                    height={size}
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    animate={controls}
                    variants={CUP_VARIANTS}
                    style={{originY: 1}}
                >
                    <motion.path d="M6 9H4.5a2.5 2.5 0 0 1 0-5H6" animate={controls} variants={SPARKLE_VARIANTS} custom={0}/>
                    <motion.path d="M18 9h1.5a2.5 2.5 0 0 0 0-5H18" animate={controls} variants={SPARKLE_VARIANTS} custom={1}/>
                    <path d="M4 22h16"/>
                    <motion.path d="M10 14.66V17c0 .55-.47.98-.97 1.21C7.85 18.75 7 20.24 7 22" animate={controls} variants={SPARKLE_VARIANTS} custom={2}/>
                    <motion.path d="M14 14.66V17c0 .55.47.98.97 1.21C16.15 18.75 17 20.24 17 22" animate={controls} variants={SPARKLE_VARIANTS} custom={3}/>
                    <path d="M18 2H6v7a6 6 0 0 0 12 0V2Z"/>
                </motion.svg>
            </div>
        );
    },
);

Trophy.displayName = "Trophy";
export {Trophy};

