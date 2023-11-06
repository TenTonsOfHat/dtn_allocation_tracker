// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.Diagnostics;
using Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Polly;
using Polly.Extensions.Http;
using Refit;


async Task<int> RunQuery(string username, string apiKey, string serviceKey, IServiceProvider serviceProvider)
{
    var credentials = new AllocationTrackerCredentials()
    {
        WebServiceKey = serviceKey,
        Apikey = apiKey,
        Username = username
    };

    var sw = Stopwatch.StartNew();

    var service = serviceProvider.GetRequiredService<IAllocationTrackerService>();
    var allocResult = await service.Allocations(new AllocationRequest()
    {
        Credentials = credentials
    });

    Console.Out.WriteLine("Elapsed: " + sw.Elapsed);
    
    File.WriteAllText($"{credentials.Username}_allocations.json", JsonConvert.SerializeObject(allocResult.Content, Formatting.Indented));
    return 1;
}

IServiceProvider ConfigureServices(string[] strings)
{
    var builder = Host.CreateApplicationBuilder(strings);
    builder.Services
        .AddRefitClient<IAllocationTrackerRefitClient>(new RefitSettings(
            new NewtonsoftJsonContentSerializer(
                new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }
            )
        ))
        .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.dtn.com/fuelsuite/"))
        .AddPolicyHandler(_ =>
        {
            return HttpPolicyExtensions.HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        });

    builder.Services.AddEasyCaching(options =>
    {
        // use memory cache with a simple way
        options.UseInMemory("default");
    });

    builder.Services.AddTransient<IAllocationTrackerService, AllocationTrackerService>();

    builder.Logging.ClearProviders();
    return builder.Build().Services;
}


async Task<int> InvokeAsync(string[] cliArgs)
{
    var webServiceKeyParam = new Option<string>("--WebServiceKey", "The WebServiceKey");
    var apikeyParam = new Option<string>("--Apikey", "The Apikey");
    var userParam = new Option<string>("--Username", "The Service username");
    var serviceProvider = ConfigureServices(cliArgs);


    var rootCommand = new RootCommand("query allocations from the AllocationTracker web service");
    rootCommand.AddOption(webServiceKeyParam);
    rootCommand.AddOption(apikeyParam);
    rootCommand.AddOption(userParam);

    int? result = null;
    
    rootCommand.SetHandler(
        async (username, apiKey, serviceKey) => { result = await RunQuery(username, apiKey, serviceKey, serviceProvider); },
        userParam,
        apikeyParam,
        webServiceKeyParam
    );

    await rootCommand.InvokeAsync(cliArgs);

    return result.GetValueOrDefault();
}

// Globals
// ReSharper disable InconsistentNaming
return await InvokeAsync(args);