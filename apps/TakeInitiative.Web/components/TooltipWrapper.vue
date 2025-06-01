<template>
    <TooltipProvider :delayDuration="150" disableClosingTrigger>
        <Tooltip>
            <TooltipTrigger><slot name="Trigger" /></TooltipTrigger>
            <TooltipContent>
                <div class="w-[200px] text-wrap">
                    <slot name="Content" />
                </div>
            </TooltipContent>
        </Tooltip>
    </TooltipProvider>
</template>
