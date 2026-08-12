using AutoParts.Api.Contracts;
using AutoParts.Api.Models;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoParts.Api.Controllers;

[ApiController, Route("api/vehicles")]
public sealed class VehiclesController(IVehicleService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<Vehicle>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));
    [HttpGet("{id:int}")] public async Task<ActionResult<Vehicle>> Get(int id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));
    [HttpPost] public async Task<ActionResult<Vehicle>> Create(VehicleRequest request, CancellationToken ct) { var entity = await service.CreateAsync(request, ct); return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity); }
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, VehicleRequest request, CancellationToken ct) { await service.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}
