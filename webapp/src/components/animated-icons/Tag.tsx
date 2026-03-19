'use client';

import type {Variants} from "framer-motion";
import {motion, useAnimation} from "framer-motion";
import type {HTMLAttributes} from "react";
import {forwardRef, useCallback, useImperativeHandle, useRef} from "react";
import clsx from "clsx";

export interface TagIconHandle {
    startAnimation: () => void;
    stopAnimation: () => void;
}

const SWING_VARIANTS: Variants = {
    normal: {rotate: 0},
    animate: {
        rotate: [0, -12, 12, -6, 0],
        transition: {duration: 0.5, ease: "easeInOut"},
    },
};

const Tag = forwardRef<TagIconHandle, HTMLAttributes<HTMLDivElement> & { size?: number }>(
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
                    variants={SWING_VARIANTS}
                >
                    <path d="M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z"/>
                    <circle cx="7.5" cy="7.5" r=".5" fill="currentColor"/>
                </motion.svg>
            </div>
        );
    },
);

Tag.displayName = "Tag";
export {Tag};

