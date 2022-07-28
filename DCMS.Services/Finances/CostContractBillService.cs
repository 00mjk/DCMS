using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Data;
using DCMS.Core.Domain.Campaigns;
using DCMS.Core.Domain.Configuration;
using DCMS.Core.Domain.Finances;
using DCMS.Core.Domain.Products;
using DCMS.Core.Domain.Sales;
using DCMS.Core.Domain.Tasks;
using DCMS.Core.Domain.Terminals;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Campaigns;
using DCMS.Services.Events;
using DCMS.Services.Products;
using DCMS.Services.Tasks;
using DCMS.Services.Terminals;
using DCMS.Services.Users;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using DCMS.Services.Caching;
using System.Text.RegularExpressions;

namespace DCMS.Services.Finances
{
    /// <summary>
    /// ���ú�ͬ����
    /// </summary>
    public partial class CostContractBillService : BaseService, ICostContractBillService
    {
        private readonly IUserService _userService;
        private readonly IQueuedMessageService _queuedMessageService;
        private readonly ISpecificationAttributeService _specificationAttributeService;
        private readonly IProductService _productService;
        private readonly ICampaignService _campaignService;
        private readonly ITerminalService _terminalService;
        private readonly ICategoryService _categoryService;
        private readonly IGiveQuotaService _giveQuotaService;
        private readonly ICostExpenditureBillService _costExpenditureBillService;

        public CostContractBillService(IServiceGetter getter,
            IStaticCacheManager cacheManager,
            IEventPublisher eventPublisher,
            IUserService userService,
            IQueuedMessageService queuedMessageService,
            ISpecificationAttributeService specificationAttributeService,
            IProductService productService,
            ICampaignService campaignService,
            ITerminalService terminalService,
            ICategoryService categoryService,
            IGiveQuotaService giveQuotaService,
            ICostExpenditureBillService costExpenditureBillService
            ) : base(getter, cacheManager, eventPublisher)
        {
            _userService = userService;
            _queuedMessageService = queuedMessageService;
            _specificationAttributeService = specificationAttributeService;
            _productService = productService;
            _campaignService = campaignService;
            _terminalService = terminalService;
            _categoryService = categoryService;
            _giveQuotaService = giveQuotaService;
            _costExpenditureBillService = costExpenditureBillService;
        }


        #region ����
        public bool Exists(int billId)
        {
            return CostContractBillsRepository.TableNoTracking.Where(a => a.Id == billId).Count() > 0;
        }

        public virtual IPagedList<CostContractBill> GetAllCostContractBills(int? store, int? makeuserId, int? customerId, string customerName, int? employeeId, string billNumber = "", string remark = "", DateTime? start = null, DateTime? end = null, bool? deleted = null, int? accountingOptionId = 0, int? contractType = 0, int? cType = 0, bool? auditedStatus = null,  bool? showReverse = null, int pageIndex = 0, int pageSize = int.MaxValue)
        {

            if (pageSize >= 50)
                pageSize = 50;

            DateTime.TryParse(start?.ToString("yyyy-MM-dd 00:00:00"), out DateTime startDate);
            DateTime.TryParse(end?.ToString("yyyy-MM-dd 23:59:59"), out DateTime endDate);

            var query = from pc in CostContractBillsRepository.Table
                         .Include(cr => cr.Items)
                        select pc;


            if (store.HasValue)
            {
                query = query.Where(c => c.StoreId == store);
            }
            else
            {
                return null;
            }

            if (makeuserId.HasValue && makeuserId > 0)
            {
                var userIds = _userService.GetSubordinate(store, makeuserId ?? 0)?.Where(s => s > 0).ToList();
                if (userIds.Count > 0)
                    query = query.Where(x => userIds.Contains(x.MakeUserId));
            }

            if (customerId.HasValue && customerId.Value > 0)
            {
                query = query.Where(c => c.CustomerId == customerId);
            }

            //�ͻ����Ƽ���
            if (!string.IsNullOrEmpty(customerName))
            {
                var terminalIds = _terminalService.GetTerminalIds(store, customerName);
                query = query.Where(a => terminalIds.Contains(a.CustomerId));
            }

            if (employeeId.HasValue && employeeId.Value > 0)
            {
                query = query.Where(c => c.EmployeeId == employeeId);
            }

            if(accountingOptionId.HasValue && accountingOptionId.Value > 0)
            {
                query = query.Where(c => c.AccountingOptionId == accountingOptionId);
            }

            if (!string.IsNullOrWhiteSpace(billNumber))
            {
                query = query.Where(c => c.BillNumber.Contains(billNumber));
            }

            if (!string.IsNullOrWhiteSpace(remark))
            {
                query = query.Where(c => c.Remark.Contains(remark));
            }

            if (start.HasValue)
            {
                query = query.Where(o => startDate <= o.CreatedOnUtc);
            }

            if (end.HasValue)
            {
                query = query.Where(o => endDate >= o.CreatedOnUtc);
            }

            if (deleted.HasValue)
            {
                query = query.Where(o => o.Deleted == deleted);
            }

            if (contractType.HasValue)
            {
                query = query.Where(o => o.ContractType == contractType);
            }

            if (cType.HasValue)
            {
                query = query.Where(o => o.Items.Count(s=>s.CType== cType)>0);
            }


            //���״̬
            if (auditedStatus.HasValue)
            {
                query = query.Where(a => a.AuditedStatus == auditedStatus);
            }

            //���״̬
            if (showReverse.HasValue)
            {
                query = query.Where(a => a.ReversedStatus == showReverse);
            }

            query = query.OrderByDescending(c => c.CreatedOnUtc);

        
            return new PagedList<CostContractBill>(query, pageIndex, pageSize);
        }

        public IPagedList<CostContractBill> GetAllCostContractBills(int? store, int? userId, int? customerId, int? accountOptionId, int? accountCodeTypeId, int year,int month, int? contractType = 0, bool? auditedStatus = null, bool? showReverse = null, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;

            var query = from pc in CostContractBillsRepository.Table
                         .Include(cr => cr.Items)
                        select pc;


            if (store.HasValue)
            {
                query = query.Where(c => c.StoreId == store);
            }
            else
            {
                return null;
            }

            if (userId.HasValue && userId > 0)
            {
                var userIds = _userService.GetSubordinate(store, userId ?? 0)?.Where(s => s > 0).ToList();
                if (userIds.Count > 0)
                    query = query.Where(x => userIds.Contains(x.MakeUserId));
            }

            if (customerId.HasValue && customerId.Value > 0)
            {
                query = query.Where(c => c.CustomerId == customerId);
            }

            if (accountOptionId.HasValue && accountOptionId.Value > 0)
            {
                query = query.Where(c => c.AccountingOptionId == accountOptionId);
            }

            if (accountCodeTypeId.HasValue && accountCodeTypeId.Value > 0)
            {
                query = query.Where(c => c.AccountCodeTypeId == accountCodeTypeId);
            }

            if (contractType.HasValue)
            {
                query = query.Where(o => o.ContractType == contractType);
            }

            if (year > 0)
            {
                query = query.Where(o => o.Year == year);
            }

            if (month > 0)
            {
                query = query.Where(o => o.Month == month);
            }

            //���״̬
            if (auditedStatus.HasValue)
            {
                query = query.Where(a => a.AuditedStatus == auditedStatus);
            }

            //���״̬
            if (showReverse.HasValue)
            {
                query = query.Where(a => a.ReversedStatus == showReverse);
            }

            query = query.OrderByDescending(c => c.CreatedOnUtc);

            //��ҳ��
            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<CostContractBill>(plists, pageIndex, pageSize, totalCount);
        }

        public virtual IList<CostContractBill> GetAllCostContractBills()
        {
            var query = from c in CostContractBillsRepository.Table
                        orderby c.Id
                        select c;

            var categories = query.ToList();
            return categories;
        }

        public virtual CostContractBill GetCostContractBillById(int? store, int costContractBillId)
        {
            if (costContractBillId == 0)
            {
                return null;
            }

            var key = DCMSDefaults.COSTCONTRACTBILL_BY_ID_KEY.FillCacheKey(store ?? 0, costContractBillId);
            return _cacheManager.Get(key, () =>
            {
                return CostContractBillsRepository.ToCachedGetById(costContractBillId);
            });
        }

        public virtual CostContractBill GetCostContractBillByNumber(int? store, string billNumber)
        {
            var query = CostContractBillsRepository.Table;
            var bill = query.Where(a => a.StoreId == store && a.BillNumber == billNumber).FirstOrDefault();
            return bill;
        }

        public virtual CostContractBill GetCostContractBillById(int? store, int costContractBillId, bool isInclude = false)
        {
            if (costContractBillId == 0)
            {
                return null;
            }

            if (isInclude)
            {
                var query = CostContractBillsRepository.Table.Include(cb => cb.Items);
                return query.FirstOrDefault(c => c.Id == costContractBillId);
            }
            return CostContractBillsRepository.ToCachedGetById(costContractBillId);
        }

        public virtual void InsertCostContractBill(CostContractBill costContractBill)
        {
            if (costContractBill == null)
            {
                throw new ArgumentNullException("costContractBill");
            }

            var uow = CostContractBillsRepository.UnitOfWork;
            CostContractBillsRepository.Insert(costContractBill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(costContractBill);
        }

        public virtual void UpdateCostContractBill(CostContractBill costContractBill)
        {
            if (costContractBill == null)
            {
                throw new ArgumentNullException("costContractBill");
            }

            var uow = CostContractBillsRepository.UnitOfWork;
            CostContractBillsRepository.Update(costContractBill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(costContractBill);
        }

        public virtual void DeleteCostContractBill(CostContractBill costContractBill)
        {
            if (costContractBill == null)
            {
                throw new ArgumentNullException("costContractBill");
            }

            var uow = CostContractBillsRepository.UnitOfWork;
            CostContractBillsRepository.Delete(costContractBill);
            uow.SaveChanges();
            //event notification
            _eventPublisher.EntityDeleted(costContractBill);
        }


        #endregion

        #region ������Ŀ

        public virtual IPagedList<CostContractItem> GetCostContractItemsByCostContractBillId(int costContractBillId, int? userId, int? storeId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (costContractBillId == 0)
            {
                return new PagedList<CostContractItem>(new List<CostContractItem>(), pageIndex, pageSize);
            }

            var key = DCMSDefaults.COSTCONTRACTBILLITEM_ALL_KEY.FillCacheKey(storeId, costContractBillId, pageIndex, pageSize, userId);

            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CostContractItemsRepository.Table
                            where pc.CostContractBillId == costContractBillId
                            orderby pc.Id
                            select pc;
                //var productCostContractBills = new PagedList<CostContractItem>(query.ToList(), pageIndex, pageSize);
                //return productCostContractBills;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CostContractItem>(plists, pageIndex, pageSize, totalCount);
            });
        }

        public virtual IPagedList<CostContractItem> GetAllCostContractItems(int? storeId, int customerId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            var key = DCMSDefaults.COSTCONTRACTBILLITEMS_ALL_KEY.FillCacheKey(storeId, customerId, pageIndex, pageSize);

            return _cacheManager.Get(key, () =>
            {
                var query = from cc in CostContractBillsRepository.Table
                            join ci in CostContractItemsRepository.Table on cc.Id equals ci.CostContractBillId
                            where cc.CustomerId == customerId
                            orderby ci.Id
                            select ci;

                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CostContractItem>(plists, pageIndex, pageSize, totalCount);
            });
        }

        public virtual IList<CostContractItem> GetAllCostContractItems(int? storeId, int customerId)
        {
            var query = from cc in CostContractBillsRepository.Table
                        join ci in CostContractItemsRepository.Table on cc.Id equals ci.CostContractBillId
                        where cc.CustomerId == customerId
                        orderby ci.Id
                        select ci;
            return query.ToList();
        }

        public virtual IList<CostContractBill> GetAvailableCostContracts(int storeId, int customerId)
        {
            var key = DCMSDefaults.CAMPAIGN_GETTCOSTCONTRACTS.FillCacheKey(storeId, customerId);
            return _cacheManager.Get(key, () =>
            {
                int year = DateTime.Now.Year;
                var query = from a in CostContractBillsRepository.Table
                            where a.StoreId == storeId
                            && a.CustomerId == customerId
                            && a.Year == year
                            && a.Items.Count > 0
                            select a;

                return query.ToList();

            });
        }

        public virtual IList<CostContractBill> GetAvailableCostContracts(int storeId, int customerId, int businessUserId)
        {
            var key = DCMSDefaults.CAMPAIGN_GETTCOSTCONTRACTS_STOREID_CUSTOMERID.FillCacheKey(storeId, customerId);
            return _cacheManager.Get(key, () =>
            {
                int year = DateTime.Now.Year;
                var query = from a in CostContractBillsRepository.Table
                            .Include(cc => cc.Items)
                            where a.StoreId == storeId
                            && a.CustomerId == customerId
                            //&& a.EmployeeId == businessUserId //��ʱ�涨��������ҵ��Ա
                            && a.Year == year
                            && a.Items.Count > 0
                            select a;

                return query.ToList();

            });
        }


        public virtual IList<CostContractBill> GetCostContractBillsByIds(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return new List<CostContractBill>();
            }

            var query = from c in CostContractBillsRepository.Table
                        where ids.Contains(c.Id)
                        select c;
            var list = query.ToList();

            var result = new List<CostContractBill>();
            foreach (int id in ids)
            {
                var model = list.Find(x => x.Id == id);
                if (model != null)
                {
                    result.Add(model);
                }
            }
            return result;
        }
        public virtual IList<CostContractItem> GetCostContractItemsByIds(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return new List<CostContractItem>();
            }

            var query = from c in CostContractItemsRepository.Table
                        where ids.Contains(c.Id)
                        select c;
            var list = query.ToList();

