using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.Moneris.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Moneris.Controllers
{
    public class PaymentMonerisController : BasePaymentController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPaymentService _paymentService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly MonerisPaymentSettings _monerisPaymentSettings;
        private readonly INotificationService _notificationService;

        #endregion

        #region Ctor

        public PaymentMonerisController(ILocalizationService localizationService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IPaymentService paymentService,
            IPermissionService permissionService,
            ISettingService settingService,
            IWebHelper webHelper,
            MonerisPaymentSettings monerisPaymentSettings,
            INotificationService notificationService)
        {
            _localizationService = localizationService;
            _orderService = orderService;
            _orderProcessingService = orderProcessingService;
            _paymentService = paymentService;
            _permissionService = permissionService;
            _settingService = settingService;
            _webHelper = webHelper;
            _monerisPaymentSettings = monerisPaymentSettings;
            _notificationService = notificationService;
        }

        #endregion

        #region Utilites

        private string GetValue(string key, IFormCollection form)
        {
            return (form.Keys.Contains(key) ? form[key].ToString() : _webHelper.QueryString<string>(key)) ?? string.Empty;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                AdditionalFee = _monerisPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = _monerisPaymentSettings.AdditionalFeePercentage,
                HppKey = _monerisPaymentSettings.HppKey,
                PsStoreId = _monerisPaymentSettings.PsStoreId,
                UseSandbox = _monerisPaymentSettings.UseSandbox
            };

            return View("~/Plugins/Payments.Moneris/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //save settings
            _monerisPaymentSettings.AdditionalFee = model.AdditionalFee;
            _monerisPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            _monerisPaymentSettings.HppKey = model.HppKey;
            _monerisPaymentSettings.PsStoreId = model.PsStoreId;
            _monerisPaymentSettings.UseSandbox = model.UseSandbox;
            await _settingService.SaveSettingAsync(_monerisPaymentSettings);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return RedirectToAction("Configure");
        }

        public async Task<IActionResult> SuccessCallbackHandler(IFormCollection form)
        {
            if (string.IsNullOrEmpty(GetValue("transactionKey", form)) || string.IsNullOrEmpty(GetValue("rvar_order_id", form)))
                return RedirectToAction("Index", "Home", new {area = ""});

            var transactionKey = GetValue("transactionKey", form);
            if (!TransactionVerification(transactionKey, out Dictionary<string, string> values))
                return RedirectToAction("Index", "Home", new { area = "" });

            var orderIdValue = GetValue("rvar_order_id", form);
            if (!int.TryParse(orderIdValue, out int orderId))
                return RedirectToAction("Index", "Home", new {area = ""});

            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null || !_orderProcessingService.CanMarkOrderAsPaid(order))
                return RedirectToAction("Index", "Home", new {area = ""});

            if (values.ContainsKey("txn_num"))
            {
                order.AuthorizationTransactionId = values["txn_num"];
                await _orderService.UpdateOrderAsync(order);
            }

            await _orderProcessingService.MarkOrderAsPaidAsync(order);
            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
        }

        public IActionResult FailCallbackHandler()
        {
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        #endregion

        #region Utilites

        /// <summary>
        /// Gets verify URL
        /// </summary>
        /// <returns></returns>
        private string GetVerifyUrl()
        {
            return _monerisPaymentSettings.UseSandbox ? "https://esqa.moneris.com/HPPDP/verifyTxn.php" :
                "https://www3.moneris.com/HPPDP/verifyTxn.php";
        }

        /// <summary>
        /// Transaction verification
        /// </summary>
        /// <param name="transactionKey">transactionKey</param>
        /// <param name="values">values</param>
        /// <returns>Result</returns>
        public bool TransactionVerification(string transactionKey, out Dictionary<string, string> values)
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var url = GetVerifyUrl();
            var gatewayUrl = new Uri(url);

            var req = (HttpWebRequest)WebRequest.Create(gatewayUrl);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";

            var formContent = $"ps_store_id={_monerisPaymentSettings.PsStoreId}&hpp_key={_monerisPaymentSettings.HppKey}&transactionKey={transactionKey}";

            req.ContentLength = formContent.Length;
            using (var sw = new StreamWriter(req.GetRequestStream(), Encoding.ASCII))
            {
                sw.Write(formContent);
            }

            var response = string.Empty;
            var responseStream = req.GetResponse().GetResponseStream();
            if (responseStream != null)
            {
                using (var sr = new StreamReader(responseStream))
                {
                    response = WebUtility.UrlDecode(sr.ReadToEnd());
                }
            }

            if (string.IsNullOrEmpty(response))
                return false;

            var xmlResponse = new XmlDocument();
            xmlResponse.LoadXml(response);

            var responseSingleNode = xmlResponse.SelectSingleNode("response");
            if (responseSingleNode != null)
            {
                foreach (XmlNode child in responseSingleNode.ChildNodes)
                {
                    values.Add(child.Name, child.InnerText);
                }
            }

            if (!values.ContainsKey("response_code"))
                return false;
            var responseCodeValue = values["response_code"];
            int responseCode;

            return int.TryParse(responseCodeValue, out responseCode) && responseCode < 50;
        }

        #endregion
    }
}