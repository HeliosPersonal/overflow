import {Card, CardBody, CardFooter, CardHeader} from "@heroui/card";
import Link from "next/link";
import {Tag} from "@/lib/types";
import {Chip} from "@heroui/chip";
import {Button} from "@heroui/button";

type Props = {
    tag: Tag;
}

export default function TagCard({tag}: Props) {
    return (
        <Link href={`/questions?tag=${tag.slug}`}>
            <Card isHoverable isPressable>
                <CardHeader>
                    <Chip variant='bordered'>
                        {tag.slug}
                    </Chip>
                </CardHeader>
                <CardBody>
                    <p className='line-clamp-3'>{tag.description}</p>
                </CardBody>
                <CardFooter>
                    {tag.usageCount} {tag.usageCount === 1 ? 'question' : 'questions'}
                </CardFooter>
            </Card> </Link>
    );
}