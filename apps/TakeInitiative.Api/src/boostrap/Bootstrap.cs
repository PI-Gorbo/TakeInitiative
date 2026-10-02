using Amazon.S3;
using FastEndpoints.Security;
using Microsoft.Extensions.Options;
using TakeInitiative.Api.Features.Images;

using TakeInitiative.Api.Identity;

using Marten;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten.Schema;
using Weasel.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SendGrid.Extensions.DependencyInjection;
using Serilog;
using TakeInitiative.Api.Features.Admin;
using TakeInitiative.Api.Features.Reference.KnowledgeBase;
using TakeInitiative.Utilities;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace TakeInitiative.Api.Bootstrap;
public static class Bootstrap
{
    /// <summary>
    /// Turns the startup schema migration off. On by default; see the comment beside the call.
    /// </summary>
    public const string ApplySchemaOnStartupKey = "Marten:ApplySchemaOnStartup";

    /// <summary>
    /// Where the cookie key ring is persisted. Unset — dev and the tests — changes nothing; see
    /// <see cref="AddDataProtectionKeyRing"/>.
    /// </summary>
    public const string DataProtectionKeyPathKey = "DataProtection:KeyPath";

    /// <summary>The application name pinned into the key ring; see <see cref="AddDataProtectionKeyRing"/>.</summary>
    public const string DataProtectionApplicationName = "TakeInitiative";

