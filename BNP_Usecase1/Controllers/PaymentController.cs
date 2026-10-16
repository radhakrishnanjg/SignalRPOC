using BNB.UsecaseEntities;
using BNB.UsecaseServices.Interfaces;
using BNP_Usecase1.Messaging;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace BNP_Usecase1.Controllers
{
    /// <summary>
    /// API endpoints to publish test payments to Kafka and to read payment reports.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController(IPaymentBusiness business, PaymentProducer producer) : ControllerBase
    {
        // Payments added from the UI arrive through PaymentHub.AddPayment (SignalR), not a REST endpoint.

        /// <summary>
        /// Test producer: puts records on the Kafka topic, as a source system would. The consumer saves them.
        /// </summary>
        [HttpPost("publish")]
        public async Task<IActionResult> Publish([FromBody] List<PaymentEntity> payments)
        {
            if (payments == null || payments.Count == 0) return BadRequest("At least one record is required.");
            await producer.PublishAsync(payments);
            return Accepted();
        }

        /// <summary>
        /// PnL report by status: GET api/payment/valid or api/payment/invalid.
        /// </summary>
        [HttpGet("{status}")]
        public ActionResult<List<PaymentDTO>> GetByStatus(string status)
        {
            return Ok(business.GetByStatus(status));
        }

        /// <summary>
        /// Returns all stored payments: GET api/payment.
        /// </summary>
        [HttpGet]
        public ActionResult<List<PaymentDTO>> GetAll()
        {
            return Ok(business.GetAll());
        }
    }
}
