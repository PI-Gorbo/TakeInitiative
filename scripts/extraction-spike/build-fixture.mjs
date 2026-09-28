// Builds step 23a's invented test set (apps/TakeInitiative.Web/tests/fixtures/extraction/).
// Every note, name and place below is made up for this file. Gold spans are written inline as
// {text|K} (K: C Character, P Place, F Faction, I Item, E Event) and turned into offsets here,
// so the JSON is never edited by hand. Gold spans leave out a leading "the" and a possessive 's.
// Usage: node build-fixture.mjs
import { mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const out = resolve(dirname(fileURLToPath(import.meta.url)), "../../apps/TakeInitiative.Web/tests/fixtures/extraction");
const kinds = { C: "Character", P: "Place", F: "Faction", I: "Item", E: "Event" };

const entries = [
    ["e1", "Rellan Ashvale", "Character", ["Rellan"]],
    ["e2", "Greyhollow Keep", "Place", []],
    ["e3", "The Ember Court", "Faction", ["Ember Court"]],
    ["e4", "The Salt Road", "Place", ["Salt Road"]],
    ["e5", "The Moonfall Blade", "Item", ["Moonfall Blade"]],
    ["e6", "The Night of Ash", "Event", ["Night of Ash"]],
    ["e7", "Mara Quillfeather", "Character", ["Mara"]],
    ["e8", "Ash", "Character", []],
    ["e9", "Brother Odo", "Character", ["Odo"]],
    ["e10", "Vessa Thornwick", "Character", ["Vessa"]],
    ["e11", "Captain Irzel Dunmore", "Character", ["Irzel", "Captain Dunmore"]],
    ["e12", "Kettlewick", "Place", []],
    ["e13", "The Drowned Library", "Place", ["Drowned Library"]],
    ["e14", "Harrowmere", "Place", []],
    ["e15", "The Gilded Hand", "Faction", ["Gilded Hand"]],
    ["e16", "The Order of the Pale Lantern", "Faction", ["Pale Lantern"]],
    ["e17", "Stonejaw Clan", "Faction", []],
    ["e18", "The Cinder Crown", "Item", ["Cinder Crown"]],
    ["e19", "Orb of Whispers", "Item", []],
    ["e20", "Skyglass Compass", "Item", []],
    ["e21", "The Siege of Varn", "Event", ["Siege of Varn"]],
    ["e22", "The Long Winter", "Event", ["Long Winter"]],
    ["e23", "Varn", "Place", []],
    ["e24", "Lady Sefa Morrow", "Character", ["Sefa", "Lady Sefa"]],
    ["e25", "Tollan Bridge", "Place", []],
    ["e26", "Aldous Venn", "Character", []],
    ["e27", "The Harvest Moot", "Event", ["Harvest Moot"]],
].map(([id, name, kind, aliases]) => ({ id, name, kind, aliases }));

const short = [
    "met {rellan|C} at the gates of {Greyhollow Keep|P}, and the {Ember Court|F}'s spies watched from the wall.",
    "{Mara|C} rolled 2d6+3 on the lock. Nothing. We camp on the {Salt Road|P} tonight and try again at dawn.",
    "pizza's here, break for 20 min. Everyone owes the DM a slice for that TPK joke. Back at quarter past.",
    "{Ash|C} growled at the ash drifting over the field. Something burned here, and not that long ago either.",
    "**Loot:** the {Moonfall Blade|I}, 40 gp, a bent spoon. {Vessa|C} wants the blade. Nobody argued with her.",
    "Rolled initiative: 17, 12, 9, 4. Two goblins down in round one, one ran for the trees. We let it go.",
    "@[Rellan Ashvale](entry:e1) paid the toll at {Tollan Bridge|P} with {Stonejaw Clan|F} coin. Suspicious, honestly.",
    "{Brother Odo|C} says the {Night of Ash|E} was no accident. He would not say more in front of {Irzel|C}.",
    "- bought rope\n- sold the {Orb of Whispers|I} to a fence in {Harrowmere|P}\n- {Sefa|C} hates us now, fair enough",
    "Remember to ask the DM about the long rest rules. Also bring dice next week, mine are cursed. Again.",
    "{Kettlewick|P} is empty. Doors open, bread on the tables, no one home. {Mara|C} thinks it's the {Gilded Hand|F}.",
    "{Captain Dunmore|C} gave us a writ for the {Drowned Library|P}. We leave at first light, weather permitting.",
    "the {Cinder Crown|I} is real. {Odo|C} saw it in a dream and woke up with burned palms. Creepy stuff.",
    "{Varn|P} still has scars from the {Siege of Varn|E}. Walls patched with cart wood and old doors.",
    "we owe the {Pale Lantern|F} a favour now. {vessa|C} is not happy about it. Neither am I, honestly.",
];

const typical = [
    "Session opened in {Harrowmere|P}. {Rellan|C} met us at the docks with a crate he wouldn't open, and {Captain Irzel Dunmore|C} had the harbour watch looking the other way. The crate ticked. **Nobody** asked. We rowed out to the {Drowned Library|P} at dusk and found the east stair flooded to the knee. {Mara|C} found a door marked with the {Gilded Hand|F}'s sigil and refused to go near it. Good instinct.",
    "Long talk with {Lady Sefa Morrow|C} about the {Long Winter|E}. She was a girl in {Kettlewick|P} when the snow came and didn't leave for three years. Her father sold the family's {Skyglass Compass|I} for grain, and she has been trying to buy it back ever since. She thinks the {Stonejaw Clan|F} has it now. We promised to ask around. {Vessa|C} rolled a 3 on insight, so who knows if any of it is true.",
    "Combat notes: the wight hit {Odo|C} twice (14 and 9 damage), then {Ash|C} dragged it off him. {Mara|C} cast bless, I missed everything. 1d8+2, 1d8+2, 1d8+2, all under 5. The wight dropped a ring of black iron with a tiny lantern stamped inside it. Probably {Pale Lantern|F} work? Keep it, don't wear it. Loot split: 60 gp each, the ring stays with {Odo|C}.",
    "Quiet day. Shopping in town, argued about whether to buy a cart, ended up buying a mule instead. We named her {Biscuit|C} after a long vote. Next week we head north along the {Salt Road|P} if the weather holds. Everyone is level 4 now, so remember to update hit points and pick new spells before we start. The mule already bit the paladin twice, which everyone agrees is a good sign.",
    "The {Ember Court|F} sent a letter. Wax seal, red ribbon, very polite, very threatening. They want the {Moonfall Blade|I} back by the new moon or they will 'consider {Greyhollow Keep|P} forfeit'. {Rellan|C} laughed, then stopped laughing when he read the second page. Apparently the blade was stolen on the {Night of Ash|E}, and they think we know who did it. We do. It was {Vessa|C}.",
    "## Recap\n\nWe crossed {Tollan Bridge|P} at noon. The toll keeper, a dwarf called {Hobb|C}, charged us double because {Mara|C} insulted his beard in session 12. Fair. On the far side the road splits: west to {Varn|P}, north to the {Stonejaw Clan|F} holds. We went west. Rain the whole way. {Brother Odo|C} prayed for dry socks and got a nat 20, so now his socks are dry and ours are not.",
    "met with @[Lady Sefa Morrow](entry:e24) and @[Rellan Ashvale](entry:e1) at the inn. {Sefa|C} wants us to find the {Skyglass Compass|I} before the {Harvest Moot|E}, when the {Stonejaw Clan|F} trades in {Kettlewick|P}. {rellan|C} will come along. He says he knows a shortcut through the old mines under {Greyhollow Keep|P}. He does not. We got lost for six hours.",
    "{Irzel|C} is lying to us. Three things don't add up:\n1. The watch log says the gate was locked all night.\n2. {Ash|C} tracked the thief to the harbour, not the gate.\n3. The {Gilded Hand|F} paid {Irzel|C}'s bar tab the next morning.\nWe are going to confront her tomorrow, carefully. {Vessa|C} wants to just stab someone. Outvoted 3 to 1.",
    "The {Order of the Pale Lantern|F} holds a chapel in the ruins east of {Harrowmere|P}. Six knights, one old priest, lots of candles. They say the {Cinder Crown|I} was forged during the {Siege of Varn|E} and carried out of the city by a knight named {Aldous Venn|C}, who never came back. The priest gave {Odo|C} a map. The map is mostly blank. Priest says 'the blank parts are the important parts'. Great.",
    "Short session, two players out. The rest of us did downtime in {Kettlewick|P}. {Mara|C} brewed potions (2 successes, 1 fail that smells like feet). I trained with the smith's daughter, {Tilly|C}, who is better with a hammer than I am with a sword. {Ash|C} slept by the fire all day. Honestly the best session in a while. No one died, no one got cursed, and the bread was fresh.",
    "Things we know about the {Night of Ash|E}:\n- it happened eleven years ago\n- {Greyhollow Keep|P} burned for three days\n- the {Ember Court|F} blamed the {Stonejaw Clan|F}\n- the {Stonejaw Clan|F} blamed the {Ember Court|F}\n- {Rellan|C} was there, and won't talk about it\nOdd that both sides blame each other and neither has proof. Somebody else lit that fire.",
    "Dice were cold tonight. I rolled four 1s in a row and {Vessa|C}'s player rolled a 20 on a 1d20+7 stealth check, which was wasted because we were already hiding. The fight at the mill was ugly: two cultists, one ogre, one very angry miller. We saved the miller. The ogre's name was {Grumm|C}, according to the tattoo on its arm. RIP {Grumm|C}.",
    "The {Drowned Library|P}, level two. Water to the waist. {Mara|C} found a shelf of dry books behind a glass wall, including a ledger from the {Long Winter|E} listing grain sold to {Harrowmere|P}. One entry says: '40 sacks, paid with a compass'. The {Skyglass Compass|I}? Has to be. {Brother Odo|C} copied the page. We left before the tide came back in.",
    "{Captain Irzel Dunmore|C} confessed. She did take money from the {Gilded Hand|F}, but only to keep her brother out of prison. She gave us the name of their contact: a fence called {Moss|C} who works out of the fish market. {Moss|C} is also a common word so write it with a capital M. We let {Irzel|C} go. {Vessa|C} sulked for the rest of the night.",
    "Travel day on the {Salt Road|P}. Random encounter: a merchant caravan from {Varn|P} with a broken axle. We helped fix it (DC 12, success) and they gave us a cask of wine and a rumour. The rumour: the {Ember Court|F} is hiring sellswords in {Varn|P}. Lots of them, and paying in advance. The wine was bad. {Odo|C} drank it anyway and regretted it by morning.",
    "OOC: next session is on the 14th, not the 7th, because of the holiday. Bring snacks, it's Priya's turn for drinks. Josh is doing the battle map for the library dungeon and asked for more time. Also a reminder that we agreed on no phones at the table during combat. It slows everything down. Last week's fight took almost two hours for four rounds. Let's keep it snappy.",
    "{Rellan Ashvale|C} told us the truth tonight. He was a squire at {Greyhollow Keep|P} on the {Night of Ash|E}, and he opened the postern gate for someone in a red cloak. He never saw a face. He thinks it was someone from the {Ember Court|F}, and he has spent eleven years trying to prove it. {Mara|C} believes him. I'm not sure. The {Moonfall Blade|I} was his lord's sword.",
    "Loot, finally:\n- **{Orb of Whispers|I}** (whispers secrets, DM says roll a d6 every dawn)\n- 3 potions of healing\n- a deed to a farm near {Kettlewick|P}\n- a silver key with the {Pale Lantern|F} mark\nWe sold nothing. {Odo|C} wants the orb destroyed; {Vessa|C} wants to sell it; I want to hear what it says first.",
    "Went to the {Harvest Moot|E} in {Kettlewick|P}. Pie contest, caber toss, a bard who only knew two songs. {Biscuit|C} the mule won a ribbon for 'most stubborn'. The {Stonejaw Clan|F} traders brought iron and furs and asked, very casually, whether anyone had seen a compass with a glass face. {Lady Sefa|C} went pale. We now think the traders do not have it either.",
    "the siege tunnels under {varn|P} are older than the city. {ash|C} found a way in by following the rats. Inside: old murals of a crowned figure holding fire, which must be the {Cinder Crown|I}, and a wall of names. One of them was {Aldous Venn|C}. {Brother Odo|C} recognised the handwriting from the priest's map. So the map was drawn down here, by him.",
];

const long = [
    "# Session 14 recap\n\nWe started in {Harrowmere|P}, where {Captain Irzel Dunmore|C} was waiting at the docks with bad news: the {Drowned Library|P} had flooded again, and the {Gilded Hand|F} had been seen rowing out to it at night. {Rellan|C} insisted we go immediately. {Mara|C} insisted we eat first. We ate first.\n\nThe row out took an hour. The water around the library was black and very still, and {Ash|C} would not stop growling at it. Inside, level one was as we left it, but level two had a new door, freshly cut, with the Hand's sigil burned into the frame. Behind it: three of their agents, a crate of stolen ledgers and a very surprised clerk named {Pim|C}.\n\nCombat went fine. {Vessa|C} took one out before initiative (surprise round, 3d6 sneak attack, 14 damage), {Brother Odo|C} held the door, and I missed twice before landing a hit that ended it. {Pim|C} surrendered on the spot and offered to tell us everything in exchange for a boat home.\n\nWhat {Pim|C} told us:\n- the Hand is buying every ledger from the {Long Winter|E} they can find\n- they are looking for one sale in particular: the {Skyglass Compass|I}\n- their buyer is someone in the {Ember Court|F}, but {Pim|C} doesn't know who\n\nWe took the ledgers. {Mara|C} thinks the compass is the key to finding the {Cinder Crown|I}, because the priest's blank map only makes sense if you read it with something that points at more than north. {Rellan|C} went quiet at the mention of the Court. We rowed back after midnight, soaked, and slept in the harbour master's loft.\n\nNext time: read the ledgers, find the compass, and figure out what the Court wants with it.",
    "## What we know about the {Cinder Crown|I}\n\nCollected from @[Brother Odo](entry:e9)'s notes, the priest at the chapel and the murals under {Varn|P}.\n\n1. It was made during the {Siege of Varn|E}, by smiths who were paid in grain, not coin. That was the year before the {Long Winter|E}.\n2. A knight of the {Order of the Pale Lantern|F}, {Aldous Venn|C}, carried it out of the city through the tunnels. His name is on the wall of names.\n3. The {Ember Court|F} claims the crown belongs to them, because the Court's founder, {Queen Maelis|C}, paid for the siege. Nobody outside the Court believes this.\n4. It burns anyone who wears it without the right to. {Odo|C}'s dream suggests that 'right' is about blood, not about claims.\n5. The {Moonfall Blade|I} and the crown were forged in the same week. The blade is the crown's opposite: it cools, the crown burns.\n\n**Open questions**\n\n- Where did {Aldous Venn|C} go after the tunnels? The murals stop at a bridge. Maybe {Tollan Bridge|P}?\n- Why does the {Gilded Hand|F} care? They are merchants, not kings.\n- Does {Rellan|C} know more than he is saying? He flinched when {Mara|C} said 'the crown burns'.\n- Is the {Skyglass Compass|I} really needed to read the map, or is that just {Mara|C}'s theory?\n\nThe DM smiled when we asked question 4. That's never good.\n\nTable note: next session starts at 6, not 7. Bring the dice bag you keep forgetting.",
    "Big fight at {Tollan Bridge|P}. Long one, so notes in order.\n\nRound 1: the {Stonejaw Clan|F} raiders came up both ends of the bridge at once. Eight of them, plus a war chief called {Korrag Stonejaw|C} riding a bear. Initiative: {Vessa|C} 21, {Korrag|C} 19, me 15, {Mara|C} 11, {Odo|C} 8, raiders 6. {Vessa|C} shot the bear (1d8+4 = 9), which made the bear angry and {Korrag|C} angrier.\n\nRound 2: {Korrag|C} charged. 2d12+5 on {Odo|C}, who went down to 3 hp. {Mara|C} cast hold person on the chief, he saved on a 17. I dropped two raiders with a lucky 20 and a follow-up 18. {Ash|C} bit a raider's leg and would not let go.\n\nRound 3: {Odo|C} healed himself (1d8+3 = 10), then got knocked off the bridge into the river. {Hobb|C} the toll keeper threw him a rope, which is the first nice thing {Hobb|C} has ever done. The raiders at the far end started to break.\n\nRound 4: {Vessa|C} climbed onto the bear. Nobody at the table believed it would work. It worked (athletics 24). She put a dagger to {Korrag|C}'s throat and he called the retreat.\n\nAfter: {Korrag|C} says the Clan was paid to hold the bridge by someone from the {Gilded Hand|F}, and that they were told to look for a woman carrying a glass compass. We don't have the compass. So who does the Hand think has it? {Lady Sefa Morrow|C}, maybe.\n\nXP: 1,800 each. Loot: 140 gp, a bear-claw necklace, and {Korrag|C}'s horn, which {Vessa|C} wants to keep as a trophy. The bear went home. We did not follow it.",
    "Travel journal, week three.\n\nLeft {Kettlewick|P} early with {Biscuit|C} loaded up. The road north joins the {Salt Road|P} at a shrine to a saint nobody remembers, and we stopped there to eat. {mara|C} left an offering of bread. {ash|C} ate the offering. Bad omen, or just a hungry wolf, depending on who you ask.\n\nTwo days on the {Salt Road|P} after that. Flat, white, dusty, and full of traders. We passed a caravan from {Varn|P} (the same one we helped last month; they waved) and a patrol of the {Pale Lantern|F}, who asked where we were going and did not like the answer. Their captain, a stern woman called {Dame Oriel|C}, warned us that the road past {Harrowmere|P} is watched by the {Ember Court|F}'s scouts.\n\nOn the third night we camped by the salt pans and {rellan|C} finally talked about his lord. {Lord Castellan Vey|C} held {Greyhollow Keep|P} until the {Night of Ash|E}, and died in the fire. {Rellan|C}'s voice broke when he said the name. He still has the lord's signet ring. He showed it to us: a crescent moon over a tower, the same mark that's on the {Moonfall Blade|I}'s pommel.\n\nSo the blade isn't just a sword we stole back from the Court. It's {Rellan|C}'s inheritance, in a way. {Vessa|C} offered to give it to him. He said not yet.\n\nWe reach {Harrowmere|P} tomorrow. Plan: find {Captain Dunmore|C}, ask about the scouts, keep a low profile. {Brother Odo|C} says we are bad at low profiles. He is right.",
    "**Politics catch-up**, because I keep forgetting who hates who.\n\nThe {Ember Court|F}: nobles of {Varn|P} and the lands around it. Led by a council of five, whose names we mostly don't know. Only one we've met is {Lord Aurel Crane|C}, who is charming and a liar. They want the {Cinder Crown|I}, the {Moonfall Blade|I} and, as of last week, us, preferably in chains.\n\nThe {Gilded Hand|F}: merchant guild. Runs the fish market in {Harrowmere|P} and half the boats on the lake. Their contact with us is {Moss|C}, the fence. They're buying up old ledgers and looking for the {Skyglass Compass|I}, and they paid off {Irzel|C} and the {Stonejaw Clan|F}. Nobody knows who they are working for. My guess: the Court.\n\nThe {Stonejaw Clan|F}: mountain folk north of {Tollan Bridge|P}. Proud, poor since the {Long Winter|E}, and willing to take coin from anyone. {Korrag Stonejaw|C} owes {Vessa|C} his life now, which might matter later.\n\nThe {Order of the Pale Lantern|F}: knights and priests, based in the chapel east of {Harrowmere|P}. Old enemies of the Court since the {Siege of Varn|E}. {Dame Oriel|C} leads their patrols. They seem honest but they keep secrets about {Aldous Venn|C}.\n\nWho we trust, in order: {Mara|C}, {Ash|C}, {Brother Odo|C}, {Rellan|C} (mostly), {Lady Sefa|C} (sort of), everybody else not at all.\n\n(Snack break here. Somebody spilled soda on the character sheets. Session resumed after 15 min.)\n\nWhat we want: the crown destroyed or hidden, {Rellan|C} cleared of the {Night of Ash|E}, and a quiet week in {Kettlewick|P}. What we will get: probably none of that.",
];

function parse(source) {
    let text = "";
    const gold = [];
    const re = /\{([^{}|]+)\|([CPFIE])\}/g;
    let last = 0;
    for (const m of source.matchAll(re)) {
        text += source.slice(last, m.index);
        gold.push({ start: text.length, length: m[1].length, text: m[1], kind: kinds[m[2]] });
        text += m[1];
        last = m.index + m[0].length;
    }
    text += source.slice(last);
    if (/[{}]/.test(text)) throw new Error(`stray brace in: ${text.slice(0, 60)}`);
    return { text, gold };
}

const notes = [
    ...short.map((s, i) => ({ id: `s${i + 1}`, size: "short", ...parse(s) })),
    ...typical.map((s, i) => ({ id: `t${i + 1}`, size: "typical", ...parse(s) })),
    ...long.map((s, i) => ({ id: `l${i + 1}`, size: "long", ...parse(s) })),
];

for (const size of ["short", "typical", "long"]) {
    const lengths = notes.filter((n) => n.size === size).map((n) => n.text.length);
    console.log(size, lengths.length, "notes, chars", Math.min(...lengths), "–", Math.max(...lengths));
}
console.log("gold spans", notes.reduce((n, x) => n + x.gold.length, 0), "; notes without gold", notes.filter((n) => n.gold.length === 0).map((n) => n.id).join(", "));

await mkdir(out, { recursive: true });
await writeFile(`${out}/notes.json`, JSON.stringify(notes, null, 2) + "\n");
await writeFile(`${out}/entries.json`, JSON.stringify(entries, null, 2) + "\n");
console.log("wrote", out);
