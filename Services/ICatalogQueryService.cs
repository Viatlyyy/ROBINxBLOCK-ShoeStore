using ShoeStore.Models;

namespace ShoeStore.Services;

public interface ICatalogQueryService
{
    Task<HomePageViewModel> GetHomeAsync(CancellationToken cancellationToken);
    Task<HomePageViewModel> GetHomeAsync(CatalogSelection selection, CancellationToken cancellationToken);
    Task<CatalogPageViewModel> GetCatalogAsync(int page, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductCardViewModel>> GetProductsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
    Task<ProductCardViewModel?> GetProductAsync(int id, CancellationToken cancellationToken);
}
