using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StyleMart.Data;
using StyleMart.Models;
using System.Security.Claims;

namespace StyleMart.Controllers
{
    [Authorize]
    public class ReturnsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }


        private readonly StyleMartContext _db;

        public ReturnsController(StyleMartContext db)
        {
            _db = db;
        }

       // [HttpGet]
       //// public async Task<IActionResult> Request(int orderId)
       // public async Task<IActionResult> CreateReturnRequest(int orderId)
       // {
       //     var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

       //     var order = await _db.Orders
       //         .FirstOrDefaultAsync(o =>
       //             o.Id == orderId && o.UserId == userId);

       //     if (order == null)
       //         return NotFound();

       //     if (order.Status != "Delivered")
       //     {
       //         TempData["Error"] =
       //             "Return / Exchange is available only after delivery.";

       //         return RedirectToAction("MyOrders", "Account");
       //     }

       //     var model = new OrderReturn
       //     {
       //         OrderId = order.Id,
       //         UserId = userId!,
       //         RequestType = "Return",
       //         Status = "Pending",
       //         RefundStatus = "Not Initiated"
       //     };
       //     LoadReasons();
       //     return View(model);
           
       // }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Request(OrderReturn model)
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    if (userId == null)
        //        return Challenge();

        //    var order = await _db.Orders
        //        .FirstOrDefaultAsync(o =>
        //            o.Id == model.OrderId && o.UserId == userId);

        //    if (order == null)
        //        return NotFound();

        //    if (order.Status != "Delivered")
        //        return BadRequest("Order must be delivered before requesting a return.");

        //    ModelState.Remove(nameof(OrderReturn.UserId));
        //    ModelState.Remove(nameof(OrderReturn.Status));
        //    ModelState.Remove(nameof(OrderReturn.RefundStatus));
        //    ModelState.Remove(nameof(OrderReturn.RequestDate));

        //    if (!ModelState.IsValid)
        //        return View(model);

        //    model.UserId = userId;
        //    model.Status = "Pending";
        //    model.RefundStatus = "Not Initiated";
        //    model.RequestDate = DateTime.Now;

        //    _db.OrderReturns.Add(model);
        //    await _db.SaveChangesAsync();

        //    TempData["Message"] = "Return / Exchange request submitted successfully.";

        //    return RedirectToAction("MyRequests");
        //}




        private void LoadReasons()
        {
            ViewBag.Reasons = new List<string>
            {
                "Wrong product received",
                "Product damaged",
                "Size issue",
                "Color issue",
                "Product quality not good",
                "Product not as shown in image",
                "Received different item",
                "Changed my mind",
                "Other"
            };
        }




        [HttpGet]
        public async Task<IActionResult> MyRequests()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var requests = await _db.OrderReturns
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }





        [HttpGet]
        public async Task<IActionResult> CreateReturnRequest(int orderId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
                return Challenge();

            var order = await _db.Orders
                .FirstOrDefaultAsync(o =>
                    o.Id == orderId && o.UserId == userId);

            if (order == null)
                return NotFound();

            if (order.Status != "Delivered")
            {
                TempData["Error"] =
                    "Return / Exchange is available only after delivery.";

                return RedirectToAction("MyOrders", "Account");
            }

            var model = new OrderReturn
            {
                OrderId = order.Id,
                UserId = userId,
                RequestType = "Return",
                Status = "Pending",
                RefundStatus = "Not Initiated"
            };

            LoadReasons();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReturnRequest(OrderReturn model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
                return Challenge();

            var order = await _db.Orders
                .FirstOrDefaultAsync(o =>
                    o.Id == model.OrderId && o.UserId == userId);

            if (order == null)
                return NotFound();

            if (order.Status != "Delivered")
                return BadRequest(
                    "Order must be delivered before requesting a return.");

            ModelState.Remove(nameof(OrderReturn.UserId));
            ModelState.Remove(nameof(OrderReturn.Status));
            ModelState.Remove(nameof(OrderReturn.RefundStatus));
            ModelState.Remove(nameof(OrderReturn.RequestDate));

            if (!ModelState.IsValid)
            {
                LoadReasons();
                return View(model);
            }

            model.UserId = userId;
            model.Status = "Pending";
            model.RefundStatus = "Not Initiated";
            model.RequestDate = DateTime.Now;

            _db.OrderReturns.Add(model);
            await _db.SaveChangesAsync();

            TempData["Message"] =
                "Return / Exchange request submitted successfully.";

            return RedirectToAction("MyRequests");
        }





    }
}
