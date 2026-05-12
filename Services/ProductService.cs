using AutoMapper;
using DTOs;
using Entities;
using Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _iProductRepository;
        private readonly IOrderRepository _iOrderRepository;
        private readonly IMapper _mapper;
        private readonly ICacheService _cacheService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProductService> _logger;

        public ProductService(
            IProductRepository iProductRepository,
            IOrderRepository iOrderRepository,
            IMapper mapper,
            ICacheService cacheService,
            IConfiguration configuration,
            ILogger<ProductService> logger)
        {
            this._iProductRepository = iProductRepository;
            this._iOrderRepository = iOrderRepository;
            this._mapper = mapper;
            this._cacheService = cacheService;
            this._configuration = configuration;
            this._logger = logger;
        }

        public async Task<PageResponseDTO<ProductSummaryDTO>> GetProducts(int?[] categoryIds, string? title, string? city, decimal? minPrice, decimal? maxPrice, int? rooms, int? beds, int position, int skip)
        {
            if (skip <= 0) skip = 10;
            if (position <= 0) position = 1;
            (List<Product>, int) response = await _iProductRepository.GetProducts(categoryIds, title, city, minPrice, maxPrice, rooms, beds, position, skip);
            response.Item1 = response.Item1.Where(p => p.IsAvailable == true).ToList();

            List<ProductSummaryDTO> data = _mapper.Map<List<Product>, List<ProductSummaryDTO>>(response.Item1);
            
            //if (data.Count > 0)
            //{
            //    Console.WriteLine($"First product: {data[0].Title}, TransactionType: {data[0].TransactionType}");
            //    Console.WriteLine($"Source product TransactionType: {response.Item1[0].TransactionType}");
            //}
            PageResponseDTO<ProductSummaryDTO> pageResponse = new();
            pageResponse.Data = data;
            pageResponse.TotalItems = response.Item2; 
            pageResponse.CurrentPage = position;
            pageResponse.PageSize = skip;
            pageResponse.HasPreviousPage = position > 1;
            int numOfPages = pageResponse.TotalItems / skip;
            if (pageResponse.TotalItems % skip != 0)
                numOfPages++;
            pageResponse.HasNextPage = position < numOfPages;
            return pageResponse;
        }


        public async Task<ProductDetailsDTO> GetProductById(int id)
        {
            string cacheKey = $"product:{id}";
            
            // Try to get from cache first
            var cachedProduct = await _cacheService.GetAsync<ProductDetailsDTO>(cacheKey);
            if (cachedProduct != null)
            {
                return cachedProduct;
            }

            // Cache miss - fetch from database
            Product product = await _iProductRepository.GetProductById(id);
            if (product == null || product.IsAvailable == false)
            {
                return null;
            }

            var result = _mapper.Map<Product, ProductDetailsDTO>(product);
            
            // Store in cache with TTL from configuration
            var productTtl = _configuration.GetValue("Redis:CacheTTL:ProductSeconds", 600);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromSeconds(productTtl));
            
            return result;
        }


        public async Task<List<ProductSummaryDTO>> GetProductsByOwnerId(int ownerId)
            {
                List<Product> ownerProducts = await _iProductRepository.GetProductsByOwnerId(ownerId);
                ownerProducts = ownerProducts.Where(p => p.IsAvailable == true).ToList();
                return _mapper.Map<List<Product>, List<ProductSummaryDTO>>(ownerProducts);
            }
        

        public async Task<ProductDetailsDTO> AddProduct(ProductCreateDTO productCreateDto)
        {
            Product product = _mapper.Map<ProductCreateDTO, Product>(productCreateDto);
            Product newProduct = await _iProductRepository.AddProduct(product);
            return _mapper.Map<Product, ProductDetailsDTO>(newProduct);
        }

 
        public async Task<ProductDetailsDTO> UpdateProduct(int id, ProductUpdateDTO productUpdateDto)
        {
            Product existingProduct = await _iProductRepository.GetProductById(id);
            if (existingProduct == null)
            {
                return null;
            }

            if (productUpdateDto.Title != null) existingProduct.Title = productUpdateDto.Title;
            if (productUpdateDto.Description != null) existingProduct.Description = productUpdateDto.Description;
            if (productUpdateDto.Price.HasValue) existingProduct.Price = productUpdateDto.Price.Value;
            if (productUpdateDto.City != null) existingProduct.City = productUpdateDto.City;
            if (productUpdateDto.CategoryId.HasValue) existingProduct.CategoryId = productUpdateDto.CategoryId.Value;
            if (productUpdateDto.TransactionType != null) existingProduct.TransactionType = productUpdateDto.TransactionType;
            if (productUpdateDto.Rooms.HasValue) existingProduct.Rooms = productUpdateDto.Rooms;
            if (productUpdateDto.Beds.HasValue) existingProduct.Beds = productUpdateDto.Beds;
            if (productUpdateDto.IsAvailable.HasValue) existingProduct.IsAvailable = productUpdateDto.IsAvailable.Value;
            if (productUpdateDto.ImageUrl != null) existingProduct.ImageUrl = productUpdateDto.ImageUrl;

            Product updatedProduct = await _iProductRepository.UpdateProduct(id, existingProduct);
            
            // Invalidate cache for this product
            string cacheKey = $"product:{id}";
            await _cacheService.RemoveAsync(cacheKey);
            
            return _mapper.Map<Product, ProductDetailsDTO>(updatedProduct);
        }

        public async Task<bool> DeleteProduct(int id)
        {
            bool result = await _iProductRepository.DeleteProduct(id);
            
            // Invalidate cache for this product
            if (result)
            {
                string cacheKey = $"product:{id}";
                await _cacheService.RemoveAsync(cacheKey);
            }
            
            return result;
        }

        public async Task<bool> CheckAvailability(int productId, DateTime? start, DateTime? end)
        {
            Product product = await _iProductRepository.GetProductById(productId);

            if (product == null || product.IsAvailable != true)
                return false;

            if (product.TransactionType == "Sale" || product.TransactionType == "מכירה")
                return false;

            if (!start.HasValue || !end.HasValue)
                return false;

            DateTime startDate = start.Value.Date;
            DateTime endDate = end.Value.Date;

            if (startDate >= endDate)
                return false;

            if (startDate < DateTime.UtcNow.Date)
                return false;

            List<OrderItem> existingOrders =
                await _iOrderRepository.GetOrderItemsByProductId(productId);

            foreach (OrderItem oi in existingOrders)
            {
                if (!oi.StartDate.HasValue || !oi.EndDate.HasValue)
                    continue;

                DateTime existingStart = oi.StartDate.Value.Date;
                DateTime existingEnd = oi.EndDate.Value.Date;

                if (startDate < existingEnd && endDate > existingStart)
                    return false;
            }

            return true;
        }

        public async Task<List<ProductSummaryDTO>> SearchProducts(string query)
        {
            List<Product> products = await _iProductRepository.SearchProducts(query);
            products = products.Where(p => p.IsAvailable == true).ToList();
            return _mapper.Map<List<Product>, List<ProductSummaryDTO>>(products);
        }

        public async Task<List<ProductSummaryDTO>> GetFeaturedProducts(int count = 5)
        {
            List<Product> allProducts = await _iProductRepository.GetFeaturedProducts(count);
            List<ProductSummaryDTO> featuredProducts = _mapper.Map<List<Product>, List<ProductSummaryDTO>>(allProducts);
            return featuredProducts;
        }

    }
}