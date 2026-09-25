# 16 — Images

## Goal

A session note can carry **images**. Any member attaches them from the composer
(🖼 from the gallery, 📷 from the camera on a phone, paste or drop on desktop), and
the note's text becomes their **caption**, so `@` mentions work in it. A note with
images and no caption can be posted, but the composer nudges first, and such a note
is the seam for step 19's loose ends. The bytes live in an **S3 blob store**: MinIO
in `compose.dev.yml`, any S3-compatible bucket in production. An image is only ever
served by the API, after the same visibility check as its note, so a 🔒 DM image
never reaches a player, on a read, a push or a direct request (invariant 5). The
Images and Text filters start working. **Galleries** show the images of a session
and the images whose caption mentions an entry. On Android, sharing a picture to the
installed PWA opens the composer with it attached, posting to the current session.

The step ships as five PRs stacked with `gh stack` on top of this file's docs PR,
which sits on 15g (#213). Each PR leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 16a | `v2/16a-blob-store` | Blob store, upload and serve (API) | 15's app unchanged in the browser. `pnpm dev` also starts MinIO. The API stores an upload as two WebP variants and serves them to their uploader only | [x] |
| 16b | `v2/16b-image-notes-api` | Image notes (API) | The same in the browser. Notes take `imageIds`, may have no text, and their images are served to exactly the note's audience. The Text and Images filters work on the server | [x] |
| 16c | `v2/16c-image-notes-web` | Image notes in the composer and stream | 🖼, paste and drop attach images. The stream draws them, a viewer opens them full screen, the note editor adds and removes them, and the filters work | [ ] |
| 16d | `v2/16d-galleries` | Session and entry galleries | A session divider opens its session's gallery, and an entry page has a Gallery section | [ ] |
| 16e | `v2/16e-share-target` | Camera and share target | 📷 on a phone. Sharing images to the installed PWA opens the composer with them attached. The step's Verify passes | [ ] |

Loose ends proper (the counts on dividers and in the wiki, resolving) are step 19,
and ⌘K's IMAGES section is step 17. This step leaves a seam for each (Notes) and
builds neither.

## Depends on

**14** and **15** (captions have mentions, and the entry gallery reads
`MentionIndex`). The glossary (§1), the images line of §3, the mobile toolbar and
share target of §3a, the entry page of §4, loose ends (§5), the architecture (§9:
`IBlobStore` over the S3 API, MinIO in `compose.dev.yml`) and the invariants (§10,
especially 5, 10 and 11) in [12-v2-design-session.md](12-v2-design-session.md) are
binding.

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web), except where they start at the repo root.

**This PR (docs)**
- `docs/roadmap/12-v2-design-session.md` §1: the new nouns (step 0 below)
- `docs/roadmap/README.md`: link step 16, `in progress`

**16a**
- Root `compose.dev.yml`: the `minio` service and its volume; `api` gains the `Blobs__*` variables
- Root `package.json`: `setup_environment` starts `postgres minio`
- API `TakeInitiative.Api.csproj`: `AWSSDK.S3` (Apache-2.0), `SkiaSharp` and `SkiaSharp.NativeAssets.Linux.NoDependencies` (MIT)
- API `appsettings.json`: the `Blobs` and `Images` sections (dev defaults)
- API add `src/Features/Images/Blobs/{IBlobStore,S3BlobStore,BlobOptions,BlobBucketInitializer}.cs`
- API add `src/Features/Images/Models/{Image,ImageVariant,ImageOptions}.cs`
- API add `src/Features/Images/Processing/{IImageProcessor,SkiaImageProcessor,ProcessedImage}.cs`
- API add `src/Features/Images/{ImageAccess,ImageSweeper}.cs`
- API add `src/Features/Images/Api/{PostImage,GetImageVariant,DeleteImage}/**`
- API modify `src/boostrap/Bootstrap.cs` (the `Image` document and its indexes), `Program.cs` (`AddImages`), `GlobalUsings.cs`
- Tests add `Fixtures/images/*` (see 16a step 9), `Scopes/Unit/ImageProcessorTests.cs`, `Scopes/Integration/MinioFixture.cs`, `Scopes/Integration/InMemoryBlobStore.cs`, `Scopes/Integration/Features/Images/{S3BlobStoreTests,ImageUploadTests,ImageSweeperTests}.cs`
- Tests modify `TakeInitiative.Api.Tests.csproj` (`Testcontainers.Minio`), `WebAppWithDatabaseFixture.cs` (`InMemoryBlobStore`), `WebAppClientExtensions.cs`
- Web `utils/api/schema.d.ts`: regenerated

**16b**
- API add `src/Features/Sessions/Models/NoteImage.cs`, `src/Features/Images/ImageAttachments.cs`,
  `src/Features/Images/NoteWrite.cs` (added in 16b, see Notes)
- API modify `src/Features/Entries/Api/PostEntryQuote/PostEntryQuote.cs` (a note with no caption),
  `src/boostrap/Bootstrap.cs` (the `Image` correlation id), `src/boostrap/CorrelationMiddleware.cs`
