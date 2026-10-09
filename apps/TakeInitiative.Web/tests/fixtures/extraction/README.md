# Extraction test set (step 23a)

An **invented** test set for the suggestion model. Every note, name, place and faction here
was made up for this fixture: no module text, no real campaign notes, no user data.

- `notes.json`: 40 notes (15 short, about 100 characters; 20 typical, 300–400; 5 long,
  1,300–1,550), each with **gold spans**: `start`, `length` (UTF-16 offsets into `text`),
  `text` and `kind` (`Character`, `Place`, `Faction`, `Item`, `Event`).
- `entries.json`: 27 wiki entries with kinds and aliases, for the matching steps (23c, 23d).

## How it was made

`scripts/extraction-spike/build-fixture.mjs` holds the notes with their gold spans written
inline (`{Rellan|C}`) and writes both files. Edit the script and rerun it
(`node build-fixture.mjs` in that folder); never edit the JSON by hand.

Labelling rules:

- A gold span is a proper name of something that could be a wiki entry, at every occurrence.
- A leading "the" and a possessive `'s` are left out ("the {Ember Court}'s spies"); titles that
  are part of how the name is written stay in ("Captain Dunmore", "Brother Odo", "Lady Sefa").
- Existing mentions (`@[Rellan Ashvale](entry:e1)`) are never spans; a plain "rellan" after
  one is.
- Players' real-world names in table talk ("Priya", "Josh") are not spans: they are not
  campaign entries.

The hard cases are there on purpose: lower-case names ("met rellan", "ash", "varn"),
possessives, multi-word names, names at a sentence start, mentions, markdown (headings, bold,
lists), dice (`2d6+3`, `1d20+7`), numbers, "the" before a name, "Ash" the wolf next to the
word "ash", "Moss" the fence, table talk ("pizza's here"), and three notes with no names at
all (`s3`, `s6`, `s10`; `t16` has only players' names).
