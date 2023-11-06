using System.Collections.Concurrent;
using EasyCaching.Core;
using Refit;

// ReSharper disable CollectionNeverUpdated.Global

namespace Library;

public interface IAllocationTrackerService
{
    Task<IApiResponse<ApiResult_Product>> GetGroupedProducts(ProductGroupingRequest request);
    Task<IApiResponse<ApiResult_AllocationProduct>> AllocationProducts(ProductRequest request);
    Task<IApiResponse<ApiResult_Location>> Locations(LocationRequest request);
    Task<IApiResponse<ApiResult_Supplier>> Suppliers(SupplierRequest request);
    Task<IApiResponse<ApiResult_AllocationV2>> Allocations(AllocationRequest request, bool pullReferenceDataGroups = true);
    Task<IApiResponse<ApiResult_Terminal>> GetGroupedTerminals(TerminalGroupingRequest request);
}

public class AllocationTrackerService : IAllocationTrackerService
{
    private readonly IAllocationTrackerRefitClient _refitClient;
    private readonly IEasyCachingProvider _provider;

    public AllocationTrackerService(IAllocationTrackerRefitClient refitClient, IEasyCachingProvider provider)
    {
        _refitClient = refitClient;
        this._provider = provider;
    }


    public async Task<IApiResponse<ApiResult_Terminal>> GetGroupedTerminals(TerminalGroupingRequest request)
    {
        var result = await _provider.GetAsync(
            $"{nameof(GetGroupedTerminals)}-{request.SellerNumber}-{request.Id}",
            async () => await MakeReq(),
            TimeSpan.FromHours(12),
            CancellationToken.None
        );

        return result.Value;

        async Task<IApiResponse<ApiResult_Terminal>> MakeReq()
        {
            var res = await _refitClient.TerminalsInTerminalGroup(
                request.SellerNumber,
                request.Id,
                request.Accept,
                request.Credentials.WebServiceKey,
                request.Credentials.Apikey,
                request.Credentials.Username
            );
            if (res.Error != null) throw res.Error;
            return res;
        }
    }