            var result = new List<CostContractItem>();
            foreach (int id in ids)
            {
                var model = list?.Find(x => x.Id == id);

                if (model != null)
                {
                    var contracts = GetCostContractBillById(model.StoreId, model.CostContractBillId, true);
                    //�������
                    var item = CalcCostContractBalances(model.StoreId, contracts.CustomerId, contracts);

                    result.AddRange(item);
                }
            }
            return result;
        }

        public virtual CostContractItem GetCostContractItemById(int? store, int costContractItemId)
        {
            if (costContractItemId == 0)
            {
                return null;
            }

            return CostContractItemsRepository.ToCachedGetById(costContractItemId);
        }

        public virtual void InsertCostContractItem(CostContractItem costContractItem)
        {
            if (costContractItem == null)
            {
                throw new ArgumentNullException("costContractItem");
            }

            var uow = CostContractItemsRepository.UnitOfWork;
            CostContractItemsRepository.Insert(costContractItem);
            uow.SaveChanges();
            //֪ͨ
            _eventPublisher.EntityInserted(costContractItem);
        }

        public virtual void UpdateCostContractItem(CostContractItem costContractItem)
        {
            if (costContractItem == null)
            {
                throw new ArgumentNullException("costContractItem");
            }

            var uow = CostContractItemsRepository.UnitOfWork;
            CostContractItemsRepository.Update(costContractItem);
            uow.SaveChanges();
            //֪ͨ
            _eventPublisher.EntityUpdated(costContractItem);
        }

        public virtual void DeleteCostContractItem(CostContractItem costContractItem)
        {
            if (costContractItem == null)
            {
                throw new ArgumentNullException("costContractItem");
            }

            var uow = CostContractItemsRepository.UnitOfWork;
            CostContractItemsRepository.Delete(costContractItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(costContractItem);
        }


        #endregion

        public void UpdateCostContractBillActive(int? store, int? billId, int? user)
        {
            var query = CostContractBillsRepository.Table.ToList();

            query = query.Where(x => x.StoreId == store && x.MakeUserId == user && x.AuditedStatus == true && (DateTime.Now.Subtract(x.AuditedDate ?? DateTime.Now).Duration().TotalDays > 30)).ToList();

            if (billId.HasValue && billId.Value > 0)
            {
                query = query.Where(x => x.Id == billId).ToList();
            }

            var result = query;

            if (result != null && result.Count > 0)
            {
                var uow = CostContractBillsRepository.UnitOfWork;
                foreach (CostContractBill bill in result)
                {
                    if ((bill.AuditedStatus && !bill.ReversedStatus) || bill.Deleted) continue;
                    bill.Deleted = true;
                    CostContractBillsRepository.Update(bill);
                }
                uow.SaveChanges();
            }
        }


        #region ��Ʒ�߼�
        /// <summary>
        /// ��֤���۵���Ʒ
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="terminalId"></param>
        /// <param name="items"></param>
        /// <param name="errMsg"></param>
        /// <returns></returns>
        public virtual bool CheckGift(int storeId, int terminalId, List<SaleItem> items, out string errMsg)
        {
            errMsg = string.Empty;
            try
            {
                if (items == null || items.Count == 0 || items.Where(it => it.SaleProductTypeId > 0).Count() == 0)
                {
                    return true;
                }
                items = items.Where(it => it.SaleProductTypeId > 0).ToList();

                //�����
                List<int> campaignIds = items.Select(it => it.CampaignId ?? 0).Distinct().ToList();
                List<Campaign> allCampaigns = _campaignService.GetCampaignsByIds(campaignIds.ToArray()).ToList();

                //���������
                List<CampaignBuyProduct> allCampaignBuyProducts = _campaignService.GetCampaignBuyByCampaignIds(campaignIds.ToArray()).ToList();
                //���������
                List<CampaignGiveProduct> allCampaignGiveProducts = _campaignService.GetCampaignGiveByCampaignIds(campaignIds.ToArray()).ToList();

                //���д������Ʒ
                List<int> productIds = new List<int>();
                productIds.AddRange(items.Select(it => it.ProductId).Distinct().ToList());
                productIds.AddRange(allCampaignBuyProducts.Select(it => it.ProductId).Distinct().ToList());
                productIds.AddRange(allCampaignGiveProducts.Select(it => it.ProductId).Distinct().ToList());
                List<Product> allProducts = _productService.GetProductsByIds(storeId, productIds.ToArray()).ToList();
                var allOptions = _specificationAttributeService.GetSpecificationAttributeOptionByIds(storeId, allProducts.GetProductBigStrokeSmallUnitIds());

                //���ú�ͬ
                List<CostContractBill> allCostContractBills = GetCostContractBillsByIds(items.Where(it => it.GiveTypeId == (int)GiveTypeEnum.Contract).Select(it => it.CostContractId ?? 0).Distinct().ToArray()).ToList();

                //���ú�ͬ��ϸ
                List<CostContractItem> allCostContractItems = GetCostContractItemsByIds(items.Where(it => it.GiveTypeId == (int)GiveTypeEnum.Contract).Select(it => it.CostContractItemId ?? 0).Distinct().ToArray()).ToList();

                Terminal terminal = _terminalService.GetTerminalById(storeId, terminalId);
                if (allCampaigns != null && allCampaigns.Count > 0)
                {
                    foreach (var campaigns in allCampaigns)
                    {
                        if (campaigns.CampaignChannels == null || campaigns.CampaignChannels.Count == 0 || campaigns.CampaignChannels.Where(cc => cc.ChannelId == terminal.ChannelId).Count() == 0)
                        {
                            errMsg = $"�����������������ǰ�ͻ�����";
                            return false;
                        }
                    }
                }

                if (allCostContractBills != null && allCostContractBills.Count > 0 && allCostContractBills.Where(acb => acb.CustomerId != terminalId).Count() > 0)
                {
                    errMsg = $"��Ʒ���ú�ͬ�д��ڲ�������ǰ�ͻ�����Ʒ";
                    return false;
                }

                foreach (var item in items)
                {
                    if (item.SaleProductTypeId != null)
                    {
                        Product product = allProducts.Where(ap => ap.Id == item.ProductId).FirstOrDefault();
                        if (product == null)
                        {
                            errMsg = $"��Ʒ������δ�ҵ�IdΪ{item.Id}��������Ʒ";
                            return false;
                        }

                        //�����������Ʒ
                        if (item.SaleProductTypeId == (int)SaleProductTypeEnum.CampaignGiveProduct)
                        {
                            //��ȡ��ǰ�����������Ʒ
                            CampaignGiveProduct campaignGiveProduct = allCampaignGiveProducts.Where(acb => acb.ProductId == item.CampaignGiveProductId).FirstOrDefault();
                            if (campaignGiveProduct == null)
                            {
                                errMsg = $"������Ʒ{product.Name},�ڴ������δ�ҵ�";
                                return false;
                            }

                            //��ȡ��Ʒ����������Ʒ
                            List<CampaignBuyProduct> campaignBuyProducts = allCampaignBuyProducts.Where(acb => acb.CampaignId == item.CampaignId).ToList();
                            if (campaignBuyProducts == null || campaignBuyProducts.Count == 0)
                            {
                                errMsg = $"������Ʒ{item.ProductName},�ڴ������δ�ҵ�����������Ʒ��Ϣ";
                                return false;
                            }

                            foreach (var campaignBuyProduct in campaignBuyProducts)
                            {

                                Product productBuy = allProducts.Where(ap => ap.Id == campaignBuyProduct.ProductId).FirstOrDefault();
                                if (productBuy == null)
                                {
                                    errMsg = $"δ�ҵ���Ӧ������Ʒ";
                                    return false;
                                }

                                //��ȡ��ǰ������Ʒ ����������Ʒ
                                var buyItem = items.Where(it => it.CampaignLinkNumber == item.CampaignLinkNumber && it.SaleProductTypeId == (int)SaleProductTypeEnum.CampaignBuyProduct && it.CampaignBuyProductId == campaignBuyProduct.ProductId).FirstOrDefault();

                                if (buyItem == null)
                                {
                                    errMsg = $"������Ʒ{item.ProductName}���ڵ���������Ʒ��,δ�ҵ���Ӧ������Ʒ{productBuy.Name}";
                                    return false;
                                }

                                //������������� ��С��λ
                                var buyConversionQuantity = productBuy.GetConversionQuantity(allOptions, campaignBuyProduct.UnitId ?? 0);
                                int buyQuantity = campaignBuyProduct.Quantity * buyConversionQuantity;

                                //������������� ��С��λ
                                var giveConversionQuantity = product.GetConversionQuantity(allOptions, campaignGiveProduct.UnitId ?? 0);
                                int giveQuantity = campaignGiveProduct.Quantity * giveConversionQuantity;

                                //��ǰ��������
                                var thisBuyConversionQuantity = productBuy.GetConversionQuantity(allOptions, buyItem.UnitId);
                                int thisBuyQuantity = buyItem.Quantity * thisBuyConversionQuantity;
                                //��ǰ��������
                                var thisGiveConversionQuantity = product.GetConversionQuantity(allOptions, item.UnitId);
                                int thisGiveQuantity = item.Quantity * thisGiveConversionQuantity;

                                //��ǰ�������������������
                                var maxGive = (thisBuyQuantity / buyQuantity) * giveQuantity;
                                if (thisGiveQuantity > maxGive)
                                {
                                    string thisBuyFormat = productBuy.GetConversionFormat(allOptions, productBuy.SmallUnitId, thisBuyQuantity);
                                    string maxGiveFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, maxGive);
                                    if (maxGive == 0)
                                    {
                                        maxGiveFormat = "0";
                                    }
                                    errMsg = $"��ǰ������Ʒ��{productBuy.Name},����������{thisBuyFormat}��������Ʒ��{product.Name}�������������{maxGiveFormat}";
                                    return false;
                                }
                            }

                        }
                        //���ú�ͬ���¶Ҹ�
                        else if (item.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractByMonth)
                        {
                            //���ú�ͬ
                            var costContractItem = allCostContractItems.Where(acci => acci.Id == item.CostContractItemId).FirstOrDefault();
                            if (costContractItem == null)
                            {
                                errMsg = $"δ�ҵ����ú�ͬ";
                                return false;
                            }

                            //ע����ͬ��Ʒ���ú�ͬ��ֹ�ظ���ӣ�Ҫ������֤
                            var thisItems = items.Where(it => it.CostContractItemId == item.CostContractItemId && it.CostContractMonth == item.CostContractMonth).ToList();

                            //���ú�ͬΪ ��Ʒ
                            if (costContractItem.CType == null || costContractItem.CType == 0)
                            {

                                if (thisItems == null || thisItems.Count == 0)
                                {
                                    errMsg = $"��Ʒ�ڷ��ú�ͬ��δ�ҵ�";
                                    return false;
                                }

                                //��ǰ�·ݵ�����
                                var monthQuantity = 0;
                                switch (item.CostContractMonth)
                                {
                                    case 1:
                                        monthQuantity = costContractItem.Jan_Balance == null ? 0 : (int)costContractItem.Jan_Balance;
                                        break;
                                    case 2:
                                        monthQuantity = costContractItem.Feb_Balance == null ? 0 : (int)costContractItem.Feb_Balance;
                                        break;
                                    case 3:
                                        monthQuantity = costContractItem.Mar_Balance == null ? 0 : (int)costContractItem.Mar_Balance;
                                        break;
                                    case 4:
                                        monthQuantity = costContractItem.Apr_Balance == null ? 0 : (int)costContractItem.Apr_Balance;
                                        break;
                                    case 5:
                                        monthQuantity = costContractItem.May_Balance == null ? 0 : (int)costContractItem.May_Balance;
                                        break;
                                    case 6:
                                        monthQuantity = costContractItem.Jun_Balance == null ? 0 : (int)costContractItem.Jun_Balance;
                                        break;
                                    case 7:
                                        monthQuantity = costContractItem.Jul_Balance == null ? 0 : (int)costContractItem.Jul_Balance;
                                        break;
                                    case 8:
                                        monthQuantity = costContractItem.Aug_Balance == null ? 0 : (int)costContractItem.Aug_Balance;
                                        break;
                                    case 9:
                                        monthQuantity = costContractItem.Sep_Balance == null ? 0 : (int)costContractItem.Sep_Balance;
                                        break;
                                    case 10:
                                        monthQuantity = costContractItem.Oct_Balance == null ? 0 : (int)costContractItem.Oct_Balance;
                                        break;
                                    case 11:
                                        monthQuantity = costContractItem.Nov_Balance == null ? 0 : (int)costContractItem.Nov_Balance;
                                        break;
                                    case 12:
                                        monthQuantity = costContractItem.Dec_Balance == null ? 0 : (int)costContractItem.Dec_Balance;
                                        break;
                                    default:
                                        break;

                                }
                                if (monthQuantity <= 0)
                                {
                                    errMsg = $"��Ʒ�ڷ��ú�ͬ������";
                                    return false;
                                }
                                //ת������С��λ���� ע�������ʣ������������С��λ����
                                //var costConversionQuantity = product.GetConversionQuantity(costContractItem.UnitId ?? 0, _specificationAttributeService, _productService);
                                //���ú�ͬ��������
                                //int costQuantity = product.GetConversionQuantity(allOptions, costContractItem.UnitId ?? 0) * monthQuantity;

                                //��ǰ����������
                                var thisQuantity = 0;
                                foreach (var tit in thisItems)
                                {
                                    //��ֹ�û�ѡ��������Ʒ
                                    if (tit.ProductId != item.ProductId)
                                    {
                                        errMsg = $"��Ʒ���ڵ�ǰ���ú�ͬ��";
                                        return false;
                                    }
                                    var titConversionQuantity = product.GetConversionQuantity(allOptions, tit.UnitId);
                                    thisQuantity += titConversionQuantity * tit.Quantity;
                                }
                                if (thisQuantity > monthQuantity)
                                {
                                    string thisFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, thisQuantity);
                                    string costFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, monthQuantity);
                                    if (monthQuantity == 0)
                                    {
                                        costFormat = "0";
                                    }
                                    errMsg = $"��ǰ������Ʒ�ڷ��ú�ͬ������������";
                                    return false;
                                }
                            }
                            //���ú�ͬΪ �ֽ�
                            else if (costContractItem.CType == 1)
                            {
                                //��ǰ�·ݵ�����
                                decimal monthAmount = 0;
                                switch (item.CostContractMonth)
                                {
                                    case 1:
                                        monthAmount = costContractItem.Jan_Balance ?? 0;
                                        break;
                                    case 2:
                                        monthAmount = costContractItem.Feb_Balance ?? 0;
                                        break;
                                    case 3:
                                        monthAmount = costContractItem.Mar_Balance ?? 0;
                                        break;
                                    case 4:
                                        monthAmount = costContractItem.Apr_Balance ?? 0;
                                        break;
                                    case 5:
                                        monthAmount = costContractItem.May_Balance ?? 0;
                                        break;
                                    case 6:
                                        monthAmount = costContractItem.Jun_Balance ?? 0;
                                        break;
                                    case 7:
                                        monthAmount = costContractItem.Jul_Balance ?? 0;
                                        break;
                                    case 8:
                                        monthAmount = costContractItem.Aug_Balance ?? 0;
                                        break;
                                    case 9:
                                        monthAmount = costContractItem.Sep_Balance ?? 0;
                                        break;
                                    case 10:
                                        monthAmount = costContractItem.Oct_Balance ?? 0;
                                        break;
                                    case 11:
                                        monthAmount = costContractItem.Nov_Balance ?? 0;
                                        break;
                                    case 12:
                                        monthAmount = costContractItem.Dec_Balance ?? 0;
                                        break;
                                    default:
                                        break;

                                }
                                if (monthAmount <= 0)
                                {
                                    errMsg = $"���ú�ͬ�е�������";
                                    return false;
                                }
                                if (thisItems == null || thisItems.Count == 0)
                                {
                                    errMsg = $"��Ʒ�ڷ��ú�ͬ��δ�ҵ�";
                                    return false;
                                }

                                decimal thisAmount = 0;
                                foreach (var tit in thisItems)
                                {
                                    Product thisProduct = allProducts.Where(ap => ap.Id == tit.ProductId).FirstOrDefault();
                                    if (thisProduct == null)
                                    {
                                        errMsg = $"������Ʒ������";
                                        return false;
                                    }
                                    if (thisProduct.ProductPrices != null && thisProduct.ProductPrices.Count > 0)
                                    {
                                        ProductPrice productPrice = thisProduct.ProductPrices.Where(pp => pp.UnitId == tit.UnitId).FirstOrDefault();
                                        if (productPrice != null)
                                        {
                                            thisAmount += (productPrice.TradePrice ?? 0) * tit.Quantity;
                                        }
                                    }
                                }
                                if (thisAmount > monthAmount)
                                {
                                    errMsg = $"���ú�ͬʹ�ö�Ȳ���";
                                    return false;
                                }

                            }

                        }
                        //���ú�ͬ����λ���ܼƶҸ�
                        else if (item.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractUnitQuantity)
                        {
                            //ע����ͬ��Ʒ���ú�ͬ��ֹ�ظ���ӣ�Ҫ������֤
                            var thisItems = items.Where(it => it.CostContractItemId == item.CostContractItemId && it.ProductId == item.ProductId).ToList();
                            if (thisItems == null || thisItems.Count == 0)
                            {
                                errMsg = $"��Ʒ�ڷ��ú�ͬ��δ�ҵ�";
                                return false;
                            }
                            var costContractItem = allCostContractItems.Where(acci => acci.Id == item.CostContractItemId && acci.ProductId == item.ProductId).FirstOrDefault();
                            if (costContractItem == null)
                            {
                                errMsg = $"��Ʒδ�ҵ����ú�ͬ";
                                return false;
                            }
                            //���ú�ͬΪ ��Ʒ
                            if (costContractItem.CType == null || costContractItem.CType == 0)
                            {
                                //����ʣ��
                                var totalQuantity = costContractItem.Total_Balance == null ? 0 : (int)costContractItem.Total_Balance;
                                if (totalQuantity <= 0)
                                {
                                    errMsg = $"��Ʒ�ڷ��ú�ͬ������";
                                    return false;
                                }
                                //ת������С��λ���� ע�������ʣ������������С��λ����
                                //var costConversionQuantity = product.GetConversionQuantity(costContractItem.UnitId ?? 0, _specificationAttributeService, _productService);
                                //���ú�ͬ��������
                                int costQuantity = totalQuantity;

                                //��ǰ����������
                                var thisQuantity = 0;
                                thisItems.ForEach(tit =>
                                {
                                    var titConversionQuantity = product.GetConversionQuantity(allOptions, tit.UnitId);
                                    thisQuantity += titConversionQuantity * tit.Quantity;
                                });
                                if (thisQuantity > costQuantity)
                                {
                                    string thisFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, thisQuantity);
                                    string costFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, costQuantity);
                                    if (costQuantity == 0)
                                    {
                                        costFormat = "0";
                                    }
                                    errMsg = $"��ǰ������Ʒ�ڷ��ú�ͬ������������";
                                    return false;
                                }
                            }
                            //���ú�ͬΪ �ֽ�
                            else if (costContractItem.CType == 1)
                            {
                                //����ʣ��
                                var totalAmount = costContractItem.Total_Balance ?? 0;
                                if (totalAmount <= 0)
                                {
                                    errMsg = $"���ú�ͬ������";
                                    return false;
                                }
                                decimal thisAmount = 0;
                                foreach (var tit in thisItems)
                                {
                                    Product thisProduct = allProducts.Where(ap => ap.Id == tit.ProductId).FirstOrDefault();
                                    if (thisProduct == null)
                                    {
                                        errMsg = $"������Ʒ������";
                                        return false;
                                    }
                                    if (thisProduct.ProductPrices != null && thisProduct.ProductPrices.Count > 0)
                                    {
                                        ProductPrice productPrice = thisProduct.ProductPrices.Where(pp => pp.UnitId == tit.UnitId).FirstOrDefault();
                                        if (productPrice != null)
                                        {
                                            thisAmount += (productPrice.TradePrice ?? 0) * tit.Quantity;
                                        }
                                    }
                                }
                                if (thisAmount > totalAmount)
                                {
                                    errMsg = $"���ú�ͬ��ʹ�ö�Ȳ���";
                                    return false;
                                }
                            }

                        }
                        //���ú�ͬ��������Ʒ�ۼ�
                        else if (item.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractManageGift)
                        {
                            //ע����ͬ��Ʒ���ú�ͬ��ֹ�ظ���ӣ�Ҫ������֤
                            var thisItems = items.Where(it => it.CostContractItemId == item.CostContractItemId && it.ProductId == item.ProductId).ToList();
                            if (thisItems == null || thisItems.Count == 0)
                            {
                                errMsg = $"��Ʒ�ڷ��ú�ͬ��δ�ҵ�";
                                return false;
                            }
                            var costContractItem = allCostContractItems.Where(acci => acci.Id == item.CostContractItemId && acci.ProductId == item.ProductId).FirstOrDefault();
                            if (costContractItem == null)
                            {
                                errMsg = $"��Ʒδ�ҵ����ú�ͬ";
                                return false;
                            }
                            //���ú�ͬΪ ��Ʒ
                            if (costContractItem.CType == null || costContractItem.CType == 0)
                            {
                                //����ʣ��
                                var totalQuantity = costContractItem.Total_Balance == null ? 0 : (int)costContractItem.Total_Balance;
                                if (totalQuantity <= 0)
                                {
                                    errMsg = $"��Ʒ�ڷ��ú�ͬ������";
                                    return false;
                                }
                                //ת������С��λ���� ע�������ʣ������������С��λ����
                                //var costConversionQuantity = product.GetConversionQuantity(costContractItem.UnitId ?? 0, _specificationAttributeService, _productService);
                                //���ú�ͬ��������
                                int costQuantity = totalQuantity;

                                //��ǰ����������
                                var thisQuantity = 0;
                                thisItems.ForEach(tit =>
                                {
                                    var titConversionQuantity = product.GetConversionQuantity(allOptions, tit.UnitId);
                                    thisQuantity += titConversionQuantity * tit.Quantity;
                                });
                                if (thisQuantity > costQuantity)
                                {
                                    string thisFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, thisQuantity);
                                    string costFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, costQuantity);
                                    if (costQuantity == 0)
                                    {
                                        costFormat = "0";
                                    }
                                    errMsg = $"��ǰ������Ʒ�ڷ��ú�ͬ������������";
                                    return false;
                                }
                            }
                            //���ú�ͬΪ �ֽ�
                            else if (costContractItem.CType == 1)
                            {
                                //����ʣ��
                                var totalAmount = costContractItem.Total_Balance ?? 0;
                                if (totalAmount <= 0)
                                {
                                    errMsg = $"���ú�ͬ������";
                                    return false;
                                }
                                decimal thisAmount = 0;
                                foreach (var tit in thisItems)
                                {
                                    Product thisProduct = allProducts.Where(ap => ap.Id == tit.ProductId).FirstOrDefault();
                                    if (thisProduct == null)
                                    {
                                        errMsg = $"������Ʒ������";
                                        return false;
                                    }
                                    if (thisProduct.ProductPrices != null && thisProduct.ProductPrices.Count > 0)
                                    {
                                        ProductPrice productPrice = thisProduct.ProductPrices.Where(pp => pp.UnitId == tit.UnitId).FirstOrDefault();
                                        if (productPrice != null)
                                        {
                                            thisAmount += (productPrice.TradePrice ?? 0) * tit.Quantity;
                                        }
                                    }
                                }
                                if (thisAmount > totalAmount)
                                {
                                    errMsg = $"���ú�ͬ��ʹ�ö�Ȳ���";
                                    return false;
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errMsg = ex.ToString();
                return false;
            }

            return true;
        }

        /// <summary>
        /// ��֤���۶�����Ʒ
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="terminalId"></param>
        /// <param name="items"></param>
        /// <param name="errMsg"></param>
        /// <returns></returns>
        public virtual bool CheckGift(int storeId, int terminalId, List<SaleReservationItem> items, out string errMsg)
        {
            errMsg = string.Empty;
            try
            {
                if (items == null || items.Count == 0 || items.Where(it => it.SaleProductTypeId > 0).Count() == 0)
                {
                    return true;
                }
                items = items.Where(it => it.SaleProductTypeId > 0).ToList();

                //�����
                List<int> campaignIds = items.Select(it => it.CampaignId ?? 0).Distinct().ToList();
                List<Campaign> allCampaigns = _campaignService.GetCampaignsByIds(campaignIds.ToArray()).ToList();

                //���������
                List<CampaignBuyProduct> allCampaignBuyProducts = _campaignService.GetCampaignBuyByCampaignIds(campaignIds.ToArray()).ToList();
                //���������
                List<CampaignGiveProduct> allCampaignGiveProducts = _campaignService.GetCampaignGiveByCampaignIds(campaignIds.ToArray()).ToList();

                //���д������Ʒ
                List<int> productIds = new List<int>();
                productIds.AddRange(items.Select(it => it.ProductId).Distinct().ToList());
                productIds.AddRange(allCampaignBuyProducts.Select(it => it.ProductId).Distinct().ToList());
                productIds.AddRange(allCampaignGiveProducts.Select(it => it.ProductId).Distinct().ToList());
                List<Product> allProducts = _productService.GetProductsByIds(storeId, productIds.ToArray()).ToList();
                var allOptions = _specificationAttributeService.GetSpecificationAttributeOptionByIds(storeId, allProducts.GetProductBigStrokeSmallUnitIds());

                //���ú�ͬ
                List<CostContractBill> allCostContractBills = GetCostContractBillsByIds(items.Where(it => it.GiveTypeId == (int)GiveTypeEnum.Contract).Select(it => it.CostContractId ?? 0).Distinct().ToArray()).ToList();

                //���ú�ͬ��ϸ
                List<CostContractItem> allCostContractItems = GetCostContractItemsByIds(items.Where(it => it.GiveTypeId == (int)GiveTypeEnum.Contract).Select(it => it.CostContractItemId ?? 0).Distinct().ToArray()).ToList();

                if (allCostContractBills != null && allCostContractBills.Count > 0 && allCostContractBills.Where(acb => acb.CustomerId != terminalId).Count() > 0)
                {
                    errMsg = $"��Ʒ��Ϣ�д��ڲ�������ǰ�ͻ�����Ʒ";
                    return false;
                }

                //var _specificationAttributeService = EngineContext.Current.Resolve<ISpecificationAttributeService>();
                //var _productService = EngineContext.Current.Resolve<IProductService>();

                foreach (var item in items)
                {
                    if (item.SaleProductTypeId != null)
                    {
                        Product product = allProducts.Where(ap => ap.Id == item.ProductId).FirstOrDefault();
                        if (product == null)
                        {
                            errMsg = $"��Ʒ������δ�ҵ�IdΪ{item.Id}��������Ʒ";
                            return false;
                        }

                        //�����������Ʒ
                        if (item.SaleProductTypeId == (int)SaleProductTypeEnum.CampaignGiveProduct)
                        {
                            //��ȡ��ǰ�����������Ʒ
                            CampaignGiveProduct campaignGiveProduct = allCampaignGiveProducts.Where(acb => acb.ProductId == item.CampaignGiveProductId).FirstOrDefault();

                            if (campaignGiveProduct == null)
                            {
                                errMsg = $"������Ʒ{product.Name},�ڴ������δ�ҵ�";
                                return false;
                            }

                            //��ȡ��Ʒ����������Ʒ
                            List<CampaignBuyProduct> campaignBuyProducts = allCampaignBuyProducts.Where(acb => acb.CampaignId == item.CampaignId).ToList();
                            if (campaignBuyProducts == null || campaignBuyProducts.Count == 0)
                            {
                                errMsg = $"������Ʒ{item.ProductName},�ڴ������δ�ҵ�����������Ʒ��Ϣ";
                                return false;
                            }

                            foreach (var campaignBuyProduct in campaignBuyProducts)
                            {

                                Product productBuy = allProducts.Where(ap => ap.Id == campaignBuyProduct.ProductId).FirstOrDefault();
                                if (productBuy == null)
                                {
                                    errMsg = $"δ�ҵ���Ӧ������Ʒ";
                                    return false;
                                }

                                //��ȡ��ǰ������Ʒ ����������Ʒ
                                var buyItem = items.Where(it => it.CampaignLinkNumber == item.CampaignLinkNumber && it.SaleProductTypeId == (int)SaleProductTypeEnum.CampaignBuyProduct && it.CampaignBuyProductId == campaignBuyProduct.ProductId).FirstOrDefault();

                                if (buyItem == null)
                                {
                                    errMsg = $"������Ʒ{item.ProductName}���ڵ���������Ʒ��,δ�ҵ���Ӧ������Ʒ{productBuy.Name}";
                                    return false;
                                }

                                //������������� ��С��λ
                                var buyConversionQuantity = productBuy.GetConversionQuantity(allOptions, campaignBuyProduct.UnitId ?? 0);
                                int buyQuantity = campaignBuyProduct.Quantity * buyConversionQuantity;

                                //������������� ��С��λ
                                var giveConversionQuantity = product.GetConversionQuantity(allOptions, campaignGiveProduct.UnitId ?? 0);
                                int giveQuantity = campaignGiveProduct.Quantity * giveConversionQuantity;

                                //��ǰ��������
                                var thisBuyConversionQuantity = productBuy.GetConversionQuantity(allOptions, buyItem.UnitId);
                                int thisBuyQuantity = buyItem.Quantity * thisBuyConversionQuantity;
                                //��ǰ��������
                                var thisGiveConversionQuantity = product.GetConversionQuantity(allOptions, item.UnitId);
                                int thisGiveQuantity = item.Quantity * thisGiveConversionQuantity;

                                //��ǰ�������������������
                                var maxGive = (thisBuyQuantity / buyQuantity) * giveQuantity;
                                if (thisGiveQuantity > maxGive)
                                {
                                    string thisBuyFormat = productBuy.GetConversionFormat(allOptions, productBuy.SmallUnitId, thisBuyQuantity);
                                    string maxGiveFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, maxGive);
                                    if (maxGive == 0)
                                    {
                                        maxGiveFormat = "0";
                                    }
                                    errMsg = $"��ǰ������Ʒ��{productBuy.Name},����������{thisBuyFormat}��������Ʒ��{product.Name}�������������{maxGiveFormat}";
                                    return false;
                                }
                            }

                        }
                        //���ú�ͬ���¶Ҹ�
                        else if (item.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractByMonth)
                        {
                            //���ú�ͬ
                            var costContractItem = allCostContractItems.Where(acci => acci.Id == item.CostContractItemId).FirstOrDefault();
                            if (costContractItem == null)
                            {
                                errMsg = $"δ�ҵ����ú�ͬ";
                                return false;
                            }

                            //ע����ͬ��Ʒ���ú�ͬ��ֹ�ظ���ӣ�Ҫ������֤
                            var thisItems = items.Where(it => it.CostContractItemId == item.CostContractItemId && it.CostContractMonth == item.CostContractMonth).ToList();

                            //���ú�ͬΪ ��Ʒ
                            if (costContractItem.CType == null || costContractItem.CType == 0)
                            {

                                if (thisItems == null || thisItems.Count == 0)
                                {
                                    errMsg = $"��Ʒ��{product.Name},�ڱ���δ�Ҵ��¶Ҹ�";
                                    return false;
                                }

                                //��ǰ�·ݵ�����
                                var monthQuantity = 0;
                                switch (item.CostContractMonth)
                                {
                                    case 1:
                                        monthQuantity = costContractItem.Jan_Balance == null ? 0 : (int)costContractItem.Jan_Balance;
                                        break;
                                    case 2:
                                        monthQuantity = costContractItem.Feb_Balance == null ? 0 : (int)costContractItem.Feb_Balance;
                                        break;
                                    case 3:
                                        monthQuantity = costContractItem.Mar_Balance == null ? 0 : (int)costContractItem.Mar_Balance;
                                        break;
                                    case 4:
                                        monthQuantity = costContractItem.Apr_Balance == null ? 0 : (int)costContractItem.Apr_Balance;
                                        break;
                                    case 5:
                                        monthQuantity = costContractItem.May_Balance == null ? 0 : (int)costContractItem.May_Balance;
                                        break;
                                    case 6:
                                        monthQuantity = costContractItem.Jun_Balance == null ? 0 : (int)costContractItem.Jun_Balance;
                                        break;
                                    case 7:
                                        monthQuantity = costContractItem.Jul_Balance == null ? 0 : (int)costContractItem.Jul_Balance;
                                        break;
                                    case 8:
                                        monthQuantity = costContractItem.Aug_Balance == null ? 0 : (int)costContractItem.Aug_Balance;
                                        break;
                                    case 9:
                                        monthQuantity = costContractItem.Sep_Balance == null ? 0 : (int)costContractItem.Sep_Balance;
                                        break;
                                    case 10:
                                        monthQuantity = costContractItem.Oct_Balance == null ? 0 : (int)costContractItem.Oct_Balance;
                                        break;
                                    case 11:
                                        monthQuantity = costContractItem.Nov_Balance == null ? 0 : (int)costContractItem.Nov_Balance;
                                        break;
                                    case 12:
                                        monthQuantity = costContractItem.Dec_Balance == null ? 0 : (int)costContractItem.Dec_Balance;
                                        break;
                                    default:
                                        break;

                                }
                                if (monthQuantity <= 0)
                                {
                                    errMsg = $"��Ʒ�ڷ��ú�ͬ����!";
                                    return false;
                                }
                                //ת������С��λ���� ע�������ʣ������������С��λ����
                                //var costConversionQuantity = product.GetConversionQuantity(costContractItem.UnitId ?? 0, _specificationAttributeService, _productService);

                                //��ǰ����������
                                var thisQuantity = 0;
                                foreach (var tit in thisItems)
                                {
                                    //��ֹ�û�ѡ��������Ʒ
                                    if (tit.ProductId != item.ProductId)
                                    {
                                        errMsg = $"��Ʒ���ڵ�ǰ���ú�ͬ��";
                                        return false;
                                    }
                                    var titConversionQuantity = product.GetConversionQuantity(allOptions, tit.UnitId);
                                    thisQuantity += titConversionQuantity * tit.Quantity;
                                }
                                if (thisQuantity > monthQuantity)
                                {
                                    string thisFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, thisQuantity);
                                    string costFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, monthQuantity);
                                    if (monthQuantity == 0)
                                    {
                                        costFormat = "0";
                                    }
                                    errMsg = $"��Ʒ�ڷ��ú�ͬ����!";
                                    return false;
                                }
                            }
                            //���ú�ͬΪ �ֽ�
                            else if (costContractItem.CType == 1)
                            {
                                //��ǰ�·ݵ�����
                                decimal monthAmount = 0;
                                switch (item.CostContractMonth)
                                {
                                    case 1:
                                        monthAmount = costContractItem.Jan_Balance ?? 0;
                                        break;
                                    case 2:
                                        monthAmount = costContractItem.Feb_Balance ?? 0;
                                        break;
                                    case 3:
                                        monthAmount = costContractItem.Mar_Balance ?? 0;
                                        break;
                                    case 4:
                                        monthAmount = costContractItem.Apr_Balance ?? 0;
                                        break;
                                    case 5:
                                        monthAmount = costContractItem.May_Balance ?? 0;
                                        break;
                                    case 6:
                                        monthAmount = costContractItem.Jun_Balance ?? 0;
                                        break;
                                    case 7:
                                        monthAmount = costContractItem.Jul_Balance ?? 0;
                                        break;
                                    case 8:
                                        monthAmount = costContractItem.Aug_Balance ?? 0;
                                        break;
                                    case 9:
                                        monthAmount = costContractItem.Sep_Balance ?? 0;
                                        break;
                                    case 10:
                                        monthAmount = costContractItem.Oct_Balance ?? 0;
                                        break;
                                    case 11:
                                        monthAmount = costContractItem.Nov_Balance ?? 0;
                                        break;
                                    case 12:
                                        monthAmount = costContractItem.Dec_Balance ?? 0;
                                        break;
                                    default:
                                        break;

                                }
                                if (monthAmount <= 0)
                                {
                                    errMsg = $"���ú�ͬ��������";
                                    return false;
                                }
                                if (thisItems == null || thisItems.Count == 0)
                                {
                                    errMsg = $"��Ʒ�ں�ͬ���¶Ҹ���δ�ҵ�";
                                    return false;
                                }

                                decimal thisAmount = 0;
                                foreach (var tit in thisItems)
                                {
                                    Product thisProduct = allProducts.Where(ap => ap.Id == tit.ProductId).FirstOrDefault();
                                    if (thisProduct == null)
                                    {
                                        errMsg = $"������Ʒ�����ڣ�";
                                        return false;
                                    }
                                    if (thisProduct.ProductPrices != null && thisProduct.ProductPrices.Count > 0)
                                    {
                                        ProductPrice productPrice = thisProduct.ProductPrices.Where(pp => pp.UnitId == tit.UnitId).FirstOrDefault();
                                        if (productPrice != null)
                                        {
                                            thisAmount += (productPrice.TradePrice ?? 0) * tit.Quantity;
                                        }
                                    }
                                }
                                if (thisAmount > monthAmount)
                                {
                                    errMsg = $"���ú�ͬ�ɶҸ���Ȳ��㣡";
                                    return false;
                                }

                            }

                        }
                        //���ú�ͬ����λ���ܼƶҸ�
                        else if (item.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractUnitQuantity)
                        {
                            //ע����ͬ��Ʒ���ú�ͬ��ֹ�ظ���ӣ�Ҫ������֤
                            var thisItems = items.Where(it => it.CostContractItemId == item.CostContractItemId && it.ProductId == item.ProductId).ToList();
                            if (thisItems == null || thisItems.Count == 0)
                            {
                                errMsg = $"��Ʒ�ں�ͬ���¶Ҹ���δ�ҵ�";
                                return false;
                            }
                            var costContractItem = allCostContractItems.Where(acci => acci.Id == item.CostContractItemId && acci.ProductId == item.ProductId).FirstOrDefault();
                            if (costContractItem == null)
                            {
                                errMsg = $"���ú�ͬ��δ�ҵ�����Ʒ";
                                return false;
                            }
                            //���ú�ͬΪ ��Ʒ
                            if (costContractItem.CType == null || costContractItem.CType == 0)
                            {
                                //����ʣ��
                                var totalQuantity = costContractItem.Total_Balance == null ? 0 : (int)costContractItem.Total_Balance;
                                if (totalQuantity <= 0)
                                {
                                    errMsg = $"��Ʒ�ڷ��ú�ͬ������";
                                    return false;
                                }
                                //ת������С��λ���� ע�������ʣ������������С��λ����
                                //var costConversionQuantity = product.GetConversionQuantity(costContractItem.UnitId ?? 0, _specificationAttributeService, _productService);
                                //���ú�ͬ��������
                                int costQuantity = totalQuantity;

                                //��ǰ����������
                                var thisQuantity = 0;
                                thisItems.ForEach(tit =>
                                {
                                    var titConversionQuantity = product.GetConversionQuantity(allOptions, tit.UnitId);
                                    thisQuantity += titConversionQuantity * tit.Quantity;
                                });
                                if (thisQuantity > costQuantity)
                                {
                                    string thisFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, thisQuantity);
                                    string costFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, costQuantity);
                                    if (costQuantity == 0)
                                    {
                                        costFormat = "0";
                                    }
                                    errMsg = $"��Ʒ�ڷ��ú�ͬ������";
                                    return false;
                                }
                            }
                            //���ú�ͬΪ �ֽ�
                            else if (costContractItem.CType == 1)
                            {
                                //����ʣ��
                                var totalAmount = costContractItem.Total_Balance ?? 0;
                                if (totalAmount <= 0)
                                {
                                    errMsg = $"���ú�ͬ������";
                                    return false;
                                }
                                decimal thisAmount = 0;
                                foreach (var tit in thisItems)
                                {
                                    Product thisProduct = allProducts.Where(ap => ap.Id == tit.ProductId).FirstOrDefault();
                                    if (thisProduct == null)
                                    {
                                        errMsg = $"������Ʒ������";
                                        return false;
                                    }
                                    if (thisProduct.ProductPrices != null && thisProduct.ProductPrices.Count > 0)
                                    {
                                        ProductPrice productPrice = thisProduct.ProductPrices.Where(pp => pp.UnitId == tit.UnitId).FirstOrDefault();
                                        if (productPrice != null)
                                        {
                                            thisAmount += (productPrice.TradePrice ?? 0) * tit.Quantity;
                                        }
                                    }
                                }
                                if (thisAmount > totalAmount)
                                {
                                    errMsg = $"���ú�ͬ�ɶҸ�����";
                                    return false;
                                }
                            }

                        }
                        //���ú�ͬ��������Ʒ�ۼ�
                        else if (item.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractManageGift)
                        {
                            //ע����ͬ��Ʒ���ú�ͬ��ֹ�ظ���ӣ�Ҫ������֤
                            var thisItems = items.Where(it => it.CostContractItemId == item.CostContractItemId && it.ProductId == item.ProductId).ToList();
                            if (thisItems == null || thisItems.Count == 0)
                            {
                                errMsg = $"��Ʒ�ڷ��ú�ͬ��δ�ҵ�";
                                return false;
                            }
                            var costContractItem = allCostContractItems.Where(acci => acci.Id == item.CostContractItemId && acci.ProductId == item.ProductId).FirstOrDefault();
                            if (costContractItem == null)
                            {
                                errMsg = $"��Ʒ�ڷ��ú�ͬ��δ�ҵ�";
                                return false;
                            }
                            //���ú�ͬΪ ��Ʒ
                            if (costContractItem.CType == null || costContractItem.CType == 0)
                            {
                                //����ʣ��
                                var totalQuantity = costContractItem.Total_Balance == null ? 0 : (int)costContractItem.Total_Balance;
                                if (totalQuantity <= 0)
                                {
                                    errMsg = $"��Ʒ�ڷ��ú�ͬ������";
                                    return false;
                                }
                                //ת������С��λ���� ע�������ʣ������������С��λ����
                                //var costConversionQuantity = product.GetConversionQuantity(costContractItem.UnitId ?? 0, _specificationAttributeService, _productService);
                                //���ú�ͬ��������
                                int costQuantity = totalQuantity;

                                //��ǰ����������
                                var thisQuantity = 0;
                                thisItems.ForEach(tit =>
                                {
                                    var titConversionQuantity = product.GetConversionQuantity(allOptions, tit.UnitId);
                                    thisQuantity += titConversionQuantity * tit.Quantity;
                                });
                                if (thisQuantity > costQuantity)
                                {
                                    string thisFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, thisQuantity);
                                    string costFormat = product.GetConversionFormat(allOptions, product.SmallUnitId, costQuantity);
                                    if (costQuantity == 0)
                                    {
                                        costFormat = "0";
                                    }
                                    errMsg = $"������Ʒ�ڷ��ú�ͬ������";
                                    return false;
                                }
                            }
                            //���ú�ͬΪ �ֽ�
                            else if (costContractItem.CType == 1)
                            {
                                //����ʣ��
                                var totalAmount = costContractItem.Total_Balance ?? 0;
                                if (totalAmount <= 0)
                                {
                                    errMsg = $"���ú�ͬ������";
                                    return false;
                                }
                                decimal thisAmount = 0;
                                foreach (var tit in thisItems)
                                {
                                    Product thisProduct = allProducts.Where(ap => ap.Id == tit.ProductId).FirstOrDefault();
                                    if (thisProduct == null)
                                    {
                                        errMsg = $"��Ʒ������δ�ҵ�������Ʒ";
                                        return false;
                                    }
                                    if (thisProduct.ProductPrices != null && thisProduct.ProductPrices.Count > 0)
                                    {
                                        ProductPrice productPrice = thisProduct.ProductPrices.Where(pp => pp.UnitId == tit.UnitId).FirstOrDefault();
                                        if (productPrice != null)
                                        {
                                            thisAmount += (productPrice.TradePrice ?? 0) * tit.Quantity;
                                        }
                                    }
                                }
                                if (thisAmount > totalAmount)
                                {
                                    errMsg = $"���ú�ͬʹ�ö�Ȳ���";
                                    return false;
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errMsg = ex.ToString();
                return false;
            }
            return true;
        }

        /// <summary>
        /// ���ú�ͬ�޸ġ����ͼ�¼�޸�
        /// </summary>
        /// <param name="type">1����Ʒ������ӣ� -1����Ʒ��ȼ���</param>
        /// <param name="bill"></param>
        public virtual void CostContractRecordUpdate(int storeId, int type, SaleBill bill)
        {
            try
            {
                //��ѯ��Ʒ�������������Ʒ�����ú�ͬ���¶Ҹ������ú�ͬ����λ���ܼƶҸ������ú�ͬ��������Ʒ�ۼ�
                //var gifts11 = bill.Items.Where(it => it.SaleProductTypeId == (int)SaleProductTypeEnum.CampaignGiveProduct || it.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractByMonth || it.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractUnitQuantity || it.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractManageGift).ToList();
                var gifts = new List<SaleItem>();
                if (bill != null && bill.Items != null && bill.Items.Count > 0 && bill.Items.Where(it => it.SaleProductTypeId > 0).Count() > 0)
                {
                    //SaleProductTypeId=1Ϊ�����������Ʒ
                    gifts = bill.Items.Where(it => it.SaleProductTypeId > 1).ToList();
                }

                if (gifts != null && gifts.Count > 0)
                {
                    //var _userService = EngineContext.Current.Resolve<IUserService>();
                    //var _specificationAttributeService = EngineContext.Current.Resolve<ISpecificationAttributeService>();
                    //var _productService = EngineContext.Current.Resolve<IProductService>();
                    //var _categoryService = EngineContext.Current.Resolve<ICategoryService>();
                    //var _terminalService = EngineContext.Current.Resolve<ITerminalService>();
                    //var _giveQuotaService = EngineContext.Current.Resolve<IGiveQuotaService>();

                    List<Product> allGiftProducts = _productService.GetProductsByIds(storeId, gifts.Select(gi => gi.ProductId).Distinct().ToArray()).ToList();
                    List<Category> allGiftCategorys = _categoryService.GetCategoriesByCategoryIds(storeId, allGiftProducts.Select(ag => ag.CategoryId).Distinct().ToArray()).ToList();
                    var allOptions = _specificationAttributeService.GetSpecificationAttributeOptionByIds(storeId, allGiftProducts.GetProductBigStrokeSmallUnitIds());
                    Terminal terminal = _terminalService.GetTerminalById(storeId, bill.TerminalId);

                    List<CostContractBill> allCostConstaceBills = GetCostContractBillsByIds(gifts.Select(gi => gi.CostContractId ?? 0).Distinct().ToArray()).ToList();
                    List<CostContractItem> allCostConstaceItems = GetCostContractItemsByIds(gifts.Select(gi => gi.CostContractItemId ?? 0).Distinct().ToArray()).ToList();

                    foreach (var gift in gifts)
                    {
                        GiveQuotaRecords giveQuotaRecords = new GiveQuotaRecords();
                        Product product = allGiftProducts.Where(agp => agp.Id == gift.ProductId).FirstOrDefault();
                        Category category = null;
                        if (product != null)
                        {
                            category = allGiftCategorys.Where(agc => agc.Id == gift.CategoryId).FirstOrDefault();
                        }
                        var unit = allOptions.Where(ao => ao.Id == gift.UnitId).FirstOrDefault();

                        CostContractBill costContractBill = allCostConstaceBills.Where(alc => alc.Id == gift.CostContractId).FirstOrDefault();
                        CostContractItem costContractItem = allCostConstaceItems.Where(alc => alc.Id == gift.CostContractItemId).FirstOrDefault();

                        //��������
                        giveQuotaRecords.StoreId = bill.StoreId;
                        giveQuotaRecords.BillId = bill.Id;
                        giveQuotaRecords.BusinessUserId = bill.BusinessUserId;
                        giveQuotaRecords.BusinessUserName = _userService.GetUserName(bill.StoreId, bill.BusinessUserId);
                        giveQuotaRecords.TerminalId = bill.TerminalId;
                        giveQuotaRecords.TerminalName = terminal == null ? "" : terminal.Name;
                        giveQuotaRecords.TerminalCode = terminal == null ? "" : terminal.Code;
                        giveQuotaRecords.CategoryId = product == null ? 0 : product.CategoryId;
                        giveQuotaRecords.CategoryName = category == null ? "" : category.Name;
                        giveQuotaRecords.CostingCalCulateMethodId = 0;
                        giveQuotaRecords.ProductId = gift.ProductId;
                        giveQuotaRecords.ProductName = product == null ? "" : product.Name;
                        giveQuotaRecords.BarCode = product.GetProductBarCode(gift.UnitId);
                        giveQuotaRecords.UnitId = gift.UnitId;
                        giveQuotaRecords.UnitName = unit == null ? "" : unit.Name;
                        giveQuotaRecords.UnitConversion = product.GetProductUnitConversion(allOptions);
                        giveQuotaRecords.Quantity = gift.Quantity;
                        giveQuotaRecords.CostAmount = gift.CostAmount;
                        giveQuotaRecords.CreatedOnUtc = DateTime.Now;
                        giveQuotaRecords.Remark = gift.Remark;

                        //�����������Ʒ
                        if (gift.SaleProductTypeId == (int)SaleProductTypeEnum.CampaignGiveProduct)
                        {
                            giveQuotaRecords.CampaignId = gift.CampaignId;
                            giveQuotaRecords.GiveTypeId = gift.GiveTypeId ?? 0;
                        }
                        //���ú�ͬ���¶Ҹ�
                        else if (gift.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractByMonth)
                        {
                            giveQuotaRecords.ContractId = gift.CostContractId;
                            giveQuotaRecords.GiveTypeId = gift.GiveTypeId ?? 0;
                            giveQuotaRecords.Year = costContractBill == null ? 0 : costContractBill.Year;
                            giveQuotaRecords.Monthly = gift.CostContractMonth ?? 0;

                            #region ����
                            //�ۼ����
                            //if (costContractItem != null)
                            //{
                            //    //��Ʒ
                            //    if (costContractItem.CType == null || costContractItem.CType == 0)
                            //    {
                            //        var thisConversionQuantity = product.GetConversionQuantity(allOptions, gift.UnitId);
                            //        int thisGiveQuantity = gift.Quantity * thisConversionQuantity;
                            //        switch (gift.CostContractMonth)
                            //        {
                            //            case 1:
                            //                costContractItem.Jan_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 2:
                            //                costContractItem.Feb_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 3:
                            //                costContractItem.Mar_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 4:
                            //                costContractItem.Apr_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 5:
                            //                costContractItem.May_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 6:
                            //                costContractItem.Jun_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 7:
                            //                costContractItem.Jul_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 8:
                            //                costContractItem.Aug_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 9:
                            //                costContractItem.Sep_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 10:
                            //                costContractItem.Oct_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 11:
                            //                costContractItem.Nov_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 12:
                            //                costContractItem.Dec_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //        }

                            //    }
                            //    //�ֽ�
                            //    else if (costContractItem.CType == 1)
                            //    {
                            //        switch (gift.CostContractMonth)
                            //        {
                            //            case 1:
                            //                costContractItem.Jan_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 2:
                            //                costContractItem.Feb_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 3:
                            //                costContractItem.Mar_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 4:
                            //                costContractItem.Apr_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 5:
                            //                costContractItem.May_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 6:
                            //                costContractItem.Jun_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 7:
                            //                costContractItem.Jul_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 8:
                            //                costContractItem.Aug_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 9:
                            //                costContractItem.Sep_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 10:
                            //                costContractItem.Oct_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 11:
                            //                costContractItem.Nov_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 12:
                            //                costContractItem.Dec_Balance += (type) * gift.CostAmount;
                            //                break;
                            //        }
                            //    }
                            //    ////�޸�ʣ�����
                            //    //costContractItem.Total_Balance = (costContractItem.Jan_Balance ?? 0) + (costContractItem.Feb_Balance ?? 0) + (costContractItem.Mar_Balance) + (costContractItem.Apr_Balance ?? 0) + (costContractItem.May_Balance ?? 0) + (costContractItem.Jun_Balance ?? 0) + (costContractItem.Jul_Balance ?? 0) + (costContractItem.Aug_Balance ?? 0) + (costContractItem.Sep_Balance ?? 0) + (costContractItem.Oct_Balance ?? 0) + (costContractItem.Nov_Balance ?? 0) + (costContractItem.Dec_Balance ?? 0);
                            //    UpdateCostContractItem(costContractItem);
                            //}
                            #endregion
                        }
                        //���ú�ͬ����λ���ܼƶҸ�
                        else if (gift.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractUnitQuantity)
                        {
                            giveQuotaRecords.ContractId = gift.CostContractId;
                            giveQuotaRecords.GiveTypeId = gift.GiveTypeId ?? 0;
                            giveQuotaRecords.Year = costContractBill == null ? 0 : costContractBill.Year;
                            giveQuotaRecords.Monthly = gift.CostContractMonth ?? 0;

                            #region ����
                            //�ۼ����
                            //if (costContractItem != null)
                            //{
                            //    //��Ʒ
                            //    if (costContractItem.CType == null || costContractItem.CType == 0)
                            //    {
                            //        var thisConversionQuantity = product.GetConversionQuantity(allOptions, gift.UnitId);
                            //        int thisGiveQuantity = gift.Quantity * thisConversionQuantity;
                            //        costContractItem.Total_Balance += (type) * thisGiveQuantity;

                            //    }
                            //    //�ֽ�
                            //    else if (costContractItem.CType == 1)
                            //    {
                            //        costContractItem.Total_Balance += (type) * gift.CostAmount;
                            //    }
                            //    UpdateCostContractItem(costContractItem);

                            //}
                            #endregion
                        }
                        //���ú�ͬ��������Ʒ�ۼ�
                        else if (gift.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractManageGift)
                        {
                            giveQuotaRecords.ContractId = gift.CostContractId;
                            giveQuotaRecords.GiveTypeId = gift.GiveTypeId ?? 0;
                            giveQuotaRecords.Year = costContractBill == null ? 0 : costContractBill.Year;
                            giveQuotaRecords.Monthly = gift.CostContractMonth ?? 0;

                            #region ����
                            //�ۼ����
                            //if (costContractItem != null)
                            //{
                            //    //��Ʒ
                            //    if (costContractItem.CType == null || costContractItem.CType == 0)
                            //    {
                            //        var thisConversionQuantity = product.GetConversionQuantity(allOptions, gift.UnitId);
                            //        int thisGiveQuantity = gift.Quantity * thisConversionQuantity;
                            //        costContractItem.Total_Balance += (type) * thisGiveQuantity;

                            //    }
                            //    //�ֽ�
                            //    else if (costContractItem.CType == 1)
                            //    {
                            //        costContractItem.Total_Balance += (type) * gift.CostAmount;
                            //    }
                            //    UpdateCostContractItem(costContractItem);
                            //}
                            #endregion
                        }

                        if (type == -1)
                        {
                            //��¼ ���ͼ�¼
                            _giveQuotaService.InsertGiveQuotaRecords(giveQuotaRecords);
                        }
                        else if (type == 1)
                        {
                            //ɾ�� ���ͼ�¼
                            List<GiveQuotaRecords> giveQuotaRecordsLists = _giveQuotaService.GetQuotaRecordsByBillId(bill.Id).ToList();
                            if (giveQuotaRecordsLists != null && giveQuotaRecordsLists.Count > 0)
                            {
                                giveQuotaRecordsLists.ForEach(gq =>
                                {
                                    _giveQuotaService.DeleteGiveQuotaRecords(gq);
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// ���ú�ͬ�޸ġ����ͼ�¼�޸�
        /// </summary>
        /// <param name="type">1����Ʒ������ӣ� -1����Ʒ��ȼ���</param>
        /// <param name="bill"></param>
        public virtual void CostContractRecordUpdate(int type, SaleReservationBill bill)
        {
            try
            {
                //��ѯ��Ʒ�������������Ʒ�����ú�ͬ���¶Ҹ������ú�ͬ����λ���ܼƶҸ������ú�ͬ��������Ʒ�ۼ�
                //var gifts11 = bill.Items.Where(it => it.SaleProductTypeId == (int)SaleProductTypeEnum.CampaignGiveProduct || it.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractByMonth || it.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractUnitQuantity || it.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractManageGift).ToList();
                var gifts = new List<SaleReservationItem>();
                if (bill != null && bill.Items != null && bill.Items.Count > 0 && bill.Items.Where(it => it.SaleProductTypeId > 0).Count() > 0)
                {
                    gifts = bill.Items.Where(it => it.SaleProductTypeId > 0).ToList();
                }

                if (gifts != null && gifts.Count > 0)
                {

                    List<Product> allGiftProducts = _productService.GetProductsByIds(bill.StoreId, gifts.Select(gi => gi.ProductId).Distinct().ToArray()).ToList();
                    List<Category> allGiftCategorys = _categoryService.GetCategoriesByCategoryIds(bill.StoreId, allGiftProducts.Select(ag => ag.CategoryId).Distinct().ToArray()).ToList();
                    var allOptions = _specificationAttributeService.GetSpecificationAttributeOptionByIds(bill.StoreId, allGiftProducts.GetProductBigStrokeSmallUnitIds());
                    Terminal terminal = _terminalService.GetTerminalById(bill.StoreId, bill.TerminalId);

                    List<CostContractBill> allCostConstaceBills = GetCostContractBillsByIds(gifts.Select(gi => gi.CostContractId ?? 0).Distinct().ToArray()).ToList();
                    List<CostContractItem> allCostConstaceItems = GetCostContractItemsByIds(gifts.Select(gi => gi.CostContractItemId ?? 0).Distinct().ToArray()).ToList();

                    foreach (var gift in gifts)
                    {
                        GiveQuotaRecords giveQuotaRecords = new GiveQuotaRecords();
                        Product product = allGiftProducts.Where(agp => agp.Id == gift.ProductId).FirstOrDefault();
                        Category category = null;
                        if (product != null)
                        {
                            category = allGiftCategorys.Where(agc => agc.Id == gift.CategoryId).FirstOrDefault();
                        }
                        var unit = allOptions.Where(ao => ao.Id == gift.UnitId).FirstOrDefault();

                        CostContractBill costContractBill = allCostConstaceBills.Where(alc => alc.Id == gift.CostContractId).FirstOrDefault();
                        CostContractItem costContractItem = allCostConstaceItems.Where(alc => alc.Id == gift.CostContractItemId).FirstOrDefault();

                        //��������
                        giveQuotaRecords.StoreId = bill.StoreId;
                        giveQuotaRecords.BillId = bill.Id;
                        giveQuotaRecords.BusinessUserId = bill.BusinessUserId;
                        giveQuotaRecords.BusinessUserName = _userService.GetUserName(bill.StoreId, bill.BusinessUserId);
                        giveQuotaRecords.TerminalId = bill.TerminalId;
                        giveQuotaRecords.TerminalName = terminal == null ? "" : terminal.Name;
                        giveQuotaRecords.TerminalCode = terminal == null ? "" : terminal.Code;
                        giveQuotaRecords.CategoryId = product == null ? 0 : product.CategoryId;
                        giveQuotaRecords.CategoryName = category == null ? "" : category.Name;
                        giveQuotaRecords.CostingCalCulateMethodId = 0;
                        giveQuotaRecords.ProductId = gift.ProductId;
                        giveQuotaRecords.ProductName = product == null ? "" : product.Name;
                        giveQuotaRecords.BarCode = product.GetProductBarCode(gift.UnitId);
                        giveQuotaRecords.UnitId = gift.UnitId;
                        giveQuotaRecords.UnitName = unit == null ? "" : unit.Name;
                        giveQuotaRecords.UnitConversion = product.GetProductUnitConversion(allOptions);
                        giveQuotaRecords.Quantity = gift.Quantity;
                        giveQuotaRecords.CostAmount = gift.CostAmount;
                        giveQuotaRecords.CreatedOnUtc = DateTime.Now;
                        giveQuotaRecords.Remark = gift.Remark;

                        //�����������Ʒ
                        if (gift.SaleProductTypeId == (int)SaleProductTypeEnum.CampaignGiveProduct)
                        {
                            giveQuotaRecords.CampaignId = gift.CampaignId;
                            giveQuotaRecords.GiveTypeId = gift.GiveTypeId ?? 0;
                        }
                        //���ú�ͬ���¶Ҹ�
                        else if (gift.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractByMonth)
                        {
                            giveQuotaRecords.ContractId = gift.CostContractId;
                            giveQuotaRecords.GiveTypeId = gift.GiveTypeId ?? 0;
                            giveQuotaRecords.Year = costContractBill == null ? 0 : costContractBill.Year;
                            giveQuotaRecords.Monthly = gift.CostContractMonth ?? 0;

                            #region ����
                            //�ۼ����
                            //if (costContractItem != null)
                            //{
                            //    //��Ʒ
                            //    if (costContractItem.CType == null || costContractItem.CType == 0)
                            //    {
                            //        var thisConversionQuantity = product.GetConversionQuantity(allOptions, gift.UnitId);
                            //        int thisGiveQuantity = gift.Quantity * thisConversionQuantity;
                            //        switch (gift.CostContractMonth)
                            //        {
                            //            case 1:
                            //                costContractItem.Jan_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 2:
                            //                costContractItem.Feb_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 3:
                            //                costContractItem.Mar_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 4:
                            //                costContractItem.Apr_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 5:
                            //                costContractItem.May_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 6:
                            //                costContractItem.Jun_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 7:
                            //                costContractItem.Jul_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 8:
                            //                costContractItem.Aug_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 9:
                            //                costContractItem.Sep_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 10:
                            //                costContractItem.Oct_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 11:
                            //                costContractItem.Nov_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //            case 12:
                            //                costContractItem.Dec_Balance += (type) * thisGiveQuantity;
                            //                break;
                            //        }

                            //    }
                            //    //�ֽ�
                            //    else if (costContractItem.CType == 1)
                            //    {
                            //        switch (gift.CostContractMonth)
                            //        {
                            //            case 1:
                            //                costContractItem.Jan_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 2:
                            //                costContractItem.Feb_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 3:
                            //                costContractItem.Mar_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 4:
                            //                costContractItem.Apr_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 5:
                            //                costContractItem.May_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 6:
                            //                costContractItem.Jun_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 7:
                            //                costContractItem.Jul_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 8:
                            //                costContractItem.Aug_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 9:
                            //                costContractItem.Sep_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 10:
                            //                costContractItem.Oct_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 11:
                            //                costContractItem.Nov_Balance += (type) * gift.CostAmount;
                            //                break;
                            //            case 12:
                            //                costContractItem.Dec_Balance += (type) * gift.CostAmount;
                            //                break;
                            //        }
                            //    }

                            //    UpdateCostContractItem(costContractItem);
                            //}
                            #endregion
                        }
                        //���ú�ͬ����λ���ܼƶҸ�
                        else if (gift.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractUnitQuantity)
                        {
                            giveQuotaRecords.ContractId = gift.CostContractId;
                            giveQuotaRecords.GiveTypeId = gift.GiveTypeId ?? 0;
                            giveQuotaRecords.Year = costContractBill == null ? 0 : costContractBill.Year;
                            giveQuotaRecords.Monthly = gift.CostContractMonth ?? 0;

                            #region ����
                            //�ۼ����
                            //if (costContractItem != null)
                            //{
                            //    //��Ʒ
                            //    if (costContractItem.CType == null || costContractItem.CType == 0)
                            //    {
                            //        var thisConversionQuantity = product.GetConversionQuantity(allOptions, gift.UnitId);
                            //        int thisGiveQuantity = gift.Quantity * thisConversionQuantity;
                            //        costContractItem.Total_Balance += (type) * thisGiveQuantity;

                            //    }
                            //    //�ֽ�
                            //    else if (costContractItem.CType == 1)
                            //    {
                            //        costContractItem.Total_Balance += (type) * gift.CostAmount;
                            //    }
                            //    UpdateCostContractItem(costContractItem);

                            //}
                            #endregion
                        }
                        //���ú�ͬ��������Ʒ�ۼ�
                        else if (gift.SaleProductTypeId == (int)SaleProductTypeEnum.CostContractManageGift)
                        {
                            giveQuotaRecords.ContractId = gift.CostContractId;
                            giveQuotaRecords.GiveTypeId = gift.GiveTypeId ?? 0;
                            giveQuotaRecords.Year = costContractBill == null ? 0 : costContractBill.Year;
                            giveQuotaRecords.Monthly = gift.CostContractMonth ?? 0;

                            #region ����
                            //�ۼ����
                            //if (costContractItem != null)
                            //{
                            //    //��Ʒ
                            //    if (costContractItem.CType == null || costContractItem.CType == 0)
                            //    {
                            //        var thisConversionQuantity = product.GetConversionQuantity(allOptions, gift.UnitId);
                            //        int thisGiveQuantity = gift.Quantity * thisConversionQuantity;
                            //        costContractItem.Total_Balance += (type) * thisGiveQuantity;

                            //    }
                            //    //�ֽ�
                            //    else if (costContractItem.CType == 1)
                            //    {
                            //        costContractItem.Total_Balance += (type) * gift.CostAmount;
                            //    }
                            //    UpdateCostContractItem(costContractItem);
                            //}
                            #endregion
                        }


                        if (type == -1)
                        {
                            //��¼ ���ͼ�¼
                            _giveQuotaService.InsertGiveQuotaRecords(giveQuotaRecords);
                        }
                        else if (type == 1)
                        {
                            //ɾ�� ���ͼ�¼
                            List<GiveQuotaRecords> giveQuotaRecordsLists = _giveQuotaService.GetQuotaRecordsByBillId(bill.Id).ToList();
                            if (giveQuotaRecordsLists != null && giveQuotaRecordsLists.Count > 0)
                            {
                                giveQuotaRecordsLists.ForEach(gq =>
                                {
                                    _giveQuotaService.DeleteGiveQuotaRecords(gq);
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }
        #endregion



        public BaseResult BillCreateOrUpdate(int storeId, int userId, int? billId, List<AccountingOption> accountings, CostContractBillUpdate data, List<CostContractItem> items, bool isAdmin = false,bool doAudit = true)
        {
            var uow = CostContractBillsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();



                var costContractBill = new CostContractBill();

                if (billId.HasValue && billId.Value != 0)
                {
                    #region ���·��ú�ͬ

                    costContractBill = GetCostContractBillById(storeId, billId.Value, false);
                    if (costContractBill != null)
                    {
                        costContractBill.CustomerId = data.CustomerId;
                        costContractBill.EmployeeId = data.EmployeeId;
                        costContractBill.AccountingOptionId = data.AccountingOptionId;
                        costContractBill.AccountCodeTypeId = data.AccountCodeTypeId;
                        costContractBill.Year = data.Year;
                        costContractBill.ContractType = data.ContractType;
                        costContractBill.LeaderId = data.LeaderId;
                        costContractBill.Month = data.Month;
                        //��ע
                        costContractBill.Remark = data.Remark;
                        costContractBill.SaleRemark = data.SaleRemark;
                        UpdateCostContractBill(costContractBill);
                    }

                    #endregion
                }
                else
                {
                    #region ��ӷ��ú�ͬ


                    costContractBill.StoreId = storeId;
                    //�ͻ�
                    costContractBill.CustomerId = data.CustomerId;
                    costContractBill.EmployeeId = data.EmployeeId;
                    costContractBill.AccountingOptionId = data.AccountingOptionId;
                    costContractBill.AccountCodeTypeId = data.AccountCodeTypeId;
                    costContractBill.Year = data.Year;
                    costContractBill.ContractType = data.ContractType;
                    costContractBill.LeaderId = data.LeaderId;
                    costContractBill.Month = data.Month;

                    //��������
                    costContractBill.CreatedOnUtc = DateTime.Now;
                    //���ݱ��
                    costContractBill.BillNumber = string.IsNullOrEmpty(data.BillNumber) ? CommonHelper.GetBillNumber("FYHT", storeId): data.BillNumber;

                    var sb = GetCostContractBillByNumber(storeId, costContractBill.BillNumber);
                    if (sb != null)
                    {
                        return new BaseResult { Success = false, Message = "����ʧ�ܣ��ظ��ύ" };
                    }


                    //�Ƶ���
                    costContractBill.MakeUserId = userId;
                    //״̬(���)
                    costContractBill.AuditedStatus = false;
                    costContractBill.AuditedDate = null;

                    costContractBill.AbandonedDate = null;
                    costContractBill.AbandonedUserId = 0;
                    costContractBill.AbandonedStatus = false;

                    //��ע
                    costContractBill.Remark = data.Remark;
                    costContractBill.SaleRemark = data.SaleRemark;
                    costContractBill.Operation = data.Operation;//��ʶ����Դ

                    InsertCostContractBill(costContractBill);

                    #endregion
                }

                #region ������Ŀ

                data.Items.ForEach(p =>
                {
                    //�ֽ�
                    if (p.CType == (int)CostContractCTypeEnum.Cash)
                    {
                        var sd = GetCostContractItemById(storeId, p.Id);
                        if (sd == null)
                        {
                            var item = p;
                            item.StoreId = storeId;
                            item.CostContractBillId = costContractBill.Id;
                            item.Name = "�ֽ�";
                            item.ProductId = 0;
                            item.UnitId = 0;
                            item.CreatedOnUtc = DateTime.Now;

                            item.GiveQuotaOptionId = p.GiveQuotaOptionId;
                            item.GiveQuotaId = p.GiveQuotaId;
                            item.Jan_Balance = p.Jan ?? 0;
                            item.Feb_Balance = p.Feb ?? 0;
                            item.Mar_Balance = p.Mar ?? 0;
                            item.Apr_Balance = p.Apr ?? 0;
                            item.May_Balance = p.May ?? 0;
                            item.Jun_Balance = p.Jun ?? 0;
                            item.Jul_Balance = p.Jul ?? 0;
                            item.Aug_Balance = p.Aug ?? 0;
                            item.Sep_Balance = p.Sep ?? 0;
                            item.Oct_Balance = p.Oct ?? 0;
                            item.Nov_Balance = p.Nov ?? 0;
                            item.Dec_Balance = p.Dec ?? 0;
                            item.Total_Balance = p.Total ?? 0;
                            item.StoreId = storeId;
                            InsertCostContractItem(item);
                            p.Id = item.Id;
                            if (!costContractBill.Items.Select(s => s.Id).Contains(item.Id))
                            {
                                costContractBill.Items.Add(item);
                            }
                        }
                        else
                        {
                            //���������
                            sd.CType = (int)CostContractCTypeEnum.Cash;
                            sd.StoreId = storeId;
                            sd.Name = "�ֽ�";
                            sd.UnitName = "";
                            sd.ProductId = 0;
                            sd.UnitId = 0;
                            sd.Jan = p.Jan ?? 0;
                            sd.Feb = p.Feb ?? 0;
                            sd.Mar = p.Mar ?? 0;
                            sd.Apr = p.Apr ?? 0;
                            sd.May = p.May ?? 0;
                            sd.Jun = p.Jun ?? 0;
                            sd.Jul = p.Jul ?? 0;
                            sd.Aug = p.Aug ?? 0;
                            sd.Sep = p.Sep ?? 0;
                            sd.Oct = p.Oct ?? 0;
                            sd.Nov = p.Nov ?? 0;
                            sd.Dec = p.Dec ?? 0;
                            sd.Total = p.Total ?? 0;
                            sd.SmallUnitId = 0;
                            sd.SmallUnitQuantity = 0;
                            sd.BigUnitId = 0;
                            sd.BigUnitQuantity = 0;
                            sd.Remark = p.Remark;

                            sd.GiveQuotaOptionId = p.GiveQuotaOptionId;
                            sd.GiveQuotaId = p.GiveQuotaId;
                            sd.Jan_Balance = p.Jan ?? 0;
                            sd.Feb_Balance = p.Feb ?? 0;
                            sd.Mar_Balance = p.Mar ?? 0;
                            sd.Apr_Balance = p.Apr ?? 0;
                            sd.May_Balance = p.May ?? 0;
                            sd.Jun_Balance = p.Jun ?? 0;
                            sd.Jul_Balance = p.Jul ?? 0;
                            sd.Aug_Balance = p.Aug ?? 0;
                            sd.Sep_Balance = p.Sep ?? 0;
                            sd.Oct_Balance = p.Oct ?? 0;
                            sd.Nov_Balance = p.Nov ?? 0;
                            sd.Dec_Balance = p.Dec ?? 0;
                            sd.Total_Balance = p.Total ?? 0;

                            UpdateCostContractItem(sd);
                        }
                    }
                    //��Ʒ
                    if (p.CType == (int)CostContractCTypeEnum.Product)
                    {
                        if (p.ProductId != 0)
                        {
                            var sd = GetCostContractItemById(storeId, p.Id);
                            if (sd == null)
                            {
                                ////׷����
                                //if (costContractBill.Items.Count(cp => cp.Id == p.Id) == 0)
                                //{
                                //    var item = p.ToEntity();

                                //    item.CType = p.ProductId > 0 ? 0 : 1;
                                //    item.Name = p.Name;
                                //    item.UnitName = p.UnitName;
                                //    item.CostContractBillId = costContractBill.Id;
                                //    item.CreatedOnUtc = DateTime.Now;
                                //    _costContractBillService.InsertCostContractItem(item);
                                //    //���ų�
                                //    p.Id = item.Id;
                                //    //costContractBill.Items.Add(item);
                                //    if (!costContractBill.Items.Select(s => s.Id).Contains(item.Id))
                                //        costContractBill.Items.Add(item);
                                //}
                                var item = p;

                                item.StoreId = storeId;
                                item.CType = p.ProductId > 0 ? 0 : 1;
                                item.Name = p.Name;
                                item.UnitName = p.UnitName;
                                item.CostContractBillId = costContractBill.Id;
                                item.CreatedOnUtc = DateTime.Now;

                                item.GiveQuotaOptionId = p.GiveQuotaOptionId;
                                item.GiveQuotaId = p.GiveQuotaId;
                                item.Jan_Balance = p.Jan ?? 0;
                                item.Feb_Balance = p.Feb ?? 0;
                                item.Mar_Balance = p.Mar ?? 0;
                                item.Apr_Balance = p.Apr ?? 0;
                                item.May_Balance = p.May ?? 0;
                                item.Jun_Balance = p.Jun ?? 0;
                                item.Jul_Balance = p.Jul ?? 0;
                                item.Aug_Balance = p.Aug ?? 0;
                                item.Sep_Balance = p.Sep ?? 0;
                                item.Oct_Balance = p.Oct ?? 0;
                                item.Nov_Balance = p.Nov ?? 0;
                                item.Dec_Balance = p.Dec ?? 0;
                                item.Total_Balance = p.Total ?? 0;

                                //������Ʒ��Ϣ
                                item.GiveQuotaOptionId = p.GiveQuotaOptionId;
                                item.GiveQuotaId = p.GiveQuotaId;

                                InsertCostContractItem(item);
                                //���ų�
                                p.Id = item.Id;
                                //costContractBill.Items.Add(item);
                                if (!costContractBill.Items.Select(s => s.Id).Contains(item.Id))
                                {
                                    costContractBill.Items.Add(item);
                                }
                            }
                            else
                            {
                                //���������
                                sd.StoreId = storeId;
                                sd.CType = (int)CostContractCTypeEnum.Product;
                                sd.Name = p.Name;
                                sd.UnitName = p.UnitName;
                                sd.ProductId = p.ProductId;
                                sd.UnitId = p.UnitId;
                                sd.Jan = p.Jan ?? 0;
                                sd.Feb = p.Feb ?? 0;
                                sd.Mar = p.Mar ?? 0;
                                sd.Apr = p.Apr ?? 0;
                                sd.May = p.May ?? 0;
                                sd.Jun = p.Jun ?? 0;
                                sd.Jul = p.Jul ?? 0;
                                sd.Aug = p.Aug ?? 0;
                                sd.Sep = p.Sep ?? 0;
                                sd.Oct = p.Oct ?? 0;
                                sd.Nov = p.Nov ?? 0;
                                sd.Dec = p.Dec ?? 0;
                                sd.Total = p.Total ?? 0;
                                sd.SmallUnitId = p.SmallUnitId;
                                sd.SmallUnitQuantity = p.SmallUnitQuantity;
                                sd.BigUnitId = p.BigUnitId;
                                sd.BigUnitQuantity = p.BigUnitQuantity;
                                sd.Remark = p.Remark;

                                sd.GiveQuotaOptionId = p.GiveQuotaOptionId;
                                sd.GiveQuotaId = p.GiveQuotaId;
                                sd.Jan_Balance = p.Jan ?? 0;
                                sd.Feb_Balance = p.Feb ?? 0;
                                sd.Mar_Balance = p.Mar ?? 0;
                                sd.Apr_Balance = p.Apr ?? 0;
                                sd.May_Balance = p.May ?? 0;
                                sd.Jun_Balance = p.Jun ?? 0;
                                sd.Jul_Balance = p.Jul ?? 0;
                                sd.Aug_Balance = p.Aug ?? 0;
                                sd.Sep_Balance = p.Sep ?? 0;
                                sd.Oct_Balance = p.Oct ?? 0;
                                sd.Nov_Balance = p.Nov ?? 0;
                                sd.Dec_Balance = p.Dec ?? 0;
                                sd.Total_Balance = p.Total ?? 0;

                                //������Ʒ��Ϣ
                                sd.GiveQuotaOptionId = p.GiveQuotaOptionId;
                                sd.GiveQuotaId = p.GiveQuotaId;

                                UpdateCostContractItem(sd);
                            }
                        }
                    }

                });

                #endregion

                #region Grid �Ƴ���ӿ��Ƴ�ɾ����

                costContractBill.Items.ToList().ForEach(p =>
                {
                    if (data.Items.Count(cp => cp.Id == p.Id) == 0)
                    {
                        costContractBill.Items.Remove(p);
                        var item = GetCostContractItemById(storeId, p.Id);
                        if (item != null)
                        {
                            DeleteCostContractItem(item);
                        }
                    }
                });

                #endregion

                //����Ա�����Զ����
                if (isAdmin && doAudit) //�жϵ�ǰ��¼���Ƿ�Ϊ����Ա,��Ϊ����Ա�������Զ����
                {
                    AuditingNoTran(storeId, userId, costContractBill);
                }
                else
                {
                    #region ����֪ͨ ����Ա
                    try
                    {
                        //����Ա
                        var adminNumbers = _userService.GetAllAdminUserMobileNumbersByStore(storeId).ToList();
                        QueuedMessage queuedMessage = new QueuedMessage()
                        {
                            StoreId = storeId,
                            MType = MTypeEnum.Message,
                            Title = CommonHelper.GetEnumDescription<MTypeEnum>(MTypeEnum.Message),
                            Date = costContractBill.CreatedOnUtc,
                            BillType = BillTypeEnum.CostContractBill,
                            BillNumber = costContractBill.BillNumber,
                            BillId = costContractBill.Id,
                            CreatedOnUtc = DateTime.Now
                        };
                        _queuedMessageService.InsertQueuedMessage(adminNumbers,queuedMessage);
                    }
                    catch (Exception ex)
                    {
                        _queuedMessageService.WriteLogs(ex.Message);
                    }
                    #endregion
                }

                //��������
                transaction.Commit();

                return new BaseResult { Success = true, Return = billId ?? 0, Message = Resources.Bill_CreateOrUpdateSuccessful };
            }
            catch (Exception)
            {
                //������񲻴��ڻ���Ϊ����ع�
                transaction?.Rollback();
                return new BaseResult { Success = false, Message = Resources.Bill_CreateOrUpdateFailed };
            }
            finally
            {
                //����������󶼻�رյ��������
                using (transaction) { }
            }
        }

        public BaseResult Auditing(int storeId, int userId, CostContractBill costContractBill)
        {
            var uow = CostContractBillsRepository.UnitOfWork;
            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();

                AuditingNoTran(storeId, userId, costContractBill);

                //��������
                transaction.Commit();

                return new BaseResult { Success = true, Message = "������˳ɹ�" };
            }
            catch (Exception)
            {
                //������񲻴��ڻ���Ϊ����ع�
                transaction?.Rollback();
                return new BaseResult { Success = false, Message = "�������ʧ��" };
            }
            finally
            {
                //����������󶼻�رյ��������
                using (transaction) { }
            }
        }

        public BaseResult AuditingNoTran(int storeId, int userId, CostContractBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "������˳ɹ�" };
            var failed = new BaseResult { Success = false, Message = "�������ʧ��" };

            try
            {

                #region �۳�������Ʒʣ������
                //�۳�������Ʒʣ������
                //ע��ʣ��������Ϊ��С��λ����
                if (bill.ContractType == 2)
                {
                    if (bill.Items != null && bill.Items.Count > 0)
                    {
                        var allProducts = _productService.GetProductsByIds(bill.StoreId, bill.Items.Select(ci => ci.ProductId).Distinct().ToArray());
                        var allOptions = _specificationAttributeService.GetSpecificationAttributeOptionByIds(storeId, allProducts.GetProductBigStrokeSmallUnitIds());

                        foreach (var item in bill.Items)
                        {
                            Product product = allProducts.Where(ap => ap.Id == item.ProductId).FirstOrDefault();
                            int thisQuantity = 0;
                            //��Ʒת����
                            var conversionQuantity2 = product.GetConversionQuantity(allOptions, item.UnitId ?? 0);
                            //��С��λ����
                            thisQuantity = item.Total == null ? 0 : (int)item.Total;

                            GiveQuotaOption giveQuotaOption = _giveQuotaService.GetGiveQuotaOptionById(item.GiveQuotaOptionId);
                            if (giveQuotaOption != null)
                            {
                                //�ü��¶�ȣ��۳����¶�ȣ�
                                //ע�⣺����ʣ������Ϊ��С��λ����
                                switch (bill.Month)
                                {
                                    case 1:
                                        giveQuotaOption.Jan_Balance -= thisQuantity;
                                        break;
                                    case 2:
                                        giveQuotaOption.Feb_Balance -= thisQuantity;
                                        break;
                                    case 3:
                                        giveQuotaOption.Mar_Balance -= thisQuantity;
                                        break;
                                    case 4:
                                        giveQuotaOption.Apr_Balance -= thisQuantity;
                                        break;
                                    case 5:
                                        giveQuotaOption.May_Balance -= thisQuantity;
                                        break;
                                    case 6:
                                        giveQuotaOption.Jun_Balance -= thisQuantity;
                                        break;
                                    case 7:
                                        giveQuotaOption.Jul_Balance -= thisQuantity;
                                        break;
                                    case 8:
                                        giveQuotaOption.Aug_Balance -= thisQuantity;
                                        break;
                                    case 9:
                                        giveQuotaOption.Sep_Balance -= thisQuantity;
                                        break;
                                    case 10:
                                        giveQuotaOption.Oct_Balance -= thisQuantity;
                                        break;
                                    case 11:
                                        giveQuotaOption.Nov_Balance -= thisQuantity;
                                        break;
                                    case 12:
                                        giveQuotaOption.Dec_Balance -= thisQuantity;
                                        break;
                                }
                                _giveQuotaService.UpdateGiveQuotaOption(giveQuotaOption);
                            }
                        }
                    }
                }
                #endregion

                #region �޸ĵ��ݱ�״̬
                bill.AuditedUserId = userId;
                bill.AuditedDate = DateTime.Now;
                bill.AuditedStatus = true;

                UpdateCostContractBill(bill);
                #endregion

                #region ����֪ͨ
                try
                {
                    //�Ƶ���
                    var userNumbers = new List<string>() { _userService.GetMobileNumberByUserId(bill.MakeUserId) };
                    QueuedMessage queuedMessage = new QueuedMessage()
                    {
                        StoreId = storeId,
                        MType = MTypeEnum.Audited,
                        Title = CommonHelper.GetEnumDescription<MTypeEnum>(MTypeEnum.Audited),
                        Date = bill.CreatedOnUtc,
                        BillType = BillTypeEnum.CostContractBill,
                        BillNumber = bill.BillNumber,
                        BillId = bill.Id,
                        CreatedOnUtc = DateTime.Now
                    };
                    _queuedMessageService.InsertQueuedMessage(userNumbers.ToList(),queuedMessage);
                }
                catch (Exception ex)
                {
                    _queuedMessageService.WriteLogs(ex.Message);
                }
                #endregion

                return successful;
            }
            catch (Exception)
            {
                return failed;
            }
        }

        public BaseResult Rejected(int storeId, int userId, CostContractBill bill)
        {
            var uow = CostContractBillsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();

                #region ����������Ʒʣ������
                //����������Ʒʣ������
                //ע��ʣ��������Ϊ��С��λ����
                if (bill.ContractType == 2)
                {
                    if (bill.Items != null && bill.Items.Count > 0)
                    {
                        var allProducts = _productService.GetProductsByIds(storeId, bill.Items.Select(ci => ci.ProductId).Distinct().ToArray());
                        var allOptions = _specificationAttributeService.GetSpecificationAttributeOptionByIds(storeId, allProducts.GetProductBigStrokeSmallUnitIds());

                        foreach (var item in bill.Items)
                        {
                            Product product = allProducts.Where(ap => ap.Id == item.ProductId).FirstOrDefault();
                            int thisQuantity = 0;
                            //��Ʒת����
                            var conversionQuantity2 = product.GetConversionQuantity(allOptions, item.UnitId ?? 0);
                            //��С��λ����
                            thisQuantity = item.Total == null ? 0 : (int)item.Total;

                            GiveQuotaOption giveQuotaOption = _giveQuotaService.GetGiveQuotaOptionById(item.GiveQuotaOptionId);
                            if (giveQuotaOption != null)
                            {
                                //�ü��¶�ȣ��۳����¶�ȣ�
                                //ע�⣺����ʣ������Ϊ��С��λ����
                                switch (bill.Month)
                                {
                                    case 1:
                                        giveQuotaOption.Jan_Balance += thisQuantity;
                                        break;
                                    case 2:
                                        giveQuotaOption.Feb_Balance += thisQuantity;
                                        break;
                                    case 3:
                                        giveQuotaOption.Mar_Balance += thisQuantity;
                                        break;
                                    case 4:
                                        giveQuotaOption.Apr_Balance += thisQuantity;
                                        break;
                                    case 5:
                                        giveQuotaOption.May_Balance += thisQuantity;
                                        break;
                                    case 6:
                                        giveQuotaOption.Jun_Balance += thisQuantity;
                                        break;
                                    case 7:
                                        giveQuotaOption.Jul_Balance += thisQuantity;
                                        break;
                                    case 8:
                                        giveQuotaOption.Aug_Balance += thisQuantity;
                                        break;
                                    case 9:
                                        giveQuotaOption.Sep_Balance += thisQuantity;
                                        break;
                                    case 10:
                                        giveQuotaOption.Oct_Balance += thisQuantity;
                                        break;
                                    case 11:
                                        giveQuotaOption.Nov_Balance += thisQuantity;
                                        break;
                                    case 12:
                                        giveQuotaOption.Dec_Balance += thisQuantity;
                                        break;
                                }
                                _giveQuotaService.UpdateGiveQuotaOption(giveQuotaOption);
                            }
                        }
                    }
                }
                #endregion

                #region �޸ĵ��ݱ�״̬
                bill.RejectUserId = userId;
                bill.RejectedDate = DateTime.Now;
                bill.RejectedStatus = true;
                UpdateCostContractBill(bill);
                #endregion

                //��������
                transaction.Commit();

                return new BaseResult { Success = true, Message = "���ݲ��سɹ�" };
            }
            catch (Exception)
            {
                //������񲻴��ڻ���Ϊ����ع�
                transaction?.Rollback();
                return new BaseResult { Success = false, Message = "���ݲ���ʧ��" };
            }
            finally
            {
                //����������󶼻�رյ��������
                using (transaction) { }
            }
        }
        public BaseResult Abandoned(int storeId, int userId, CostContractBill costContractBill)
        {
            var uow = CostContractBillsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();

                #region �޸ĵ��ݱ�״̬
                costContractBill.AbandonedUserId = userId;
                costContractBill.AbandonedDate = DateTime.Now;
                costContractBill.AbandonedStatus = true;
                UpdateCostContractBill(costContractBill);
                #endregion

                //��������
                transaction.Commit();

                return new BaseResult { Success = true, Message = "������ֹ�ɹ�" };
            }
            catch (Exception)
            {
                //������񲻴��ڻ���Ϊ����ع�
                transaction?.Rollback();
                return new BaseResult { Success = false, Message = "������ֹʧ��" };
            }
            finally
            {
                //����������󶼻�رյ��������
                using (transaction) { }
            }
        }

        /// <summary>
        /// ����ͻ��ն˷��ú�ͬ���öҸ���̯���
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="customerId"></param>
        /// <param name="bill"></param>
        /// <returns></returns>
        public IList<CostContractItem> CalcCostContractBalances(int? storeId, int customerId, CostContractBill bill)
        {
            var result = new List<CostContractItem>();
            if (bill == null)
                return result;

            //��ȡ��ͬ�Ҹ���¼
            var giveRecoreds = _giveQuotaService.GetQuotaRecordsByType(storeId, customerId, (int)GiveTypeEnum.Contract, bill?.Id ?? 0).ToList();

            //��ȡ����֧����֧ͬ����ϸ
            var expenseExpenditures = _costExpenditureBillService.GetCostExpenditureItemByCostContractId(storeId, bill?.Id ?? 0).ToList();

            //��ȡ��ǰ��ͬ������ϸ�а�������Ʒ
            var productIds = bill?.Items?.Select(it => it.ProductId).Distinct().ToArray();

            //��ȡ������Ʒ��Ϣ
            var allProducts = _productService.GetProductsByIds(storeId ?? 0, productIds).ToList();

            //��ȡ������Ʒ�������
            var allOptions = _specificationAttributeService.GetSpecificationAttributeOptionByIds(storeId, allProducts?.GetProductBigStrokeSmallUnitIds());

            //�Ҹ���¼����ʱ��������Ʒ�Ҹ�
            if (giveRecoreds?.Any() ?? false)
            {
                giveRecoreds.ForEach(s =>
                {
                    var product = allProducts.Where(ap => ap.Id == s.ProductId).FirstOrDefault();
                    //�Ҹ���λתΪС��λ���ټ���
                    var conversionQuantity = product?.GetConversionQuantity(allOptions, s.UnitId ?? 0) ?? 0;
                    //���¼���С��λ��
                    s.Quantity *= conversionQuantity;
                });
            }

            //��ͬ��ϸ
            foreach (var item in bill?.Items)
            {
                var product = allProducts.Where(ap => ap.Id == item.ProductId).FirstOrDefault();
                var conversionQuantity = product.GetConversionQuantity(allOptions, item.UnitId ?? 0);

                switch (bill.ContractType)
                {
                    //���¶Ҹ�
                    case 0:
                        {
                            //��Ʒ
                            if (item.CType == 0)
                            {
                               
                                decimal? jan=item.Jan, feb= item.Feb, mar= item.Mar, apr= item.Apr, may= item.May, jun= item.Jun, jul= item.Jul, aug= item.Aug, sep= item.Sep, oct= item.Oct, nov= item.Nov, dec= item.Dec;

                                //������ת��˫������
                                if (product.SmallUnitId != item.UnitId)
                                {
                                    jan = CalcDoubleQuality(item.Jan.Value.ToString("0.0"), conversionQuantity);
                                    feb = CalcDoubleQuality(item.Feb.Value.ToString("0.0"), conversionQuantity);
                                    mar = CalcDoubleQuality(item.Mar.Value.ToString("0.0"), conversionQuantity);
                                    apr = CalcDoubleQuality(item.Apr.Value.ToString("0.0"), conversionQuantity);
                                    may = CalcDoubleQuality(item.May.Value.ToString("0.0"), conversionQuantity);
                                    jun = CalcDoubleQuality(item.Jun.Value.ToString("0.0"), conversionQuantity);
                                    jul = CalcDoubleQuality(item.Jul.Value.ToString("0.0"), conversionQuantity);
                                    aug = CalcDoubleQuality(item.Aug.Value.ToString("0.0"), conversionQuantity);
                                    sep = CalcDoubleQuality(item.Sep.Value.ToString("0.0"), conversionQuantity);
                                    oct = CalcDoubleQuality(item.Oct.Value.ToString("0.0"), conversionQuantity);
                                    nov = CalcDoubleQuality(item.Nov.Value.ToString("0.0"), conversionQuantity);
                                    dec = CalcDoubleQuality(item.Dec.Value.ToString("0.0"), conversionQuantity);
                                }

                                //if (product.StrokeUnitId == item.UnitId)
                                //{
                                //    item.Jan = CalcDoubleQuality(item.Jan.ToString(), conversionQuantity);
                                //    item.Feb = CalcDoubleQuality(item.Feb.ToString(), conversionQuantity);
                                //    item.Mar = CalcDoubleQuality(item.Mar.ToString(), conversionQuantity);
                                //    item.Apr = CalcDoubleQuality(item.Apr.ToString(), conversionQuantity);
                                //    item.May = CalcDoubleQuality(item.May.ToString(), conversionQuantity);
                                //    item.Jun = CalcDoubleQuality(item.Jun.ToString(), conversionQuantity);
                                //    item.Jul = CalcDoubleQuality(item.Jul.ToString(), conversionQuantity);
                                //    item.Aug = CalcDoubleQuality(item.Aug.ToString(), conversionQuantity);
                                //    item.Sep = CalcDoubleQuality(item.Sep.ToString(), conversionQuantity);
                                //    item.Oct = CalcDoubleQuality(item.Oct.ToString(), conversionQuantity);
                                //    item.Nov = CalcDoubleQuality(item.Nov.ToString(), conversionQuantity);
                                //    item.Dec = CalcDoubleQuality(item.Dec.ToString(), conversionQuantity);
                                //}
                                //else if (product.BigUnitId == item.UnitId)
                                //{
                                //    item.Jan = CalcDoubleQuality(item.Jan.ToString(), conversionQuantity);
                                //    item.Feb = CalcDoubleQuality(item.Feb.ToString(), conversionQuantity);
                                //    item.Mar = CalcDoubleQuality(item.Mar.ToString(), conversionQuantity);
                                //    item.Apr = CalcDoubleQuality(item.Apr.ToString(), conversionQuantity);
                                //    item.May = CalcDoubleQuality(item.May.ToString(), conversionQuantity);
                                //    item.Jun = CalcDoubleQuality(item.Jun.ToString(), conversionQuantity);
                                //    item.Jul = CalcDoubleQuality(item.Jul.ToString(), conversionQuantity);
                                //    item.Aug = CalcDoubleQuality(item.Aug.ToString(), conversionQuantity);
                                //    item.Sep = CalcDoubleQuality(item.Sep.ToString(), conversionQuantity);
                                //    item.Oct = CalcDoubleQuality(item.Oct.ToString(), conversionQuantity);
                                //    item.Nov = CalcDoubleQuality(item.Nov.ToString(), conversionQuantity);
                                //    item.Dec = CalcDoubleQuality(item.Dec.ToString(), conversionQuantity);
                                //}

                                //�������
                                item.Jan_Balance = item.Jan > 0 ? (jan - giveRecoreds?.Where(g => g.Monthly == 1)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.Feb_Balance = item.Feb > 0 ? (feb - giveRecoreds?.Where(g => g.Monthly == 2)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.Mar_Balance = item.Mar > 0 ? (mar - giveRecoreds?.Where(g => g.Monthly == 3)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.Apr_Balance = item.Apr > 0 ? (apr - giveRecoreds?.Where(g => g.Monthly == 4)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.May_Balance = item.May > 0 ? (may - giveRecoreds?.Where(g => g.Monthly == 5)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.Jun_Balance = item.Jun > 0 ? (jun - giveRecoreds?.Where(g => g.Monthly == 6)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.Jul_Balance = item.Jul > 0 ? (jul - giveRecoreds?.Where(g => g.Monthly == 7)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.Aug_Balance = item.Aug > 0 ? (aug - giveRecoreds?.Where(g => g.Monthly == 8)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.Sep_Balance = item.Sep > 0 ? (sep - giveRecoreds?.Where(g => g.Monthly == 9)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.Oct_Balance = item.Oct > 0 ? (oct - giveRecoreds?.Where(g => g.Monthly == 10)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.Nov_Balance = item.Nov > 0 ? (nov - giveRecoreds?.Where(g => g.Monthly == 11)?.Sum(s => s.Quantity) ?? 0) : 0;
                                item.Dec_Balance = item.Dec > 0 ? (dec - giveRecoreds?.Where(g => g.Monthly == 12)?.Sum(s => s.Quantity) ?? 0) : 0;
                            }
                            //�ֽ�
                            else if (item.CType == 1)
                            {
                                item.Jan_Balance = item.Jan - expenseExpenditures?.Where(g => g.Month == 1)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.Feb_Balance = item.Feb - expenseExpenditures?.Where(g => g.Month == 2)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.Mar_Balance = item.Mar - expenseExpenditures?.Where(g => g.Month == 3)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.Apr_Balance = item.Apr - expenseExpenditures?.Where(g => g.Month == 4)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.May_Balance = item.May - expenseExpenditures?.Where(g => g.Month == 5)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.Jun_Balance = item.Jun - expenseExpenditures?.Where(g => g.Month == 6)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.Jul_Balance = item.Jul - expenseExpenditures?.Where(g => g.Month == 7)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.Aug_Balance = item.Aug - expenseExpenditures?.Where(g => g.Month == 8)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.Sep_Balance = item.Sep - expenseExpenditures?.Where(g => g.Month == 9)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.Oct_Balance = item.Oct - expenseExpenditures?.Where(g => g.Month == 10)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.Nov_Balance = item.Nov - expenseExpenditures?.Where(g => g.Month == 11)?.Sum(o => o.Amount ?? 0) ?? 0;
                                item.Dec_Balance = item.Dec - expenseExpenditures?.Where(g => g.Month == 12)?.Sum(o => o.Amount ?? 0) ?? 0;
                            }
                        }
                        break;
                    //����λ���ܼƶҸ�
                    case 1:
                        {
                            //��Ʒ
                            if (item.CType == 0)
                            {
                                //������ת��˫������
                                decimal? total = item.Total;
                                if (product.SmallUnitId != item.UnitId)
                                {
                                    total=CalcDoubleQuality(item.Total.Value.ToString("0.0"), conversionQuantity);
                                }

                                item.Total_Balance = item.Total > 0 ? total - giveRecoreds?.Sum(g => g.Quantity) ?? 0 : 0;
                            }
                            //�ֽ�
                            else if (item.CType == 1)
                            {
                                item.Total_Balance = item.Total > 0 ? item.Total - expenseExpenditures?.Sum(o => o.Amount ?? 0) ?? 0 : 0;
                            }

                        }
                        break;
                    //��������Ʒ��ȿۼ�
                    case 2:
                        {
                            //������ת��˫������
                            decimal? total = item.Total;
                            if (product.SmallUnitId != item.UnitId)
                            {
                                total = CalcDoubleQuality(item.Total.Value.ToString("0.0"), conversionQuantity);
                            }

                            item.Total_Balance = item.Total > 0 ? total - giveRecoreds?.Sum(g => g.Quantity) ?? 0 : 0;
                        }
                        break;
                }

                result.Add(item);
            }

            return result;
        }

        /// <summary>
        /// ���¶Ҹ�ʱ�жϷ��ú�ͬ�Ƿ����ظ�
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="year"></param>
        /// <param name="terminalId"></param>
        /// <param name="accountOptionId"></param>
        /// <param name="items"></param>
        /// <param name="errMsg"></param>
        /// <returns></returns>
        public bool CheckContract(int storeId, int year, int terminalId, int accountOptionId, List<CostContractItem> items, out string errMsg)
        {
            errMsg = string.Empty;

            var oldContracts = CostContractBillsRepository.Table.Include(cc => cc.Items).Where(cc=>cc.StoreId==storeId && cc.ContractType==0 && cc.CustomerId==terminalId && cc.Year==year && cc.AccountingOptionId==accountOptionId && cc.AuditedStatus==true && cc.RejectedStatus==false && cc.AbandonedStatus==false); //���еİ��¶Ҹ���ͬ

            if(oldContracts!=null && oldContracts.ToList().Count > 0)
            {
                foreach(var cos in oldContracts)
                {
                    if (cos.Items.Count(o => o.Jan > 0) > 0 && items.Count(n => n.Jan > 0) > 0)
                    {
                        errMsg = "һ�����к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.Feb > 0) > 0 && items.Count(n => n.Feb > 0) > 0)
                    {
                        errMsg = "�������к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.Apr > 0) > 0 && items.Count(n => n.Apr > 0) > 0)
                    {
                        errMsg = "�������к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.Mar > 0) > 0 && items.Count(n => n.Mar > 0) > 0)
                    {
                        errMsg = "�������к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.May > 0) > 0 && items.Count(n => n.May > 0) > 0)
                    {
                        errMsg = "�������к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.Jun > 0) > 0 && items.Count(n => n.Jun > 0) > 0)
                    {
                        errMsg = "�������к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.Jul > 0) > 0 && items.Count(n => n.Jul > 0) > 0)
                    {
                        errMsg = "�������к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.Aug > 0) > 0 && items.Count(n => n.Aug > 0) > 0)
                    {
                        errMsg = "�������к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.Sep > 0) > 0 && items.Count(n => n.Sep > 0) > 0)
                    {
                        errMsg = "�������к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.Oct > 0) > 0 && items.Count(n => n.Oct > 0) > 0)
                    {
                        errMsg = "ʮ�����к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.Nov > 0) > 0 && items.Count(n => n.Nov > 0) > 0)
                    {
                        errMsg = "ʮһ�����к�ͬ!";
                        return false;
                    }
                    else if (cos.Items.Count(o => o.Dec > 0) > 0 && items.Count(n => n.Dec > 0) > 0)
                    {
                        errMsg = "ʮ�������к�ͬ!";
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// ����˫������Ʒ����ת��
        /// </summary>
        /// <param name="num"></param>
        /// <param name="conversionQuantity"></param>
        /// <returns></returns>
        protected decimal CalcDoubleQuality(string num, int conversionQuantity)
        {
            if (num.IndexOf(".") > -1)
            {
                string[] idArray = num.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                double[] idsDoubles = Array.ConvertAll<string, double>(idArray, s => Convert.ToDouble(s));
                int[] idInts = Array.ConvertAll<double, int>(idsDoubles, s => Convert.ToInt32(s));

                return idInts[0] * conversionQuantity + idInts[1];
            }

            return int.Parse(num) * conversionQuantity;
        }
    }
}
