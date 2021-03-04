using Newtonsoft.Json;
using Nop.Plugin.Api.DTO.Base;


namespace Nop.Plugin.Api.DTOs.Shipments
{
	[JsonObject(Title = "ship_complete")]
	//[Validator(typeof(ShipCompleteDtoValidator))]
	public class ShipCompleteDto : BaseDto
	{
		[JsonProperty("order_id")]
		public string OrderReference { get; set; }

		[JsonProperty("tracking_number")]
		public string TrackingNumber { get; set; }

		[JsonProperty("admin_comment")]
		public string AdminComment { get; set; }

		[JsonProperty("notify_customer")]
		public bool NotifyCustomer { get; set; }

	}
}