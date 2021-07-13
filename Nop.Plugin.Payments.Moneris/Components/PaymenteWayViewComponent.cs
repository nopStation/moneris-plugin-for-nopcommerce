using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Moneris.Components
{
    [ViewComponent(Name = "PaymentMoneris")]
    public class PaymentMonerisViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.Moneris/Views/PaymentInfo.cshtml");
        }
    }
}
