using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Data;
using DCMS.Core.Domain.WareHouses;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Events;
using DCMS.Services.Products;
using DCMS.Services.Users;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using DCMS.Services.Caching;

namespace DCMS.Services.WareHouses
{
    /// <summary>
    /// ���ڱ�ʾ�����Ʒ��ϵ�����
    /// </summary>
    public partial class CombinationProductBillService : BaseService, ICombinationProductBillService
    {
        private readonly IStockService _stockService;
        private readonly IProductService _productService;
        private readonly ISpecificationAttributeService _specificationAttributeService;
        private readonly IUserService _userService;

        public CombinationProductBillService(IServiceGetter getter,
            IStaticCacheManager cacheManager,
            IEventPublisher eventPublisher,
            IStockService stockService,
            IProductService productService,
            ISpecificationAttributeService specificationAttributeService,
            IUserService userService
            ) : base(getter, cacheManager, eventPublisher)
        {
            _stockService = stockService;
            _productService = productService;
            _specificationAttributeService = specificationAttributeService;
            _userService = userService;
        }

        #region ����

        public bool Exists(int billId)
        {
            return CombinationProductBillsRepository.TableNoTracking.Where(a => a.Id == billId).Count() > 0;
        }

        public virtual IPagedList<CombinationProductBill> GetAllCombinationProductBills(int? store, int? makeuserId, int? wareHouseId, string billNumber = "", bool? status = null, DateTime? start = null, DateTime? end = null, bool? isShowReverse = null, bool? sortByAuditedTime = null, string remark = "", int pageIndex = 0, int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;
            var query = CombinationProductBillsRepository.Table;

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

            if (wareHouseId.HasValue && wareHouseId.Value > 0)
            {
                query = query.Where(c => c.WareHouseId == wareHouseId);
            }

            if (!string.IsNullOrWhiteSpace(billNumber))
            {
                query = query.Where(c => c.BillNumber.Contains(billNumber));
            }

            if (status.HasValue)
            {
                query = query.Where(c => c.AuditedStatus == status);
            }

            if (start.HasValue)
            {
                query = query.Where(o => start.Value <= o.CreatedOnUtc);
            }

            if (end.HasValue)
            {
                query = query.Where(o => end.Value >= o.CreatedOnUtc);
            }

            if (isShowReverse.HasValue)
            {
                query = query.Where(c => c.ReversedStatus == isShowReverse);
            }

            if (!string.IsNullOrWhiteSpace(remark))
            {
                query = query.Where(c => c.Remark.Contains(remark));
            }

            //�����ʱ������
            if (sortByAuditedTime.HasValue && sortByAuditedTime.Value == true)
            {
                query = query.OrderByDescending(c => c.AuditedDate);
            }
            //Ĭ�ϴ���ʱ������
            else
            {
                query = query.OrderByDescending(c => c.CreatedOnUtc);
            }


            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<CombinationProductBill>(plists, pageIndex, pageSize, totalCount);
        }

        public virtual IList<CombinationProductBill> GetAllCombinationProductBills()
        {
            var query = from c in CombinationProductBillsRepository.Table
                        orderby c.Id
                        select c;

            var categories = query.ToList();
            return categories;
        }

        public virtual CombinationProductBill GetCombinationProductBillById(int? store, int combinationProductBillId)
        {
            if (combinationProductBillId == 0)
            {
                return null;
            }

            var key = DCMSDefaults.COMBINATIONPRODUCTBILL_BY_ID_KEY.FillCacheKey(store ?? 0, combinationProductBillId);
            return _cacheManager.Get(key, () =>
            {
                return CombinationProductBillsRepository.ToCachedGetById(combinationProductBillId);
            });
        }

        public virtual CombinationProductBill GetCombinationProductBillById(int? store, int combinationProductBillId, bool isInclude = false)
        {
            if (combinationProductBillId == 0)
            {
                return null;
            }

            if (isInclude)
            {
                var query = CombinationProductBillsRepository_RO.Table.Include(cp => cp.Items);
                return query.FirstOrDefault(c => c.Id == combinationProductBillId);
            }
            return CombinationProductBillsRepository.ToCachedGetById(combinationProductBillId);
        }


        public virtual CombinationProductBill GetCombinationProductBillByNumber(int? store, string billNumber)
        {
            var key = DCMSDefaults.COMBINATIONPRODUCTBILL_BY_NUMBER_KEY.FillCacheKey(store ?? 0, billNumber);
            return _cacheManager.Get(key, () =>
            {
                var query = CombinationProductBillsRepository.Table;
                var bill = query.Where(a => a.BillNumber == billNumber).FirstOrDefault();
                return bill;
            });
        }