    public static IServiceCollection AddMartenDB(this IServiceCollection services, IConfiguration config)
    {
        var martenOpts = services.AddMarten(opts =>
        {
            
            opts.Connection(config.GetConnectionString("TakeDB") ?? throw new OperationCanceledException("Required Configuration 'ConnectionStrings:Marten' is missing."));

            // ⌘K search (step 17a). pg_trgm gives similarity() and word_similarity() for fuzzy
            // name matching, and unaccent folds accents so "gundren" finds "Gündren". Both are
            // contrib modules shipped by every common host, and both are trusted from PG 13, so
            // the app's database owner creates them without being a superuser. A host that
            // refuses fails startup loudly on CREATE EXTENSION rather than at the first search.
            // Nothing is paid and nothing new runs (invariant 10).
            opts.Storage.ExtendedSchemaObjects.Add(new Extension("pg_trgm"));
            opts.Storage.ExtendedSchemaObjects.Add(new Extension("unaccent"));

            // The knowledge base's table (26c): a flat table with no events and no aggregate,
            // written by the ingest CLI and read by 26d's provider and 26e's browse endpoint. It is
            // registered here, after pg_trgm, because its trigram index needs gin_trgm_ops and
            // Weasel writes extended schema objects in the order they were added.
            //
            // The API creates it and the CLI never does. The CLI checks that it exists and stops
            // with "start the API once" if it does not, so there is one owner for the schema in
            // every environment rather than two that have to agree. See KnowledgeBaseTable for why
            // it writes its own SQL instead of being a Weasel Table.
            opts.Storage.ExtendedSchemaObjects.Add(new KnowledgeBaseTable(opts.DatabaseSchemaName));

            // Use system.text.json. Enums are stored as strings so LINQ queries and the
            // JSON bodies agree (Role is also [JsonConverter]-annotated for the API).
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString);

            // Registers the identity documents and enforces a unique index on NormalizedEmail.
            opts.RegisterIdentityModels<ApplicationUser, ApplicationUserRole>();

            // Provenance (design §9, invariant 9): correlation, causation and headers are
            // stored on every event. CorrelationMiddleware fills them in per request.
            opts.Events.MetadataConfig.CorrelationIdEnabled = true;
            opts.Events.MetadataConfig.CausationIdEnabled = true;
            opts.Events.MetadataConfig.HeadersEnabled = true;

            // The one Marten 9 default this upgrade declines (step 30). Marten 9 turned
            // UseIdentityMapForAggregates on: the aggregate FetchForWriting returns is put in the
            // session's item map, and a later read of that type in the same session is served from
            // there instead of the database. Marten's own documentation states the precondition —
            // "only safe if you treat the aggregate from FetchForWriting() as read-only and express
            // every change as an event". This codebase does exactly that, and the precondition it
            // does not meet is the unwritten one: that an aggregate is fetched once per session.
            //
            // Every write here is a retry loop (CombatWrite.Write, PutEntryArticle): read the stream
            // at its version, decide, append, and on a concurrent write eject what was staged and
            // decide again on the fresh state. The second FetchForWriting reads the version from
            // Postgres but takes the document from the item map — and the instance in the item map is
            // the one the inline projection already applied the failed attempt's events to. Measured
            // on ArticleTests.LosingTheRaceToAWriteTheCallerCannotSee_ReMergesAndSucceeds, the retry
            // saw version 3 (correct, the concurrent writer's) with blocks [A2, S, B]: A2 is this
            // request's own never-committed edit and S is the pre-conflict secret, so the concurrent
            // writer's edit is missing. That state has never existed in the database, and a save from
            // it would silently drop the other write. Three tests caught it.
            //
            // Ejecting is not a way out: EjectAllPendingChanges clears the unit of work and not the
            // item map, Eject(document) and EjectAllOfType(type) both leave the fetch serving the
            // same instance (the generated identity-map storage holds its own reference to the
            // dictionary EjectAllOfType removes), and EjectAggregateFromIdentityMap — the method that
            // would do it — is internal to Marten. So the setting is the seam, not the call site.
            //
            // What it costs is one document load per aggregate per save, which is what Marten 7 did.
            opts.Events.UseIdentityMapForAggregates = false;

            // Campaign stream -> Campaign document, updated in the same transaction as the append.
            opts.Projections.Snapshot<Campaign>(SnapshotLifecycle.Inline);
            opts.Schema.For<Campaign>()
                .UniqueIndex(UniqueIndexType.Computed, x => x.JoinCode)
                // Marten turns Members.Any(m => m.UserId == id) into
                // `data -> 'Members' @> '[{"UserId": ...}]'`, which this GIN index serves.
                .Index(x => x.Members, idx => idx.Method = IndexMethod.gin);

            // Session stream -> Session document. The unique (CampaignId, Number) index is
            // the backstop when two members start the next session at once.
            opts.Projections.Snapshot<Session>(SnapshotLifecycle.Inline);
            opts.Schema.For<Session>()
                .UniqueIndex(UniqueIndexType.Computed, x => x.CampaignId, x => x.Number);

            // SessionNote stream -> SessionNote document (SessionNoteDeleted deletes it).
            // The GIN index serves MentionIndex's "notes that mention this entry" containment.
            opts.Projections.Snapshot<SessionNote>(SnapshotLifecycle.Inline);
            opts.Schema.For<SessionNote>()
                .Index([x => x.CampaignId, x => x.PostedAt])
                .Index(x => x.SessionId)
                .Index(x => x.MentionedEntryIds, idx => idx.Method = IndexMethod.gin)
                // ⌘K's Notes and Images sections (17a.3). An expression index, so it goes on the
                // document mapping rather than through Index(x => …), which only takes members:
                // FullTextIndex is the mapping's way in, and DocumentConfig is the text to convert.
                // `simple`, not `english`: the stemmer would turn "Rockseeker" into `rockseek` and
                // break prefix-as-you-type (Notes). SearchSql holds the expression and the queries
                // use the same constant, which is what lets Postgres use the index. The expression is
                // SearchSql.PlainText — what a reader sees — so the index and the note query match on
                // exactly the same lexemes and the index cannot miss a note the query wants.
                .FullTextIndex(idx =>
                {
                    idx.Name = SearchSql.NoteIndexName;
                    idx.RegConfig = SearchSql.Config;
                    idx.DocumentConfig = SearchSql.NoteText;
                });

            // Entry stream -> Entry document. (CampaignId, Kind) serves the wiki's lists; the
            // GIN indexes serve alias lookups and MentionIndex's "articles that mention this
            // entry".
            //
            // LinkedItemKeys (27b) is the third GIN index, for one question asked from outside the
            // API entirely: 26c's prune, run from the ingest CLI, asks "does any entry link to this
            // row?" before it deletes anything (EntryKnowledgeBaseLinks). The index is what makes
            // that a probe rather than a scan of every entry in every campaign, and the prune is on
            // the operator's critical path — a slow answer there is a slow ingest.
            opts.Projections.Snapshot<Entry>(SnapshotLifecycle.Inline);
            opts.Schema.For<Entry>()
                .Index([x => x.CampaignId, x => x.Kind])
                .Index(x => x.Aliases, idx => idx.Method = IndexMethod.gin)
                .Index(x => x.ArticleMentionIds, idx => idx.Method = IndexMethod.gin)
                .Index(x => x.LinkedItemKeys, idx => idx.Method = IndexMethod.gin);

            // The article prefilter (17a.3): a stored generated tsvector column and a GIN index on
            // it, rather than an expression index. The planner will not choose a GIN index for a
            // prefix tsquery at a campaign's size, so an expression index still left Postgres
            // computing to_tsvector(jsonb_path_query_array(…)) for every entry on every search;
            // stored, Postgres computes it once, in the statement that writes the entry document.
            // It covers every block, secret ones too, so it is still only a prefilter: a candidate
            // entry is re-matched block by block on the blocks the viewer can see, and a hit on a
            // secret block alone yields nothing. Names, aliases and session titles have no index;
            // they are scanned per campaign through the (CampaignId, Kind) index above, which is a
            // few thousand short strings.
            SearchSchema.AddEntryArticleVector(opts);

            // Combat stream -> Combat document (step 18a). (CampaignId, Status) serves the Combat
            // tab's list and the live banner, SessionId the combat cards in the stream, and the
            // GIN index on EntryIds an entry's combats (18e), the way ArticleMentionIds is served.
            opts.Projections.Snapshot<Combat>(SnapshotLifecycle.Inline);
            opts.Schema.For<Combat>()
                .Index(x => x.CampaignId)
                .Index([x => x.CampaignId, x => x.Status])
                .Index(x => x.SessionId)
                .Index(x => x.EntryIds, idx => idx.Method = IndexMethod.gin);

            // Image documents (step 16a): storage bookkeeping, not an aggregate. Optimistic
            // concurrency makes two writers racing on one image (attaching it to two notes,
            // or attaching it while it is swept) a conflict for the loser. The correlation id
            // (16b) ties an attach to the note event saved with it (invariant 9).
            opts.Schema.For<Image>()
                .UseOptimisticConcurrency(true)
                .Metadata(m => m.CorrelationId.Enabled = true)
                .Index([x => x.CampaignId, x => x.UploaderMemberId, x => x.NoteId!])
                .Index(x => x.NoteId!);

            opts.Schema.For<IAdminConfig>()
                .AddSubClass<MaintenanceConfig>();
        }).AddAsyncDaemon(DaemonMode.Solo);

