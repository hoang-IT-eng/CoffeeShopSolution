using System.Net;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeShop.Controllers
{
    /// <summary>
    /// In-process simulated checkout page for MobileSim (no MoMo/VNPay).
    /// Open on localhost or via optional ngrok tunnel on a phone.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("pay/sim")]
    [IgnoreAntiforgeryToken]
    public class PaySimController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaySimController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpGet("{paymentId:int}")]
        public async Task<IActionResult> Page(int paymentId)
        {
            var payment = await _paymentService.GetPaymentWithDetailsAsync(paymentId);
            if (payment == null)
            {
                return Content(ErrorHtml("Không tìm thấy giao dịch."), "text/html; charset=utf-8");
            }

            var status = IPaymentService.CanonicalStatus(payment.Status);
            var amount = payment.Amount.ToString("N0");
            var orderId = payment.OrderId;
            var table = WebUtility.HtmlEncode(payment.Order?.Table?.Name ?? "—");
            var customer = WebUtility.HtmlEncode(payment.CustomerInfo ?? "Khách");
            var tx = WebUtility.HtmlEncode(payment.TransactionId ?? "");

            var actionHtml = status == "Pending"
                ? $"""
                   <form method="post" action="/pay/sim/{paymentId}/success" class="actions">
                     <button type="submit" class="ok">Giả lập thành công</button>
                   </form>
                   <form method="post" action="/pay/sim/{paymentId}/fail" class="actions">
                     <button type="submit" class="bad">Giả lập thất bại</button>
                   </form>
                   """
                : $"""
                   <p class="done">Giao dịch đã kết thúc: <strong>{WebUtility.HtmlEncode(status)}</strong></p>
                   <p class="hint">Poll API: <code>GET /api/payments/{paymentId}</code></p>
                   """;

            var html = $$"""
                <!DOCTYPE html>
                <html lang="vi">
                <head>
                  <meta charset="utf-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1" />
                  <title>An Roastery — Giả lập thanh toán #{{paymentId}}</title>
                  <style>
                    :root { --ink:#2b1d14; --cream:#f5ecdf; --caramel:#b5835a; --ok:#2f6b3a; --bad:#8b2e2e; }
                    * { box-sizing: border-box; }
                    body { margin:0; min-height:100vh; font-family: "Segoe UI", system-ui, sans-serif;
                      background: radial-gradient(circle at top, #fff8ef, var(--cream)); color: var(--ink);
                      display:flex; align-items:center; justify-content:center; padding:24px; }
                    .card { width:min(420px,100%); background:#fffdf8; border:1px solid #e6d7c4;
                      padding:28px 24px; box-shadow: 0 18px 40px rgba(43,29,20,.08); }
                    h1 { font-family: Georgia, "Times New Roman", serif; font-weight:600; font-size:1.55rem;
                      margin:0 0 4px; letter-spacing:.02em; }
                    .brand { color: var(--caramel); font-size:.8rem; text-transform:uppercase; letter-spacing:.14em; margin-bottom:12px; }
                    .row { display:flex; justify-content:space-between; gap:12px; padding:8px 0; border-bottom:1px dashed #e4d6c5; font-size:.95rem; }
                    .amount { font-size:1.8rem; font-weight:700; margin:18px 0 8px; }
                    .qr { margin:16px auto; width:160px; height:160px; background:
                      repeating-linear-gradient(45deg,#2b1d14 0 6px,#f5ecdf 6px 12px);
                      display:flex; align-items:center; justify-content:center; color:#f5ecdf; font-size:.75rem; text-align:center; padding:8px; }
                    .actions { margin-top:14px; }
                    button { width:100%; border:0; padding:14px 16px; font-size:1rem; cursor:pointer; }
                    .ok { background: var(--ok); color:#fff; }
                    .bad { background: var(--bad); color:#fff; margin-top:8px; }
                    .done { margin-top:18px; padding:12px; background:#f0e6d8; }
                    .hint { font-size:.85rem; color:#6b5646; }
                    code { font-size:.8rem; }
                  </style>
                </head>
                <body>
                  <main class="card">
                    <div class="brand">An Roastery · MobileSim</div>
                    <h1>Thanh toán giả lập</h1>
                    <p class="hint">Không gọi cổng thật. Dùng cho admin / demo / điện thoại qua ngrok.</p>
                    <div class="amount">{{amount}} ₫</div>
                    <div class="row"><span>Mã GD</span><strong>#{{paymentId}}</strong></div>
                    <div class="row"><span>Đơn</span><strong>#HD-{{orderId}}</strong></div>
                    <div class="row"><span>Bàn</span><strong>{{table}}</strong></div>
                    <div class="row"><span>Khách</span><strong>{{customer}}</strong></div>
                    <div class="row"><span>TX</span><strong>{{tx}}</strong></div>
                    <div class="row"><span>Trạng thái</span><strong>{{WebUtility.HtmlEncode(status)}}</strong></div>
                    <div class="qr">QR giả lập<br/>MobileSim</div>
                    {{actionHtml}}
                  </main>
                </body>
                </html>
                """;

            return Content(html, "text/html; charset=utf-8");
        }

        [HttpPost("{paymentId:int}/success")]
        public Task<IActionResult> Success(int paymentId) => Finish(paymentId, true);

        [HttpPost("{paymentId:int}/fail")]
        public Task<IActionResult> Fail(int paymentId) => Finish(paymentId, false);

        private async Task<IActionResult> Finish(int paymentId, bool success)
        {
            try
            {
                await _paymentService.CompleteSimulatedPaymentAsync(paymentId, success, "pay-sim-page");
            }
            catch (InvalidOperationException ex)
            {
                return Content(ErrorHtml(ex.Message), "text/html; charset=utf-8");
            }

            return Redirect($"/pay/sim/{paymentId}");
        }

        private static string ErrorHtml(string message)
        {
            var safe = WebUtility.HtmlEncode(message);
            return $"""
                <!DOCTYPE html><html lang="vi"><head><meta charset="utf-8"/><title>Lỗi giả lập</title></head>
                <body style="font-family:system-ui;background:#f5ecdf;color:#2b1d14;padding:40px">
                <h1>Không xử lý được</h1><p>{safe}</p>
                <p><a href="/">Về API</a></p></body></html>
                """;
        }
    }
}
