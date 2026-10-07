using ShoeStore.Models;

namespace ShoeStore.Data;

// Демонстрационное наполнение макета взято из готового сайта.
// Эти объекты создаются в памяти: класс не обращается к БД и не сохраняет в неё товары.
public static class HomePageDemoData
{
    public static HomePageViewModel Create()
    {
        List<ProductCardViewModel> products =
        [
            CreateProduct(1, "New Balance", "9060", 22990m, true,
                CreateVariant(1, "Sea Salt", "#e8e8e8", "/images/products/9060.jpg", "/images/products/thumbs/9060.webp", true),
                CreateVariant(29, "Black / Castlerock", "#25272b", "/images/gallery/optimized/colors/9060-black-castlerock-side.jpg", "/images/gallery/optimized/colors/9060-black-castlerock-side.jpg", false)),
            CreateProduct(2, "New Balance", "1906R", 19990m, true,
                CreateVariant(2, "Silver Metallic", "#e8e8e8", "/images/products/1906r.jpg", "/images/products/thumbs/1906r.webp", true)),
            CreateProduct(3, "New Balance", "2002R", 21990m, true,
                CreateVariant(3, "Protection Pack", "#e8e8e8", "/images/products/2002r.jpg", "/images/products/thumbs/2002r.webp", true),
                CreateVariant(31, "Phantom / Magnet", "#797770", "/images/gallery/optimized/colors/2002r-phantom-magnet-side.jpg", "/images/gallery/optimized/colors/2002r-phantom-magnet-side.jpg", false)),
            CreateProduct(8, "Balenciaga", "Track LED", 84990m, true,
                CreateVariant(8, "Black", "#e8e8e8", "/images/products/track-led.jpg", "/images/products/thumbs/track-led.webp", true),
                CreateVariant(37, "Sand / Beige", "#c9b69e", "/images/gallery/optimized/colors/track-led-sand-beige-side.jpg", "/images/gallery/optimized/colors/track-led-sand-beige-side.jpg", false),
                CreateVariant(39, "Silver / Ice Grey", "#a8adb6", "/images/gallery/optimized/colors/track-led-silver-ice-side.jpg", "/images/gallery/optimized/colors/track-led-silver-ice-side.jpg", false)),
            CreateProduct(16, "Adidas", "Campus 00s", 15990m, true,
                CreateVariant(16, "Core Black", "#e8e8e8", "/images/products/campus-00s.jpg", "/images/products/thumbs/campus-00s.webp", true),
                CreateVariant(32, "Dark Green / Gum", "#164b35", "/images/gallery/optimized/colors/campus-00s-dark-green-side.jpg", "/images/gallery/optimized/colors/campus-00s-dark-green-side.jpg", false)),
            CreateProduct(19, "ASICS", "Gel-Kayano 14", 18990m, true,
                CreateVariant(19, "Cream / Black", "#e8e8e8", "/images/products/gel-kayano-14.jpg", "/images/products/thumbs/gel-kayano-14.webp", true),
                CreateVariant(30, "Midnight / Silver", "#182b52", "/images/gallery/optimized/colors/gel-kayano-14-midnight-silver-side.jpg", "/images/gallery/optimized/colors/gel-kayano-14-midnight-silver-side.jpg", false)),
            CreateProduct(22, "Jordan", "Air Jordan 1 Low", 21990m, true,
                CreateVariant(22, "Vintage Grey", "#e8e8e8", "/images/products/air-jordan-1-low.jpg", "/images/products/thumbs/air-jordan-1-low.webp", true),
                CreateVariant(38, "University Blue / White", "#77b7e5", "/images/gallery/optimized/colors/air-jordan-1-low-university-blue-side.jpg", "/images/gallery/optimized/colors/air-jordan-1-low-university-blue-side.jpg", false),
                CreateVariant(40, "Burgundy / Sail", "#671e2a", "/images/gallery/optimized/colors/air-jordan-1-low-burgundy-sail-side.jpg", "/images/gallery/optimized/colors/air-jordan-1-low-burgundy-sail-side.jpg", false)),
            CreateProduct(25, "Salomon", "XT-6", 24990m, true,
                CreateVariant(25, "Black Phantom", "#e8e8e8", "/images/products/xt-6.jpg", "/images/products/thumbs/xt-6.webp", true)),
            CreateProduct(4, "New Balance", "530", 14990m, false,
                CreateVariant(4, "White / Silver", "#e8e8e8", "/images/products/530.jpg", "/images/products/thumbs/530.webp", true),
                CreateVariant(36, "Sea Salt / Silver", "#d9d4ca", "/images/gallery/optimized/colors/530-sea-salt-silver-side.jpg", "/images/gallery/optimized/colors/530-sea-salt-silver-side.jpg", false)),
            CreateProduct(7, "Balenciaga", "3XL", 89990m, false,
                CreateVariant(7, "White / Grey", "#e8e8e8", "/images/products/3xl.jpg", "/images/products/thumbs/3xl.webp", true),
                CreateVariant(35, "Space Grey / Black", "#34363a", "/images/gallery/optimized/colors/3xl-space-grey-side.jpg", "/images/gallery/optimized/colors/3xl-space-grey-side.jpg", false)),
            CreateProduct(9, "Balenciaga", "Triple S", 79990m, false,
                CreateVariant(9, "Off White", "#e8e8e8", "/images/products/triple-s.jpg", "/images/products/thumbs/triple-s.webp", true)),
            CreateProduct(11, "Nike", "Dunk Low", 17990m, false,
                CreateVariant(11, "Panda", "#e8e8e8", "/images/products/dunk-low.jpg", "/images/products/thumbs/dunk-low.webp", true),
                CreateVariant(27, "University Red / White", "#d91c2a", "/images/gallery/optimized/colors/dunk-low-university-red-side.jpg", "/images/gallery/optimized/colors/dunk-low-university-red-side.jpg", false)),
            CreateProduct(13, "Nike", "Air Force 1", 13990m, false,
                CreateVariant(13, "White", "#e8e8e8", "/images/products/air-force-1.jpg", "/images/products/thumbs/air-force-1.webp", true),
                CreateVariant(33, "Triple Black", "#141414", "/images/gallery/optimized/colors/air-force-1-triple-black-side.jpg", "/images/gallery/optimized/colors/air-force-1-triple-black-side.jpg", false)),
            CreateProduct(15, "Adidas", "Samba OG", 14990m, false,
                CreateVariant(15, "Cloud White", "#e8e8e8", "/images/products/samba-og.jpg", "/images/products/thumbs/samba-og.webp", true),
                CreateVariant(28, "Core Black / Cloud White", "#161616", "/images/gallery/optimized/colors/samba-og-core-black-side.jpg", "/images/gallery/optimized/colors/samba-og-core-black-side.jpg", false)),
            CreateProduct(18, "Adidas", "Yeezy Foam Runner", 19990m, false,
                CreateVariant(18, "Onyx", "#e8e8e8", "/images/products/yeezy-foam-runner.jpg", "/images/products/yeezy-foam-runner.jpg", true)),
            CreateProduct(23, "Jordan", "Air Jordan 4", 39990m, false,
                CreateVariant(23, "Military Black", "#e8e8e8", "/images/products/air-jordan-4.jpg", "/images/products/thumbs/air-jordan-4.webp", true))
        ];

        return new HomePageViewModel
        {
            Products = products,
            Brands = products.Select(product => product.BrandName).Distinct().Order().ToList()
        };
    }

    private static ProductCardViewModel CreateProduct(
        int id, string brandName, string name, decimal price, bool isPopular,
        params ProductCardVariantViewModel[] variants) => new()
    {
        Id = id,
        BrandName = brandName,
        Name = name,
        Price = price,
        // Сохраняем прежний состав подборок: первые модели — хиты, остальные — новинки.
        IsNew = !isPopular,
        IsPopular = isPopular,
        Variants = [.. variants]
    };

    private static ProductCardVariantViewModel CreateVariant(
        int id, string name, string swatch, string imageUrl, string? thumbnailUrl, bool isDefault) => new()
    {
        Id = id,
        Name = name,
        Swatch = swatch,
        ImageUrl = imageUrl,
        ThumbnailUrl = thumbnailUrl,
        IsDefault = isDefault
    };
}
