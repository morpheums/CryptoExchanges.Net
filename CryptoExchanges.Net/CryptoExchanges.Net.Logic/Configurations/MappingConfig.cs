using AutoMapper;
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

            });
        }
    }
}
