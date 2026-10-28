using GeoPolitics_Game.Services;
using GeoPolitics_Game.Data;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Read the "MongoDb" settings and make MongoContext available everywhere
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.AddSingleton<MongoContext>();

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<GameEngineService>();
builder.Services.AddSingleton<NpcAIService>(); // Add this line

var app = builder.Build();

// Startup check: ping the database so we know the connection works
using (var scope = app.Services.CreateScope())
{
    var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();
    try
    {
        mongo.Database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
        app.Logger.LogInformation("? Connected to MongoDB Atlas");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "? Could not connect to MongoDB");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
