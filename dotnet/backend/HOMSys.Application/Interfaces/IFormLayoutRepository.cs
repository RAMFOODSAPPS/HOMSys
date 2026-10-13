using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface IFormLayoutRepository
{
    Task<FormLayout?> GetAsync(string formKey);
    Task UpsertAsync(string formKey, string layoutJson, string updatedBy);
    Task DeleteAsync(string formKey);
}
