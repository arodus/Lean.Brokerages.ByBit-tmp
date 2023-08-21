﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Brokerages;
using QuantConnect.BybitBrokerage.Models.Enums;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;
using OrderStatus = QuantConnect.Orders.OrderStatus;
using OrderType = QuantConnect.BybitBrokerage.Models.Enums.OrderType;

namespace QuantConnect.BybitBrokerage;

public partial class BybitBrokerage
{
    #region Brokerage


    /// <summary>
    /// Gets all open orders on the account.
    /// NOTE: The order objects returned do not have QC order IDs.
    /// </summary>
    /// <returns>The open orders returned from IB</returns>
    public override List<Order> GetOpenOrders()
    {
        var orders = ApiClient.GetOpenOrders(Category);

        var mapped = orders.Select(item =>
        {
            var symbol = _symbolMapper.GetLeanSymbol(item.Symbol, SecurityType.CryptoFuture, Market.Bybit);
            var price = item.Price!.Value;


            Order order;
            if (item.StopOrderType != null)
            {
                if (item.StopOrderType == StopOrderType.TrailingStop)
                {
                    throw new NotImplementedException();
                }

                order = item.OrderType == OrderType.Limit
                    ? new StopLimitOrder(symbol, item.Quantity, price, item.Price!.Value, item.CreateTime)
                    : new StopMarketOrder(symbol, item.Quantity, price, item.CreateTime);
            }
            else
            {
                order = item.OrderType == OrderType.Limit
                    ? new LimitOrder(symbol, item.Quantity, price, item.CreateTime)
                    : new MarketOrder(symbol, item.Quantity, item.CreateTime);
            }

            order.BrokerId.Add(item.OrderId);
            //todo order.Status=
            // todo order.Id = item.ClientOrderId
            return order;
        });
        return mapped.ToList();
    }

    /// <summary>
    /// Gets all holdings for the account
    /// </summary>
    /// <returns>The current holdings from the account</returns>
    public override List<Holding> GetAccountHoldings()
    {
        var holdings =  ApiClient.GetPositions(Category)
            .Select(x =>
            {
                return new Holding()
                {
                    Symbol = _symbolMapper.GetLeanSymbol(x.Symbol, GetSupportedSecurityType(), MarketName),
                    AveragePrice = x.AveragePrice,
                    Quantity = x.Side == Models.PositionSide.Buy ? x.Size : x.Size * -1,
                    MarketValue = x.PositionValue,
                    UnrealizedPnL = x.UnrealisedPnl,
                    MarketPrice = x.MarkPrice
                };
            }).ToList();
        return holdings;
    }

    /// <summary>
    /// Gets the current cash balance for each currency held in the brokerage account
    /// </summary>
    /// <returns>The current cash balance for each currency available for trading</returns>
    public override List<CashAmount> GetCashBalance()
    {
        return ApiClient.GetWalletBalances(Category).Assets.Select(x => new CashAmount(x.WalletBalance, x.Asset)).ToList();
        //return new List<CashAmount>();

    }

    /// <summary>
    /// Places a new order and assigns a new broker ID to the order
    /// </summary>
    /// <param name="order">The order to be placed</param>
    /// <returns>True if the request for a new order has been placed, false otherwise</returns>
    public override bool PlaceOrder(Order order)
    {
        if (!CanSubscribe(order.Symbol))
        {
            OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, $"Symbol is not supported {order.Symbol}"));
            return false;
        }
        var submitted = false;
        
        _messageHandler.WithLockedStream(() =>
        {
            var result = ApiClient.PlaceOrder(Category, order);
            order.BrokerId.Add(result.OrderId);
            OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero, "Bybit Order Event"){Status = OrderStatus.Submitted});
            submitted = true;
            //OnOrderIdChangedEvent(new BrokerageOrderIdChangedEvent(){BrokerId = cachedOrder.BrokerId, OrderId = order.Id});
        });
        
        return submitted;
        
    }

    /// <summary>
    /// Updates the order with the same id
    /// </summary>
    /// <param name="order">The new order information</param>
    /// <returns>True if the request was made for the order to be updated, false otherwise</returns>
    public override bool UpdateOrder(Order order)
    {
        
        if (!CanSubscribe(order.Symbol))
        {
            OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, $"Symbol is not supported {order.Symbol}"));
            return false;
        }
        var submitted = false;
        
        _messageHandler.WithLockedStream(() =>
        {
            var result = ApiClient.UpdateOrder(Category, order);
            OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero, "Bybit Order Event"){Status = OrderStatus.UpdateSubmitted}); //todo fee
            submitted = true;
            //OnOrderIdChangedEvent(new BrokerageOrderIdChangedEvent(){BrokerId = cachedOrder.BrokerId, OrderId = order.Id});
        });
        
        return submitted;
    }

    /// <summary>
    /// Cancels the order with the specified ID
    /// </summary>
    /// <param name="order">The order to cancel</param>
    /// <returns>True if the request was made for the order to be canceled, false otherwise</returns>
    public override bool CancelOrder(Order order)
    {

        if (!CanSubscribe(order.Symbol))
        {
            OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1,
                $"Symbol is not supported {order.Symbol}"));
            return false;
        }

        var canceled = false;
        _messageHandler.WithLockedStream(() => { 
        ApiClient.CancelOrder(Category, order);
        OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero) { Status = OrderStatus.CancelPending });
        canceled = true;
        });
        return canceled;
    }

    protected override void OnMessage(object sender, WebSocketMessage e)
    {
        _messageHandler.HandleNewMessage(e);
    }

    /// <summary>
    /// Connects the client to the broker's remote servers
    /// </summary>
    public override void Connect()
    {
        if (IsConnected)
            return;

        // cannot reach this code if rest api client is not created
        // WebSocket is  responsible for Binance UserData stream only
        // as a result we don't need to connect user data stream if BinanceBrokerage is used as DQH only
        // or until Algorithm is actually initialized
           
        //todo reconnect
        if(WebSocket == null) return;
        WebSocket.Initialize(_webSocketBaseUrl);
        ConnectSync();
    }

    /// <summary>
    /// Disconnects the client from the broker's remote servers
    /// </summary>
    public override void Disconnect()
    {
        if(WebSocket?.IsOpen != true) return;
            
        _keepAliveTimer.Stop();
        WebSocket.Close();
    }

    #endregion
}