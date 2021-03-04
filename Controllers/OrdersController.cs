using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Infrastructure;
using Nop.Plugin.Api.Attributes;
using Nop.Plugin.Api.Delta;
using Nop.Plugin.Api.DTO.Errors;
using Nop.Plugin.Api.DTO.OrderItems;
using Nop.Plugin.Api.DTO.Orders;
using Nop.Plugin.Api.Factories;
using Nop.Plugin.Api.Helpers;
using Nop.Plugin.Api.JSON.ActionResults;
using Nop.Plugin.Api.JSON.Serializers;
using Nop.Plugin.Api.ModelBinders;
using Nop.Plugin.Api.Models.OrdersParameters;
using Nop.Plugin.Api.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.Api.DTOs.Shipments;
using static Nop.Plugin.Api.Infrastructure.Constants;

namespace Nop.Plugin.Api.Controllers
{

    public class OrdersController : BaseApiController
    {
        private readonly IOrderApiService _orderApiService;
        private readonly IProductService _productService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IShipmentService _shipmentService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IShippingService _shippingService;
        private readonly IDTOHelper _dtoHelper;        
        private readonly IProductAttributeConverter _productAttributeConverter;
        private readonly IStoreContext _storeContext;
        private readonly IFactory<Order> _factory;

        // We resolve the order settings this way because of the tests.
        // The auto mocking does not support concreate types as dependencies. It supports only interfaces.
        private OrderSettings _orderSettings;

        private OrderSettings OrderSettings => _orderSettings ?? (_orderSettings = EngineContext.Current.Resolve<OrderSettings>());

        public OrdersController(IOrderApiService orderApiService,
            IJsonFieldsSerializer jsonFieldsSerializer,
            IAclService aclService,
            ICustomerService customerService,
            IStoreMappingService storeMappingService,
            IStoreService storeService,
            IDiscountService discountService,
            ICustomerActivityService customerActivityService,
            ILocalizationService localizationService,
            IProductService productService,
            IFactory<Order> factory,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IShipmentService shipmentService,
            IShoppingCartService shoppingCartService,
            IGenericAttributeService genericAttributeService,
            IStoreContext storeContext,
            IShippingService shippingService,
            IPictureService pictureService,
            IDTOHelper dtoHelper,
            IProductAttributeConverter productAttributeConverter)
            : base(jsonFieldsSerializer, aclService, customerService, storeMappingService,
                 storeService, discountService, customerActivityService, localizationService,pictureService)
        {
            _orderApiService = orderApiService;
            _factory = factory;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _shipmentService = shipmentService;
            _shoppingCartService = shoppingCartService;
            _genericAttributeService = genericAttributeService;
            _storeContext = storeContext;
            _shippingService = shippingService;
            _dtoHelper = dtoHelper;
            _productService = productService;
            _productAttributeConverter = productAttributeConverter;
        }

