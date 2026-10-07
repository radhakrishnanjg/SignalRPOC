using BNB.UsecaseEntities;
using BNB.UsecaseServices.Interfaces;
using BNP_Usecase1.Hubs;
using BNP_Usecase1.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace BNP_Usecase1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController(IPaymentBusiness business, PaymentNotifier notifier, PaymentProducer producer) : ControllerBase
    {
        ///// <summary>Start-of-day flat file feed (CSV: SourceSystem,AccountNumber,PnLAmount).</summary>
        //[HttpPost("feed-file")]
        //public ActionResult<FeedResult> UploadFeedFile(IFormFile file)
        //{
        //    if (file == null || file.Length == 0) return BadRequest("A non-empty file is required.");
        //    using var reader = new StreamReader(file.OpenReadStream());
        //    return Ok(business.ProcessFeedFile(reader, file.FileName));
        //}

        /// <summary>Real-time messages from a source system.</summary>
        [HttpPost("realtime")]
        public async Task<ActionResult<FeedResult>> Realtime([FromBody] List<PaymentDTO> payments)
        {
            if (payments == null || payments.Count == 0) return BadRequest("At least one record is required.");
            foreach (var p in payments) p.SourceType = PaymentConstants.Realtime;
            var result = business.InsertBatch(payments);
            await notifier.PublishAsync(business);
            return Ok(result);
        }

        /// <summary>Test producer: puts records on the Kafka topic, as a source system would. The consumer saves them.</summary>
        [HttpPost("publish")]
        public async Task<IActionResult> Publish([FromBody] List<PaymentDTO> payments)
        {
            if (payments == null || payments.Count == 0) return BadRequest("At least one record is required.");
            await producer.PublishAsync(payments);
            return Accepted();
        }

        /// <summary>PnL report by status: GET api/payment/valid or api/payment/invalid.</summary>
        [HttpGet("{status}")]
        public ActionResult<List<PaymentDTO>> GetByStatus(string status)
        {
            return Ok(business.GetByStatus(status));
        }

        [HttpGet]
        public ActionResult<List<PaymentDTO>> GetAll()
        {
            return Ok(business.GetAll());
        }
    }
}
