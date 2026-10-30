using AspNetCore.Identity.MongoDbCore.Extensions;
using AspNetCore.Identity.MongoDbCore.Infrastructure;
using GeoPolitics_Game.Data;
using GeoPolitics_Game.Data.Repositories;
using GeoPolitics_Game.Identity;
using GeoPolitics_Game.Services.Engine;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

MongoConventions.Register();   // must run BEFORE anything touches MongoDB

var builder = WebApplication.CreateBuilder(args);

// MVC + an automatic anti-forgery check on EVERY form POST (stops cross-site request forgery)
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// ---- Database layer ----
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<ICountryRepository, MongoCountryRepository>();
builder.Services.AddScoped<IGameRepository, MongoGameRepository>();
builder.Services.AddScoped<IGameEventRepository, MongoGameEventRepository>();
builder.Services.AddScoped<DatabaseSeeder>();

// ---- Game engine ----
builder.Services.AddSingleton<IRandomSource, SecureRandomSource>();
builder.Services.AddSingleton<IGameEngine, GameEngine>();

// ---- Login: ASP.NET Identity, users stored in MongoDB ----
var mongoConnection = builder.Configuration["MongoDb:ConnectionString"]
    ?? throw new InvalidOperationException("MongoDb:ConnectionString is missing. Set it with dotnet user-secrets.");
var mongoDatabase = builder.Configuration["MongoDb:DatabaseName"] ?? "geopolitics";

builder.Services.ConfigureMongoDbIdentity<ApplicationUser, ApplicationRole, Guid>(new MongoDbIdentityConfiguration
{
    MongoDbSettings = new MongoDbSettings { ConnectionString = mongoConnection, DatabaseName = mongoDatabase },
    IdentityOptionsAction = options =>
    {
        // Password rules
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // Lock the account for 5 minutes after 5 wrong passwords (stops password guessing)
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

        options.User.RequireUniqueEmail = true;
    }
})
.AddSignInManager()
.AddDefaultTokenProviders();

// Use cookies to remember who is logged in, and send visitors to the login page when needed
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies();

// The login cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.Cookie.Name = "GeoPolitics.Auth";
    options.Cookie.HttpOnly = true;                              // JavaScript can't read it
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;     // only sent over HTTPS
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

var app = builder.Build();

// ---- Startup: check the connection, then seed data ----
using (var scope = app.Services.CreateScope())
{
    try
    {
        var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();
        mongo.Database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
        app.Logger.LogInformation("[OK] Connected to MongoDB Atlas");

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "[FAILED] MongoDB startup failed");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();   // who are you?   (must come BEFORE authorization)
app.UseAuthorization();    // are you allowed?

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();