- API modify `src/Features/Sessions/Models/SessionNote.cs` (`Images`, `HasImages`), `Models/Events/{SessionNotePosted,SessionNoteEdited}.cs`
- API modify `src/Features/Sessions/Api/{PostSessionNote,PutSessionNote,DeleteSessionNote,GetSessionStream,GetSessionNoteHistory}/*.cs`, `Api/GetSessionNote/SessionNoteResponse.cs`
- API modify `src/Features/Images/Api/GetImageVariant/GetImageVariant.cs` and `ImageAccess.cs` (the note's rule)
- Tests add `Scopes/Integration/Features/Images/{ImageNoteTests,ImageVisibilityTests}.cs`; modify `Features/Sessions/SessionTests.cs` (the filters)
- Web `utils/api/schema.d.ts`: regenerated; the `images: []` field in the `SessionNote` test fixtures

**16c**
- Web add `utils/images.ts` (limits, `imageUrl`, `prepareImage` decisions, attachments, layout), `utils/api/image/{postImageRequest,deleteImageRequest}.ts`, `composables/useImageAttachments.ts`
- Web add `components/Image/{AttachmentStrip,NoteImages,ImageViewer,CaptionNudge}.vue`
- Web modify `components/Composer/Composer.vue`, `utils/composer.ts` (state, draft, POST body, toolbar items), `components/Session/{SessionNoteCard,NoteEditor,SessionStream}.vue`, `utils/sessionStreamCache.ts` (`noteMatchesFilter`), `utils/streamFilters.ts`, `utils/queries/sessions.ts`, `composables/useApi.ts`, `utils/api/types.ts`
- Web add `tests/unit/images.test.ts`; modify `tests/unit/{composer,sessionStreamCache}.test.ts`

**16d**
- API add `src/Features/Images/Api/{GetSessionImages,GetEntryImages}/**`, `GalleryResponse.cs`
- API modify `src/Features/Entries/Mentions/MentionIndex.cs` (`imagesOnly`)
- Tests add `Scopes/Integration/Features/Images/GalleryTests.cs`
- Web add `utils/api/image/{getSessionImagesRequest,getEntryImagesRequest}.ts`, `utils/gallery.ts`, `components/Image/{ImageGrid,SessionGallerySheet}.vue`, `components/Wiki/EntryGallery.vue`
- Web modify `components/Session/SessionDivider.vue`, `pages/app/campaigns/[campaignId]/wiki/[entryId].vue`, `utils/queries/sessions.ts`, `utils/queries/entries.ts`, `composables/useCampaignHub.ts` (gallery invalidation)
- Web add `tests/unit/gallery.test.ts`
- Web `utils/api/schema.d.ts`: regenerated

**16e**
- Web modify `nuxt.config.ts` (the manifest's `share_target`), `public/sw.js` (one `fetch` handler, for the share POST only)
- Web add `utils/shareTarget.ts`, `pages/app/share.vue`, `server/routes/app/share-target.post.ts` (the fallback when no service worker is active)
- Web modify `components/Composer/Composer.vue` (📷, `?share=`), `pages/app/campaigns/[campaignId]/index.vue`, `layouts/campaign.vue` (remember the last campaign)
- Web add `tests/unit/shareTarget.test.ts`

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/15g-merge-claim-stats
gh stack add v2/16-step-file
```

Add each sub-step on top with `gh stack add v2/16a-blob-store` and so on.

**Glossary check.** Session note ("markdown text plus optional images") and Filter
(`Images`) are in §1. This PR adds the nouns the step puts into code and UI:

- **Image** (code: `Image`, and `NoteImage` on a note): a picture on a session note,
  stored as two variants (`display` and `thumb`). It has its note's visibility.
  Until its note is posted only its uploader can see it, and one never posted is
  deleted after 24 hours.
- **Image note**: a session note with at least one image.
- **Caption**: an image note's text. It can contain mentions, and can be empty.
- **Gallery**: the images of one session, or the images whose caption mentions an
  entry.
- **Share target**: the PWA as a destination in the phone's share sheet ("Take
  Initiative"). Sharing images there opens the composer with them attached.
- **Blob store** (code: `IBlobStore`): where image bytes live, behind the S3 API.
  Only the API talks to it.

### 16a. Blob store, upload and serve (API)

1. **MinIO in dev** (`compose.dev.yml`), pinned. The plan was Ripple's image; 16a
   uses `pgsty/minio:RELEASE.2026-08-04T00-00-00Z` because that one no longer pulls
   (Notes, "16a, as built"):

   ```yaml
   minio:
       image: pgsty/minio:RELEASE.2026-08-04T00-00-00Z
       container_name: takeminio
       command: server /data --console-address ":9001"
       ports: ["7404:9000", "7405:9001"]     # S3 API, console
       environment:
           - MINIO_ROOT_USER=takeinitiative
           - MINIO_ROOT_PASSWORD=takeinitiative-dev
       volumes: [takeminio-data:/data]
       healthcheck: { test: ["CMD", "mc", "ready", "local"], interval: 10s, retries: 5 }
   ```

   `api` gains `Blobs__ServiceUrl=http://minio:9000` and `depends_on: minio`. The
   root `setup_environment` script starts `postgres minio`. No bucket-creating
   container: the API creates its bucket (step 3). `docker compose … down -v` now
   also resets images.
2. **Configuration** (`BlobOptions`, section `Blobs`; dev defaults in
   `appsettings.json`):

   | Key | Dev default | Production |
   |---|---|---|
   | `ServiceUrl` | `http://localhost:7404` | the provider's S3 endpoint |
   | `Region` | `us-east-1` | e.g. `auto` for R2 |
   | `Bucket` | `takeinitiative` | |
   | `AccessKey`, `SecretKey` | `takeinitiative`, `takeinitiative-dev` | secrets, from the environment |
   | `ForcePathStyle` | `true` | `true` (MinIO, Garage, R2 all accept it) |
   | `CreateBucket` | `true` | `false` |

   The bucket is **private**. Nothing in the app ever links to it, and it needs no
   CORS.
3. **`IBlobStore`** (`Features/Images/Blobs`), the only thing that talks S3:

   ```csharp
   interface IBlobStore
   {
       Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);
       Task<BlobRead?> GetAsync(string key, CancellationToken ct);   // null when missing
       Task DeleteAsync(string key, CancellationToken ct);           // missing is not an error
   }
   record BlobRead(Stream Content, long Length, string ContentType) : IAsyncDisposable;
   ```

   `S3BlobStore` uses `AWSSDK.S3` with `ForcePathStyle`, and with
   `RequestChecksumCalculation` and `ResponseChecksumValidation` set to
   `WHEN_REQUIRED` (see Notes). `BlobBucketInitializer` is an `IHostedService` that
   creates the bucket when `CreateBucket` is set, so `--export-openapi`, which never
   starts the host, needs no MinIO. Keys are
   `campaigns/{campaignId}/images/{imageId}/{variant}.webp`.
4. **Processing** (`IImageProcessor.Process(Stream) → ProcessedImage`, with
   `SkiaImageProcessor` on SkiaSharp):
   - The type comes from the **bytes** (`SKCodec`), never from the file name or the
     `Content-Type`. JPEG, PNG, WebP and GIF are accepted. Anything else (HEIC, AVIF,
     SVG, a PDF, a text file) is a 415, "This image type is not supported. Try JPEG
     or PNG." SVG is refused on purpose: it can carry script.
   - The pixel count is checked from the header **before** decoding. Over 50
     megapixels is a 400, so a tiny PNG that claims 20,000 × 20,000 is refused
     without allocating 1.6 GB (a decompression bomb).
   - The EXIF orientation is applied, and then **all metadata is dropped**: phone
     photos carry GPS positions.
   - Two variants, both WebP, never upscaled:

     | Variant | Long edge at most | Quality | Used by |
     |---|---|---|---|
     | `display` | 3200 px | 82 | the full-screen viewer |
     | `thumb` | 640 px | 75 | the stream, galleries and the composer |

   - GIFs keep their first frame only.
   - At most two images are processed at once (a `SemaphoreSlim` in the processor),
     which bounds memory on a small server.
5. **The `Image` document** (a Marten document with optimistic concurrency, not an
   event stream; see Notes):

   ```
   Image { Id, CampaignId, UploaderMemberId, UploadedAt,        // microseconds
           Width, Height,                                       // of the display variant
           Variants: { Display: ImageVariant, Thumb: ImageVariant },
           NoteId?, AttachedAt?,                                 // 16b
           DeletedAt? }                                          // blobs still to delete
   ImageVariant { Key, ContentType, Length, Width, Height }
   ```

   Indexes: `(CampaignId, UploaderMemberId, NoteId)` and `NoteId`.
6. **Endpoints.** All take `{campaignId}` and resolve the caller's member first with
   `RequireMember`.

   | Endpoint | Who | Does |
   |---|---|---|
   | `POST /api/campaigns/{campaignId}/images` | members | `multipart/form-data` with one `file`. Processes it, puts both variants, stores the `Image`. Answers `ImageResponse { id, width, height, uploadedAt }` |
   | `GET …/images/{imageId}/{variant}` | who can see it | `variant` is `display` or `thumb` (anything else is a 404). Streams the bytes from the blob store |
   | `DELETE …/images/{imageId}` | its uploader, while it is on no note | Removing an attachment in the composer. A 409 once it is on a note (edit the note instead). Marks `DeletedAt`, then deletes the blobs |

   - **Limits.** The request body is capped at 20 MB before it is read
     (`IHttpMaxRequestBodySizeFeature`), so a bigger one is a 413 without being
     buffered. A member can hold at most 20 images that are on no note: the 21st is
     a 409, "Post or remove some images first."
   - **The read rule in 16a** (`ImageAccess.RequireVisibleImage`): an image on no
     note is visible to its uploader only, not even to a DM. Anything else, including another campaign's id, is a 404, never a 403.
   - **Response headers** on `GET`: the stored `Content-Type` (`image/webp`),
     `X-Content-Type-Options: nosniff`, `Content-Disposition: inline`,
     `Cache-Control: private, no-cache`, and a strong `ETag` of
     `"{imageId}-{variant}"` (variants never change). `If-None-Match` gives a 304,
     but only **after** the visibility check (16b's tests hold this).
7. **Sweeper** (`ImageSweeper`, a `BackgroundService`, hourly, on the keyed clock the
   gap prompt uses). It deletes the blobs of every `Image` with `DeletedAt`, then the
   document. It marks `DeletedAt` on every image still on no note 24 hours after
   `UploadedAt`. `SweepOnce(now)` is public, so tests call it directly.
8. **Web.** Regenerate `schema.d.ts`. No UI change.
9. **Tests.**
   - `Fixtures/images/`: `rotated-exif6-gps.jpg` (EXIF orientation 6 and a GPS tag),
     `alpha.png`, `photo.webp`, `animated.gif`, `wide-6000x1000.jpg`,
     `truncated.jpg`, `not-an-image.jpg` (text), `drawing.svg`, `photo.heic` and
     `bomb-20000.png` (a tiny file with a 20,000 × 20,000 header). Each is a few KB,
     generated by a script kept beside them.
   - `ImageProcessorTests` (unit): each fixture's type, 415 or 400; the rotated
     JPEG comes out portrait with no EXIF and no GPS in the WebP; `alpha.png` keeps
     alpha; the wide JPEG gives 3200 × 533 and 640 × 107; a small image is not
     upscaled; the bomb is refused before decoding (checked by allocation, or by a
     processor spy that records whether decode ran).
   - `S3BlobStoreTests` against `MinioFixture` (Testcontainers, the compose image):
     put, get, a missing key is null, delete, deleting twice is fine, and the bucket
     is created on start.
   - `ImageUploadTests` (the default fixture now uses `InMemoryBlobStore`): upload,
     then both variants served to the uploader with the headers above; the DM and
     the other player get 404; another campaign's member gets 403 on the campaign
     first; 413 over 20 MB; 415 for the SVG and the text file; the 21-image 409;
     `DELETE` by the uploader and by someone else (404).
   - `ImageSweeperTests`: with `ShiftableTimeProvider`, an image on no note is kept
     at 23 h and gone at 25 h (document and both blobs); a `DeletedAt` image's blobs
     are retried when the first delete threw.

### 16b. Image notes (API)

1. **On the note.** `NoteImage { ImageId, Width, Height }` (width and height of the
   display variant, so the web lays the note out before any byte arrives).

   ```
   SessionNotePosted  += Images: NoteImage[]          // [] for a text note
   SessionNoteEdited  += Images: NoteImage[]?         // null = unchanged
   SessionNote        += Images: NoteImage[], HasImages: bool
   SessionNoteResponse += images: NoteImageResponse[] { id, width, height }
   ```

   - Old events have no `Images`. They read as `[]` and `null`, so replaying 14's
     and 15's streams is unchanged, and no database reset is needed (Notes).
   - `HasImages` is a flat field, like the hidden fields in 14a, so the filters stay
     a simple LINQ expression.
2. **Posting.** `POST notes` and `PUT notes/{id}` take `imageIds?: string[]`, in
   order, at most 10.
   - **Text rule.** A note needs text **or** at least one image. So the 14a rule
     ("A session note needs some text.") becomes "A session note needs some text or
     an image." A caption is trimmed and has the same 10,000-character limit.
   - **Each id** must be an `Image` of this campaign, uploaded by the caller, not
     deleted, and on no other note. Otherwise it is a 400 with `errors.imageIds`,
     with one message for all of those cases, so another member's image id tells
     the caller nothing. The same id twice is a 400.
   - **Attaching** sets `Image.NoteId` and `AttachedAt` in the same
     `SaveChangesAsync` as the note's event, so the note and its images commit
     together. The `Image` document's optimistic concurrency turns a race (the same
     upload on two notes, from two tabs) into a 409 for the loser.
   - **Editing.** On `PUT`, `imageIds` left out keeps the images. Sent, it is the
     whole ordered list: new ids are attached, and removed ones get `DeletedAt`
     (their blobs are deleted after the save, and the sweeper retries). Only the
     author edits a note (invariant 4), so only the author changes its images. An
     unchanged text, recap flag and list appends nothing, as today.
   - **Deleting a note** marks every one of its images `DeletedAt` in the same
     transaction. Images are deleted for real. The note's events keep only their
     ids.
3. **The read rule** (`ImageAccess`, invariant 5), in the order it is checked:

   | Image | Visible to |
   |---|---|
   | on no note | its uploader |
   | on a note | exactly who can see the note: `SessionNoteVisibility.CanSee(note, viewer)` |
   | `DeletedAt` set, or its note gone | nobody (404) |

   So an image follows its note through every visibility change, hide and unhide,
   with nothing to update: the check reads the note at request time. There is no
   copy of the visibility on the `Image` that could drift. The `GET` loads the image
   and then its note, and runs the same function the stream uses.
4. **Filters** (`GetSessionStream.ApplyFilter`): `Text` is `!HasImages`, and
   `Images` is `HasImages`. `Combats` still matches nothing until step 18.
5. **Push.** No new messages. `sessionNoteUpserted` carries `images`, and it already
   reaches exactly the note's audience. Image ids in a payload reveal nothing to its
   receivers, who can fetch those images anyway.
6. **History.** `GET notes/{id}/history` versions gain `imageCount`. Removed images
   are deleted, so a past version shows "2 images" rather than the pictures.
7. **Tests.**
   - `ImageNoteTests`: a note with two images and a caption; with images and no
     text (200); with neither (400); 11 images (400); an id of another member's
     upload, another campaign's, a deleted one and one already on a note (400, the
     same message each time); an edit that adds, removes and reorders images, where
     the removed one is gone from the blob store after a sweep; deleting the note
     deletes its images; the note's events and the `Image` attach share a
     correlation id; two racing posts with the same upload give one 200 and one
     409 (`InterferingSaveFixture`).
   - `ImageVisibilityTests`: for every cell of 14a's note table (`Everyone`, `DM`
     and `Me`, hidden or not, × author, DM, other player), `GET
     images/{id}/thumb` is 200 exactly when `GET notes/{id}` is. After a visibility
     change to `DM`, and after a hide, the player's next `GET` is a 404, **also
     when it sends the `If-None-Match` it got before** (never a 304). After an
     unhide it is a 200 again.
   - `SessionTests`: `?filter=Text` and `?filter=Images` split a session's notes,
     under visibility.
   - `SessionNoteAudienceTests` pass unchanged.
