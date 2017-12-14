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

        public static void Initialize()
        {

            Mapper.Initialize(configuration =>
            {
                configuration.CreateMap<JToken, CurrencyInfo>()
                    .ForMember(o => o.Symbol, cfg => { cfg.MapFrom(jo => jo["baseAsset"]); })
                    .ForMember(o => o.BaseSymbol, cfg => { cfg.MapFrom(jo => jo["quoteAsset"]); })
                    .ForMember(o => o.Pair, cfg => { cfg.MapFrom(jo => jo["symbol"]); })
                    .ForMember(o => o.MinTradePrice, cfg => cfg.ResolveUsing(jo => MinTradeResolver(jo)));


                //cfg.CreateMap<Agreement, DMAgreement>()
                //.ForMember(o => o.AgreementStatus, o => o.Ignore())
                //.ForMember(o => o.AgreementType, o => o.Ignore());
                //cfg.CreateMap<DMAgreementDetail, AgreementDetail>().ReverseMap();
                //cfg.CreateMap<DMPaymentStatus, PaymentStatus>().ReverseMap();
                //cfg.CreateMap<DMAccountBalanceDistributionType, AccountBalanceDistributionType>().ReverseMap();
                //cfg.CreateMap<DMAgreementConfiguration, AgreementConfiguration>().ReverseMap();
                //cfg.CreateMap<DMAgreementConfigurationDataType, AgreementConfigurationDataType>().ReverseMap();
                //cfg.CreateMap<DMAgreementConfigurationChangeStatus, AgreementConfigurationChangeStatus>().ReverseMap();
                //cfg.CreateMap<DMStateConfigurationChange, ConfigurationChange>().ReverseMap();
                //cfg.CreateMap<DMStateConfigurationChangeApproval, ConfigurationChangeApproval>().ReverseMap();
                //cfg.CreateMap<DMAccountBalanceDistributionType, AccountBalanceDistributionType>().ReverseMap();
                //cfg.CreateMap<DMStateConfigurationChangeValue, ConfigurationChangeValue>().ReverseMap();
                //cfg.CreateMap<DMStateConfigurationValue, ConfigurationValue>().ReverseMap();
                //cfg.CreateMap<DMAgreementDetailStatus, AgreementDetailStatus>().ReverseMap();
                //cfg.CreateMap<DMAgreementStatus, AgreementStatus>().ReverseMap();
                //cfg.CreateMap<DMAgreementType, AgreementType>().ReverseMap();
                //cfg.CreateMap<DMConsumerToken, ConsumerToken>().ReverseMap();
                //cfg.CreateMap<DMCustomerAccountBalanceDistributionType, CustomerAccountBalanceDistributionType>().ReverseMap();
                //cfg.CreateMap<DMPayment, DTO.Payment.Payment>().ReverseMap();
                //cfg.CreateMap<DMPaymentProcessor, PaymentProcessor>().ReverseMap();
                //cfg.CreateMap<DMPaymentProcessorResponse, PaymentProcessorResponse>().ReverseMap();
                //cfg.CreateMap<DMPaymentType, PaymentType>().ReverseMap();
            });
        }
    }
}
