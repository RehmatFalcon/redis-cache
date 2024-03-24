using System.Text.Json;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using NRedisStack.Search.Literals.Enums;
using RedisCaching.Models;
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

app.MapGet("/countries", async () =>
{
    var instance = GetRedisInstance(builder);
    var key = "countries-list";
    var existingData = instance.StringGet(key);
    if (!existingData.HasValue)
    {
        var countries = await GetCountryDataFromUpstream();
        var value = JsonSerializer.Serialize(countries);
        instance.StringSet(key, value);
        return value;
    }

    return existingData.ToString();
});

app.MapGet("/create-json-index", () =>
{
    var instance = GetRedisInstance(builder);
    var ft = instance.FT();
    var schema = new Schema()
        .AddTextField(new FieldName("$.Flag", "Flag"))
        .AddNumericField(new FieldName("$.Population", "Population"))
        .AddTextField(new FieldName("$.Capital", "Capital"))
        .AddTextField(new FieldName("$.Name", "Name"));
    ft.Create(
        "idx:country",
        new FTCreateParams().On(IndexDataType.JSON).Prefix("country:"),
        schema);
});

app.MapGet("/countries-json", async () =>
{
    var instance = GetRedisInstance(builder);
    var key = "countries-list-json";
    var jsonBuilder = instance.JSON();
    var ft = instance.FT();
    var query1 = new Query("*");
    var res1 = ft.Search("idx:country", query1).Documents;
    if (res1.Any())
    {
        return res1.Select(x => x["json"].ToString());
    }

    var data = await GetCountryDataFromUpstream();

    for (int i = 0; i < data.Count; i++)
    {
        var item = new JsonCountryData();
        item.Flag = data[i].Flag;
        item.Population = data[i].Population;
        item.Capital = data[i].Capital?[0];
        item.Name = data[i].Name.Official;
        jsonBuilder.Set($"country:{i}", "$", item);
    }
    
    var res2 = ft.Search("idx:country", query1).Documents;
    return res2.Select(x => x["json"].ToString());
});

app.MapGet("/redis-subscribe/{channel}", async (string channel) =>
{
    var multiplexer = GetRedisMultiplexer(builder);
    multiplexer.GetSubscriber().Subscribe(channel, (channel, value) =>
    {
        Console.WriteLine($"Received. Channel: {channel}, Message: {value}");
    });
    return "Subscription Successful";
});

app.MapGet("/redis-publish/{channel}/{message}", async (string channel, string message) =>
{
    var instance = GetRedisInstance(builder);
    await instance.PublishAsync(channel, message);
    return $"Publish successful. Channel: {channel}, Message: {message}.";
});

app.Run();

IDatabase GetRedisInstance(WebApplicationBuilder webApplicationBuilder)
{
    var redis = GetRedisMultiplexer(webApplicationBuilder);
    IDatabase db = redis.GetDatabase();
    return db;
}

async Task<List<CountryInformation>?> GetCountryDataFromUpstream()
{
    return await new HttpClient()
        .GetFromJsonAsync<List<CountryInformation>>("https://restcountries.com/v3.1/all");
}

ConnectionMultiplexer GetRedisMultiplexer(WebApplicationBuilder webApplicationBuilder1)
{
    ConnectionMultiplexer connectionMultiplexer = ConnectionMultiplexer.Connect(webApplicationBuilder1.Configuration["redis"]);
    return connectionMultiplexer;
}

public class JsonCountryData()
{

    public string Flag { get; set; }
    public long Population { get; set; }
    public string Capital { get; set; }
    public string Name { get; set; }
}