8. **Web.** Regenerate `schema.d.ts`, and add `images: []` to the `SessionNote`
   fixtures in the unit tests. No UI change.

### 16c. Image notes in the composer and stream

1. **Attachments** (`utils/images.ts`, pure, and `useImageAttachments`). The
   composer's state gains `attachments[]`:

   ```ts
   type Attachment = {
       key: string;                       // local
       status: "preparing" | "uploading" | "ready" | "failed";
       previewUrl: string;                // an object URL, revoked on remove and after post
       progress: number;                  // 0–1, from axios's onUploadProgress
       image?: { id: string; width: number; height: number };
       error?: string;
   };
   ```

   - An image uploads **as soon as it is attached**, so ➤ is quick on a slow phone
     connection. ➤ waits while any attachment is `preparing` or `uploading`, and it
     is disabled while one has `failed` (tap to retry, or ✕).
   - ✕ removes an attachment. A `ready` one also gets `DELETE images/{id}`, and a
     failure there is ignored (the sweeper has it).
   - At most 10 per note. The 11th shows "A note can have at most 10 images."
   - **Drafts.** The draft (15d's JSON) gains `imageIds` for `ready` attachments.
     On load they are drawn from `thumb`, since their uploader can fetch them. One
     that is a 404 (swept) is dropped quietly.
2. **Preparing** (`prepareImage(file)`, with its decision logic pure and unit
   tested). A file goes up unchanged when it is JPEG, PNG, WebP or GIF, at most
   20 MB, and has a long edge of at most 8000 px. Otherwise, if the browser can
   decode it (`createImageBitmap`: Safari decodes HEIC), it is redrawn on a canvas
   at a long edge of at most 4096 px and sent as JPEG at quality 0.9. If it cannot
   be decoded, the attachment fails with the server's wording ("This image type is
   not supported. Try JPEG or PNG.") and nothing is uploaded.
3. **Where images come in.**
   - 🖼 in the toolbar (§3a), after `@`, opens `<input type="file"
     accept="image/*" multiple>`. It is on every screen size.
   - Desktop: pasting an image into the text box, and dropping files on the
     composer, attach them.
   - `AttachmentStrip` sits in the composer's strip area above the toolbar, so on a
     phone it stays above the keyboard: 64 px thumbnails with progress, ✕ and
     retry. It scrolls sideways.
4. **Caption nudge** (`CaptionNudge`, §3 and §5).
   - With attachments, the placeholder becomes "Add a caption. Who or what is in
     this? @ to link".
   - ➤ with images and **no caption** does not post at first. The strip shows "Post
     without a caption? Images without one are hard to find later." with
     **Post anyway** and **Add caption** (which focuses the text box). One tap on
     Post anyway posts.
   - A caption with no mention posts at once. The note card shows the author (only
     the author, who is the one who resolves it, §5) a small "⚠ Tag what's in this?"
     under an image note with no mention (§3's sketch), which opens the note editor.
     That hint is the note-level half of the loose end. Counting and listing loose
     ends are step 19.
5. **Posting.** `buildPostBody` sends `imageIds` in order and allows an empty text
   when there are images. The optimistic note (14d) carries `images` from the
   `ready` attachments, and it can draw them at once, because the uploader may
   fetch their own uploads. After a successful post the attachments are cleared and
   their object URLs revoked. On an error they stay, still uploaded, for a retry.
6. **Drawing** (`NoteImages` in `SessionNoteCard`).
   - `imageUrl(campaignId, imageId, variant)` builds
     `{apiBase}/api/campaigns/{cid}/images/{id}/{variant}`. It is a plain `<img>`:
     the session cookie goes with it (Notes: same-site), and no JavaScript fetches
     the bytes.
   - Layout (`imageLayout(count)`, pure): one image is full width, with its aspect
     ratio from `width` and `height` and a height of at most 70vh; two side by side;
     three as one large and two small; four as 2 × 2; five or more as 2 × 2 with
     "+n" on the last tile. Every `<img>` has `width`, `height`, `loading="lazy"`
     and `decoding="async"`, so nothing shifts when it loads.
   - The `alt` text is the caption as plain text, or "Image from Sam" when there is
     none.
   - The caption is drawn under the images, with chips, as a note's text is.
   - The timeline's compact card (15c) draws a strip of up to four thumbnails.
7. **Viewer** (`ImageViewer`). Tapping an image opens a full-screen dialog on the
   `display` variant, with `?image={imageId}` in the URL, so the phone's back
   gesture closes it. It has swipe and the arrow keys through the note's images,
   pinch zoom (`touch-action: pinch-zoom` on the image), the caption with chips,
   "S12 · Sam · 8:15pm" linking to `?note=`, and ✕. The note actions (promote,
   edit, hide) stay on the card.
8. **Editing** (`NoteEditor`). The same `AttachmentStrip`: remove, reorder (move
   left and right buttons) and add. Save sends `imageIds` only when the list
   changed. Removing an image says "The image will be deleted."
9. **Filters.** `noteMatchesFilter`: `Text` is `images.length === 0` and `Images`
   is `images.length > 0`. The Images empty state becomes "No images yet. Attach one
   with 🖼." A note edited from text to image moves between filtered streams
   through the existing upsert path.
10. **Tests** (`tests/unit/images.test.ts` and updates):
    - the `prepareImage` decision per type, size and dimensions;
    - the attachment transitions, including ✕ on each status;
    - `buildPostBody` with images, captionless, and with an upload still going;
    - the draft round trip with `imageIds`;
    - `imageLayout` for 1 to 6 images;
    - `noteMatchesFilter` for Text and Images;
    - `captionNudge(state)`: none, the confirm, and the confirm skipped when there is
      a caption.

### 16d. Galleries

1. **Endpoints** (both answer `GalleryResponse { items: { note:
   SessionNoteResponse, sessionNumber }[], hasOlder }`: image notes, newest page
   first, oldest first within a page, `take` 30 by default and 60 at most, `before`
   a `postedAt` cursor, as 15b's timeline):

   | Endpoint | Who | Does |
   |---|---|---|
   | `GET …/sessions/{sessionId}/images?before=&take=` | members | the session's image notes the caller can see |
   | `GET …/entries/{entryId}/images?before=&take=` | who can see the entry | image notes the caller can see whose caption mentions the entry, merged ids included |

   - The session gallery is `SessionNote` where `SessionId` matches, `HasImages`,
     and `SessionNoteVisibility.VisibleTo(viewer)`.
   - The entry gallery is `MentionIndex.NotesMentioning(…, imagesOnly: true)`, which
     adds `HasImages` to 15b's query. It takes `entry.MentionIds()` (15g), and an
     entry the caller cannot see is a 404. A mention in an article block does not
     put an image in the gallery: promote copies text only (Notes).
   - The web flattens the notes into images, so a note with three images is three
     tiles that share one caption.
2. **Session gallery.**
   - A session divider shows "🖼 5" when its loaded notes have images. The count is
     from the loaded notes, so it is shown under the All and Images filters only,
     where a session's notes are all loaded.
   - Tapping it opens `SessionGallerySheet`, a bottom sheet on a phone and a dialog
     from `md`, with an `ImageGrid` and "Session 12 · 5 images". A tile opens the
     `ImageViewer`, whose swipe then runs through the whole gallery.
3. **Entry gallery** (`WikiEntryGallery`, §4). A **Gallery** section on the entry
   page, after the timeline, with the first 12 tiles and "See all (n)" loading more
   in place. It is not drawn at all when there are no images.
4. **`ImageGrid`**: square `thumb` tiles (`object-cover`), three columns on a phone,
   four from `md` and six from `lg`, each a 44 px or larger target, keyboard
   focusable, with the caption as `alt`.
5. **Staying current.** The queries are `["sessionImages", campaignId, sessionId]`
   and `["entryImages", campaignId, entryId]` (infinite). `sessionNoteUpserted` and
   `sessionNoteRemoved` invalidate the session's gallery when the note has images,
   or had them, and the entry galleries its mentions touch (15c's
   `timelineTouchedBy`, which has the same inputs).
6. **Tests.**
   - `GalleryTests`: per viewer (a `DM` image note is in the DM's session gallery
     and not a player's; a hidden one is in the author's and the DMs'); text notes
     left out; the entry gallery by a mention in the caption, and through a merged
     id; an entry the caller cannot see is a 404; paging with `take=2`.
   - `gallery.test.ts`: flattening notes to tiles, the divider count, and which
     galleries a pushed note invalidates.

### 16e. Camera and share target

1. **📷** (§3a), after 🖼 in the toolbar, **on touch screens only** (on desktop
   `capture` is ignored and it would be a second 🖼). It opens `<input type="file"
   accept="image/*" capture="environment">`. The photo goes through the same
   `prepareImage` and upload.
2. **Web Share Target** (§3a).
   - The manifest (`nuxt.config.ts`) gains:

     ```ts
     share_target: {
         action: "/app/share-target",
         method: "POST",
         enctype: "multipart/form-data",
         params: { title: "title", text: "text", url: "url",
                   files: [{ name: "images", accept: ["image/*"] }] },
     },
     ```

   - `public/sw.js` gains **one** `fetch` handler that only acts on a `POST` to
     `/app/share-target`. It reads the form data and keeps up to 10 image files,
     plus `title`, `text` and `url`, in Cache Storage (`ti-share`, keyed by a random
     id). It answers `303` to `/app/share?id={id}`. Every other request is left to
     the network, as today (no offline, §12).
   - `server/routes/app/share-target.post.ts` is the fallback for a POST that
     reaches Nitro because no service worker was active yet. It redirects `303` to
     `/app/share?error=unavailable`.
3. **`/app/share`** (`pages/app/share.vue`, logic in `utils/shareTarget.ts`).
   - Signed out: go to login, then come back to the same URL.
   - It chooses the campaign. The campaign layout remembers the last opened one
     (`ti:lastCampaignId` in `localStorage`). With one campaign, or a remembered one
     the user is still in, it goes straight there. Otherwise it lists the campaigns,
     the remembered one first ("Share to…").
   - It navigates to `/app/campaigns/{id}?share={shareId}`. The composer consumes
     `share` once, as it does 15c's `about`: it takes the files from the cache,
     attaches them (preparing and uploading as usual), puts `title`, `text` and
     `url` into the caption if the caption is empty, and targets the **current
     session**. Then it deletes the cache entry and drops the parameter.
   - A missing or expired entry says "Nothing to share. Try sharing again." Entries
     older than an hour are deleted whenever the page loads.
4. **Tests** (`shareTarget.test.ts`): the shared form → payload (non-images
   dropped, at most 10, text joined), the campaign choice for none, one, a
   remembered one and a stale remembered one, and the caption rule (an existing
   caption is kept).

## Verify

1. `dotnet test` and `pnpm build` pass, `vitest` passes, `schema.d.ts` is fresh, and
   CI is green on every PR in the stack. CI's API job runs MinIO through
   Testcontainers, so no workflow change is needed.
2. `pnpm dev` from a clean clone starts Postgres and MinIO, and the API creates the
   bucket. The MinIO console on 7405 shows the `takeinitiative` bucket.
3. Three browser profiles at 390 × 844: A (the owner, DM), with B and C joined as
   Players.
   1. B attaches two images with 🖼, types `Map of @Cragmaw Hideout`, and posts. A
      and C see the note live, with both images laid out side by side. Tapping one
      opens the viewer, swipe goes to the other, and back closes it.
   2. B attaches one image and taps ➤ with no caption. The nudge shows. Post anyway
      posts it, and B's card shows "⚠ Tag what's in this?". A's and C's cards do
      not.
   3. A posts a 🔒 DM image note. C's stream has no such note. C requests the
      image's URL (copied from A's devtools) and gets a 404. A hides B's first note:
      C's next request for its image, with the old `ETag`, is a 404 as well.
   4. The Images filter shows only image notes, and Text only the others.
   5. B edits the first note, removes one image and reorders nothing. The image is
      gone for everyone, and its URL is a 404 after the sweep.
   6. Session 1's divider shows "🖼 2", and its gallery opens. Cragmaw Hideout's
      entry page has a Gallery with B's map, and A's DM image appears there only for
      A when its caption mentions the entry.
   7. A photo with GPS in its EXIF, uploaded and then saved from the viewer, has no
      GPS and is the right way up.
4. 📱 On an Android phone with the PWA installed: 📷 opens the camera and the photo
   attaches. Sharing two photos from the Photos app to Take Initiative opens the
   composer with both attached, posting to the current session. On iOS (no share
   target), 🖼 picks from the photo library, and a HEIC photo uploads as JPEG.
5. In Postgres, every `mt_events` row of the note streams still has a
   `correlation_id` and an `Actor`, and the `mt_doc_image` rows of posted notes have
   a `note_id`.

## Notes / gotchas

- **Commit scopes:** `api`, `web`, `root` (compose, the root `package.json`) and
  `docs`. Every PR that changes an API contract regenerates (`pnpm gen:api`) and
  commits `schema.d.ts`.
- **Why the API streams the bytes, and not presigned URLs** (invariant 5). A
  presigned URL is a bearer token: whoever holds it can fetch the object until it
  expires, with no check at fetch time. After A hides a note or narrows it to `DM`,
  a player who loaded the stream a minute earlier would still hold working URLs,
  and could paste them to anyone outside the campaign. Short expiry narrows that
  window but does not close it, and then every stream response would need fresh
  URLs, made per viewer. Streaming through `GET images/{id}/{variant}` runs the
  note's own read rule on **every** request, the same function the stream uses, so
  hiding, a visibility change and deleting take effect on the next request, with no
  window. It also keeps the bucket private: it needs no public endpoint, no CORS,
  and no second hostname in dev. The cost is bandwidth through the API. At this
  scale (a table of friends) that is small, and thumbnails, 304s and lazy loading
  keep it down. If it ever matters, a CDN can sit in front of the API with
  `private` responses left uncached.
- **Why `private, no-cache` with an `ETag`.** The browser keeps the bytes but asks
  every time, and the API answers 304 only after the visibility check. So a
  revoked image stops being served at once, and a repeat view costs a small
  request, not the image. `max-age` would let a browser show an image from its
  cache without asking.
- **Same-site cookies.** `<img>` requests carry the session cookie only because the
  web and the API are same-site: `localhost:3000` and `localhost:5010` in dev (ports
  do not change the site), and subdomains of one domain under `CookieDomain` in
  production. The cookie is `SameSite=Lax` by default, which is sent on same-site
  subresource requests. If the API ever moves to another site, `<img>` would stop
  authenticating. The fallback is to fetch through axios (`withCredentials`) into
  object URLs.
- **Why `Image` is a document and not an event stream.** §9 lists the aggregates
  (Campaign, Session, SessionNote, Entry, Combat), and an image is not one. The
  domain fact, "this note has these images", is on the note's events
  (`SessionNotePosted.Images`, `SessionNoteEdited.Images`), with an `Actor`
  (invariant 9). The `Image` document is storage bookkeeping: where the bytes are,
  who uploaded them, and whether they are on a note.
- **No database reset.** `SessionNote` gains `Images` and `HasImages`, whose
  defaults (`[]`, `false`) are right for every existing note, and the events gain
  optional fields.
- **SkiaSharp, not ImageSharp.** ImageSharp 3 is under the Six Labors Split
  License: free under Apache-2.0 only for open-source-licensed projects,
  transitive use, or organisations under $1M a year, and this repository has no
  licence. SkiaSharp is MIT and so is its native Skia build. The API image is
  `aspnet:10.0` (Debian), so `SkiaSharp.NativeAssets.Linux.NoDependencies`
  (no fontconfig) is enough. macOS dev gets its native asset from `SkiaSharp`
  itself. Alternatives considered: Magick.NET (Apache-2.0, a large native
  dependency) and NetVips (MIT, but libvips is LGPL and needs its own native
  package).
- **`AWSSDK.S3` against MinIO, Garage or R2.** Recent versions of the SDK send
  CRC32 checksums by default, which some S3-compatible stores reject. Set
  `RequestChecksumCalculation` and `ResponseChecksumValidation` to
  `WHEN_REQUIRED`, and keep `ForcePathStyle`. Integration tests run against MinIO,
  so a regression shows in CI.
- **MinIO's images.** MinIO stopped publishing community Docker images in late
  2025. By 16a, its `quay.io` and Docker Hub repositories no longer pull anonymously,
  not even the tag Ripple pins, so dev and CI use `pgsty/minio`, a community build of
  the same server (16a, as built). The app speaks only S3, so replacing MinIO in dev
  (Garage, SeaweedFS, RustFS) is a change to compose and `MinioFixture.Image`. In
  production a self-hosted S3 server beside Postgres keeps invariant 10 plainly,
  and R2 (§9's example) also works through config alone.
- **EXIF.** Phone photos carry GPS positions. Re-encoding drops all metadata after
  applying the orientation. The original upload is never stored.
- **Deleting is real.** Deleting a note, or removing an image from it, deletes the
  bytes. `DeletedAt` plus the sweeper makes that survive a blob-store hiccup.
- **Seams for later steps.**
  - **Loose ends (19)**: "an image note with no mention" is `HasImages &&
    MentionedEntryIds == []`, a query on fields this step adds. 16c's "⚠ Tag what's
    in this?" is the note-level nudge. The counts on dividers and in the wiki are
    19's.
  - **⌘K (17)**: captions are note text, so they are searched with it. The IMAGES
    section is notes with `HasImages`.
  - **Discord import (24)**: imported images go through `IImageProcessor` and the
    same attach path.
  - **Promote** copies text only. Carrying an image into an article is not in 16.
- **Not in 16:** video and other files, animated GIFs (first frame only), image
  editing or cropping, alt text separate from the caption, a campaign-wide gallery
  page (the Images filter is that), storage quotas per campaign, and offline
  (§12).
- **Share target limits.** Web Share Target works only in an installed PWA, and not
  on iOS Safari. iOS users attach with 🖼, which also handles HEIC through the
  browser's own conversion or `prepareImage`. The service worker's handler must
  run before any page code, which is why the files go through Cache Storage and a
  redirect rather than straight into the composer.
- **iOS and the file picker.** Opening the picker blurs the text box, so the
  keyboard drops. Focus returns to the text box after files are chosen, so the
  pinned composer (14e) comes back.
- **16a, as built** (PR on `v2/16a-blob-store`):
  - **Test choice: an in-memory fake plus one real-MinIO suite.** Both default
    fixtures (`AuthenticatedWebAppWithDatabaseFixture`, `WebAppWithDatabaseFixture`)
    replace `IBlobStore` with `InMemoryBlobStore` (`fixture.Blobs`, with
    `Contains(key)` and `FailNextDeletes(n)`) and set `Blobs:CreateBucket=false`, so the
    ~400 other tests start no extra container. `S3BlobStoreTests` runs the real
    `S3BlobStore` and `BlobBucketInitializer` against `MinioFixture` (Testcontainers.Minio
    3.9.0, on the compose image), so the path-style and checksum settings are tested in
    CI with no workflow change. The fixtures also set `Images:SweepStartDelay` to a day:
    tests sweep by hand with `ImageSweeper.SweepOnce`.
  - **Deviation: the MinIO image.** The plan pinned Ripple's
    `quay.io/minio/minio:RELEASE.2025-07-18T21-56-31Z`. It ran locally only because it
    was cached. CI's first run failed on the pull ("unauthorized: access to the
    requested resource is not authorized"), and an anonymous registry check gives 401 for
    every tag on both `quay.io/minio/minio` and Docker Hub's `minio/minio`. Compose and
    `MinioFixture` now use `pgsty/minio:RELEASE.2026-08-04T00-00-00Z`, a maintained
    community build of the same MinIO server (AGPLv3, like MinIO). It has the same
    entrypoint, the same `MINIO_ROOT_*` variables and `mc`, so the healthcheck and
    Testcontainers.Minio work unchanged. The ports and container name are as planned.
    Ripple on a fresh machine has the same problem. **Local gotcha:** on this Mac,
    `docker pull` and `docker manifest inspect` hang in the Docker Desktop credential
    and Scout hooks. `DOCKER_CONFIG=<a dir with {}> DOCKER_HOST=unix://$HOME/.docker/run/docker.sock
    docker pull …` works, and after that `docker compose up -d --pull never`.
  - **Packages.** `AWSSDK.S3` 4.0.103.4, `SkiaSharp` and
    `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.152.1. A `docker build` of the API
    image has `runtimes/linux-*/native/libSkiaSharp.so` with every `ldd` dependency
    found in `aspnet:10.0`.
  - **Dev.** MinIO is `takeminio` on 7404 (S3) and 7405 (console), with the volume
    `takeminio-data`. Ripple's MinIO is on 3002 and 9001, and its Postgres on 5432, so
    nothing clashes. `setup_environment` starts `postgres minio`. `scripts/setup-env.mjs`
    needed no change: the API's `Blobs` defaults are in `appsettings.json`. The API
    creates the bucket on start. `BlobBucketInitializer` retries 15 times, 2 seconds
    apart, because MinIO may still be starting, and then logs an error rather than
    failing startup (only uploads need the bucket). `--export-openapi` runs with MinIO
    stopped.
  - **The rule function for 16b** is `ImageAccess.CanSee(session, image, viewer, ct)`,
    which `ImageAccess.RequireVisibleImage` (the 404) calls. It checks `DeletedAt` first,
    then "on no note: its uploader only". The branch for `NoteId` set returns false and
    carries a comment for 16b: load the note, return false when it is missing, and
    otherwise return `SessionNoteVisibility.CanSee(note, viewer)`. `GetImageVariant` runs it
    before comparing `If-None-Match`, so 16b needs no endpoint change for the 304 rule.
  - **Shapes.** `Image` has `Variants: ImageVariants { Display, Thumb }` (a record with
    `All()` and the names `display` and `thumb`), `Image.Variant(name)` (null for any
    other name) and `Image.BlobKey(campaignId, imageId, variant)`. It implements Marten's
    `IVersioned` (`Version`) with `UseOptimisticConcurrency`, so a `Store` or `Update` of a
    stale copy is a `ConcurrencyException`, which 16b's attach can turn into its 409.
    `ImageSweeper.Purge(image, ct)` deletes the blobs and then the document. `DeleteImage`
    uses it after marking `DeletedAt`, and 16b's edit and delete paths can too.
    `ImageResponse { id, width, height, uploadedAt }` is in `PostImage.cs`.
  - **Processing details.** Output is converted to sRGB, so the WebP carries no ICC
    profile either. A JPEG larger than the display variant is decoded at a reduced scale
    (`SKCodec.GetScaledDimensions`) when that still covers 3200 px, which saves most of the
    memory for a large photo. **Added:** a file Skia cannot fully decode (the truncated
    JPEG) is a 400, "This image could not be read. It may be damaged.", rather than a half-grey
    picture. `ImageRejectedException` carries the status. The bomb test checks
    `SkiaImageProcessor.Decodes` stays 0.
  - **Uploads.** `PostImage` uses `AllowFileUploads(dontAutoBindFormData: true)` and reads
    the `file` part with `FormFileSectionsAsync`, after `RequireMember` and the 20-image
    check, so an outsider's body is never read. The 413 comes from `Content-Length`
    (and a byte count while copying, when there is none). **Gotcha for 16c:** Kestrel
    sends that 413 and can then reset the connection while the client is still sending,
    so a browser may see a network error instead of the 413. The composer must check the
    size before uploading. `TestServer` has no `IHttpMaxRequestBodySizeFeature`, so the
    tests exercise the endpoint's own check. Two uploads racing at 19 unposted images
    can both pass the count, reaching 21. That is harmless, so it is not locked.
  - **Also.** `UseChunkEncoding = false` on puts (a plain signed body, since not every
    S3-compatible store accepts `aws-chunked`). The sweeper's first run is one minute
    after start (`Images:SweepStartDelay`) and then hourly, so a server that restarts
    often still sweeps. `DELETE` on a visible image that is on a note is a 409, and one
    that someone else uploaded is a 403. Neither can happen until 16b. The fixtures are
    made by `Fixtures/images/generate.py` (`uv run`, Pillow and pillow-heif, so
    `photo.heic` is real HEIC), and `Scopes/ImageFixtures.cs` names them.
  - **Verify, as run in 16a** (API only, so no browser):
    - `dotnet test` passes 417/417 on three runs. That is 379 from 15g plus 38 new:
      `ImageProcessorTests` 15, `S3BlobStoreTests` 5, `ImageUploadTests` 15 and
      `ImageSweeperTests` 3.
    - `nuxi typecheck` is clean, `vitest` passes 263/263, `nuxt build` succeeds, and
      `schema.d.ts` is regenerated.
    - `16a.mjs` passed 47/47 against the API on 5010 and the dev MinIO. It checked
      upload and both variants with their headers, with no EXIF, GPS or camera make in
      the bytes. It checked 304 for the uploader, and 404 for the DM and the other player,
      also with the ETag. It checked 403 for a non-member, 403 for an anonymous GET
      straight to MinIO, the 415s, the 400s, and a 21 MB upload refused. It checked the
      wide JPEG's 3200 x 533, the 21st upload's 409, and a delete. Afterwards, MinIO's data
      directory had both variants of a kept image and nothing of the deleted one.
    - The earlier scripts still pass: `smoke14a`, `hub14b`, `stream14c` 111/111,
      `composer14d` 142/142, `filters14e` 233/233, `pages13d`, `entries15a`, `mentions15b`
      44/44, `pages15c` 24/24, `wiki15c` 64/64, `mentions15d` 56/56, `pages15d` 19/19,
      `articles15e` 35/35, `article15f` 64/64, `pages15f` 34/34, `merge15g` 63/63 and
      `pages15g` 23/23.
    - The 417 tests and `16a.mjs` (47/47) were re-run on the `pgsty/minio` container.
    - Verify 2: `docker compose … up -d postgres minio` starts MinIO, the healthcheck
      (`mc ready local`) reports healthy, and the API logs "Blob store bucket
      takeinitiative is ready". The console on 7405 was not opened (no browser).
- **16b, as built** (PR on `v2/16b-image-notes-api`):
  - **Shapes for 16c.** `SessionNoteResponse.images: NoteImageResponse[]`, each
    `{ id, width, height }` (display-variant size), in order, `[]` for a text note, on every
    read and on `sessionNoteUpserted`. `text` stays a required string and may be `""` when
    there are images. `POST notes` and `PUT notes/{id}` take `imageIds?: string[]` (at most
    10, in order). On `PUT`, `imageIds` left out (or null) keeps the images, and `[]` removes
    them all. History versions gain `imageCount`. Filter values are `?filter=Text` and
    `?filter=Images`.
  - **Flow.** Upload each file with `POST images` (multipart `file`), keep the returned ids,
    and send them as `imageIds` with the note. Removing an attachment before posting is
    `DELETE images/{id}`. Once posted, only the note's `PUT` removes an image, and the image
    is then deleted for real.
  - **Error keys.** `errors.text`: "A session note needs some text or an image." (400).
    `errors.imageIds` (400): "A session note can have at most 10 images.", "The same image
    cannot be on a note twice.", and `ImageAttachments.UnavailableMessage`, "An image could
    not be attached. Upload it again." (one message for another member's image, another
    campaign's, a deleted one, one already on a note, and an unknown id).
    `errors.imageIds` (409): `ConflictMessage`, "An image was attached to another note at the
    same time. Upload it again." (the race). `DELETE images/{id}` on an attached image is 16a's
    409.
  - **Deviation: `NoteWrite`, two batches in one transaction.** Marten 7.31.1 reports a false
    `ConcurrencyException` when an optimistic-concurrency document (`Image`) is updated in the
    same batch as a **new** event stream (a note's `StartStream`, or a new entry's), even when
    the version matches. Appends to an existing stream are fine. The batch's SQL was correct,
    so this is a Marten result-reading bug. So `POST`, `PUT` and `DELETE notes` open one
    Postgres transaction (`NoteWrite`: its own connection, and a Marten session over it with
    `SessionOptions.ForTransaction` and the request's correlation id and `request` header). The
    image changes are saved first, with Marten's real version check (a stale image is the
    409, and the row locks are held until commit, so a racing attach waits and then fails).
    Then the note's events and any new entries are saved, and then the transaction commits. The note
    and its images still commit together, and the race test uses `InterferingSaveFixture` as
    planned. The `Image` document now stores Marten's `correlation_id` metadata, so an attach
    shares the note event's correlation id. That column is additive, so no reset is needed.
  - **Old notes.** SQL reads a missing `HasImages` as null, so `!HasImages` alone dropped
    every note projected before 16b from the Text filter (a test caught it). The Text filter is
    `SessionNote.WithoutImages` (`!HasImages || Images == null`) and Images is `HasImages ==
    true`. `ANoteProjectedBeforeImages_IsText_WithNoDatabaseReset` strips both keys from a
    document to check this. The dev database had 424 such notes and needed no reset.
  - **Loose-ends seam.** `SessionNote.UntaggedImageNote` (`HasImages && no
    MentionedEntryIds`) is the step 19 query, with a test. `MentionIndex` needed no change,
    because captions are note text.
  - **Added.** Quoting a note with no caption into an article (`POST entries/{id}/quotes`)
    is a 400, "This note has no text to quote." (`errors.text`). Promote copies text only.
  - **Timestamps.** `AttachedAt` and `DeletedAt` are truncated to microseconds.
  - **Verify, as run in 16b** (API only, so no browser):
    - `dotnet test` passes 453/453 on three runs. That is 417 plus 36 new:
      `ImageNoteTests` 13, `ImageAttachRaceTests` 2 (the race, and the push payload carrying
      images to the DM group only), `ImageVisibilityTests` 19 (the 15 cells of Everyone, DM
      and Me, hidden or not, × author, DM and other player, each with and without the ETag,
      plus visibility changes, hide and unhide, a deleted note, and a cross-campaign URL) and
      `SessionTests` 2. `SessionNoteAudienceTests` pass unchanged.
    - `nuxi typecheck` is clean, `vitest` passes 263/263, `nuxt build` succeeds, and
      `schema.d.ts` is regenerated. `images: []` was added to the `SessionNote` fixtures and
      to `optimisticNote` in `utils/composer.ts`.
    - `16b.mjs` passed 57/57 against the API on 5010 and the dev MinIO. It checked D, P and Q
      across Everyone, DM and Me, hide and unhide with the old ETag, a visibility change, the
      filters per viewer, the error keys, an edit that removes an image (gone from MinIO's
      data directory), and a delete. Every earlier script still passes, from `16a` 47/47 and
      `smoke14a` through `merge15g` 63/63 and `pages15g` 23/23.
