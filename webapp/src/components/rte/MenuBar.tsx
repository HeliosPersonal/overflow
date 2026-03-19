import {Editor} from "@tiptap/core";
import {useEditorState} from "@tiptap/react";
import {Bold, Code, Italic, Link as LinkIcon, Strikethrough} from "lucide-react";
import {Button} from "@heroui/button";

type Props = {
    editor: Editor | null;
}

export default function MenuBar({editor}: Props) {
    const editorState = useEditorState({
        editor,
        selector: ({editor}) => {
            if (!editor) return null;
            
            return {
                isBold: editor.isActive('bold'),
                isItalic: editor.isActive('italic'),
                isStrike: editor.isActive('strike'),
                isCodeBlock: editor.isActive('codeBlock'),
                isLink: editor.isActive('link'),
            }
        }
    })
    
    if (!editor || !editorState) return null;

    const options = [
        {
            icon: <Bold className='w-5 h-5' />,
            onClick: () => editor.chain().focus().toggleBold().run(),
            pressed: editorState.isBold
        },
        {
            icon: <Italic className='w-5 h-5' />,
            onClick: () => editor.chain().focus().toggleItalic().run(),
            pressed: editorState.isItalic
        },
        {
            icon: <Strikethrough className='w-5 h-5' />,
            onClick: () => editor.chain().focus().toggleStrike().run(),
            pressed: editorState.isStrike
        },
        {
            icon: <Code className='w-5 h-5' />,
            onClick: () => editor.chain().focus().toggleCodeBlock().run(),
            pressed: editorState.isCodeBlock
        },
        {
            icon: <LinkIcon className='w-5 h-5' />,
            onClick: () => editor.chain().focus().toggleLink().run(),
            pressed: editorState.isLink
        },
    ]
    
    return (
        <div className='rounded-md space-x-1 pb-1 z-50'>
            {options.map((option, index) => (
                <Button
                    key={index}
                    type='button'
                    radius='sm'
                    size='sm'
                    isIconOnly
                    color={option.pressed ? 'primary' : 'default'}
                    onPress={option.onClick}
                >
                    {option.icon}
                </Button>
            ))}
        </div>
    );
}