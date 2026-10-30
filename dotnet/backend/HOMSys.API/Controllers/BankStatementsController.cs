using System.Security.Claims;
using HOMSys.Application.DTOs.Collections;
using HOMSys.Infrastructure.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>
/// Bank accounts (Finance Manager) + daily statement uploads (Finance Manager or Verifier). Verifiers read the lines to check deposits;
/// every collection user can read "as of" and their branch's deposit-account picklist.
/// </summary>
[ApiController]
[Route("api/bank-statements")]
[Authorize(Policy = "collections-any")]
public class BankStatementsController(BankStatementService bank) : ControllerBase
{
    private string Me => User.FindFirstValue(ClaimTypes.Name) ?? "";

    private IActionResult Result<T>((T? data, string? error) r) =>
        r.error is null ? Ok(new { success = true, data = r.data }) : BadRequest(new { success = false, message = r.error });

    [HttpGet("as-of")]
    public async Task<IActionResult> AsOf() => Ok(new { success = true, data = await bank.AsOfAsync() });

    /// <summary>Accounts a branch deposits into (custodian picklist). Branch access is enforced when the slip is saved.</summary>
    [HttpGet("deposit-accounts")]
    public async Task<IActionResult> DepositAccounts([FromQuery] int siteId) => Ok(new { success = true, data = await bank.DepositOptionsAsync(siteId) });

    [HttpGet("accounts"), Authorize(Policy = "collections-finance")]
    public async Task<IActionResult> Accounts() => Ok(new { success = true, data = await bank.ListAccountsAsync() });

    [HttpPost("accounts"), Authorize(Policy = "collections-manage")]
    public async Task<IActionResult> CreateAccount([FromBody] SaveBankAccountDto dto) => Result(await bank.SaveAccountAsync(null, dto, Me));

    [HttpPut("accounts/{id:int}"), Authorize(Policy = "collections-manage")]
    public async Task<IActionResult> UpdateAccount(int id, [FromBody] SaveBankAccountDto dto) => Result(await bank.SaveAccountAsync(id, dto, Me));

    /// <summary>Only an account with no deposits and no statement uploads; a BANKS4W one stays deleted on the next sync.</summary>
    [HttpDelete("accounts/{id:int}"), Authorize(Policy = "collections-manage")]
    public async Task<IActionResult> DeleteAccount(int id) => Result(await bank.DeleteAccountAsync(id, Me));

    /// <summary>Step 1: what the file would import (dates, totals, opening → closing balance). Nothing is saved.</summary>
    [HttpPost("upload/preview"), Authorize(Policy = "collections-finance")]
    [RequestSizeLimit(BankStatementService.MaxFileBytes + 1024 * 1024)]
    public async Task<IActionResult> Preview([FromForm] int bankAccountId, IFormFile? file)
    {
        if (file is null) return BadRequest(new { success = false, message = "Choose the statement file." });
        await using var stream = file.OpenReadStream();
        return Result(await bank.PreviewAsync(bankAccountId, file.FileName, stream, file.Length));
    }

    /// <summary>Step 2: import after the user confirmed the balance (confirmedBalance = the previewed closing balance).</summary>
    [HttpPost("upload"), Authorize(Policy = "collections-finance")]
    [RequestSizeLimit(BankStatementService.MaxFileBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] int bankAccountId, IFormFile? file, [FromForm] bool confirmed, [FromForm] decimal? confirmedBalance)
    {
        if (file is null) return BadRequest(new { success = false, message = "Choose the statement file." });
        await using var stream = file.OpenReadStream();
        return Result(await bank.UploadAsync(bankAccountId, file.FileName, stream, file.Length, confirmed, confirmedBalance, Me));
    }

    [HttpGet("uploads"), Authorize(Policy = "collections-finance")]
    public async Task<IActionResult> Uploads([FromQuery] int? bankAccountId) => Ok(new { success = true, data = await bank.ListUploadsAsync(bankAccountId) });

    [HttpGet("lines"), Authorize(Policy = "collections-finance")]
    public async Task<IActionResult> Lines([FromQuery] int? bankAccountId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? filter) =>
        Ok(new { success = true, data = await bank.ListLinesAsync(bankAccountId, from, to, filter) });

    [HttpGet("lines/{id:int}/candidates"), Authorize(Policy = "collections-manage")]
    public async Task<IActionResult> Candidates(int id) => Result(await bank.CandidatesAsync(id));

    [HttpPost("lines/{id:int}/match"), Authorize(Policy = "collections-manage")]
    public async Task<IActionResult> Match(int id, [FromBody] BankMatchDto dto) => Result(await bank.MatchAsync(id, dto.DepositBatchId, Me));

    [HttpPost("lines/{id:int}/unmatch"), Authorize(Policy = "collections-manage")]
    public async Task<IActionResult> Unmatch(int id) => Result(await bank.UnmatchAsync(id, Me));
}
