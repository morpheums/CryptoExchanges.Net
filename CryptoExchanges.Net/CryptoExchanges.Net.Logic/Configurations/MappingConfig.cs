using AutoMapper;
using CryptoExchanges.Net.Binance.Utils;
using CryptoExchanges.Net.Models.Account;
using CryptoExchanges.Net.Models.Market;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Binance.Configurations
{
    public static class MappingConfig
    {
        private static decimal MinTradeResolver(JToken source)
        {
            var filters = (JArray)source["filters"];

            var filterType = filters.Where(r => r.Value<string>("filterType") == "MIN_NOTIONAL").FirstOrDefault();

            return filterType.Value<decimal>("minNotional");
        }

        private static List<OrderBookOffer> OrderBookOfferResolver(JArray source)
        {
            var result = new List<OrderBookOffer>();

            foreach (JToken item in source)
            {
                result.Add(new OrderBookOffer() { Price = decimal.Parse(item[0].ToString()), Quantity = decimal.Parse(item[1].ToString()) });
            }

            return result;
        }

        public static void Initialize()
        {

            // Market entities
            Mapper.Initialize(configuration =>
            {
                configuration.CreateMap<JToken, CurrencyInfo>()
                    .ForMember(o => o.Symbol, cfg => { cfg.MapFrom(jo => jo["baseAsset"]); })
                    .ForMember(o => o.BaseSymbol, cfg => { cfg.MapFrom(jo => jo["quoteAsset"]); })
                    .ForMember(o => o.Pair, cfg => { cfg.MapFrom(jo => jo["symbol"]); })
                    .ForMember(o => o.MinTradePrice, cfg => cfg.ResolveUsing(jo => MinTradeResolver(jo)));

                configuration.CreateMap<JToken, TickerInfo>()
                    .ForMember(o => o.Pair, cfg => { cfg.MapFrom(jo => jo["symbol"]); })
                    .ForMember(o => o.Symbol, cfg => { cfg.MapFrom(jo => jo["symbol"]); })
                    .ForMember(o => o.BaseSymbol, cfg => { cfg.MapFrom(jo => jo["symbol"]); })
                    .ForMember(o => o.AskPrice, cfg => { cfg.MapFrom(jo => jo["askPrice"]); })
                    .ForMember(o => o.BidPrice, cfg => { cfg.MapFrom(jo => jo["bidPrice"]); })
                    .ForMember(o => o.HighPrice, cfg => { cfg.MapFrom(jo => jo["highPrice"]); })
                    .ForMember(o => o.LowPrice, cfg => { cfg.MapFrom(jo => jo["lowPrice"]); })
                    .ForMember(o => o.LastPrice, cfg => { cfg.MapFrom(jo => jo["lastPrice"]); })
                    .ForMember(o => o.Volume, cfg => { cfg.MapFrom(jo => jo["volume"]); });

                configuration.CreateMap<JToken, TickerPrice>()
                    .ForMember(o => o.Pair, cfg => { cfg.MapFrom(jo => jo["symbol"]); })
                    .ForMember(o => o.Symbol, cfg => { cfg.MapFrom(jo => jo["symbol"]); })
                    .ForMember(o => o.BaseSymbol, cfg => { cfg.MapFrom(jo => jo["symbol"]); })
                    .ForMember(o => o.Price, cfg => { cfg.MapFrom(jo => jo["price"]); });

                configuration.CreateMap<JToken, OrderBook>()
                    .ForMember(o => o.Asks, cfg => cfg.ResolveUsing(jo => OrderBookOfferResolver((JArray)jo["asks"])))
                    .ForMember(o => o.Bids, cfg => cfg.ResolveUsing(jo => OrderBookOfferResolver((JArray)jo["bids"])));

                // Account entitites
                configuration.CreateMap<JToken, AssetBalance>()
                    .ForMember(o => o.Asset, cfg => { cfg.MapFrom(jo => jo["asset"]); })
                    .ForMember(o => o.Free, cfg => { cfg.MapFrom(jo => decimal.Parse(jo["free"].ToString())); })
                    .ForMember(o => o.Locked, cfg => { cfg.MapFrom(jo => decimal.Parse(jo["locked"].ToString())); });

                configuration.CreateMap<JToken, Deposit>()
                    .ForMember(o => o.Asset, cfg => { cfg.MapFrom(jo => jo["asset"]); })
                    .ForMember(o => o.Amount, cfg => { cfg.MapFrom(jo => decimal.Parse(jo["amount"].ToString())); })
                    .ForMember(o => o.Date, cfg => { cfg.MapFrom(jo => Utilities.UnixTimeStampToDateTime(double.Parse(jo["insertTime"].ToString()))); })
                    .ForMember(o => o.Status, cfg => { cfg.MapFrom(jo => jo["status"]); });

                configuration.CreateMap<JToken, Withdraw>()
                    .ForMember(o => o.Asset, cfg => { cfg.MapFrom(jo => jo["asset"]); })
                    .ForMember(o => o.Amount, cfg => { cfg.MapFrom(jo => decimal.Parse(jo["amount"].ToString())); })
                    .ForMember(o => o.Address, cfg => { cfg.MapFrom(jo => jo["address"]); })
                    .ForMember(o => o.Date, cfg => { cfg.MapFrom(jo => Utilities.UnixTimeStampToDateTime(double.Parse(jo["applyTime"].ToString()))); })
                    .ForMember(o => o.Status, cfg => { cfg.MapFrom(jo => jo["status"]); });

                configuration.CreateMap<JToken, Order>()
                    .ForMember(o => o.OrderId, cfg => { cfg.MapFrom(jo => jo["orderId"]); })
                    .ForMember(o => o.Symbol, cfg => { cfg.MapFrom(jo => jo["symbol"]); })
                    .ForMember(o => o.Type, cfg => { cfg.MapFrom(jo => jo["type"]); })
                    .ForMember(o => o.Side, cfg => { cfg.MapFrom(jo => jo["side"]); })
                    .ForMember(o => o.TimeInForce, cfg => { cfg.MapFrom(jo => jo["timeInForce"]); })
                    .ForMember(o => o.Status, cfg => { cfg.MapFrom(jo => jo["status"]); })
                    .ForMember(o => o.Price, cfg => { cfg.MapFrom(jo => decimal.Parse(jo["price"].ToString())); })
                    .ForMember(o => o.StopPrice, cfg => { cfg.MapFrom(jo => decimal.Parse(jo["stopPrice"].ToString())); })
                    .ForMember(o => o.OriginalQuantity, cfg => { cfg.MapFrom(jo => decimal.Parse(jo["origQty"].ToString())); })
                    .ForMember(o => o.ExecutedQuantity, cfg => { cfg.MapFrom(jo => decimal.Parse(jo["executedQty"].ToString())); })
                    .ForMember(o => o.Date, cfg => { cfg.MapFrom(jo => Utilities.UnixTimeStampToDateTime(double.Parse(jo["time"].ToString()))); });

            });
        }
    }
}
