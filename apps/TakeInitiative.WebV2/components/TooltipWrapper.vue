<template>
    <TooltipProvider :delayDuration="150" disableClosingTrigger>
        <Tooltip>
            <TooltipTrigger><slot name="Trigger" /></TooltipTrigger>
            <TooltipContent>
                <div class="text-wrap">
                    <slot name="Content" />
                </div>
            </TooltipContent>
        </Tooltip>
    </TooltipProvider>
</template>
