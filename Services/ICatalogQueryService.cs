using ShoeStore.Models;

namespace ShoeStore.Services;

public interface ICatalogQueryService
{
    Task<HomePageViewModel> GetHomeAsync(CancellationToken cancellationToken);
    Task<HomePageViewModel> GetHomeAsync(CatalogSelection selection, CancellationToken cancellationToken);
    Task<CatalogPageViewModel> GetCatalogAsync(int page, CancellationToken cancellationToken);
}
