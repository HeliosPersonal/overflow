import {Button} from "@heroui/button";
import Link from "next/link";

export default function LoginButton() {
    return (
        <Button
            as={Link}
            href='/login?callbackUrl=/'
            color='primary'
            variant='bordered'
        >
            Login
        </Button>
    );
}