using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Meta;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[AllowAnonymous]
[Route("api/v1/meta")]
public sealed class MetaController : ApiControllerBase
{
    private readonly IMetaService _meta;

    public MetaController(IMetaService meta) => _meta = meta;

    [HttpGet("account-types")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AccountTypeDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<IReadOnlyList<AccountTypeDto>>> GetAccountTypes() =>
        Ok(Envelope(_meta.GetAccountTypes()));

    [HttpGet("governorates")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GovernorateDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GovernorateDto>>>> GetGovernorates(CancellationToken ct) =>
        Ok(Envelope(await _meta.GetGovernoratesAsync(ct)));

    [HttpGet("governorates/{governorateKey}/areas")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AreaDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AreaDto>>>> GetAreas(
        string governorateKey, CancellationToken ct) =>
        Ok(Envelope(await _meta.GetAreasAsync(governorateKey, ct)));

    [HttpGet("business-fields")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BusinessFieldDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<IReadOnlyList<BusinessFieldDto>>> GetBusinessFields() =>
        Ok(Envelope(_meta.GetBusinessFields()));

    [HttpGet("employee-ranges")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeRangeDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<IReadOnlyList<EmployeeRangeDto>>> GetEmployeeRanges() =>
        Ok(Envelope(_meta.GetEmployeeRanges()));

    [HttpGet("skills")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LookupDto>>>> GetSkills(CancellationToken ct) =>
        Ok(Envelope(await _meta.GetSkillsAsync(ct)));

    [HttpGet("interests")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LookupDto>>>> GetInterests(CancellationToken ct) =>
        Ok(Envelope(await _meta.GetInterestsAsync(ct)));

    [HttpGet("need-types")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NeedTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NeedTypeDto>>>> GetNeedTypes(CancellationToken ct) =>
        Ok(Envelope(await _meta.GetNeedTypesAsync(ct)));

    [HttpGet("accommodations")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AccommodationDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccommodationDto>>>> GetAccommodations(CancellationToken ct) =>
        Ok(Envelope(await _meta.GetAccommodationsAsync(ct)));
}
