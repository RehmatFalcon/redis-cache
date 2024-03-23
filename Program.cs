using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

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

app.MapGet("/set-redis-data/{data}", (string data) =>
{
    var instance = GetRedisInstance(builder);
    instance.StringSet(new RedisKey("current-data"), new RedisValue(data));
    return "Done";
});

app.MapGet("/get-redis-data", () =>
{
    var instance = GetRedisInstance(builder);
    var result = instance.StringGet(new RedisKey("current-data"));
    if (!result.HasValue)
    {
        return "Value not present";
    }
    return "Value: " + result.ToString();
});

app.Run();

IDatabase GetRedisInstance(WebApplicationBuilder webApplicationBuilder)
{
    ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(webApplicationBuilder.Configuration["redis"]);
    IDatabase db = redis.GetDatabase();
    return db;
}