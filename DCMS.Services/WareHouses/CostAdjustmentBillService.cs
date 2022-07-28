using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Data;
using DCMS.Core.Domain.WareHouses;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Events;
using DCMS.Services.Finances;
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
    /// ���ڱ�ʾ�ɱ����۵�����
    /// </summary>
    public partial class CostAdjustmentBillService : BaseService, ICostAdjustmentBillService
    {

        private readonly IProductService _productService;
        private readonly IUserService _userService;
        private readonly IRecordingVoucherService _recordingVoucherService;

        public CostAdjustmentBillService(IServiceGetter getter,
            IStaticCacheManager cacheManager,
            IEventPublisher eventPublisher,
            IProductService productService,
            IRecordingVoucherService recordingVoucherService,
            IUserService userService
            ) : base(getter, cacheManager, eventPublisher)
        {
            _productService = productService;
            _userService = userService;
            _recordingVoucherService = recordingVoucherService;
        }

        #region ����

        public bool Exists(int billId)
        {
            return CostAdjustmentBillsRepository.TableNoTracking.Where(a => a.Id == billId).Count() > 0;
        }

        public virtual IPagedList<CostAdjustmentBill> GetAllCostAdjustmentBills(int? store, int? makeuserId, string billNumber = "", bool? status = null, DateTime? start = null, DateTime? end = null, bool? isShowReverse = null, bool? sortByAuditedTime = null, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;
            var query = CostAdjustmentBillsRepository.Table;

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
                query = query.Where(o => start.Value <= o.AdjustmentDate);
            }

            if (end.HasValue)
            {
                query = query.Where(o => end.Value >= o.AdjustmentDate);
            }

            if (isShowReverse.HasValue)
            {
                query = query.Where(c => c.ReversedStatus == isShowReverse);
            }

            //���������
            if (sortByAuditedTime.HasValue && sortByAuditedTime.Value == true)
            {
                query = query.OrderByDescending(c => c.AuditedDate);
            }
            //Ĭ�ϴ���ʱ��
            else
            {
                query = query.OrderByDescending(c => c.CreatedOnUtc);
            }

            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<CostAdjustmentBill>(plists, pageIndex, pageSize, totalCount);
        }

        public virtual IList<CostAdjustmentBill> GetAllCostAdjustmentBills()
        {
            var query = from c in CostAdjustmentBillsRepository.Table
                        orderby c.Id
                        select c;

            var categories = query.ToList();
            return categories;
        }

        public virtual CostAdjustmentBill GetCostAdjustmentBillById(int? store, int costAdjustmentBillId)
        {
            if (costAdjustmentBillId == 0)
            {
                return null;
            }

            var key = DCMSDefaults.CONSTADJUSTMENTBILL_BY_ID_KEY.FillCacheKey(store ?? 0, costAdjustmentBillId);
            return _cacheManager.Get(key, () =>
            {
                return CostAdjustmentBillsRepository.ToCachedGetById(costAdjustmentBillId);
            });
        }

        public virtual CostAdjustmentBill GetCostAdjustmentBillById(int? store, int costAdjustmentBillId, bool isInclude = false)
        {
            if (costAdjustmentBillId == 0)
            {
                return null;
            }

            if (isInclude)
            {
                var query = CostAdjustmentBillsRepository_RO.Table.Include(ca => ca.Items);
                return query.FirstOrDefault(c => c.Id == costAdjustmentBillId);
            }
            return CostAdjustmentBillsRepository.ToCachedGetById(costAdjustmentBillId);
        }


        public virtual CostAdjustmentBill GetCostAdjustmentBillByNumber(int? store, string billNumber)
        {
            var key = DCMSDefaults.CONSTADJUSTMENTBILL_BY_NUMBER_KEY.FillCacheKey(store ?? 0, billNumber);
            return _cacheManager.Get(key, () =>
            {
                var query = CostAdjustmentBillsRepository.Table;
                var costAdjustmentBill = query.Where(a => a.BillNumber == billNumber).FirstOrDefault();
                return costAdjustmentBill;
            });
        }



        public virtual void InsertCostAdjustmentBill(CostAdjustmentBill costAdjustmentBill)
        {
            if (costAdjustmentBill == null)
            {
                throw new ArgumentNullException("costAdjustmentBill");
            }

            var uow = CostAdjustmentBillsRepository.UnitOfWork;
            CostAdjustmentBillsRepository.Insert(costAdjustmentBill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(costAdjustmentBill);
        }

        public virtual void UpdateCostAdjustmentBill(CostAdjustmentBill costAdjustmentBill)
        {
            if (costAdjustmentBill == null)
            {
                throw new ArgumentNullException("costAdjustmentBill");
            }

            var uow = CostAdjustmentBillsRepository.UnitOfWork;
            CostAdjustmentBillsRepository.Update(costAdjustmentBill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(costAdjustmentBill);
        }

        public virtual void DeleteCostAdjustmentBill(CostAdjustmentBill costAdjustmentBill)
        {
            if (costAdjustmentBill == null)
            {
                throw new ArgumentNullException("costAdjustmentBill");
            }

            var uow = CostAdjustmentBillsRepository.UnitOfWork;
            CostAdjustmentBillsRepository.Delete(costAdjustmentBill);
            uow.SaveChanges();

            //event notification
            _eventPublisher.EntityDeleted(costAdjustmentBill);
        }


        #endregion

        #region ������Ŀ


        public virtual IPagedList<CostAdjustmentItem> GetCostAdjustmentItemsByCostAdjustmentBillId(int costAdjustmentBillId, int? userId, int? storeId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (costAdjustmentBillId == 0)
            {
                return new PagedList<CostAdjustmentItem>(new List<CostAdjustmentItem>(), pageIndex, pageSize);
            }

            var key = DCMSDefaults.CONSTADJUSTMENTBILLITEM_ALL_KEY.FillCacheKey(storeId, costAdjustmentBillId, pageIndex, pageSize, userId);

            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CostAdjustmentItemsRepository.Table
                            where pc.CostAdjustmentBillId == costAdjustmentBillId
                            orderby pc.Id
                            select pc;
                //var productCostAdjustmentBills = new PagedList<CostAdjustmentItem>(query.ToList(), pageIndex, pageSize);
                //return productCostAdjustmentBills;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CostAdjustmentItem>(plists, pageIndex, pageSize, totalCount);
            });
        }

        public virtual CostAdjustmentItem GetCostAdjustmentItemById(int? store, int costAdjustmentItemId)
        {
            if (costAdjustmentItemId == 0)
            {
                return null;
            }

            return CostAdjustmentItemsRepository.ToCachedGetById(costAdjustmentItemId);
        }

        public virtual void InsertCostAdjustmentItem(CostAdjustmentItem costAdjustmentItem)
        {
            if (costAdjustmentItem == null)
            {
                throw new ArgumentNullException("costAdjustmentItem");
            }

            var uow = CostAdjustmentItemsRepository.UnitOfWork;
            CostAdjustmentItemsRepository.Insert(costAdjustmentItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(costAdjustmentItem);
        }

        public virtual void UpdateCostAdjustmentItem(CostAdjustmentItem costAdjustmentItem)
        {
            if (costAdjustmentItem == null)
            {
                throw new ArgumentNullException("costAdjustmentItem");
            }

            var uow = CostAdjustmentItemsRepository.UnitOfWork;
            CostAdjustmentItemsRepository.Update(costAdjustmentItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(costAdjustmentItem);
        }

        public virtual void DeleteCostAdjustmentItem(CostAdjustmentItem costAdjustmentItem)
        {
            if (costAdjustmentItem == null)
            {
                throw new ArgumentNullException("costAdjustmentItem");
            }

            var uow = CostAdjustmentItemsRepository.UnitOfWork;
            CostAdjustmentItemsRepository.Delete(costAdjustmentItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(costAdjustmentItem);
        }


        #endregion

        public BaseResult BillCreateOrUpdate(int storeId, int userId, int? billId, CostAdjustmentBill costAdjustmentBill, CostAdjustmentBillUpdate data, List<CostAdjustmentItem> items, bool isAdmin = false)
        {
            var uow = CostAdjustmentBillsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();


                if (billId.HasValue && billId.Value != 0)
                {
                    #region ���³ɱ����۵�
                    if (costAdjustmentBill != null)
                    {
                        costAdjustmentBill.AdjustmentDate = data.AdjustmentDate;
                        costAdjustmentBill.AdjustmentByMinUnit = data.AdjustmentByMinUnit;
                        costAdjustmentBill.Remark = data.Remark;
                        UpdateCostAdjustmentBill(costAdjustmentBill);
                    }

                    #endregion
                }
                else
                {
                    #region ��ӳɱ����۵�

                    costAdjustmentBill.AdjustmentDate = data.AdjustmentDate;
                    costAdjustmentBill.AdjustmentByMinUnit = data.AdjustmentByMinUnit;
                    costAdjustmentBill.Remark = data.Remark;

                    costAdjustmentBill.StoreId = storeId;
                    //��������
                    costAdjustmentBill.CreatedOnUtc = DateTime.Now;
                    //���ݱ��
                    costAdjustmentBill.BillNumber = CommonHelper.GetBillNumber("TJD", storeId);
                    //�Ƶ���
                    costAdjustmentBill.MakeUserId = userId;
                    //״̬(���)
                    costAdjustmentBill.AuditedStatus = false;
                    costAdjustmentBill.AuditedDate = null;
                    //���״̬
                    costAdjustmentBill.ReversedStatus = false;
                    costAdjustmentBill.ReversedDate = null;
                    //��ע
                    costAdjustmentBill.Remark = data.Remark;
                    costAdjustmentBill.Operation = data.Operation;//��ʶ����Դ

                    InsertCostAdjustmentBill(costAdjustmentBill);

                    #endregion
                }

                #region ���³ɱ�������Ŀ

                data.Items.ForEach(p =>
                {
                    if (p.ProductId != 0)
                    {
                        var sd = GetCostAdjustmentItemById(storeId, p.Id);
                        if (sd == null)
                        {
                            //׷����
                            if (costAdjustmentBill.Items.Count(cp => cp.Id == p.Id) == 0)
                            {
                                var item = p;
                                item.CostAdjustmentBillId = costAdjustmentBill.Id;
                                item.AdjustedPrice = p.AdjustedPrice ?? 0;
                                item.AdjustmentPriceBefore = p.AdjustmentPriceBefore ?? 0;
                                item.CreatedOnUtc = DateTime.Now;
                                item.StoreId = storeId;
                                InsertCostAdjustmentItem(item);
                                //���ų�
                                p.Id = item.Id;
                                if (!costAdjustmentBill.Items.Select(s => s.Id).Contains(item.Id))
                                {
                                    costAdjustmentBill.Items.Add(item);
                                }
                            }
                        }
                        else
                        {
                            //���������
                            sd.CostAdjustmentBillId = costAdjustmentBill.Id;
                            sd.ProductId = p.ProductId;
                            sd.UnitId = p.UnitId;
                            sd.AdjustmentPriceBefore = p.AdjustmentPriceBefore ?? 0;
                            sd.AdjustedPrice = p.AdjustedPrice ?? 0;
                            UpdateCostAdjustmentItem(sd);
                        }
                    }
                });

                #endregion

                #region Grid �Ƴ���ӿ��Ƴ�ɾ����

                costAdjustmentBill.Items.ToList().ForEach(p =>
                {
                    if (data.Items.Count(cp => cp.Id == p.Id) == 0)
                    {
                        costAdjustmentBill.Items.Remove(p);
                        var item = GetCostAdjustmentItemById(storeId, p.Id);
                        if (item != null)
                        {
                            DeleteCostAdjustmentItem(item);
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

        public BaseResult Auditing(int storeId, int userId, CostAdjustmentBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "������˳ɹ�" };
            var failed = new BaseResult { Success = false, Message = "�������ʧ��" };

            try
            {

                return _recordingVoucherService.CreateVoucher<CostAdjustmentBill, CostAdjustmentItem>(bill, bill.StoreId, userId, (voucherId) =>
                {

                    #region �޸���Ʒ�ɱ���
                    if (bill != null && bill.Items != null && bill.Items.Count > 0)
                    {
                        var allProducts = _productService.GetProductsByIds(bill.StoreId, bill.Items.Select(pr => pr.ProductId).Distinct().ToArray());
                        bill.Items.ToList().ForEach(c =>
                        {
                            var product = allProducts.Where(ap => ap.Id == c.ProductId).FirstOrDefault();

                            if (product != null && product.ProductPrices != null && product.ProductPrices.Count > 0)
                            {
                                var productPrice = product.ProductPrices.Where(pp => pp.ProductId == c.ProductId && pp.UnitId == c.UnitId).FirstOrDefault();
                                //�ɱ��� = ������۸�
                                productPrice.CostPrice = c.AdjustedPrice;

                                if (productPrice != null)
                                {
                                    _productService.UpdateProduct(product);
                                }
                            }

                        });
                    }
                    #endregion

                    #region �޸ĵ��ݱ�״̬
                    bill.VoucherId = voucherId;
                    bill.AuditedUserId = userId;
                    bill.AuditedDate = DateTime.Now;
                    bill.AuditedStatus = true;
                    UpdateCostAdjustmentBill(bill);
                    #endregion
                },
               () =>
               {
                   return successful;
               },
               () => { return failed; });

            }
            catch (Exception)
            {
                return failed;
            }

        }

        public BaseResult Reverse(int userId, CostAdjustmentBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "���ݺ��ɹ�" };
            var failed = new BaseResult { Success = false, Message = "���ݺ��ʧ��" };

            var uow = CostAdjustmentBillsRepository.UnitOfWork;
            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();

                #region ����ƾ֤

                _recordingVoucherService.CancleVoucher<CostAdjustmentBill, CostAdjustmentItem>(bill, () =>
                {
                    #region �޸���Ʒ�ɱ���
                    if (bill != null && bill.Items != null && bill.Items.Count > 0)
                    {
                        var allProducts = _productService.GetProductsByIds(bill.StoreId, bill.Items.Select(pr => pr.ProductId).Distinct().ToArray());
                        bill.Items.ToList().ForEach(c =>
                        {
                            var product = allProducts.Where(ap => ap.Id == c.ProductId).FirstOrDefault();

                            if (product != null && product.ProductPrices != null && product.ProductPrices.Count > 0)
                            {
                                var productPrice = product.ProductPrices.Where(pp => pp.ProductId == c.ProductId && pp.UnitId == c.UnitId).FirstOrDefault();
                                //�ɱ��� = ����ǰ�۸�
                                productPrice.CostPrice = c.AdjustmentPriceBefore;

                                if (productPrice != null)
                                {
                                    _productService.UpdateProduct(product);
                                }
                            }

                        });
                    }
                    #endregion

                    #region �޸ĵ��ݱ�״̬
                    bill.ReversedUserId = userId;
                    bill.ReversedDate = DateTime.Now;
                    bill.ReversedStatus = true;
                    //UpdateCostAdjustmentBill(costAdjustmentBill);
                    #endregion

                    bill.VoucherId = 0;
                    UpdateCostAdjustmentBill(bill);
                });

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
