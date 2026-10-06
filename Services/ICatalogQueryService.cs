using ShoeStore.Models;

namespace ShoeStore.Services;

public interface ICatalogQueryService
{
    Task<HomePageViewModel> GetHomeAsync(CancellationToken cancellationToken);
    Task<CatalogPageViewModel> GetCatalogAsync(int page, CancellationToken cancellationToken);
    Task<Product?> GetProductDetailsAsync(int id, CancellationToken cancellationToken);
}
