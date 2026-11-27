BEGIN;
INSERT INTO "Brands" ("Name","Description") VALUES ('New Balance',''),('Nike','');
INSERT INTO "Categories" ("Name") VALUES ('Кроссовки');
INSERT INTO "Products" ("BrandId","CategoryId","Name","Description","Price","ImageUrl","Status","IsNew","IsPopular","CreatedAt","UpdatedAt")
VALUES (1,1,'New Balance 9060','Тестовая карточка',12000,'/images/products/9060.jpg','Active',true,true,now(),now()),
(2,1,'Nike Dunk Low','Тестовая карточка',9900,'/images/products/air-force-1.jpg','Active',true,true,now(),now()),
(1,1,'New Balance 530','Тестовая карточка',8500,'/images/products/530.jpg','Active',true,false,now(),now());
INSERT INTO "ProductVariants" ("ProductId","Name","Sku","Swatch","ImageUrl","IsDefault")
VALUES (1,'Серый','DEMO-A','#aaa','/images/products/9060.jpg',true),
(1,'Белый','DEMO-B','#eee','/images/products/530.jpg',false),
(2,'Белый','DEMO-C','#eee','/images/products/air-force-1.jpg',true),
(3,'Серый','DEMO-D','#aaa','/images/products/530.jpg',true);
INSERT INTO "ProductVariantSizes" ("ProductVariantId","Size","StockQuantity") SELECT "Id", size, 30 FROM "ProductVariants" CROSS JOIN generate_series(37,44) size;
INSERT INTO "ProductVariantImages" ("ProductVariantId","ImageUrl","SortOrder")
VALUES (1,'/images/products/9060.jpg',1),(1,'/images/products/1906r.jpg',2),(2,'/images/products/530.jpg',1),(2,'/images/products/2002r.jpg',2);
COMMIT;

