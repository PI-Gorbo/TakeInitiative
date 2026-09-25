<template>
    <Sheet v-model:open="open">
        <SheetContent
            :side="isDesktop ? 'right' : 'bottom'"
            :class="[
                // The built-in close button is too small to tap; ours is 44px.
                'flex flex-col gap-0 p-0 [&>button:last-child]:hidden',
                isDesktop ? 'w-full sm:max-w-md' : 'max-h-[85dvh] rounded-t-xl pb-safe',
            ]">
            <SheetHeader class="flex-row items-center gap-2 space-y-0 border-b py-1 pl-4 pr-1 text-left">
                <SheetTitle class="flex-1">Members</SheetTitle>
                <SheetDescription class="sr-only">
                    The campaign's members, their roles and the join code.
                </SheetDescription>
                <SheetClose
                    class="flex size-11 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                    aria-label="Close members">
                    <X class="size-5" />
                </SheetClose>
            </SheetHeader>
            <div class="flex min-h-0 flex-1 flex-col gap-6 overflow-y-auto p-4">
                <!-- Join code -->
                <section
                    aria-labelledby="join-code-heading"
                    class="flex flex-col gap-3 rounded-lg border p-4">
                    <header class="flex flex-col gap-1">
                        <h2
                            id="join-code-heading"
                            class="font-semibold">
                            Join code
                        </h2>
                        <p class="text-sm text-muted-foreground">
                            Anyone with this code can join as a Player.
                        </p>
                    </header>
                    <div class="flex flex-wrap items-center gap-2">
                        <output
                            class="flex-1 select-all font-mono text-2xl tracking-[0.2em] text-gold"
                            aria-label="Join code">
                            {{ campaign.joinCode }}
                        </output>
                        <Button
                            variant="outline"
                            class="h-11 min-w-11"
                            aria-label="Copy join code"
                            @click="copyCode">
                            <Check
                                v-if="codeCopied"
                                class="text-success-tint" />
                            <Copy v-else />
                            <span class="hidden sm:inline">Copy</span>
                        </Button>
                        <Button
                            class="h-11 min-w-11"
                            @click="shareJoinLink">
                            <Share2 />
                            Share
                        </Button>
                    </div>
                </section>

                <!-- Members -->
                <section
                    aria-labelledby="members-heading"
                    class="flex flex-col gap-2">
                    <h2
                        id="members-heading"
                        class="px-1 font-semibold">
                        Members
                        <span class="text-muted-foreground">{{ campaign.members.length }}</span>
                    </h2>
                    <ul class="flex flex-col divide-y rounded-lg border">
                        <li
                            v-for="member in members"
                            :key="member.memberId"
                            class="flex min-h-14 items-center gap-3 px-4 py-2">
                            <div class="flex min-w-0 flex-1 flex-col">
                                <span class="flex items-center gap-2 truncate">
                                    <span class="truncate">{{ member.username }}</span>
                                    <span
                                        v-if="member.memberId === campaign.currentMemberId"
                                        class="text-xs text-muted-foreground"
                                        >(you)</span
                                    >
                                </span>
                                <span
                                    v-if="member.isOwner"
                                    class="flex items-center gap-1 text-xs text-gold">
                                    <Crown class="size-3" /> Owner
                                </span>
                            </div>

                            <!-- The owner changes roles; the owner stays a DM. -->
                            <div
                                v-if="callerIsOwner && !member.isOwner"
                                role="radiogroup"
                                :aria-label="`${member.username}'s role`"
                                class="flex shrink-0 rounded-md border p-0.5">
                                <button
                                    v-for="role in roles"
                                    :key="role"
                                    type="button"
                                    role="radio"
                                    :aria-checked="member.role === role"
                                    :disabled="savingMemberId === member.memberId"
                                    :class="[
                                        'h-11 min-w-16 rounded px-3 text-sm font-medium transition-colors disabled:opacity-50',
                                        member.role === role
                                            ? 'bg-primary text-primary-foreground'
                                            : 'text-muted-foreground hover:bg-accent',
                                    ]"
                                    @click="setRole(member, role)">
                                    {{ role }}
                                </button>
                            </div>
                            <Badge
                                v-else
                                :variant="member.role === 'DM' ? 'default' : 'secondary'">
                                {{ member.role }}
                            </Badge>
                        </li>
                    </ul>
                </section>
            </div>
        </SheetContent>
    </Sheet>
</template>

<script setup lang="ts">
    import { useMediaQuery } from "@vueuse/core";
    import { Check, Copy, Crown, Share2, X } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { putMemberRoleMutation } from "~/utils/queries/campaign";
    import type { Campaign, CampaignMember, Role } from "~/utils/api/types";
    import { currentMember } from "~/utils/campaign";

    // The 13d members list and join code, opened from the Campaign tab's members
    // button: a sheet from the bottom on a phone, from the side on desktop.
    const props = defineProps<{ campaign: Campaign }>();
    const open = defineModel<boolean>("open", { required: true });

    const campaign = computed(() => props.campaign);
    const isDesktop = useMediaQuery("(min-width: 768px)");

    const roles: Role[] = ["DM", "Player"];

    const callerIsOwner = computed(
        () => currentMember(campaign.value)?.isOwner ?? false
    );

    // The caller first, then the owner, then DMs, then alphabetically.
    const members = computed(() => {
        const c = campaign.value;
        if (!c) return [];
        const rank = (m: CampaignMember) =>
            m.memberId === c.currentMemberId ? 0 : m.isOwner ? 1 : m.role === "DM" ? 2 : 3;
        return [...c.members].sort(
            (a, b) => rank(a) - rank(b) || a.username.localeCompare(b.username)
        );
    });

    // Role changes (owner only).
    const putMemberRole = putMemberRoleMutation();
    const savingMemberId = ref<string | null>(null);
    async function setRole(member: CampaignMember, role: Role) {
        if (member.role === role || savingMemberId.value) return;
        savingMemberId.value = member.memberId;
        await putMemberRole
            .mutateAsync({
                campaignId: props.campaign.id,
                memberId: member.memberId,
                role,
            })
            .catch(() => toast.error("Could not change the member's role."))
            .finally(() => (savingMemberId.value = null));
    }

    // Join code: share the join link, or copy it where sharing is unavailable.
    const joinLink = computed(
        () => `${window.location.origin}/app/campaigns/join/${campaign.value?.joinCode ?? ""}`
    );

    const codeCopied = ref(false);
    async function copyCode() {
        if (!campaign.value) return;
        await copy(campaign.value.joinCode, "Join code copied");
        codeCopied.value = true;
        setTimeout(() => (codeCopied.value = false), 2000);
    }

    async function shareJoinLink() {
        if (!campaign.value) return;
        const data: ShareData = {
            title: `Join ${campaign.value.name}`,
            text: `Join ${campaign.value.name} on Take Initiative with the code ${campaign.value.joinCode}.`,
            url: joinLink.value,
        };
        if (navigator.share && (navigator.canShare?.(data) ?? true)) {
            try {
                await navigator.share(data);
                return;
            } catch (err) {
                // The user closed the share sheet.
                if (err instanceof DOMException && err.name === "AbortError") return;
            }
        }
        await copy(joinLink.value, "Join link copied");
    }

    async function copy(text: string, message: string) {
        try {
            await navigator.clipboard.writeText(text);
            toast.success(message);
        } catch {
            toast.error("Could not copy. Select the code and copy it by hand.");
        }
    }
</script>