        // Create the schema up front rather than leaning on Marten's implicit
        // auto-create, which makes a fresh database's behaviour depend on which
        // endpoint happens to be hit first. A schema conflict now fails startup
        // loudly; `docker compose -p takeinitiative -f compose.dev.yml down -v`
        // resets a stale local database.
        //
        // This runs in every environment now, not only in Development (26c). The API's
        // dockerfile sets no ASPNETCORE_ENVIRONMENT, so a container runs as Production,
        // and while `if (IsDevelopment)` guarded this call, Production applied nothing
        // at all: measured against a freshly reset database, `select extname from
        // pg_extension` came back with plpgsql alone and pg_tables was empty. The two
        // extensions above hang off no document type, so Marten's lazy per-document
        // auto-create has no reason to ensure them, and ⌘K's word_similarity() would
        // have failed at query time with "function does not exist". That is a step 17
        // bug nobody had hit only because nothing is deployed yet.
        //
        // Running DDL at boot is safe here because there is exactly one API process.
        // AddAsyncDaemon(DaemonMode.Solo) above already means only one process may run
        // the projection daemon, so the deployment must never scale the API past one
        // replica — and that same single-replica constraint is what removes the "two
        // startups race on the same CREATE" problem. The migration also finishes before
        // Kestrel opens the port, so no request can race it.
        //
        // What it is not is a no-op on a schema that has drifted. Weasel runs in
        // CreateOrUpdate, which never drops a table, but it does drop and recreate an
        // index whose stored definition no longer matches, and it drops a document-table
        // column it does not know about (see SearchSchemaTests, which asserts a second
        // start has nothing left to do — that test is what keeps a GIN index over every
        // note from being rebuilt on every boot). So the cost of a start is bounded by
        // how far the database has drifted from the configuration, not by its size.
        //
        // The default is on, so a fresh deployment works without anyone knowing the
        // setting exists. ApplySchemaOnStartupKey turns it off, for a database whose DDL
        // is applied out of band: more than one replica, a blue/green swap, or a change
        // that is not additive are exactly the cases where startup DDL stops being safe.
        if (config.GetValue(ApplySchemaOnStartupKey, true))
        {
            martenOpts.ApplyAllDatabaseChangesOnStartup();
        }