        public virtual void InsertCombinationProductBill(CombinationProductBill bill)
        {
            if (bill == null)
            {
                throw new ArgumentNullException("bill");
            }

            var uow = CombinationProductBillsRepository.UnitOfWork;
            CombinationProductBillsRepository.Insert(bill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(bill);
        }

        public virtual void UpdateCombinationProductBill(CombinationProductBill bill)
        {
            if (bill == null)
            {
                throw new ArgumentNullException("bill");
            }

            var uow = CombinationProductBillsRepository.UnitOfWork;
            CombinationProductBillsRepository.Update(bill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(bill);
        }

        public virtual void DeleteCombinationProductBill(CombinationProductBill bill)
        {
            if (bill == null)
            {
                throw new ArgumentNullException("bill");
            }

            var uow = CombinationProductBillsRepository.UnitOfWork;
            CombinationProductBillsRepository.Delete(bill);
            uow.SaveChanges();

            //event notification
            _eventPublisher.EntityDeleted(bill);
        }


        #endregion

        #region ������Ŀ


        public virtual IPagedList<CombinationProductItem> GetCombinationProductItemsByCombinationProductBillId(int combinationProductBillId, int? userId, int? storeId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (combinationProductBillId == 0)
            {
                return new PagedList<CombinationProductItem>(new List<CombinationProductItem>(), pageIndex, pageSize);
            }

            var key = DCMSDefaults.COMBINATIONPRODUCTBILLITEM_ALL_KEY.FillCacheKey(storeId, combinationProductBillId, pageIndex, pageSize, userId);

            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CombinationProductItemsRepository.Table
                            where pc.CombinationProductBillId == combinationProductBillId
                            orderby pc.Id
                            select pc;
                //var productCombinationProductBills = new PagedList<CombinationProductItem>(query.ToList(), pageIndex, pageSize);
                //return productCombinationProductBills;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CombinationProductItem>(plists, pageIndex, pageSize, totalCount);
            });
        }

        public List<CombinationProductItem> GetCombinationProductItemList(int combinationProductBillId)
        {
            List<CombinationProductItem> combinationProductItems = null;
            var query = CombinationProductItemsRepository.Table;
            combinationProductItems = query.Where(a => a.CombinationProductBillId == combinationProductBillId).ToList();
            return combinationProductItems;
        }


        public virtual CombinationProductItem GetCombinationProductItemById(int? store, int combinationProductItemId)
        {
            if (combinationProductItemId == 0)
            {
                return null;
            }

            return CombinationProductItemsRepository.ToCachedGetById(combinationProductItemId);
        }

