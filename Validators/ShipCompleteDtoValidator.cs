using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Nop.Plugin.Api.DTOs.Shipments;
using Nop.Plugin.Api.Helpers;

namespace Nop.Plugin.Api.Validators
{
	public class ShipCompleteDtoValidator : BaseDtoValidator<ShipCompleteDto>
	{
		public ShipCompleteDtoValidator(IHttpContextAccessor httpContextAccessor, IJsonHelper jsonHelper, Dictionary<string, object> requestJsonDictionary) : base(httpContextAccessor, jsonHelper, requestJsonDictionary)
		{
			//SetGreaterThanZeroCreateOrUpdateRule(s => s.Orderid, "invalid order_id", "order_id");
		}

	}
}