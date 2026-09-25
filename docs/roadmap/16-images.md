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
| 16a | `v2/16a-blob-store` | Blob store, upload and serve (API) | 15's app unchanged in the browser. `pnpm dev` also starts MinIO. The API stores an upload as two WebP variants and serves them to their uploader only | [ ] |
| 16b | `v2/16b-image-notes-api` | Image notes (API) | The same in the browser. Notes take `imageIds`, may have no text, and their images are served to exactly the note's audience. The Text and Images filters work on the server | [ ] |
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
- API add `src/Features/Sessions/Models/NoteImage.cs`, `src/Features/Images/ImageAttachments.cs`
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

1. **MinIO in dev** (`compose.dev.yml`). The Ripple image, pinned:

   ```yaml
   minio:
       image: quay.io/minio/minio:RELEASE.2025-07-18T21-56-31Z
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
  2025, and the pinned `quay.io` tag (Ripple's) still pulls. The app speaks only
  S3, so replacing MinIO in dev (Garage, SeaweedFS) is a compose change. In
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
