using System;
using Newtonsoft.Json;
using Nop.Plugin.Api.DTO;
using Nop.Plugin.Api.DTO.Orders;

namespace Nop.Plugin.Api.DTOs.Shipments
{
	[JsonObject(Title = "complete_shipment")]
	public class ShipCompleteReturnDto: ISerializableObject
	{
		[JsonProperty("shipment_id")]
		public int ShipmentId { get; set; }

		public string GetPrimaryPropertyName()
		{
			return "shipment";
		}

		public Type GetPrimaryPropertyType()
		{
			return typeof(OrderDto);
		}
	}
}