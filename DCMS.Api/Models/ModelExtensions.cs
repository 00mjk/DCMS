using DCMS.Api.Infrastructure.Mapper.Extensions;
using DCMS.Core;
using DCMS.Core.Domain.Products;
using DCMS.Services.Products;
using DCMS.ViewModel.Models.Products;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DCMS.Api.Models
{

    public static class Pexts
    {
        /// <summary>
        /// ������չ:��ʼ��Ʒģ�ͻ�������
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="product"></param>
        /// <param name="m"></param>
        /// <param name="wareHouseId">�����ָ���ֿ�,����ʾʵʱ�����</param>
        /// <param name="_productService"></param>
        /// <param name="terminalId">�û�ѡ����ն�Id</param>
        /// <returns></returns>
        public static T InitBaseModel<T>(this Product product, T m, int? wareHouseId,
            IList<SpecificationAttributeOption> allOptions,
            IList<ProductPrice> allProductPrices,
            IList<ProductTierPrice> allProductTierPrices,
            IProductService _productService,
            int terminalId = 0
            ) where T : ProductBaseModel
        {

            if (product == null)
            {
                throw new ArgumentNullException("product");
            }

            try
            {

                //��ȡ��� SmallProductPrices
                var option = product.GetProductUnit(allOptions, product.ProductPrices.ToList());

                option.smallOption.ConvertedQuantity = 1;
                option.strokOption.ConvertedQuantity = product.StockQuantity;
                option.bigOption.ConvertedQuantity = product.BigQuantity;

                option.smallOption.UnitConversion = string.Format("1{0} = {1}{2}", option.bigOption != null ? option.bigOption.Name : "/", product.BigQuantity, option.smallOption != null ? option.smallOption.Name : "/");
                option.strokOption.UnitConversion = string.Format("1{0} = {1}{2}", option.strokOption != null ? option.strokOption.Name : "/", product.StrokeQuantity, option.smallOption != null ? option.smallOption.Name : "/");
                option.bigOption.UnitConversion = string.Format("1{0} = {1}{2}", option.bigOption != null ? option.bigOption.Name : "/", product.BigQuantity, option.smallOption != null ? option.smallOption.Name : "/");

                var smallOption = option.smallOption;
                var strokOption = option.strokOption;
                var bigOption = option.bigOption;

                //��λ������
                m.BigQuantity = product.BigQuantity ?? 0;

                //�е�λ������
                m.StrokeQuantity = product.StrokeQuantity ?? 0;

                //��Ʒ����
                if (!string.IsNullOrEmpty(product.MnemonicCode))
                    m.ProductName = product.MnemonicCode;
                else
                    m.ProductName = product != null ? product.Name : "";

                //��Ʒ����(SKU)
                m.ProductSKU = product != null ? product.Sku : "";

                //����
                m.BarCode = product != null ? product.SmallBarCode : "";

                //��λת��
                m.UnitConversion = product.GetProductUnitConversion(allOptions);

                //��λ
                m.Units = product.GetProductUnits(allOptions);

                //�Ƿ��������
                m.IsAdjustPrice = product.IsAdjustPrice;

                //��λ��(Ĭ��С��λ)
                m.UnitId = smallOption.Id;
                m.UnitName = m.UnitId == 0 ? m.Units.Keys.Select(k => k).ToArray()[2] : m.Units.Where(q => q.Value == m.UnitId).Select(q => q.Key)?.FirstOrDefault();

                //ʵʱ�����(����)
                m.StockQty = product.GetProductUsableQuantity(wareHouseId ?? 0);

                //���ÿ��
                m.UsableQuantity = product.GetProductUsableQuantity(wareHouseId ?? 0);
                if (m.UsableQuantity > 0)
                {
                    m.UsableQuantityConversion = product.GetConversionFormat(allOptions, product.SmallUnitId, m.UsableQuantity ?? 0);
                }

                //�ֻ����
                m.CurrentQuantity = product.GetProductCurrentQuantity(wareHouseId ?? 0);
                if (m.CurrentQuantity > 0)
                {
                    m.CurrentQuantityConversion = product.GetConversionFormat(allOptions, product.SmallUnitId, m.CurrentQuantity ?? 0);
                }

                //Ԥռ���
                m.OrderQuantity = product.GetProductOrderQuantity(wareHouseId ?? 0);
                if (m.OrderQuantity > 0)
                {
                    m.OrderQuantityConversion = product.GetConversionFormat(allOptions, product.SmallUnitId, m.OrderQuantity ?? 0);
                }



                //��ȡ��λ
                var smallProductPrices = product.ProductPrices.Where(p => p.ProductId == product.Id && p.UnitId == product.SmallUnitId).FirstOrDefault();
                var strokeProductPrices = product.ProductPrices.Where(p => p.ProductId == product.Id && p.UnitId == (product.StrokeUnitId ?? 0)).FirstOrDefault();
                var bigProductPrices = product.ProductPrices.Where(p => p.ProductId == product.Id && p.UnitId == (product.BigUnitId ?? 0)).FirstOrDefault();

                m.SmallProductPrices = smallProductPrices != null ? smallProductPrices.ToModel<ProductPriceModel>() : new ProductPriceModel();
                m.StrokeProductPrices = strokeProductPrices != null ? strokeProductPrices.ToModel<ProductPriceModel>() : new ProductPriceModel();
                m.BigProductPrices = bigProductPrices != null ? bigProductPrices.ToModel<ProductPriceModel>() : new ProductPriceModel();
                m.smallOption = smallOption.ToModel<SpecificationAttributeOptionModel>();
                m.strokeOption = strokOption.ToModel<SpecificationAttributeOptionModel>();
                m.bigOption = bigOption.ToModel<SpecificationAttributeOptionModel>();


                //��λ�۸�
                m.Prices = new List<UnitPricesModel>()
                {
                    new UnitPricesModel() { UnitId = smallOption.Id, ProductPrice = option.smallPrice.ToModel<ProductPriceModel>(),UnitConversion= option.smallOption.UnitConversion },
                    new UnitPricesModel() { UnitId = strokOption.Id, ProductPrice = option.strokPrice.ToModel<ProductPriceModel>(),UnitConversion= option.strokOption.UnitConversion},
                    new UnitPricesModel() { UnitId = bigOption.Id, ProductPrice = option.bigPrice.ToModel<ProductPriceModel>(),UnitConversion = option.bigOption.UnitConversion}
                };

                #region ��Ʒ�۸���ϵ����μ۸� + �����۸�

                //��ǰ��Ʒ�Ĳ�μ۸�(���˵� �ϴ��ۼ�)
                var curProductAPTPrices = allProductTierPrices
                    .Where(s => s.ProductId == product.Id && s.PriceTypeId != (int)PriceType.LastedPrice).ToList();

                var productTierPriceModels = new List<ProductTierPriceModel>();

                var productTierPrices1 = curProductAPTPrices
                    .Where(s => s.ProductId == product.Id && s.PriceTypeId == (int)PriceType.ProductCost)
                    .FirstOrDefault() ?? new ProductTierPrice();

                var productTierPrices2 = curProductAPTPrices
                   .Where(s => s.ProductId == product.Id && s.PriceTypeId == (int)PriceType.WholesalePrice)
                   .FirstOrDefault() ?? new ProductTierPrice();

                var productTierPrices3 = curProductAPTPrices
                   .Where(s => s.ProductId == product.Id && s.PriceTypeId == (int)PriceType.RetailPrice)
                   .FirstOrDefault() ?? new ProductTierPrice();

                var productTierPrices4 = curProductAPTPrices
                   .Where(s => s.ProductId == product.Id && s.PriceTypeId == (int)PriceType.LowestPrice)
                   .FirstOrDefault() ?? new ProductTierPrice();

                var productTierPrices5 = curProductAPTPrices
                   .Where(s => s.ProductId == product.Id && s.PriceTypeId == (int)PriceType.CostPrice)
                   .FirstOrDefault() ?? new ProductTierPrice();

                var tierPrices = new List<ProductTierPrice>
                    {
                        //����
                        new ProductTierPrice()
                        {
                            Id= productTierPrices1?.Id??0,
                            StoreId = product.StoreId,
                            ProductId = product.Id,
                            PricesPlanId = 0,
                            PriceTypeId = (int)PriceType.ProductCost,
                            SmallUnitPrice = productTierPrices1?.SmallUnitPrice??0,
                            StrokeUnitPrice = productTierPrices1?.StrokeUnitPrice??0,
                            BigUnitPrice = productTierPrices1?.BigUnitPrice??0,
                        },
                        //����
                        new ProductTierPrice()
                        {
                            Id= productTierPrices2?.Id??0,
                            StoreId = product.StoreId,
                            ProductId = product.Id,
                            PricesPlanId = 0,
                            PriceTypeId = (int)PriceType.WholesalePrice,
                            SmallUnitPrice = productTierPrices2?.SmallUnitPrice??0,
                            StrokeUnitPrice = productTierPrices2?.StrokeUnitPrice??0,
                            BigUnitPrice = productTierPrices2?.BigUnitPrice??0,
                        },
                        //����
                        new ProductTierPrice()
                        {
                            Id= productTierPrices3?.Id??0,
                            StoreId = product.StoreId,
                            ProductId = product.Id,
                            PricesPlanId = 0,
                            PriceTypeId = (int)PriceType.RetailPrice,
                            SmallUnitPrice = productTierPrices3?.SmallUnitPrice??0,
                            StrokeUnitPrice = productTierPrices3?.StrokeUnitPrice??0,
                            BigUnitPrice = productTierPrices3?.BigUnitPrice??0,
                        },
                        //����ۼ�
                        new ProductTierPrice()
                        {
                            Id= productTierPrices4?.Id??0,
                            StoreId = product.StoreId,
                            ProductId = product.Id,
                            PricesPlanId = 0,
                            PriceTypeId = (int)PriceType.LowestPrice,
                            SmallUnitPrice = productTierPrices4?.SmallUnitPrice??0,
                            StrokeUnitPrice = productTierPrices4?.StrokeUnitPrice??0,
                            BigUnitPrice = productTierPrices4?.BigUnitPrice??0,
                        },
                        //�ɱ���
                        new ProductTierPrice()
                        {
                             Id= productTierPrices5?.Id??0,
                            StoreId = product.StoreId,
                            ProductId = product.Id,
                            PricesPlanId = 0,
                            PriceTypeId = (int)PriceType.CostPrice,
                            SmallUnitPrice = productTierPrices5?.SmallUnitPrice??0,
                            StrokeUnitPrice = productTierPrices5?.StrokeUnitPrice??0,
                            BigUnitPrice = productTierPrices5?.BigUnitPrice??0,
                        }
                    };

                foreach (var p in tierPrices)
                {
                    var model = p.ToModel<ProductTierPriceModel>();
                    model.StoreId = p.StoreId;
                    model.ProductId = p.ProductId;
                    model.PricesPlanId = p.PricesPlanId;
                    model.PriceTypeId = p.PriceTypeId;
                    model.PriceType = p.PriceType;
                    model.PriceTypeName = CommonHelper.GetEnumDescription<PriceType>(p.PriceType);

                    model.SmallUnitPrice = p.SmallUnitPrice;
                    model.StrokeUnitPrice = p.StrokeUnitPrice;
                    model.BigUnitPrice = p.BigUnitPrice;

                    model.SmallUnitId = product.SmallUnitId;
                    model.StrokeUnitId = product.StrokeUnitId ?? 0;
                    model.BigUnitId = product.BigUnitId ?? 0;

                    productTierPriceModels.Add(model);
                }


                //�Զ��巽��:��ǰ��Ʒ�ķ����۸�
                var curProductPlanPrices = allProductTierPrices
                    .Where(s => s.ProductId == product.Id && s.PriceTypeId == (int)PriceType.CustomPlan).ToList();

                var plans = _productService.GetAllPlansByStore(product.StoreId);
                foreach (var p in plans)
                {
                    var planPrice = curProductPlanPrices
                        .Where(s => s.ProductId == product.Id && s.PriceTypeId == (int)PriceType.CustomPlan && s.PricesPlanId == p.Id)
                        .FirstOrDefault() ?? new ProductTierPrice();

                    productTierPriceModels.Add(new ProductTierPriceModel()
                    {
                        StoreId = product.StoreId,
                        ProductId = product.Id,
                        PricesPlanId = p.Id,
                        PriceTypeId = (int)PriceType.CustomPlan,
                        PriceTypeName = p.Name,

                        SmallUnitId = product.SmallUnitId,
                        StrokeUnitId = product.StrokeUnitId ?? 0,
                        BigUnitId = product.BigUnitId ?? 0,

                        SmallUnitPrice = planPrice?.SmallUnitPrice ?? 0,
                        StrokeUnitPrice = planPrice?.StrokeUnitPrice ?? 0,
                        BigUnitPrice = planPrice?.BigUnitPrice ?? 0,
                        Id = planPrice?.Id ?? 0
                    });
                }

                #endregion

                //�ϴ��ۼ۵���ȡֵ RecentPrice ��
                var recentPrice = _productService.GetRecentPrice(product.StoreId, terminalId, product.Id);
                var lastedPriceModel = new ProductTierPriceModel
                {
                    StoreId = product.StoreId,
                    ProductId = product.Id,
                    PricesPlanId = 0,
                    PriceTypeId = (int)PriceType.LastedPrice,
                    PriceTypeName = "�ϴ��ۼ�",

                    SmallUnitId = product.SmallUnitId,
                    StrokeUnitId = product.StrokeUnitId ?? 0,
                    BigUnitId = product.BigUnitId ?? 0,

                    SmallUnitPrice = recentPrice?.SmallUnitPrice ?? 0,
                    StrokeUnitPrice = recentPrice?.StrokeUnitPrice ?? 0,
                    BigUnitPrice = recentPrice?.BigUnitPrice ?? 0,
                    Id = recentPrice?.Id ?? 0
                };
                productTierPriceModels.Add(lastedPriceModel);


                m.ProductTierPrices = productTierPriceModels;

                //���п��ÿ����
                m.StockQuantities = product.Stocks.Select(s =>
                {
                    return new StockQuantityModel
                    {
                        UsableQuantity = s.UsableQuantity ?? 0,
                        CurrentQuantity = s.CurrentQuantity ?? 0,
                        WareHouseId = s.WareHouseId
                    };
                }).ToList();


                return m;
            }
            catch (Exception ex)
            {
                throw new ArgumentNullException(ex.Message);
            }
        }

    }
}