        martenOpts.UseLightweightSessions();

        return services;
    }

    /// <summary>
    /// Persists the ASP.NET Core Data Protection key ring to <c>DataProtection:KeyPath</c> when that
    /// setting has a value, and changes nothing at all when it does not.
    /// <para>
    /// Authentication is cookie-based (<c>AddCookieAuth</c>), so every sign-in ticket is encrypted
    /// with a key from that ring. Nothing used to configure it, which means the ring was written to
    /// <c>$HOME/.aspnet/DataProtection-Keys</c> inside the container and thrown away with the
    /// container: <b>every redeploy signed every user out</b>. Production sets
    /// <c>DataProtection__KeyPath=/keys</c> and mounts a named volume there, so the ring outlives
    /// the container and a release stops being a mass sign-out.
    /// </para>
    /// <para>
    /// <see cref="DataProtectionApplicationName"/> is pinned because the default discriminator is
    /// the content-root path: keys written under one content root cannot be read under another, so
    /// without it a moved <c>WORKDIR</c> would invalidate the ring the volume just preserved.
    /// </para>
    /// <para>
    /// Unset is the dev and test default on purpose. `pnpm dev` and the Alba fixtures then get
    /// ASP.NET Core's own behaviour, unchanged, rather than a key directory nobody asked for.
    /// </para>
    /// </summary>
    public static IServiceCollection AddDataProtectionKeyRing(this IServiceCollection services, IConfiguration config)
    {
        var keyPath = config.GetValue<string>(DataProtectionKeyPathKey);
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            return services;
        }

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
            .SetApplicationName(DataProtectionApplicationName);

        return services;
    }

    /// <summary>
    /// Configures <c>UseForwardedHeaders</c> for the reverse proxy that terminates TLS in front of
    /// the API. <c>Program.cs</c> runs the middleware first, before anything can read
    /// <c>Request.Scheme</c> or the client address.
    /// <para>
    /// <c>KnownIPNetworks</c> and <c>KnownProxies</c> are deliberately <b>empty</b>. The middleware
    /// only honours the headers when the immediate peer is on that list, and its default list is
    /// loopback alone — but the proxy reaches the API across a Docker network whose address is
    /// assigned when the network is created and changes when it is recreated, so there is no address
    /// to put on a list. Emptying both lists is what turns the check off; a list with a guessed
    /// address in it would silently ignore the headers instead.
    /// </para>
    /// <para>
    /// That is only safe because the API is not reachable except through the proxy: no service in
    /// <c>compose.prod.yml</c> publishes a host port, the API is on the proxy's internal network,
    /// and the only route in from the internet is the proxy itself. If the API is ever published
    /// directly, a client could spoof its own address and scheme, and these two lists have to come
    /// back.
    /// </para>
    /// <para>
    /// Only the two headers the proxy actually sets are read. <c>X-Forwarded-Host</c> is left off:
    /// <c>AllowedHosts</c> names the API's real host, and honouring a forwarded host would let the
    /// header pick the host instead.
    /// </para>
    /// </summary>
    public static IServiceCollection AddForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(opts =>
        {
            opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // KnownIPNetworks, not KnownNetworks: the latter is [Obsolete] in .NET 10 (ASPDEPR005)
            // and CI treats warnings as errors. They are two views of the same list.
            opts.KnownIPNetworks.Clear();
            opts.KnownProxies.Clear();
        });

        return services;
    }

    /// <summary>
    /// Images (step 16a): the S3 blob store, the processor, the bucket for dev and the
    /// sweeper. Building the S3 client does no I/O, so <c>--export-openapi</c> needs no
    /// blob store.
    /// </summary>
    public static IServiceCollection AddImages(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<BlobOptions>(config.GetSection(BlobOptions.SectionKey));
        services.Configure<ImageOptions>(config.GetSection(ImageOptions.SectionKey));

        services.AddSingleton<IAmazonS3>(sp => S3BlobStore.CreateClient(sp.GetRequiredService<IOptions<BlobOptions>>().Value));
        services.AddSingleton<IBlobStore, S3BlobStore>();
        services.AddHostedService<BlobBucketInitializer>();

        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();

        services.AddSingleton<ImageSweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<ImageSweeper>());
        return services;
    }

    public static WebApplicationBuilder AddIdentityAuthenticationAndAuthorization(this WebApplicationBuilder builder)
    {

        builder.Services
            .AddIdentityCore<ApplicationUser>(opts =>
            {
                opts.SignIn.RequireConfirmedAccount = false;
                opts.Password = new PasswordOptions()
                {
                    RequireDigit = true,
                    RequiredLength = 6,
                    RequireLowercase = true,
                    RequireUppercase = true,
                    RequireNonAlphanumeric = true
                };
            })
            .AddRoles<ApplicationUserRole>()
            .AddUserStore<MartenUserStore<ApplicationUser, ApplicationUserRole>>()
            .AddRoleStore<MartenRoleStore<ApplicationUserRole>>()
            .AddSignInManager() // Sign in manager allows users to sign in and out, and validates these operations.
            .AddDefaultTokenProviders(); // Default token providers for password changes and other temporary auth needs.

        builder.Services
            .AddTransient<IAuthorizationHandler, RequireUserToExistInDatabaseAuthorizationHandler>()
            .AddTransient<IAuthorizationHandler, RequireNotInMaintenanceModeAuthorizationHandler>()
            .AddCookieAuth(validFor: TimeSpan.FromHours(24), opts =>
            {
                opts.SlidingExpiration = true; // Reissue new cookies when the cookie is half or more through its timespan.

                opts.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                opts.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                opts.Cookie.Domain = builder.Configuration.GetValue<string>("CookieDomain") ?? throw new InvalidOperationException("Attempted to find configuration for the value CookieDomain but there was none provided.");

                opts.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
            })
            .AddAuthorization(opts =>
            {
                opts.AddPolicy(TakePolicies.UserExists,
                    new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser() // Requires JWT to exist and be signed by the api
                        .AddRequirements(new RequireUserToExistInDatabaseAuthorizationRequirement())
                        .Build()
                );

                opts.AddPolicy(TakePolicies.NotInMaintenanceMode,
                    new AuthorizationPolicyBuilder()
                        .AddRequirements(new RequireNotInMaintenanceModeAuthorizationRequirement())
                        .Build()
                );
            })
            .AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationMiddlewareResultHandler>() // Order matters, needs to be after add authorization.
            .AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opts.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opts.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });

        builder.Services.AddTransient<ConfirmEmailSender>();
        builder.Services.AddTransient<ResetPasswordEmailSender>();
        return builder;
    }

    public static IServiceCollection AddSerilog(this IServiceCollection services)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog()); // Uses the static logger.
        return services;
    }

    public static IServiceCollection AddOptionObjects(this IServiceCollection builder, IConfiguration config)
    {
        builder.Configure<SendGridOptions>(config.GetSection(SendGridOptions.SendGridOptionsKey));
        builder.Configure<EmailOptions>(config.GetSection(EmailOptions.EmailOptionsKey));
        builder.Configure<UrlsOptions>(config.GetSection(UrlsOptions.UrlsOptionsKey));
        builder.Configure<JWTOptions>(config);
        return builder;
    }

    /// <summary>
    /// ⌘K search (step 17a): the providers, the service that runs them and the entry matcher. The
    /// providers and the service are scoped, because a provider is handed the request's Marten
    /// session as it goes; the matcher holds nothing at all and takes the session as a parameter, so
    /// it is a singleton. The providers are registered in the order §11 shows results in, wiki first;
    /// step 18 adds a combat provider and steps 20 and 21 reference providers, as more registrations
    /// here.
    /// </summary>
    public static IServiceCollection AddSearch(this IServiceCollection services)
    {
        services.AddSingleton<EntryMatcher>();
        services.AddScoped<ISearchProvider, WikiSearchProvider>();
        services.AddScoped<ISearchProvider, SessionSearchProvider>();
        services.AddScoped<ISearchProvider, CombatSearchProvider>();
        // Last: the Reference section comes after every campaign section (§11). It reads the
        // ReferenceCatalog that AddReference() registers.
        services.AddScoped<ISearchProvider, ReferenceSearchProvider>();
        services.AddScoped<SearchService>();
        return services;
    }

    /// <summary>
    /// Reference content (step 20a): the providers, registered in the order the Reference section
    /// merges them (SRD 5.2 first, then the 5eTools corpus, 21b), and the catalog that lists them.
    /// <para>
    /// <b>The order is the behaviour.</b> <c>ReferenceSearchProvider</c> breaks a tie on the match
    /// ladder by the provider's position in this list, so SRD 5.2 coming first is what makes it rank
    /// above a 5eTools row of the same name and the same similarity (step 20). Moving a line here
    /// changes ⌘K.
    /// </para>
    /// <para>
    /// The SRD's catalogue is read once from the assembly and held, so it and its provider are
    /// singletons. The knowledge-base provider (26d₂) reads <c>knowledge_base_item</c> through the
    /// request's Marten session and so is scoped, which <c>ReferenceCatalog</c> was already scoped
    /// for. Its table may be empty — nothing has been ingested — and that is a state rather than an
    /// error: it answers no rows and the Reference section is exactly step 20's.
    /// </para>
    /// </summary>
    public static IServiceCollection AddReference(this IServiceCollection services)
    {
        services.AddSingleton<SrdCatalog>();
        services.AddSingleton<SrdReferenceProvider>();
        services.AddSingleton<IReferenceProvider>(sp => sp.GetRequiredService<SrdReferenceProvider>());
        services.AddScoped<KnowledgeBaseQueries>();
        services.AddScoped<KnowledgeBaseReferenceProvider>();
        services.AddScoped<IReferenceProvider>(sp => sp.GetRequiredService<KnowledgeBaseReferenceProvider>());
        services.AddScoped<ReferenceCatalog>();
        // An entry's links (27b), resolved against the corpus on every read. Scoped for the same
        // reason the catalog is: it reads the request's Marten session through KnowledgeBaseQueries.
        services.AddScoped<EntryLinkResolver>();
        // "Does this entry look like a row in the corpus?" (28b): one query, no model. Scoped for the
        // same reason — it asks KnowledgeBaseQueries, which holds the request's session.
        services.AddScoped<KnowledgeBaseSuggester>();
        return services;
    }

    public static IServiceCollection AddDiceRollers(this IServiceCollection services, IConfiguration configuration)
    {
        // Random.Shared is thread-safe; a single shared `new Random()` is not.
        services.AddSingleton<IDiceRoller>(new DiceRoller(Random.Shared));
        return services;
    }

    public static IServiceCollection AddSendGrid(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSendGrid((_) =>
        {
            _.ApiKey = configuration.GetValue<string>("SendGrid:ApiKey");
        });

        services.AddTransient<IEmailSender, SendGridEmailSender>();
        return services;
    }
}
