'use client';

import type {Variants} from "framer-motion";
import {motion, useAnimation} from "framer-motion";
import type {HTMLAttributes} from "react";
import {forwardRef, useCallback, useImperativeHandle, useRef} from "react";
import clsx from "clsx";

export interface TrendingUpIconHandle {
    startAnimation: () => void;
    stopAnimation: () => void;
}

const DRAW_VARIANTS: Variants = {
    normal: {pathLength: 1, opacity: 1},
    animate: {
        pathLength: [0, 1],
        opacity: [0, 1],
        transition: {duration: 0.5, ease: "easeInOut"},
    },
};

const ARROW_VARIANTS: Variants = {
    normal: {opacity: 1},
    animate: {
        opacity: [0, 1],
        transition: {delay: 0.3, duration: 0.3},
    },
};

const TrendingUp = forwardRef<TrendingUpIconHandle, HTMLAttributes<HTMLDivElement> & { size?: number }>(
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
                    <motion.polyline
                        points="22 7 13.5 15.5 8.5 10.5 2 17"
                        animate={controls}
                        variants={DRAW_VARIANTS}
                    />
                    <motion.polyline
                        points="16 7 22 7 22 13"
                        animate={controls}
                        variants={ARROW_VARIANTS}
                    />
                </svg>
            </div>
        );
    },
);

TrendingUp.displayName = "TrendingUp";
export {TrendingUp};

