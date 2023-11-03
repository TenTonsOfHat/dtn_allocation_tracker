// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.Diagnostics;
using Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Extensions.Http;
using Refit;

// Globals
// ReSharper disable InconsistentNaming
var WebServiceKey = new Option<string>("--WebServiceKey", "The WebServiceKey");
var Apikey = new Option<string>("--Apikey", "The Apikey");
var Username = new Option<string>("--Username", "The Service username");
var SERVICES = GetDI(args);
AllocationTrackerCredentials CREDENTIALS = null;


var rootCommand = new RootCommand("query allocations from the AllocationTracker web service");
rootCommand.AddOption(WebServiceKey);
rootCommand.AddOption(Apikey);
rootCommand.AddOption(Username);

rootCommand.SetHandler(async (username, apiKey, serviceKey) =>
    {
        CREDENTIALS = new AllocationTrackerCredentials()
        {
            WebServiceKey = serviceKey,
            Apikey = apiKey,
            Username = username
        };
        await QueryAndWrite();
    },
    Username, Apikey, WebServiceKey);

return await rootCommand.InvokeAsync(args);



async Task<int> QueryAndWrite()
{
    var result = await ExecuteSupplierQuery();
    File.WriteAllText($"{CREDENTIALS.Username}_allocations.json", JsonConvert.SerializeObject(result.Content, Formatting.Indented));
    return 1;
}


IServiceProvider GetDI(string[] strings)
{
    var builder = Host.CreateApplicationBuilder(strings);
    builder.Services
        .AddRefitClient<IAllocationTrackerRefitClient>()
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
    var host1 = builder.Build();
    return host1.Services;
}


async Task<IApiResponse<ApiResult_AllocationV2>> ExecuteSupplierQuery()
{
    var sw = Stopwatch.StartNew();
   
    var service = SERVICES.GetRequiredService<IAllocationTrackerService>();
    var result = await service.Allocations(new AllocationRequest()
    {
        Credentials = CREDENTIALS
    });

    Console.Out.WriteLine("Elapsed: " + sw.Elapsed);

    return result;
}

