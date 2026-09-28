using Amazon.S3;
using FastEndpoints.Security;
using Microsoft.Extensions.Options;
using TakeInitiative.Api.Features.Images;

using TakeInitiative.Api.Identity;

using Marten;
using Marten.Events.Daemon.Resiliency;
using Marten.Events.Projections;
using Marten.Schema;
using Weasel.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SendGrid.Extensions.DependencyInjection;
using Serilog;
using TakeInitiative.Api.Features.Admin;
using TakeInitiative.Utilities;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace TakeInitiative.Api.Bootstrap;
public static class Bootstrap
{
    public static IServiceCollection AddMartenDB(this IServiceCollection services, IConfiguration config, bool IsDevelopment)
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
            opts.Projections.Snapshot<Entry>(SnapshotLifecycle.Inline);
            opts.Schema.For<Entry>()
                .Index([x => x.CampaignId, x => x.Kind])
                .Index(x => x.Aliases, idx => idx.Method = IndexMethod.gin)
                .Index(x => x.ArticleMentionIds, idx => idx.Method = IndexMethod.gin);

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

        if (IsDevelopment)
        {
            // Create the schema up front rather than leaning on Marten's implicit
            // auto-create, which makes a fresh database's behaviour depend on which
            // endpoint happens to be hit first. A schema conflict now fails startup
            // loudly; `docker compose -p takeinitiative -f compose.dev.yml down -v`
            // resets a stale local database.
            martenOpts.ApplyAllDatabaseChangesOnStartup();
        }

        martenOpts.UseLightweightSessions();

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
        services.AddScoped<SearchService>();
        return services;
    }

    /// <summary>
    /// Reference content (step 20a): the providers, registered in the order the Reference section
    /// merges them (SRD 5.2 first; step 21 adds the 5eTools index after it), and the catalog that
    /// lists them. The SRD's data is read once and held, so its catalog and provider are singletons.
    /// </summary>
    public static IServiceCollection AddReference(this IServiceCollection services)
    {
        services.AddSingleton<SrdCatalog>();
        services.AddSingleton<SrdReferenceProvider>();
        services.AddSingleton<IReferenceProvider>(sp => sp.GetRequiredService<SrdReferenceProvider>());
        services.AddScoped<ReferenceCatalog>();
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
