using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Services;

// Источник данных действующего макета: все 16 моделей и список брендов.
public sealed class CatalogQueryService : ICatalogQueryService
{
    public Task<HomePageViewModel> GetHomeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(HomePageDemoData.Create());
    }
}
