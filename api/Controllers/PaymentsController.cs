using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using MonoPayAggregator.Models;
using System;

namespace MonoPayAggregator.Controllers
{
    [ApiController]
    [Route("v1")]
    [Route("api/v1")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly MonoPayAggregator.Services.PaymentAggregator _aggregator;

        public PaymentsController(MonoPayAggregator.Services.PaymentAggregator aggregator)
        {
            _aggregator = aggregator;
        }

        /// <summary>
        /// Create a new payment request by forwarding it to the appropriate
        /// provider via the aggregator.
        /// </summary>
        /// <param name="request">Payment request body</param>
        [HttpPost("payments")]
        public async Task<ActionResult<PaymentResponse>> CreatePayment([FromBody] PaymentRequest request)
        {
            try
            {
                var response = await _aggregator.CreatePaymentAsync(request);
                if (!string.Equals(response.Status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(StatusCodes.Status502BadGateway, response);
                }
                return Created($"/v1/payments/{response.Id}", response);
            }
            catch (KeyNotFoundException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieve the status of a previously created payment. We iterate
        /// through providers via the aggregator until we find the payment.
        /// </summary>
        /// <param name="id">Payment identifier</param>
        [HttpGet("payments/{id}")]
        public async Task<ActionResult<PaymentResponse>> GetPayment(string id)
        {
            var payment = await _aggregator.GetPaymentAsync(id);
            if (payment == null)
            {
                return NotFound();
            }
            return Ok(payment);
        }

        /// <summary>
        /// Retrieve all payments created through the aggregator. Requires
        /// authentication. This can be used by merchants to view their
        /// transaction history. In a production system you would filter
        /// by the authenticated user's merchant ID and page the results.
        /// </summary>
        [HttpGet("payments")]
        public ActionResult<IEnumerable<PaymentResponse>> GetAllPayments()
        {
            // Use the aggregator's API to obtain a snapshot of all payments.
            // In a real implementation this data would come from persistent storage.
            return Ok(_aggregator.GetAllPayments());
        }

        /// <summary>
        /// Return a list of supported wallets and banking rails.
        /// </summary>
        [HttpGet("wallets")]
        public ActionResult<IEnumerable<object>> GetWallets()
        {
            return Ok(_aggregator.ListWallets());
        }

        /// <summary>
        /// Retrieve the balance for a specific account on a given wallet. Not all
        /// providers support this operation; unsupported methods will return null.
        /// </summary>
        /// <param name="method">Wallet or payment method code (e.g. mpesa, ecocash)</param>
        /// <param name="accountId">The account or wallet identifier</param>
        [HttpGet("wallets/{method}/balance")]
        public async Task<ActionResult<object?>> GetBalance(string method, [FromQuery] string accountId)
        {
            var balance = await _aggregator.GetBalanceAsync(method, accountId);
            if (balance == null)
            {
                return NotFound();
            }
            return Ok(new { method, accountId, balance });
        }
    }
}