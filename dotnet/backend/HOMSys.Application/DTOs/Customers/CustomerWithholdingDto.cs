namespace HOMSys.Application.DTOs.Customers;

public record CustomerWithholdingDto(string CustKey, bool Withholding, int RowsUpdated);
