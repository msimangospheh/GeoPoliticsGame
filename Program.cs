using GeoPolitics_Game.Data;
using GeoPolitics_Game.Data.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using GeoPolitics_Game.Services.Engine;

MongoConventions.Register();   // must run BEFORE anything touches MongoDB

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();