using Bite4All.Application.DTOs.Common;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Route("cities")]
public class CitiesController(IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet]
    public ActionResult<List<City>> GetActiveCities(CancellationToken cancellationToken)
    {
        return Ok(unitOfWork.Cities.Query().Where(c => c.IsActive).OrderBy(c => c.Name).ToList());
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost]
    public async Task<ActionResult<City>> Create(CityRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "City name is required." });
        }

        var city = new City
        {
            Name = request.Name,
            IsActive = request.IsActive
        };

        await unitOfWork.Cities.AddAsync(city, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetActiveCities), new { id = city.Id }, city);
    }

    [Authorize(Roles = "Administrator")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CityRequest request, CancellationToken cancellationToken)
    {
        var city = await unitOfWork.Cities.GetByIdAsync(id, cancellationToken);
        if (city is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "City name is required." });
        }

        city.Name = request.Name;
        city.IsActive = request.IsActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deactivates a city. Blocked if there are any approved hospitality partners
    /// or charity organizations still active in that city — deactivating a city
    /// that has active actors would break their matching and visibility.
    /// </summary>
    [Authorize(Roles = "Administrator")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var city = await unitOfWork.Cities.GetByIdAsync(id, cancellationToken);
        if (city is null)
        {
            return NotFound();
        }

        // Check for active hospitality partners in this city
        var hasActivePartners = unitOfWork.HospitalityPartners.Query()
            .Any(p => p.CityId == id && p.ApprovalStatus == ApprovalStatus.Approved);

        if (hasActivePartners)
        {
            return BadRequest(new { message = "Cannot deactivate a city that has approved hospitality partners. Reassign or suspend them first." });
        }

        // Check for active charity organizations in this city
        var hasActiveOrganizations = unitOfWork.CharityOrganizations.Query()
            .Any(o => o.CityId == id && o.ApprovalStatus == ApprovalStatus.Approved);

        if (hasActiveOrganizations)
        {
            return BadRequest(new { message = "Cannot deactivate a city that has approved charity organizations. Reassign or suspend them first." });
        }

        city.IsActive = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