        public virtual void InsertCombinationProductItem(CombinationProductItem combinationProductItem)
        {
            if (combinationProductItem == null)
            {
                throw new ArgumentNullException("combinationProductItem");
            }

            var uow = CombinationProductItemsRepository.UnitOfWork;
            CombinationProductItemsRepository.Insert(combinationProductItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(combinationProductItem);
        }

        public virtual void UpdateCombinationProductItem(CombinationProductItem combinationProductItem)
        {
            if (combinationProductItem == null)
            {
                throw new ArgumentNullException("combinationProductItem");
            }

            var uow = CombinationProductItemsRepository.UnitOfWork;
            CombinationProductItemsRepository.Update(combinationProductItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(combinationProductItem);
        }

        public virtual void DeleteCombinationProductItem(CombinationProductItem combinationProductItem)
        {
            if (combinationProductItem == null)
            {
                throw new ArgumentNullException("combinationProductItem");
            }

            var uow = CombinationProductItemsRepository.UnitOfWork;
            CombinationProductItemsRepository.Delete(combinationProductItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(combinationProductItem);
        }


        #endregion

        public BaseResult BillCreateOrUpdate(int storeId, int userId, int? billId, CombinationProductBill bill, CombinationProductBillUpdate data, List<CombinationProductItem> items, List<ProductStockItem> productStockItemThiss, bool isAdmin = false)
        {
            var uow = CombinationProductBillsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {
                transaction = uow.BeginOrUseTransaction();

                if (billId.HasValue && billId.Value != 0)
                {
                    #region ������ϵ�
                    if (bill != null)
                    {
                        bill.WareHouseId = data.WareHouseId;
                        bill.ProductId = data.ProductId;
                        bill.Quantity = data.Quantity;
                        bill.ProductCost = data.ProductCost;
                        bill.ProductCostAmount = data.ProductCostAmount;
                        bill.Remark = data.Remark;
                        bill.CostDifference = data.CostDifference;
                        UpdateCombinationProductBill(bill);
                    }

                    #endregion
                }
                else
                {
                    #region �����ϵ�

                    bill.WareHouseId = data.WareHouseId;
                    bill.ProductId = data.ProductId;
                    bill.Quantity = data.Quantity;
                    bill.ProductCost = data.ProductCost ?? 0;
                    bill.ProductCostAmount = data.ProductCostAmount ?? 0;
                    bill.Remark = data.Remark;
                    bill.CostDifference = data.CostDifference ?? 0;

                    bill.StoreId = storeId;
                    //��������
                    bill.CreatedOnUtc = DateTime.Now;
                    //���ݱ��
                    bill.BillNumber = CommonHelper.GetBillNumber("ZHD", storeId);
                    //�Ƶ���
                    bill.MakeUserId = userId;
                    //״̬(���)
                    bill.AuditedStatus = false;
                    bill.AuditedDate = null;
                    //���״̬
                    bill.ReversedStatus = false;
                    bill.ReversedDate = null;
                    //��ע
                    bill.Remark = data.Remark;
                    bill.Operation = data.Operation;//��ʶ����Դ

                    InsertCombinationProductBill(bill);

                    #endregion
                }

                #region ������ϵ���Ŀ

                data.Items.ForEach(p =>
                {
                    if (p.ProductId != 0)
                    {
                        var sd = GetCombinationProductItemById(storeId, p.Id);
                        if (sd == null)
                        {
                            //׷����
                            if (bill.Items.Count(cp => cp.Id == p.Id) == 0)
                            {
                                var item = p;
                                item.CombinationProductBillId = bill.Id;
                                item.CreatedOnUtc = DateTime.Now;
                                item.StoreId = storeId;
                                InsertCombinationProductItem(item);
                                //���ų�
                                p.Id = item.Id;
                                if (!bill.Items.Select(s => s.Id).Contains(item.Id))
                                {
                                    bill.Items.Add(item);
                                }
                            }
                        }
                        else
                        {
                            //���������
                            sd.CombinationProductBillId = bill.Id;
                            sd.ProductId = p.ProductId;
                            sd.SubProductQuantity = p.SubProductQuantity;
                            sd.SubProductUnitId = p.SubProductUnitId;
                            sd.UnitId = p.UnitId;
                            sd.Quantity = p.Quantity;
                            sd.CostPrice = p.CostPrice;
                            sd.CostAmount = p.CostAmount;
                            sd.Stock = p.Stock;
                            sd.Remark = p.Remark;
                            UpdateCombinationProductItem(sd);
                        }
                    }
                });

                #endregion

                #region Grid �Ƴ���ӿ��Ƴ�ɾ����

                bill.Items.ToList().ForEach(p =>
                {
                    if (data.Items.Count(cp => cp.Id == p.Id) == 0)
                    {
                        bill.Items.Remove(p);
                        var item = GetCombinationProductItemById(storeId, p.Id);
                        if (item != null)
                        {
                            DeleteCombinationProductItem(item);
                        }
                    }
                });

                #endregion

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

        public BaseResult Auditing(int storeId, int userId, CombinationProductBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "������˳ɹ�" };
            var failed = new BaseResult { Success = false, Message = "�������ʧ��" };

            var uow = CombinationProductBillsRepository.UnitOfWork;
            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();
                //��ʷ����¼
                Tuple<List<ProductStockItem>, Tuple<StockInOutRecord, StockInOutRecord>, Tuple<List<StockFlow>, List<StockFlow>>, Tuple<List<StockInOutRecordStockFlow>, List<StockInOutRecordStockFlow>>, Tuple<List<Stock>, List<Stock>>> historyDatas1 = null;
                Tuple<List<ProductStockItem>, Tuple<StockInOutRecord, StockInOutRecord>, Tuple<List<StockFlow>, List<StockFlow>>, Tuple<List<StockInOutRecordStockFlow>, List<StockInOutRecordStockFlow>>, Tuple<List<Stock>, List<Stock>>> historyDatas2 = null;

                #region �޸Ŀ��
                try
                {
                    //����Ʒ ���
                    List<ProductStockItem> productStockItems1 = new List<ProductStockItem>();
                    //����Ʒ��λ
                    var p = _productService.GetProductById(storeId, bill.ProductId);
                    productStockItems1.Add(new ProductStockItem()
                    {
                        ProductId = bill.ProductId,
                        Quantity = bill.Quantity.Value,
                        ProductName = p?.Name,
                        ProductCode = p?.ProductCode,
                        UnitId = p.SmallUnitId,
                        SmallUnitId = p.SmallUnitId,
                        BigUnitId = p.BigUnitId ?? 0
                    });
                    //�����ֻ�
                    historyDatas1 = _stockService.AdjustStockQty<CombinationProductBill, CombinationProductItem>(bill, _productService, _specificationAttributeService, DirectionEnum.In, StockQuantityType.CurrentQuantity, bill.WareHouseId, productStockItems1, StockFlowChangeTypeEnum.Audited);

                    //����Ʒ ����
                    List<ProductStockItem> productStockItems2 = new List<ProductStockItem>();

                    var allProducts = _productService.GetProductsByIds(bill.StoreId, bill.Items.Select(pr => pr.ProductId).Distinct().ToArray());
                    var allOptions = _specificationAttributeService.GetSpecificationAttributeOptionByIds(storeId, allProducts.GetProductBigStrokeSmallUnitIds());
                    foreach (var item in bill.Items)
                    {
                        if (item.ProductId != 0)
                        {
                            var product = allProducts.Where(ap => ap.Id == item.ProductId).FirstOrDefault();
                            if (product != null)
                            {
                                ProductStockItem productStockItem = productStockItems2.Where(a => a.ProductId == item.ProductId).FirstOrDefault();
                                //��Ʒת����
                                var conversionQuantity = product.GetConversionQuantity(allOptions, item.SubProductUnitId ?? 0);
                                //��������� = ��λת���� * ����
                                int thisQuantity = (item.Quantity ?? 0) * conversionQuantity;
                                if (productStockItem != null)
                                {
                                    productStockItem.Quantity += thisQuantity;
                                }
                                else
                                {
                                    productStockItem = new ProductStockItem
                                    {
                                        ProductId = item.ProductId,
                                        UnitId = item.UnitId,
                                        SmallUnitId = product.SmallUnitId,
                                        BigUnitId = product.BigUnitId ?? 0,
                                        ProductName = allProducts.Where(s => s.Id == item.ProductId).FirstOrDefault()?.Name,
                                        ProductCode = allProducts.Where(s => s.Id == item.ProductId).FirstOrDefault()?.ProductCode,
                                        Quantity = thisQuantity
                                    };

                                    productStockItems2.Add(productStockItem);
                                }
                            }
                        }

                    }

                    List<ProductStockItem> productStockItemThiss = new List<ProductStockItem>();
                    if (productStockItems2 != null && productStockItems2.Count > 0)
                    {
                        productStockItems2.ForEach(psi =>
                        {
                            ProductStockItem productStockItem = new ProductStockItem
                            {
                                ProductId = psi.ProductId,
                                UnitId = psi.UnitId,
                                SmallUnitId = psi.SmallUnitId,
                                BigUnitId = psi.BigUnitId,
                                ProductName = allProducts.Where(s => s.Id == psi.ProductId).FirstOrDefault()?.Name,
                                ProductCode = allProducts.Where(s => s.Id == psi.ProductId).FirstOrDefault()?.ProductCode,
                                Quantity = psi.Quantity * (-1)
                            };
                            productStockItemThiss.Add(productStockItem);
                        });
                    }

                    //�����ֻ�
                    historyDatas2 = _stockService.AdjustStockQty<CombinationProductBill, CombinationProductItem>(bill, _productService, _specificationAttributeService, DirectionEnum.Out, StockQuantityType.CurrentQuantity, bill.WareHouseId, productStockItemThiss, StockFlowChangeTypeEnum.Audited);
                }
                catch (Exception)
                {
                }

                #endregion

                #region �޸ĵ��ݱ�״̬
                bill.AuditedUserId = userId;
                bill.AuditedDate = DateTime.Now;
                bill.AuditedStatus = true;

                UpdateCombinationProductBill(bill);
                #endregion


                //��������
                transaction.Commit();

                return successful;
            }
            catch (Exception)
            {
                //������񲻴��ڻ���Ϊ����ع�
                transaction?.Rollback();
                return failed;
            }
            finally
            {
                //����������󶼻�رյ��������
                using (transaction) { }
            }
        }

        public BaseResult Reverse(int userId, CombinationProductBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "���ݺ��ɹ�" };
            var failed = new BaseResult { Success = false, Message = "���ݺ��ʧ��" };

            var uow = CombinationProductBillsRepository.UnitOfWork;
            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();
                //��ʷ����¼
                Tuple<List<ProductStockItem>, Tuple<StockInOutRecord, StockInOutRecord>, Tuple<List<StockFlow>, List<StockFlow>>, Tuple<List<StockInOutRecordStockFlow>, List<StockInOutRecordStockFlow>>, Tuple<List<Stock>, List<Stock>>> historyDatas1 = null;
                Tuple<List<ProductStockItem>, Tuple<StockInOutRecord, StockInOutRecord>, Tuple<List<StockFlow>, List<StockFlow>>, Tuple<List<StockInOutRecordStockFlow>, List<StockInOutRecordStockFlow>>, Tuple<List<Stock>, List<Stock>>> historyDatas2 = null;

                #region �޸Ŀ��
                try
                {
                    //����Ʒ ����
                    List<ProductStockItem> productStockItems1 = new List<ProductStockItem>();
                    //����Ʒ��λ
                    var p = _productService.GetProductById(bill.StoreId, bill.ProductId);
                    productStockItems1.Add(new ProductStockItem()
                    {
                        ProductId = bill.ProductId,
                        Quantity = bill.Quantity.Value * (-1),
                        ProductName = p?.Name,
                        ProductCode = p?.ProductCode,
                        UnitId = p.SmallUnitId,
                        SmallUnitId = p.SmallUnitId,
                        BigUnitId = p.BigUnitId ?? 0
                    });

                    historyDatas1 = _stockService.AdjustStockQty<CombinationProductBill, CombinationProductItem>(bill, _productService, _specificationAttributeService, DirectionEnum.Out, StockQuantityType.CurrentQuantity, bill.WareHouseId, productStockItems1, StockFlowChangeTypeEnum.Reversed);

                    //����Ʒ ���
                    List<ProductStockItem> productStockItems2 = new List<ProductStockItem>();

                    var allProducts = _productService.GetProductsByIds(bill.StoreId, bill.Items.Select(pr => pr.ProductId).Distinct().ToArray());
                    var allOptions = _specificationAttributeService.GetSpecificationAttributeOptionByIds(bill.StoreId, allProducts.GetProductBigStrokeSmallUnitIds());
                    foreach (var item in bill.Items)
                    {
                        var product = allProducts.Where(ap => ap.Id == item.ProductId).FirstOrDefault();
                        ProductStockItem productStockItem = productStockItems2.Where(a => a.ProductId == item.ProductId).FirstOrDefault();
                        //��Ʒת����
                        var conversionQuantity = product.GetConversionQuantity(allOptions, item.SubProductUnitId ?? 0);
                        //��������� = ��λת���� * ����
                        int thisQuantity = (item.Quantity ?? 0) * conversionQuantity;
                        if (productStockItem != null)
                        {
                            productStockItem.Quantity += thisQuantity;
                        }
                        else
                        {
                            productStockItem = new ProductStockItem
                            {
                                ProductId = item.ProductId,
                                UnitId = item.UnitId,
                                SmallUnitId = product.SmallUnitId,
                                BigUnitId = product.BigUnitId ?? 0,
                                ProductName = allProducts.Where(s => s.Id == item.ProductId).FirstOrDefault()?.Name,
                                ProductCode = allProducts.Where(s => s.Id == item.ProductId).FirstOrDefault()?.ProductCode,
                                Quantity = thisQuantity
                            };

                            productStockItems2.Add(productStockItem);
                        }

                    }

                    historyDatas2 = _stockService.AdjustStockQty<CombinationProductBill, CombinationProductItem>(bill, _productService, _specificationAttributeService, DirectionEnum.In, StockQuantityType.CurrentQuantity, bill.WareHouseId, productStockItems2, StockFlowChangeTypeEnum.Reversed);
                }
                catch (Exception)
                {
                }

                #endregion

                #region �޸ĵ��ݱ�״̬
                bill.ReversedUserId = userId;
                bill.ReversedDate = DateTime.Now;
                bill.ReversedStatus = true;

                UpdateCombinationProductBill(bill);
                #endregion


                //��������
                transaction.Commit();

                return successful;
            }
            catch (Exception)
            {
                //������񲻴��ڻ���Ϊ����ع�
                transaction?.Rollback();
                return failed;
            }
            finally
            {
                //����������󶼻�رյ��������
                using (transaction) { }
            }
        }

    }
}
