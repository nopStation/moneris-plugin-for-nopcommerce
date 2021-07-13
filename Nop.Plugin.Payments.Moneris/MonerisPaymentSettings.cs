using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Moneris
{
    public class MonerisPaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }
        public string PsStoreId { get; set; }
        public string HppKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
        /// <summary>
        /// Additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }
    }
}