    public async Task<IApiResponse<ApiResult_Product>> GetGroupedProducts(ProductGroupingRequest request)
    {
        var result = await _provider.GetAsync(
            $"{nameof(GetGroupedProducts)}{request.SellerNumber}-{request.Id}-{request.Type}",
            async () => await MakeReq(),
            TimeSpan.FromHours(12),
            CancellationToken.None
        );

        return result.Value;

        async Task<IApiResponse<ApiResult_Product>> MakeReq()
        {
            IApiResponse<ApiResult_Product> res;
            switch (request.Type)
            {
                case ProductGroupingRequestType.Family:
                    res = await _refitClient.ProductsInProductFamily(
                        request.SellerNumber,
                        request.Id,
                        request.Accept,
                        request.Credentials.WebServiceKey,
                        request.Credentials.Apikey,
                        request.Credentials.Username
                    );
                    break;
                case ProductGroupingRequestType.Group:
                    res = await _refitClient.ProductsInProductGroup(
                        request.SellerNumber,
                        request.Id,
                        request.Accept,
                        request.Credentials.WebServiceKey,
                        request.Credentials.Apikey,
                        request.Credentials.Username
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (res.Error != null) throw res.Error;
            return res;
        }
    }

    public async Task<IApiResponse<ApiResult_AllocationProduct>> AllocationProducts(ProductRequest request)
    {
        var res = await _refitClient.AllocationProducts(
            request.SellerNumber,
            request.LocationId,
            request.Accept,
            request.Credentials.WebServiceKey,
            request.Credentials.Apikey,
            request.Credentials.Username
        );

        if (res.Error != null) throw res.Error;
        return res;
    }

    public async Task<IApiResponse<ApiResult_Location>> Locations(LocationRequest request)
    {
        var res = await _refitClient.Locations(
            request.SellerNumber,
            request.Accept,
            request.Credentials.WebServiceKey,
            request.Credentials.Apikey,
            request.Credentials.Username
        );

        if (res.Error != null) throw res.Error;
        return res;
    }

    public async Task<IApiResponse<ApiResult_Supplier>> Suppliers(SupplierRequest request)
    {
        var res = await _refitClient.Suppliers(
            request.Accept,
            request.Credentials.WebServiceKey,
            request.Credentials.Apikey,
            request.Credentials.Username
        );

        if (res.Error != null) throw res.Error;
        return res;
    }


    private async Task<IApiResponse<ApiResult_AllocationV2>> GetAllocations(AllocationRequest request)
    {
        var res = await _refitClient.Allocations(
            request.CustSupplierId,
            request.CustTerminalId,
            request.CustProductId,
            request.SupplierId,
            request.TerminalId,
            request.ProductId,
            request.AtSellerNum,
            request.AtTerminal,
            request.AtTerminalGroup,
            request.AtProductCode,
            request.AtProductGroup,
            request.AtProductFamily,
            request.IncludeRackData,
            request.Page,
            request.PageSize,
            request.Accept,
            request.Credentials.WebServiceKey,
            request.Credentials.Apikey,
            request.Credentials.Username
        );

        if (res.Error != null) throw res.Error;
        return res;
    }

    #region Allocations

    public async Task<IApiResponse<ApiResult_AllocationV2>> Allocations(AllocationRequest request, bool pullReferenceDataGroups = true)
    {
        var result = await GetAllocations(request);

        if (pullReferenceDataGroups)
            await MapGroups(result, request.Credentials);

        return result;
    }

    async Task MapGroups(IApiResponse<ApiResult_AllocationV2> apiResponse, AllocationTrackerCredentials allocationTrackerCredentials)
    {
        {
            var groupingRequests = new List<ProductGroupingRequest>();
            var terminalGroupRequests = new List<TerminalGroupingRequest>();
            var allocationResults = apiResponse.Content?.Data ?? Array.Empty<AllocationV2>();
            foreach (var allocation in allocationResults)
            {
                var supplier = allocation.Supplier.SellerNum;


                var terminalGroup = allocation.Location.TerminalGroup;
                if (terminalGroup?.Id != null)
                {
                    terminalGroupRequests.Add(new TerminalGroupingRequest
                    {
                        Credentials = allocationTrackerCredentials,
                        SellerNumber = supplier,
                        Id = terminalGroup.Id.Value
                    });
                }


                foreach (var productAllocation in allocation.ProductAllocationList)
                {
                    var family = productAllocation.AllocationProduct.ProductFamily;
                    var group = productAllocation.AllocationProduct.ProductGroup;
                    if (family?.Id != null)
                    {
                        groupingRequests.Add(new ProductGroupingRequest
                        {
                            SellerNumber = supplier,
                            Id = family.Id.Value,
                            Type = ProductGroupingRequestType.Family,
                            Credentials = allocationTrackerCredentials
                        });
                    }
                    else if (group?.Id != null)
                    {
                        groupingRequests.Add(new ProductGroupingRequest
                        {
                            SellerNumber = supplier,
                            Id = group.Id.Value,
                            Type = ProductGroupingRequestType.Group,
                            Credentials = allocationTrackerCredentials
                        });
                    }
                }
            }

            await MapProductsAndLocationsFromGroups(groupingRequests, terminalGroupRequests, allocationResults);
        }

        async Task<Dictionary<(string SellerNumber, int Id, ProductGroupingRequestType Type), ApiResult_Product>>
            QueryProductGroups(List<ProductGroupingRequest> productGroupingRequests)
        {
            var distinctRequests = productGroupingRequests.DistinctBy(x => new { x.SellerNumber, x.Id, x.Type }).ToList();
            var productGroupResponses = new ConcurrentBag<(ProductGroupingRequest groupRequest, IApiResponse<ApiResult_Product>)>();
            await Parallel.ForEachAsync(distinctRequests, async (request, _) =>
            {
                var resp = await GetGroupedProducts(request);
                productGroupResponses.Add((request, resp));
            });
            return productGroupResponses
                .Where(x => x.Item2.Content != null)
                .ToDictionary(
                    x => (x.groupRequest.SellerNumber, x.groupRequest.Id, x.groupRequest.Type),
                    x => x.Item2.Content
                );
        }

        async Task<Dictionary<(string SellerNumber, int Id), ApiResult_Terminal>> QueryLocationGroups(List<TerminalGroupingRequest> terminalGroupingRequests)
        {
            var distinctTerminalGroups = terminalGroupingRequests.DistinctBy(x => new { x.SellerNumber, x.Id }).ToList();
            var terminalGroupResponses = new ConcurrentBag<(TerminalGroupingRequest request, IApiResponse<ApiResult_Terminal> resp)>();
            await Parallel.ForEachAsync(distinctTerminalGroups, async (request, _) =>
            {
                var resp = await GetGroupedTerminals(request);
                terminalGroupResponses.Add((request, resp));
            });
            return terminalGroupResponses.Where(x => x.resp.Content != null)
                .ToDictionary(
                    x => (x.request.SellerNumber, x.request.Id),
                    x => x.resp.Content
                );
        }

        void TrySetMappedTerminals(AllocationV2 allocationV2, Dictionary<(string SellerNumber, int Id), ApiResult_Terminal> apiResultTerminals)
        {
            var supplier = allocationV2.Supplier.SellerNum;
            var terminalGroup = allocationV2.Location.TerminalGroup;
            if (terminalGroup?.Id != null && apiResultTerminals.TryGetValue((supplier, terminalGroup.Id.Value), out var terminal))
            {
                allocationV2.Location.MappedTerminals = terminal.Data.Select(x => new TerminalV2
                {
                    AlternateId = null,
                    Country = x.Country,
                    Id = int.TryParse(x.Id, out var id) ? id : 0,
                    MappedName = x.MappedName,
                    Name = x.Name,
                    Owner = x.Owner,
                    PlantId = x.PlantId,
                    Splc = x.Splc,
                    Tcn = x.Tcn
                }).ToList();
            }
        }

        void TrySetMappedProducts(AllocationV2 allocationV2, Dictionary<(string SellerNumber, int Id, ProductGroupingRequestType Type), ApiResult_Product> apiResultProducts)
        {
            var supplier = allocationV2.Supplier.SellerNum;
            foreach (var productAllocation in allocationV2.ProductAllocationList)
            {
                var family = productAllocation.AllocationProduct.ProductFamily;
                var group = productAllocation.AllocationProduct.ProductGroup;
                if (family?.Id != null && apiResultProducts.TryGetValue((supplier, family.Id.Value, ProductGroupingRequestType.Family), out var familyResult))
                {
                    productAllocation.AllocationProduct.MappedProducts = familyResult.Data;
                }
                else if (group?.Id != null && apiResultProducts.TryGetValue((supplier, group.Id.Value, ProductGroupingRequestType.Group), out var groupResult))
                {
                    productAllocation.AllocationProduct.MappedProducts = groupResult.Data;
                }
            }
        }

        async Task MapProductsAndLocationsFromGroups(
            List<ProductGroupingRequest> list, List<TerminalGroupingRequest> terminalGroupRequests1, ICollection<AllocationV2> allocations)
        {
            var productGroupings = await QueryProductGroups(list);

            var locationDict = await QueryLocationGroups(terminalGroupRequests1);

            foreach (var allocation in allocations)
            {
                TrySetMappedTerminals(allocation, locationDict);
                TrySetMappedProducts(allocation, productGroupings);
            }
        }
    }

    #endregion
}

public class AllocationTrackerCredentials
{
    public string WebServiceKey { get; set; }
    public string Apikey { get; set; }
    public string Username { get; set; }
}

public abstract class AllocationTrackerRequestBase
{
    public string Accept { get; set; } = "application/vnd.dtn.energy.v2+JSON";
    public AllocationTrackerCredentials Credentials { get; set; }
}

public class SupplierRequest : AllocationTrackerRequestBase
{
    public SupplierRequest()
    {
    }

    public SupplierRequest(AllocationTrackerCredentials creds)
    {
        Credentials = creds;
    }
}

public class LocationRequest : AllocationTrackerRequestBase
{
    public string SellerNumber { get; set; }
}

public enum ProductGroupingRequestType
{
    Family,
    Group
}

public class TerminalGroupingRequest : AllocationTrackerRequestBase
{
    public string SellerNumber { get; set; }
    public int Id { get; set; }
}

public class ProductGroupingRequest : AllocationTrackerRequestBase
{
    public string SellerNumber { get; set; }
    public int Id { get; set; }
    public ProductGroupingRequestType Type { get; set; }
}

public class ProductRequest : AllocationTrackerRequestBase
{
    public string SellerNumber { get; set; }
    public int LocationId { get; set; }
}

public class AllocationRequest : AllocationTrackerRequestBase
{
    public ICollection<string> CustSupplierId { get; set; }
    public ICollection<string> CustTerminalId { get; set; }
    public ICollection<string> CustProductId { get; set; }
    public ICollection<long> SupplierId { get; set; }
    public ICollection<long> TerminalId { get; set; }
    public ICollection<long> ProductId { get; set; }
    public ICollection<string> AtSellerNum { get; set; }
    public ICollection<string> AtTerminal { get; set; }
    public ICollection<string> AtTerminalGroup { get; set; }
    public ICollection<string> AtProductCode { get; set; }
    public ICollection<string> AtProductGroup { get; set; }
    public ICollection<string> AtProductFamily { get; set; }
    public string IncludeRackData { get; set; }
    public string Page { get; set; }
    public int? PageSize { get; set; }
}