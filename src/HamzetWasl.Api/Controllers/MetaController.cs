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
    public ActionResult<ApiResponse<IReadOnlyList<AccountTypeDto>>> GetAccountTypes() =>
        Ok(Envelope(_meta.GetAccountTypes()));

    [HttpGet("governorates")]
    public ActionResult<ApiResponse<IReadOnlyList<GovernorateDto>>> GetGovernorates() =>
        Ok(Envelope(_meta.GetGovernorates()));

    [HttpGet("governorates/{governorateKey}/areas")]
    public ActionResult<ApiResponse<IReadOnlyList<AreaDto>>> GetAreas(string governorateKey)
    {
        var governorate = HamzetWasl.Application.Metadata.MetaCatalog.FindGovernorate(governorateKey);
        return governorate is null
            ? throw HamzetWasl.Application.Common.AppException.NotFound("المحافظة غير موجودة.")
            : Ok(Envelope(governorate.Areas));
    }

    [HttpGet("business-fields")]
    public ActionResult<ApiResponse<IReadOnlyList<BusinessFieldDto>>> GetBusinessFields() =>
        Ok(Envelope(_meta.GetBusinessFields()));

    [HttpGet("employee-ranges")]
    public ActionResult<ApiResponse<IReadOnlyList<EmployeeRangeDto>>> GetEmployeeRanges() =>
        Ok(Envelope(_meta.GetEmployeeRanges()));

    [HttpGet("skills")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LookupDto>>>> GetSkills(CancellationToken ct) =>
        Ok(Envelope(await _meta.GetSkillsAsync(ct)));

    [HttpGet("interests")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LookupDto>>>> GetInterests(CancellationToken ct) =>
        Ok(Envelope(await _meta.GetInterestsAsync(ct)));

    [HttpGet("need-types")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NeedTypeDto>>>> GetNeedTypes(CancellationToken ct) =>
        Ok(Envelope(await _meta.GetNeedTypesAsync(ct)));

    [HttpGet("accommodations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccommodationDto>>>> GetAccommodations(CancellationToken ct) =>
        Ok(Envelope(await _meta.GetAccommodationsAsync(ct)));
}
