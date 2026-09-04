using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface IDocClassRepository
{
    Task<IEnumerable<DocClass>> GetAllAsync();
}
