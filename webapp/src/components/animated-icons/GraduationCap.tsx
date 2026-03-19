'use client';

import type {Variants} from "framer-motion";
import {motion, useAnimation} from "framer-motion";
import type {HTMLAttributes} from "react";
import {forwardRef, useCallback, useImperativeHandle, useRef} from "react";
import clsx from "clsx";

export interface GraduationCapIconHandle {
    startAnimation: () => void;
    stopAnimation: () => void;
}

const CAP_VARIANTS: Variants = {
    normal: {rotate: 0, y: 0},
    animate: {
        rotate: [0, -5, 5, 0],
        y: [0, -2, 0],
        transition: {duration: 0.5, ease: "easeInOut"},
    },
};

const TASSEL_VARIANTS: Variants = {
    normal: {rotate: 0},
    animate: {
        rotate: [0, 15, -10, 5, 0],
        transition: {duration: 0.6, ease: "easeInOut", delay: 0.1},
    },
};

const GraduationCap = forwardRef<GraduationCapIconHandle, HTMLAttributes<HTMLDivElement> & { size?: number }>(
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
                    variants={CAP_VARIANTS}
                >
                    <path d="M21.42 10.922a1 1 0 0 0-.019-1.838L12.83 5.18a2 2 0 0 0-1.66 0L2.6 9.08a1 1 0 0 0 0 1.832l8.57 3.908a2 2 0 0 0 1.66 0z"/>
                    <path d="M22 10v6"/>
                    <motion.path
                        d="M6 12.5V16a6 3 0 0 0 12 0v-3.5"
                        animate={controls}
                        variants={TASSEL_VARIANTS}
                    />
                </motion.svg>
            </div>
        );
    },
);

GraduationCap.displayName = "GraduationCap";
export {GraduationCap};