        /// <summary>
        /// Receive a list of all Orders
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">Bad Request</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [Route("/api/orders")]
        [ProducesResponseType(typeof(OrdersRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        [GetRequestsErrorInterceptorActionFilter]
        public IActionResult GetOrders(OrdersParametersModel parameters)
        {
            if (parameters.Page < Configurations.DefaultPageValue)
            {
                return Error(HttpStatusCode.BadRequest, "page", "Invalid page parameter");
            }

            if (parameters.Limit < Configurations.MinLimit || parameters.Limit > Configurations.MaxLimit)
            {
                return Error(HttpStatusCode.BadRequest, "page", "Invalid limit parameter");
            }

            var storeId = _storeContext.CurrentStore.Id;

            var orders = _orderApiService.GetOrders(parameters.Ids, parameters.CreatedAtMin,
                parameters.CreatedAtMax,
                parameters.Limit, parameters.Page, parameters.SinceId,
                parameters.Status, parameters.PaymentStatus, parameters.ShippingStatus,
                parameters.CustomerId, storeId);

            IList<OrderDto> ordersAsDtos = orders.Select(x => _dtoHelper.PrepareOrderDTO(x)).ToList();

            var ordersRootObject = new OrdersRootObject()
            {
                Orders = ordersAsDtos
            };

            var json = JsonFieldsSerializer.Serialize(ordersRootObject, parameters.Fields);

            return new RawJsonActionResult(json);
        }

        /// <summary>
        /// Receive a count of all Orders
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [Route("/api/orders/count")]
        [ProducesResponseType(typeof(OrdersCountRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [GetRequestsErrorInterceptorActionFilter]
        public IActionResult GetOrdersCount(OrdersCountParametersModel parameters)
        {
            var storeId = _storeContext.CurrentStore.Id;

            var ordersCount = _orderApiService.GetOrdersCount(parameters.CreatedAtMin, parameters.CreatedAtMax, parameters.Status,
                                                              parameters.PaymentStatus, parameters.ShippingStatus, parameters.CustomerId, storeId, 
                                                              parameters.SinceId);

            var ordersCountRootObject = new OrdersCountRootObject()
            {
                Count = ordersCount
            };

            return Ok(ordersCountRootObject);
        }

        /// <summary>
        /// Retrieve order by spcified id
        /// </summary>
        ///   /// <param name="id">Id of the order</param>
        /// <param name="fields">Fields from the order you want your json to contain</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [Route("/api/orders/{id}")]
        [ProducesResponseType(typeof(OrdersRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.NotFound)]
        [GetRequestsErrorInterceptorActionFilter]
        public IActionResult GetOrderById(int id, string fields = "")
        {
            if (id <= 0)
            {
                return Error(HttpStatusCode.BadRequest, "id", "invalid id");
            }

            var order = _orderApiService.GetOrderById(id);

            if (order == null)
            {
                return Error(HttpStatusCode.NotFound, "order", "not found");
            }

            var ordersRootObject = new OrdersRootObject();

            var orderDto = _dtoHelper.PrepareOrderDTO(order);
            ordersRootObject.Orders.Add(orderDto);

            var json = JsonFieldsSerializer.Serialize(ordersRootObject, fields);

            return new RawJsonActionResult(json);
        }

        /// <summary>
        /// Retrieve all orders for customer
        /// </summary>
        /// <param name="customerId">Id of the customer whoes orders you want to get</param>
        /// <response code="200">OK</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [Route("/api/orders/customer/{customer_id}")]
        [ProducesResponseType(typeof(OrdersRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        [GetRequestsErrorInterceptorActionFilter]
        public IActionResult GetOrdersByCustomerId(int customerId)
        {
            IList<OrderDto> ordersForCustomer = _orderApiService.GetOrdersByCustomerId(customerId).Select(x => _dtoHelper.PrepareOrderDTO(x)).ToList();

            var ordersRootObject = new OrdersRootObject()
            {
                Orders = ordersForCustomer
            };

            return Ok(ordersRootObject);
        }

        [HttpPost]
        [Route("/api/orders")]
        [ProducesResponseType(typeof(OrdersRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ErrorsRootObject), 422)]
        public IActionResult CreateOrder([ModelBinder(typeof(JsonModelBinder<OrderDto>))] Delta<OrderDto> orderDelta)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            if (orderDelta.Dto.CustomerId == null)
            {
                return Error();
            }

            // We doesn't have to check for value because this is done by the order validator.
            var customer = CustomerService.GetCustomerById(orderDelta.Dto.CustomerId.Value);

            if (customer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            var shippingRequired = false;

            if (orderDelta.Dto.OrderItems != null)
            {
                var shouldReturnError = AddOrderItemsToCart(orderDelta.Dto.OrderItems, customer, orderDelta.Dto.StoreId ?? _storeContext.CurrentStore.Id);
                if (shouldReturnError)
                {
                    return Error(HttpStatusCode.BadRequest);
                }

                shippingRequired = IsShippingAddressRequired(orderDelta.Dto.OrderItems);
            }

            if (shippingRequired)
            {
                var isValid = true;

                isValid &= SetShippingOption(orderDelta.Dto.ShippingRateComputationMethodSystemName,
                                             orderDelta.Dto.ShippingMethod,
                                             orderDelta.Dto.StoreId ?? _storeContext.CurrentStore.Id,
                                             customer,
                                             BuildShoppingCartItemsFromOrderItemDtos(orderDelta.Dto.OrderItems.ToList(),
                                                                                     customer.Id,
                                                                                     orderDelta.Dto.StoreId ?? _storeContext.CurrentStore.Id));

                if (!isValid)
                {
                    return Error(HttpStatusCode.BadRequest);
                }
            }

            var newOrder = _factory.Initialize();
            orderDelta.Merge(newOrder);

            customer.BillingAddressId = newOrder.BillingAddressId = orderDelta.Dto.BillingAddress.Id;
            customer.ShippingAddressId = newOrder.ShippingAddressId = orderDelta.Dto.ShippingAddress.Id;


            // If the customer has something in the cart it will be added too. Should we clear the cart first? 
            newOrder.CustomerId = customer.Id;

            // The default value will be the currentStore.id, but if it isn't passed in the json we need to set it by hand.
            if (!orderDelta.Dto.StoreId.HasValue)
            {
                newOrder.StoreId = _storeContext.CurrentStore.Id;
            }

            var placeOrderResult = PlaceOrder(newOrder, customer);

            if (!placeOrderResult.Success)
            {
                foreach (var error in placeOrderResult.Errors)
                {
                    ModelState.AddModelError("order placement", error);
                }

                return Error(HttpStatusCode.BadRequest);
            }

            CustomerActivityService.InsertActivity("AddNewOrder",
                                                   LocalizationService.GetResource("ActivityLog.AddNewOrder"), newOrder);

            var ordersRootObject = new OrdersRootObject();

            var placedOrderDto = _dtoHelper.PrepareOrderDTO(placeOrderResult.PlacedOrder);

            ordersRootObject.Orders.Add(placedOrderDto);

            var json = JsonFieldsSerializer.Serialize(ordersRootObject, string.Empty);

            return new RawJsonActionResult(json);
        }

        [HttpDelete]
        [Route("/api/orders/{id}")]
        [ProducesResponseType(typeof(void), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ErrorsRootObject), 422)]
        [GetRequestsErrorInterceptorActionFilter]
        public IActionResult DeleteOrder(int id)
        {
            if (id <= 0)
            {
                return Error(HttpStatusCode.BadRequest, "id", "invalid id");
            }
            
            var orderToDelete = _orderApiService.GetOrderById(id);

            if (orderToDelete == null)
            {
                return Error(HttpStatusCode.NotFound, "order", "not found");
            }

            _orderProcessingService.DeleteOrder(orderToDelete);

            //activity log
            CustomerActivityService.InsertActivity("DeleteOrder", LocalizationService.GetResource("ActivityLog.DeleteOrder"), orderToDelete);

            return new RawJsonActionResult("{}");
        }

        [HttpPut]
        [Route("/api/orders/{id}")]
        [ProducesResponseType(typeof(OrdersRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ErrorsRootObject), 422)]
        public IActionResult UpdateOrder([ModelBinder(typeof(JsonModelBinder<OrderDto>))] Delta<OrderDto> orderDelta)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            var currentOrder = _orderApiService.GetOrderById(orderDelta.Dto.Id);

            if (currentOrder == null)
            {
                return Error(HttpStatusCode.NotFound, "order", "not found");
            }

            var customer = CustomerService.GetCustomerById(currentOrder.CustomerId);

            var shippingRequired = _orderService.GetOrderItems(currentOrder.Id).Any(item => !_productService.GetProductById(item.Id).IsFreeShipping);

            if (shippingRequired)
            {
                var isValid = true;

                if (!string.IsNullOrEmpty(orderDelta.Dto.ShippingRateComputationMethodSystemName) ||
                    !string.IsNullOrEmpty(orderDelta.Dto.ShippingMethod))
                {
                    var storeId = orderDelta.Dto.StoreId ?? _storeContext.CurrentStore.Id;

                    isValid &= SetShippingOption(orderDelta.Dto.ShippingRateComputationMethodSystemName ?? currentOrder.ShippingRateComputationMethodSystemName,
                                                 orderDelta.Dto.ShippingMethod,
                                                 storeId,
                                                 customer, BuildShoppingCartItemsFromOrderItems(_orderService.GetOrderItems(currentOrder.Id).ToList(), customer.Id, storeId));
                }

                if (isValid)
                {
                    currentOrder.ShippingMethod = orderDelta.Dto.ShippingMethod;
                }
                else
                {
                    return Error(HttpStatusCode.BadRequest);
                }
            }

            orderDelta.Merge(currentOrder);

            customer.BillingAddressId = currentOrder.BillingAddressId = orderDelta.Dto.BillingAddress.Id;
            customer.ShippingAddressId = currentOrder.ShippingAddressId = orderDelta.Dto.ShippingAddress.Id;


            _orderService.UpdateOrder(currentOrder);

            CustomerActivityService.InsertActivity("UpdateOrder",
                                                   LocalizationService.GetResource("ActivityLog.UpdateOrder"), currentOrder);

            var ordersRootObject = new OrdersRootObject();

            var placedOrderDto = _dtoHelper.PrepareOrderDTO(currentOrder);
            placedOrderDto.ShippingMethod = orderDelta.Dto.ShippingMethod;

            ordersRootObject.Orders.Add(placedOrderDto);

            var json = JsonFieldsSerializer.Serialize(ordersRootObject, string.Empty);

            return new RawJsonActionResult(json);
        }


    [HttpPost]
    [Route("/api/orders/shipcomplete")]
    [ProducesResponseType(typeof(OrdersRootObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ErrorsRootObject), 422)]
    public IActionResult ShipComplete([ModelBinder(typeof(JsonModelBinder<ShipCompleteDto>))] Delta<ShipCompleteDto> shipmentDelta)
    {
      // This method will create a shipment record that assumes that all items in the order were
      // shipped in one package. 

      // Basic steps:
      // * Make sure that we don't already have a shipment record. 
      // * Get the order and the items
      // * Build the shipment
      // * Save it and return a success


      // What we probably need to process this:
      // Order ID
      // Tracking Number
      // Admin Comment

      // Display the errors if the validation has failed at some point.
      if (!ModelState.IsValid)
      {
        return Error();
      }

      var dto = shipmentDelta.Dto;

      //try to get an order with the specified id
      var order = _orderService.GetOrderByCustomOrderNumber(dto.OrderReference);
      if (order == null)
        return Error(HttpStatusCode.NotFound, "error", "not found");

      // Check to see if we already have a shipment for this order. 
      if (_shipmentService.GetShipmentsByOrderId(order.Id).Any())
        return Error(HttpStatusCode.Conflict, "error", "Order already has shipments");

      // Get the shipment record spun up.
      var trackingNumber = dto.TrackingNumber ?? "";
      var adminComment = dto.AdminComment ?? "";
      var shipment = new Shipment
      {
        OrderId = order.Id,
        TrackingNumber = trackingNumber,
        TotalWeight = null,
        ShippedDateUtc = null,
        DeliveryDateUtc = null,
        AdminComment = adminComment,
        CreatedOnUtc = DateTime.UtcNow
      };

      _shipmentService.InsertShipment(shipment);

      // Work through the items on the order. 
      var orderItems = _orderService.GetOrderItems(order.Id, isShipEnabled: true);

      decimal? totalWeight = null;
      var itemCount = 0;
      foreach (var orderItem in orderItems)
      {
        var orderItemTotalWeight = orderItem.ItemWeight * orderItem.Quantity;
        if (orderItemTotalWeight.HasValue)
        {
          if (!totalWeight.HasValue)
            totalWeight = 0;
          totalWeight += orderItemTotalWeight.Value;
        }

        //create a shipment item
        var shipmentItem = new ShipmentItem
        {
          OrderItemId = orderItem.Id,
          Quantity = orderItem.Quantity,
          WarehouseId = 0,
          ShipmentId = shipment.Id
        };

        _shipmentService.InsertShipmentItem(shipmentItem);
        itemCount++;
      }

      //if we have at least one item in the shipment, then save it
      if (itemCount > 0)
      {
        shipment.TotalWeight = totalWeight;

        // Also set the order as shipped
        _orderProcessingService.Ship(shipment, true);  // This will notify the customer...

        //add a note
        _orderService.InsertOrderNote(new OrderNote
        {
          Note = "A shipment has been added and order set Complete via API",
          DisplayToCustomer = false,
          CreatedOnUtc = DateTime.UtcNow,
          OrderId = order.Id
        });
        order.OrderStatus = OrderStatus.Complete;
        _orderService.UpdateOrder(order);

        // TODO Return the new shipment info.  Use the dto model stuff to send data back to the caller
        var ret = new ShipCompleteReturnDto();
        ret.ShipmentId = shipment.Id;

        var json = JsonFieldsSerializer.Serialize(ret, string.Empty);

        return new RawJsonActionResult(json);
      }

      return Error(errorMessage: "No Products Selected To Ship");
    }

    private bool SetShippingOption(string shippingRateComputationMethodSystemName, 
            string shippingOptionName, 
            int storeId,
            Customer customer, 
            List<ShoppingCartItem> shoppingCartItems)
        {
            var isValid = true;

            if (string.IsNullOrEmpty(shippingRateComputationMethodSystemName))
            {
                isValid = false;

                ModelState.AddModelError("shipping_rate_computation_method_system_name",
                                         "Please provide shipping_rate_computation_method_system_name");
            }
            else if (string.IsNullOrEmpty(shippingOptionName))
            {
                isValid = false;

                ModelState.AddModelError("shipping_option_name", "Please provide shipping_option_name");
            }
            else
            {
                var shippingOptionResponse = _shippingService.GetShippingOptions(shoppingCartItems, CustomerService.GetCustomerShippingAddress(customer), customer,
                                                                                 shippingRateComputationMethodSystemName, storeId);

                if (shippingOptionResponse.Success)
                {
                    var shippingOptions = shippingOptionResponse.ShippingOptions.ToList();

                    var shippingOption = shippingOptions
                        .Find(so => !string.IsNullOrEmpty(so.Name) && so.Name.Equals(shippingOptionName, StringComparison.InvariantCultureIgnoreCase));

                    _genericAttributeService.SaveAttribute(customer,
                                                           NopCustomerDefaults.SelectedShippingOptionAttribute,
                                                           shippingOption, storeId);
                }
                else
                {
                    isValid = false;

                    foreach (var errorMessage in shippingOptionResponse.Errors)
                    {
                        ModelState.AddModelError("shipping_option", errorMessage);
                    }
                }
            }

            return isValid;
        }

        private List<ShoppingCartItem> BuildShoppingCartItemsFromOrderItems(List<OrderItem> orderItems, int customerId, int storeId)
        {
            var shoppingCartItems = new List<ShoppingCartItem>();

            foreach (var orderItem in orderItems)
            {
                shoppingCartItems.Add(new ShoppingCartItem
                {
                    ProductId = orderItem.ProductId,
                    CustomerId = customerId,
                    Quantity = orderItem.Quantity,
                    RentalStartDateUtc = orderItem.RentalStartDateUtc,
                    RentalEndDateUtc = orderItem.RentalEndDateUtc,
                    StoreId = storeId,
                    ShoppingCartType = ShoppingCartType.ShoppingCart
                });
            }

            return shoppingCartItems;
        }

        private List<ShoppingCartItem> BuildShoppingCartItemsFromOrderItemDtos(List<OrderItemDto> orderItemDtos, int customerId, int storeId)
        {
            var shoppingCartItems = new List<ShoppingCartItem>();

            foreach (var orderItem in orderItemDtos)
            {
                if (orderItem.ProductId != null)
                {
                    shoppingCartItems.Add(new ShoppingCartItem
                    {
                        ProductId = orderItem.ProductId.Value, // required field
                        CustomerId = customerId,
                        Quantity = orderItem.Quantity ?? 1,
                        RentalStartDateUtc = orderItem.RentalStartDateUtc,
                        RentalEndDateUtc = orderItem.RentalEndDateUtc,
                        StoreId = storeId,
                        ShoppingCartType = ShoppingCartType.ShoppingCart
                    });
                }
            }

            return shoppingCartItems;
        }
 
        private PlaceOrderResult PlaceOrder(Order newOrder, Customer customer)
        {
            var processPaymentRequest = new ProcessPaymentRequest
            {
                StoreId = newOrder.StoreId,
                CustomerId = customer.Id,
                PaymentMethodSystemName = newOrder.PaymentMethodSystemName
            };


            var placeOrderResult = _orderProcessingService.PlaceOrder(processPaymentRequest);

            return placeOrderResult;
        }

        private bool IsShippingAddressRequired(ICollection<OrderItemDto> orderItems)
        {
            var shippingAddressRequired = false;

            foreach (var orderItem in orderItems)
            {
                if (orderItem.ProductId != null)
                {
                    var product = _productService.GetProductById(orderItem.ProductId.Value);

                    shippingAddressRequired |= product.IsShipEnabled;
                }
            }

            return shippingAddressRequired;
        }

        private bool AddOrderItemsToCart(ICollection<OrderItemDto> orderItems, Customer customer, int storeId)
        {
            var shouldReturnError = false;

            foreach (var orderItem in orderItems)
            {
                if (orderItem.ProductId != null)
                {
                    var product = _productService.GetProductById(orderItem.ProductId.Value);

                    if (!product.IsRental)
                    {
                        orderItem.RentalStartDateUtc = null;
                        orderItem.RentalEndDateUtc = null;
                    }

                    var attributesXml = _productAttributeConverter.ConvertToXml(orderItem.Attributes.ToList(), product.Id);                

                    var errors = _shoppingCartService.AddToCart(customer, product,
                        ShoppingCartType.ShoppingCart, storeId,attributesXml,
                        0M, orderItem.RentalStartDateUtc, orderItem.RentalEndDateUtc,
                        orderItem.Quantity ?? 1);

                    if (errors.Count > 0)
                    {
                        foreach (var error in errors)
                        {
                            ModelState.AddModelError("order", error);
                        }

                        shouldReturnError = true;
                    }
                }
            }

            return shouldReturnError;
        }
     }
}