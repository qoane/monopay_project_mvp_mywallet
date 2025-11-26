using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MonoPayAggregator.Models;
using System;

namespace MonoPayAggregator.Controllers
{
    [ApiController]
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
        [HttpPost("v1/payments")]
        public async Task<ActionResult<PaymentResponse>> CreatePayment([FromBody] PaymentRequest request)
        {
            try
            {
                var response = await _aggregator.CreatePaymentAsync(request);
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
        [HttpGet("v1/payments/{id}")]
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
        [HttpGet("v1/payments")]
        public ActionResult<IEnumerable<PaymentResponse>> GetAllPayments()
        {
            // Access the internal list via reflection; the aggregator exposes
            // no public API for this in the current implementation. In a real
            // implementation you would query a database instead.
            var field = typeof(MonoPayAggregator.Services.PaymentAggregator)
                .GetField("_allPayments", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var list = field?.GetValue(_aggregator) as IEnumerable<PaymentResponse>;
            return Ok(list ?? Array.Empty<PaymentResponse>());
        }

        /// <summary>
        /// Return a list of supported wallets and banking rails.
        /// </summary>
        [HttpGet("v1/wallets")]
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
        [HttpGet("v1/wallets/{method}/balance")]
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