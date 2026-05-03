using DTOs;
using Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebApiShop.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _iProductService;
        private readonly ILogger<ProductController> _logger;
        private readonly ICategoriesServies _iCategoriesServies;

        public ProductController(IProductService iProductService, ILogger<ProductController> logger, ICategoriesServies iCategoriesServies)
        {
            _iProductService = iProductService;
            _logger = logger;
            _iCategoriesServies = iCategoriesServies;
        }

        // GET: api/<ProductController>
        [HttpGet]
        public async Task<ActionResult<PageResponseDTO<ProductSummaryDTO>>> GetProducts([FromQuery] int?[] categoryIds, string? title, string? city, decimal? minPrice, decimal? maxPrice, int? rooms, int? beds, int position, int skip)
        {
            foreach (int id in categoryIds)
            {
                CategoryDTO category = await _iCategoriesServies.GetCategoryById(id);
                if (category == null)
                {
                    _logger.LogWarning("Category with ID {Id} was not found", id);
                    return BadRequest("Category with ID was not found");
                }
            }
            try
            {
                PageResponseDTO<ProductSummaryDTO> result = await _iProductService.GetProducts(categoryIds, title, city, minPrice, maxPrice, rooms, beds, position, skip);
                _logger.LogInformation("Successfully fetched products. ");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching products. CategoryIds: {CategoryIds}, City: {City}, MinPrice: {MinPrice}, MaxPrice: {MaxPrice}, Rooms: {Rooms}, Beds: {Beds}, Position: {Position}, Skip: {Skip}",
                    categoryIds, city, minPrice, maxPrice, rooms, beds, position, skip);

                return BadRequest(new { Message = "An error occurred while fetching products.", Details = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDetailsDTO>> GetProductById(int id)
        {
            ProductDetailsDTO product = await _iProductService.GetProductById(id);
            if (product == null)
            {
                _logger.LogWarning("Product with ID {Id} was not found", id);
                return NotFound();
            }
            return product;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ProductDetailsDTO>> AddProduct(ProductCreateDTO productCreateDto)
        {
            try
            {
                ProductDetailsDTO newProduct = await _iProductService.AddProduct(productCreateDto);
                if (newProduct == null)
                {
                    _logger.LogWarning("Failed to add product with name {Name}", productCreateDto.Title);
                    return BadRequest();
                }
                _logger.LogInformation("Product added successfully with ID: {Id}", newProduct.ProductId);
                return CreatedAtAction(nameof(GetProductById), new { id = newProduct.ProductId }, newProduct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding product: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message, innerError = ex.InnerException?.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> UpdateProduct(int id, ProductUpdateDTO productUpdateDto)
        {
            ProductDetailsDTO updatedProduct = await _iProductService.UpdateProduct(id, productUpdateDto);
            if (updatedProduct == null)
            {
                _logger.LogWarning("Update failed: Product with ID {Id} not found", id);
                return NotFound();
            }
            _logger.LogInformation("Product with ID {Id} updated successfully", id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> DeleteProduct(int id)
        {
            bool isDeleted = await _iProductService.DeleteProduct(id);
            if (!isDeleted)
            {
                _logger.LogWarning("Delete failed: Product with ID {Id} not found", id);
                return NotFound();
            }
            _logger.LogInformation("Product with ID {Id} was deleted successfully", id);
            return NoContent();
        }

        [HttpGet("owner/{ownerId}")]
        public async Task<ActionResult<List<ProductSummaryDTO>>> GetProductsByOwnerId(int ownerId)
        {
            List<ProductSummaryDTO> ownerProducts = await _iProductService.GetProductsByOwnerId(ownerId);
            return ownerProducts;
        }

        [HttpGet("check-availability")]
        public async Task<ActionResult<bool>> GetAvailability([FromQuery] int productId, [FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            bool isAvailable = await _iProductService.CheckAvailability(productId, start, end);
            return Ok(isAvailable);
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<ProductSummaryDTO>>> SearchProducts([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Ok(new List<ProductSummaryDTO>());

            List<ProductSummaryDTO> results = await _iProductService.SearchProducts(query);
            return Ok(results);
        }

        [HttpGet("featured")]
        public async Task<ActionResult<List<ProductSummaryDTO>>> GetFeaturedProducts([FromQuery] int count = 5)
        {
            try
            {
                List<ProductSummaryDTO> featuredProducts = await _iProductService.GetFeaturedProducts(count);
                _logger.LogInformation("Retrieved {Count} featured products", count);
                return Ok(featuredProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving featured products: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}