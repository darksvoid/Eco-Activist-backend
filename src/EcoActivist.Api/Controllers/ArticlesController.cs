using EcoActivist.Application.Articles;
using EcoActivist.Application.Common.Models;
using EcoActivist.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class ArticlesController : ControllerBase
{
    private readonly ArticleService _articles;

    public ArticlesController(ArticleService articles)
    {
        _articles = articles;
    }

    [HttpGet("articles")]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<ArticleSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ArticleSummaryDto>>> GetPublished([FromQuery] ArticleListQuery query, CancellationToken ct)
    {
        return Ok(await _articles.GetPublishedAsync(query, ct));
    }

    [HttpGet("articles/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArticleDto>> GetPublishedById(Guid id, CancellationToken ct)
    {
        return Ok(await _articles.GetPublishedByIdAsync(id, ct));
    }

    [HttpGet("admin/articles")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType<PagedResult<ArticleSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ArticleSummaryDto>>> GetAll([FromQuery] ArticleListQuery query, CancellationToken ct)
    {
        return Ok(await _articles.GetAllAsync(query, ct));
    }

    [HttpGet("admin/articles/{id:guid}", Name = "AdminGetArticle")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArticleDto>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await _articles.GetByIdAsync(id, ct));
    }

    [HttpPost("admin/articles")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ArticleDto>> Create([FromBody] UpsertArticleRequest request, CancellationToken ct)
    {
        var article = await _articles.CreateAsync(request, ct);
        return CreatedAtRoute("AdminGetArticle", new { id = article.Id }, article);
    }

    [HttpPut("admin/articles/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArticleDto>> Update(Guid id, [FromBody] UpsertArticleRequest request, CancellationToken ct)
    {
        return Ok(await _articles.UpdateAsync(id, request, ct));
    }

    [HttpDelete("admin/articles/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _articles.DeleteAsync(id, ct);
        return NoContent();
    }
}
