using ShoeStore.Models;

namespace ShoeStore.Services;

public interface ICatalogQueryService
{
    Task<HomePageViewModel> GetHomeAsync(CancellationToken cancellationToken);
}
