'use client';

import {Button} from "@heroui/button";

export default function LoginButton() {
    return (
        <Button
            href='/login?callbackUrl=/'
            color='primary'
            variant='bordered'
        >
            Login
        </Button>
    );
}