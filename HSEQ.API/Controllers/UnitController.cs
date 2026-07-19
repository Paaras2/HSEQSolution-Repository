using HSEQ.Domain;
using HSEQ.Domain.Entities;
using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.API.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HSEQ.Domain.Common;
/*
namespace HSEQ.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UnitController : ControllerBase
    {
        private readonly IUnitService _unitService;
        public UnitController(IUnitService unitService)
        {
            _unitService = unitService;
        }

        [HttpPost]
        [Route("Add")]
        public IActionResult Add(string title)
        {
      
            _unitService.AddUnit(title);
            return Ok();

        }
    }
}
*/

namespace HSEQ.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UnitController : ControllerBase
    {

        private readonly IUnitService _unitService;
        private readonly IUnitOfWork _unitOfWork;

        public UnitController(IUnitService unitService, IUnitOfWork unitOfWork)
        {
            _unitService = unitService;
            _unitOfWork = unitOfWork;
        }

        [HttpPost]
        [Route("Add")]
        public async Task<IActionResult> Add([FromBody] CreateUnitRequestModel request)
        {
            await _unitService.AddAsync(request);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        [Route("Update")]
        public async Task<IActionResult> Update([FromBody] UpdateUnitRequestModel request)
        {
            await _unitService.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        [Route("Delete")]
        public async Task<IActionResult> Delete(Guid unitId)
        {
            await _unitService.DeleteAsync(unitId);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        [Route("Get")]
        public async Task<UnitDto> Get(Guid unitId)
        {
            return await _unitService.GetByIdAsync(unitId);
        }

        [HttpGet]
        [Route("GetAll")]
        public async Task<List<UnitDto>> GetAll([FromQuery] bool includeInactiveItems = false)
        {
            return await _unitService.GetAllAsync(includeInactiveItems);
        }


    }
}

