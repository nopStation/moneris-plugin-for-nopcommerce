using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.Moneris.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Payments.Moneris.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Moneris.Fields.PsStoreId")]
        public string PsStoreId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Moneris.Fields.HppKey")]
        public string HppKey { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Moneris.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Moneris.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
    }
}