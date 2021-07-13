using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Plugins;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Web.Framework;
using System.Threading.Tasks;
using Nop.Services.Common;
using Nop.Services.Directory;

namespace Nop.Plugin.Payments.Moneris
{
    public class MonerisPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly MonerisPaymentSettings _monerisPaymentSettings;
        private readonly IAddressService _addressService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ICountryService _countryService;

        #endregion

        #region Ctor

        public MonerisPaymentProcessor(ILocalizationService localizationService,
            IPaymentService paymentService,
            ISettingService settingService,
            IWebHelper webHelper,
            MonerisPaymentSettings monerisPaymentSettings,
            IAddressService addressService,
            IStateProvinceService stateProvinceService,
            ICountryService countryService)
        {
            _localizationService = localizationService;
            _paymentService = paymentService;
            _settingService = settingService;
            _webHelper = webHelper;
            _monerisPaymentSettings = monerisPaymentSettings;
            _addressService = addressService;
            _stateProvinceService = stateProvinceService;
            _countryService = countryService;
        }

        #endregion

        #region Utilites

        /// <summary>
        /// Gets payment URL
        /// </summary>
        /// <returns></returns>
        private string GetPaymentUrl()
        {
            return _monerisPaymentSettings.UseSandbox ? "https://esqa.moneris.com/HPPDP/index.php" :
                "https://www3.moneris.com/HPPDP/index.php";
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult
            {
                NewPaymentStatus = PaymentStatus.Pending
            };
            return Task.FromResult(result);
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public  async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var nfi = new CultureInfo("en-US", false).NumberFormat;
            var url = GetPaymentUrl();
            var gatewayUrl = new Uri(url);
            var post = new RemotePost { Url = gatewayUrl.ToString(), Method = "POST" };

            var order = postProcessPaymentRequest.Order;

            //required details
            post.Add("ps_store_id", _monerisPaymentSettings.PsStoreId);
            post.Add("hpp_key", _monerisPaymentSettings.HppKey);
            post.Add("charge_total", order.OrderTotal.ToString(nfi));

            ////other transaction details
            post.Add("cust_id", order.CustomerId.ToString());
            if (!_monerisPaymentSettings.UseSandbox)
            {
                post.Add("order_id", order.Id.ToString());
            }

            var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
            post.Add("email", billingAddress.Email);
            post.Add("rvar_order_id", order.Id.ToString());

            var shippingAddress = await _addressService.GetAddressByIdAsync(order.ShippingAddressId ?? 0);
            //shipping details
            if (shippingAddress != null)
            {
                post.Add("ship_first_name", shippingAddress.FirstName);
                post.Add("ship_last_name", shippingAddress.LastName);
                post.Add("ship_company_name", shippingAddress.Company);
                post.Add("ship_city", shippingAddress.City);
                post.Add("ship_phone", shippingAddress.PhoneNumber);
                post.Add("ship_fax", shippingAddress.FaxNumber);
                post.Add("ship_postal_code", shippingAddress.ZipPostalCode);
                post.Add("ship_address_one",
                         "1: " + shippingAddress.Address1 +
                         " 2: " + shippingAddress.Address2);
                post.Add("ship_state_or_province",
                         (await _stateProvinceService.GetStateProvinceByIdAsync(shippingAddress.StateProvinceId ?? 0))?.Name ?? "");
                post.Add("ship_country",
                         (await _countryService.GetCountryByIdAsync(shippingAddress.CountryId ?? 0))?.Name ?? "");
            }

            //billing details
            if (billingAddress != null)
            {
                post.Add("bill_first_name", billingAddress.FirstName);
                post.Add("bill_last_name", billingAddress.LastName);
                post.Add("bill_company_name", billingAddress.Company);
                post.Add("bill_phone", billingAddress.PhoneNumber);
                post.Add("bill_fax", billingAddress.FaxNumber);
                post.Add("bill_postal_code", billingAddress.ZipPostalCode);
                post.Add("bill_city", billingAddress.City);
                post.Add("bill_address_one",
                         "1: " + billingAddress.Address1 +
                         " 2: " + billingAddress.Address2);
                post.Add("bill_state_or_province",
                         (await _stateProvinceService.GetStateProvinceByIdAsync(billingAddress.StateProvinceId ?? 0))?.Name ?? "");
                post.Add("bill_country",
                         (await _countryService.GetCountryByIdAsync(billingAddress.CountryId ?? 0))?.Name ?? "");
            }

            post.Post();
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public  Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            var result = await _paymentService.CalculateAdditionalFeeAsync(cart,
                _monerisPaymentSettings.AdditionalFee, _monerisPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public  Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public  Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public  Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public  Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public  Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 1 minute passed after order is placed
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        public  Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return Task.FromResult(paymentInfo);
        }

        public string GetPublicViewComponentName()
        {
            return "PaymentMoneris";
        }

        public  Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            var warnings = new List<string>();
            return Task.FromResult<IList<string>>(warnings);
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentMoneris/Configure";
        }

        public override  async Task InstallAsync()
        {
            //settings
            var settings = new MonerisPaymentSettings()
            {
                UseSandbox = true,
                AdditionalFeePercentage = false
            };
            await _settingService.SaveSettingAsync(settings);

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.RedirectionTip", "You will be redirected to Moneris site to complete the order.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.UseSandbox", "Use Sandbox");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.AdditionalFee", "Additional fee");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.AdditionalFeePercentage", "Additinal fee. Use percentage");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.PsStoreId", "ps_store_id");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.PsStoreId.Hint", "Enter your ps_store_id");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.HppKey", "hpp_key");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.Fields.HppKey.Hint", "Enter your hpp_key");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Moneris.PaymentMethodDescription", "You will be redirected to Moneris site to complete the order.");

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<MonerisPaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.RedirectionTip");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.UseSandbox");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.UseSandbox.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.AdditionalFee");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.AdditionalFee.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.AdditionalFeePercentage");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.AdditionalFeePercentage.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.PsStoreId");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.PsStoreId.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.HppKey");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.Fields.HppKey.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Moneris.PaymentMethodDescription");

            await base.UninstallAsync();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.Moneris.PaymentMethodDescription");
        }

        #endregion
    }
}
