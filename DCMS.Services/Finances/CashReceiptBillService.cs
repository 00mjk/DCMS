using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Data;
using DCMS.Core.Domain.Configuration;
using DCMS.Core.Domain.Finances;
using DCMS.Core.Domain.Tasks;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Events;
using DCMS.Services.Sales;
using DCMS.Services.Tasks;
using DCMS.Services.Users;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using DCMS.Services.Caching;
using DCMS.Services.Configuration;
using DCMS.Core.Domain.Report;
using DCMS.Services.Common;
using DCMS.Core.Domain.Common;

namespace DCMS.Services.Finances
{
    /// <summary>
    /// �տ����
    /// </summary>
    public partial class CashReceiptBillService : BaseService, ICashReceiptBillService
    {
        private readonly IUserService _userService;
        private readonly IQueuedMessageService _queuedMessageService;
        private readonly IRecordingVoucherService _recordingVoucherService;
        private readonly ISaleBillService _saleBillService;
        private readonly IReturnBillService _returnBillService;
        private readonly IAdvanceReceiptBillService _advanceReceiptBillService;
        private readonly ICostExpenditureBillService _costExpenditureBillService;
        private readonly IFinancialIncomeBillService _financialIncomeBillService;
        private readonly ISettingService _settingService;
        private readonly ICommonBillService _commonBillService;


        public CashReceiptBillService(IServiceGetter getter,
            IStaticCacheManager cacheManager,
            IEventPublisher eventPublisher,
            IUserService userService,
            IQueuedMessageService queuedMessageService,
            IRecordingVoucherService recordingVoucherService,
            ISaleBillService saleBillService,
            IAdvanceReceiptBillService advanceReceiptBillService,
            ICostExpenditureBillService costExpenditureBillService,
            IFinancialIncomeBillService financialIncomeBillService,
            ISettingService settingService,
            ICommonBillService commonBillService,
            IReturnBillService returnBillService) : base(getter, cacheManager, eventPublisher)
        {
            _userService = userService;
            _queuedMessageService = queuedMessageService;
            _recordingVoucherService = recordingVoucherService;
            _saleBillService = saleBillService;
            _returnBillService = returnBillService;
            _advanceReceiptBillService = advanceReceiptBillService;
            _costExpenditureBillService = costExpenditureBillService;
            _financialIncomeBillService = financialIncomeBillService;
            _settingService = settingService;
            _commonBillService = commonBillService;
        }

        #region ����

        public bool Exists(int billId)
        {
            return CashReceiptBillsRepository.TableNoTracking.Where(a => a.Id == billId).Count() > 0;
        }

        public virtual IPagedList<CashReceiptBill> GetAllCashReceiptBills(int? store, int? makeuserId, int? customerId, int? payeerId, string billNumber = "", bool? status = null, DateTime? start = null, DateTime? end = null, bool? isShowReverse = null, bool? sortByAuditedTime = null, string remark = "", bool? deleted = null, bool? handleStatus = null, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;
            //var query = CashReceiptBillsRepository.Table;
            var query = from pc in CashReceiptBillsRepository.Table
                        .Include(cr => cr.Items)
                        //.ThenInclude(cr => cr.CashReceiptBill)
                        .Include(cr => cr.CashReceiptBillAccountings)
                        .ThenInclude(cr => cr.AccountingOption)
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

            if (payeerId.HasValue && payeerId.Value > 0)
            {
                query = query.Where(c => c.Payeer == payeerId);
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

            if (deleted.HasValue)
            {
                query = query.Where(c => c.Deleted == deleted);
            }

            if (handleStatus.HasValue)
            {
                if (handleStatus.Value)
                {
                    query = query.Where(c => c.HandInStatus == handleStatus);
                }
                else
                {
                    query = query.Where(c => (c.HandInStatus == handleStatus || c.HandInStatus == null) && c.HandInDate == null);
                }
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

            //var unsortedCashReceiptBills = query.ToList();
            ////��ҳ
            //return new PagedList<CashReceiptBill>(unsortedCashReceiptBills, pageIndex, pageSize);
            //��ҳ��
            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<CashReceiptBill>(plists, pageIndex, pageSize, totalCount);
        }

        public virtual IList<CashReceiptBill> GetAllCashReceiptBills()
        {
            var query = from c in CashReceiptBillsRepository.Table
                        orderby c.Id
                        select c;

            var categories = query.ToList();
            return categories;
        }

        public virtual CashReceiptBill GetCashReceiptBillById(int? store, int cashReceiptBillId)
        {
            if (cashReceiptBillId == 0)
            {
                return null;
            }

            var key = DCMSDefaults.CASHRECEIPTBILL_BY_ID_KEY.FillCacheKey(store ?? 0, cashReceiptBillId);
            return _cacheManager.Get(key, () =>
            {
                return CashReceiptBillsRepository.ToCachedGetById(cashReceiptBillId);
            });
        }

        public virtual CashReceiptBill GetCashReceiptBillById(int? store, int cashReceiptBillId, bool isInclude = false)
        {
            if (cashReceiptBillId == 0)
            {
                return null;
            }

            if (isInclude)
            {
                var query = CashReceiptBillsRepository.Table
                .Include(cr => cr.Items)
                //.ThenInclude(cb => cb.CashReceiptBill)
                .Include(cr => cr.CashReceiptBillAccountings)
                .ThenInclude(ao => ao.AccountingOption);

                return query.FirstOrDefault(c => c.Id == cashReceiptBillId);
            }
            return CashReceiptBillsRepository.ToCachedGetById(cashReceiptBillId);
        }


        public virtual CashReceiptBill GetCashReceiptBillByNumber(int? store, string billNumber)
        {
            var key = DCMSDefaults.CASHRECEIPTBILL_BY_NUMBER_KEY.FillCacheKey(store ?? 0, billNumber);
            return _cacheManager.Get(key, () =>
            {
                var query = CashReceiptBillsRepository.Table;
                var cashReceiptBill = query.Where(a => a.BillNumber == billNumber).FirstOrDefault();
                return cashReceiptBill;
            });
        }



        public virtual void InsertCashReceiptBill(CashReceiptBill cashReceiptBill)
        {
            if (cashReceiptBill == null)
            {
                throw new ArgumentNullException("cashReceiptBill");
            }

            var uow = CashReceiptBillsRepository.UnitOfWork;
            CashReceiptBillsRepository.Insert(cashReceiptBill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(cashReceiptBill);
        }

        public virtual void UpdateCashReceiptBill(CashReceiptBill cashReceiptBill)
        {
            if (cashReceiptBill == null)
            {
                throw new ArgumentNullException("cashReceiptBill");
            }

            var uow = CashReceiptBillsRepository.UnitOfWork;
            CashReceiptBillsRepository.Update(cashReceiptBill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(cashReceiptBill);
        }

        public virtual void DeleteCashReceiptBill(CashReceiptBill cashReceiptBill)
        {
            if (cashReceiptBill == null)
            {
                throw new ArgumentNullException("cashReceiptBill");
            }

            var uow = CashReceiptBillsRepository.UnitOfWork;
            CashReceiptBillsRepository.Delete(cashReceiptBill);
            uow.SaveChanges();

            //event notification
            _eventPublisher.EntityDeleted(cashReceiptBill);
        }


        #endregion

        #region ������Ŀ


        public virtual IPagedList<CashReceiptItem> GetCashReceiptItemsByCashReceiptBillId(int cashReceiptBillId, int? userId, int? storeId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (cashReceiptBillId == 0)
            {
                return new PagedList<CashReceiptItem>(new List<CashReceiptItem>(), pageIndex, pageSize);
            }

            var key = DCMSDefaults.CASHRECEIPTBILLITEM_ALL_KEY.FillCacheKey(storeId, cashReceiptBillId, pageIndex, pageSize, userId);

            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CashReceiptItemsRepository.Table
                            where pc.CashReceiptBillId == cashReceiptBillId
                            orderby pc.Id
                            select pc;
                //var productCashReceiptBills = new PagedList<CashReceiptItem>(query.ToList(), pageIndex, pageSize);
                //return productCashReceiptBills;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CashReceiptItem>(plists, pageIndex, pageSize, totalCount);
            });
        }

        public virtual List<CashReceiptItem> GetCashReceiptItemList(int cashReceiptBillId)
        {
            List<CashReceiptItem> cashReceiptItems = null;
            var query = CashReceiptItemsRepository_RO.Table.Include(s => s.CashReceiptBill);
            cashReceiptItems = query.Where(a => a.CashReceiptBillId == cashReceiptBillId).ToList();
            return cashReceiptItems;
        }

        public virtual CashReceiptItem GetCashReceiptItemById(int? store, int cashReceiptItemId)
        {
            if (cashReceiptItemId == 0)
            {
                return null;
            }

            var key = DCMSDefaults.CASHRECEIPTBILLITEM_BY_ID_KEY.FillCacheKey(store ?? 0, cashReceiptItemId);
            return _cacheManager.Get(key, () => { return CashReceiptItemsRepository.ToCachedGetById(cashReceiptItemId); });
        }

        public virtual void InsertCashReceiptItem(CashReceiptItem cashReceiptItem)
        {
            if (cashReceiptItem == null)
            {
                throw new ArgumentNullException("cashReceiptItem");
            }

            var uow = CashReceiptItemsRepository.UnitOfWork;
            CashReceiptItemsRepository.Insert(cashReceiptItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(cashReceiptItem);
        }

        public virtual void UpdateCashReceiptItem(CashReceiptItem cashReceiptItem)
        {
            if (cashReceiptItem == null)
            {
                throw new ArgumentNullException("cashReceiptItem");
            }

            var uow = CashReceiptItemsRepository.UnitOfWork;
            CashReceiptItemsRepository.Update(cashReceiptItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(cashReceiptItem);
        }

        public virtual void DeleteCashReceiptItem(CashReceiptItem cashReceiptItem)
        {
            if (cashReceiptItem == null)
            {
                throw new ArgumentNullException("cashReceiptItem");
            }

            var uow = CashReceiptItemsRepository.UnitOfWork;
            CashReceiptItemsRepository.Delete(cashReceiptItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(cashReceiptItem);
        }


        #endregion

        #region �տ��˻�ӳ��

        public virtual IPagedList<CashReceiptBillAccounting> GetCashReceiptBillAccountingsByCashReceiptBillId(int storeId, int userId, int cashReceiptBillId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (cashReceiptBillId == 0)
            {
                return new PagedList<CashReceiptBillAccounting>(new List<CashReceiptBillAccounting>(), pageIndex, pageSize);
            }

            //string key = string.Format(CASHRECEIPTBILL_ACCOUNTINGL_BY_BILLID_KEY.FillCacheKey( cashReceiptBillId, pageIndex, pageSize, _workContext.CurrentUser.Id, _workContext.CurrentStore.Id);
            var key = DCMSDefaults.CASHRECEIPTBILL_ACCOUNTING_ALLBY_BILLID_KEY.FillCacheKey(storeId, cashReceiptBillId, pageIndex, pageSize, userId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CashReceiptBillAccountingMappingRepository.Table
                            join p in AccountingOptionsRepository.Table on pc.AccountingOptionId equals p.Id
                            where pc.BillId == cashReceiptBillId
                            orderby pc.Id
                            select pc;

                //var saleAccountings = new PagedList<CashReceiptBillAccounting>(query.ToList(), pageIndex, pageSize);
                //return saleAccountings;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CashReceiptBillAccounting>(plists, pageIndex, pageSize, totalCount);
            });
        }

        public virtual IList<CashReceiptBillAccounting> GetCashReceiptBillAccountingsByCashReceiptBillId(int? store, int cashReceiptBillId)
        {

            var key = DCMSDefaults.CASHRECEIPTBILL_ACCOUNTINGL_BY_BILLID_KEY.FillCacheKey(store ?? 0, cashReceiptBillId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CashReceiptBillAccountingMappingRepository.Table
                            join p in AccountingOptionsRepository.Table on pc.AccountingOptionId equals p.Id
                            where pc.BillId == cashReceiptBillId
                            orderby pc.Id
                            select pc;

                return query.ToList();
            });
        }

        public virtual CashReceiptBillAccounting GetCashReceiptBillAccountingById(int cashReceiptBillAccountingId)
        {
            if (cashReceiptBillAccountingId == 0)
            {
                return null;
            }

            return CashReceiptBillAccountingMappingRepository.ToCachedGetById(cashReceiptBillAccountingId);
        }

        public virtual void InsertCashReceiptBillAccounting(CashReceiptBillAccounting cashReceiptBillAccounting)
        {
            if (cashReceiptBillAccounting == null)
            {
                throw new ArgumentNullException("cashReceiptBillAccounting");
            }

            var uow = CashReceiptBillAccountingMappingRepository.UnitOfWork;
            CashReceiptBillAccountingMappingRepository.Insert(cashReceiptBillAccounting);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(cashReceiptBillAccounting);
        }

        public virtual void UpdateCashReceiptBillAccounting(CashReceiptBillAccounting cashReceiptBillAccounting)
        {
            if (cashReceiptBillAccounting == null)
            {
                throw new ArgumentNullException("cashReceiptBillAccounting");
            }

            var uow = CashReceiptBillAccountingMappingRepository.UnitOfWork;
            CashReceiptBillAccountingMappingRepository.Update(cashReceiptBillAccounting);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(cashReceiptBillAccounting);
        }

        public virtual void DeleteCashReceiptBillAccounting(CashReceiptBillAccounting cashReceiptBillAccounting)
        {
            if (cashReceiptBillAccounting == null)
            {
                throw new ArgumentNullException("cashReceiptBillAccounting");
            }

            var uow = CashReceiptBillAccountingMappingRepository.UnitOfWork;
            CashReceiptBillAccountingMappingRepository.Delete(cashReceiptBillAccounting);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(cashReceiptBillAccounting);
        }


        #endregion

        /// <summary>
        /// ��ȡ��ǰ���ݵ������տ��˻�(Ŀ��:�ڲ�ѯʱ�������ӳټ���,���ڻ�Ľϸ߲�ѯ����)
        /// </summary>
        /// <returns></returns>
        public IList<CashReceiptBillAccounting> GetAllCashReceiptBillAccountingsByBillIds(int? store, int[] billIds)
        {
            if (billIds.Length == 0)
            {
                return new List<CashReceiptBillAccounting>();
            }

            var key = DCMSDefaults.CASHRECEIPTBILL_ACCOUNTINGL_BY_BILLID_KEY.FillCacheKey(store ?? 0, string.Join("_", billIds));
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CashReceiptBillAccountingMappingRepository.Table
                            .Include(cr => cr.AccountingOption)
                            where billIds.Contains(pc.BillId)
                            select pc;
                return query.ToList();
            });
        }

        public void UpdateCashReceiptBillActive(int? store, int? billId, int? user)
        {
            var query = CashReceiptBillsRepository.Table.ToList();

            query = query.Where(x => x.StoreId == store && x.MakeUserId == user && x.AuditedStatus == true && (DateTime.Now.Subtract(x.AuditedDate ?? DateTime.Now).Duration().TotalDays > 30)).ToList();

            if (billId.HasValue && billId.Value > 0)
            {
                query = query.Where(x => x.Id == billId).ToList();
            }
            
            var result = query;

            if (result != null && result.Count > 0)
            {
                var uow = CashReceiptBillsRepository.UnitOfWork;
                foreach (CashReceiptBill bill in result)
                {
                    if ((bill.AuditedStatus && !bill.ReversedStatus) || bill.Deleted) continue;
                    bill.Deleted = true;
                    CashReceiptBillsRepository.Update(bill);
                }
                uow.SaveChanges();
            }
        }

        /// <summary>
        /// ��ȡ�տ���˵�
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="status"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="businessUserId"></param>
        /// <returns></returns>
        public IList<CashReceiptBill> GetCashReceiptBillListToFinanceReceiveAccount(int? storeId, int? employeeId = null, DateTime? start = null, DateTime? end = null)
        {
            var query = CashReceiptBillsRepository.Table;

            //������
            if (storeId.HasValue && storeId != 0)
            {
                query = query.Where(a => a.StoreId == storeId);
            }

            //����ˣ�δ���
            query = query.Where(a => a.AuditedStatus == true && a.ReversedStatus == false);

            //��ʼʱ��
            if (start != null)
            {
                query = query.Where(a => a.CreatedOnUtc >= start);
            }

            //����ʱ��
            if (end != null)
            {
                query = query.Where(a => a.CreatedOnUtc <= end);
            }

            //ҵ��Ա
            if (employeeId.HasValue)
            {
                query = query.Where(a => a.Payeer == employeeId);
            }

            query = query.OrderByDescending(a => a.CreatedOnUtc);

            return query.ToList();
        }


        /// <summary>
        /// �ύ�տ
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="userId"></param>
        /// <param name="billId"></param>
        /// <param name="bill"></param>
        /// <param name="accountingOptions"></param>
        /// <param name="accountings"></param>
        /// <param name="data"></param>
        /// <param name="items"></param>
        /// <param name="isAdmin"></param>
        /// <returns></returns>
        public BaseResult BillCreateOrUpdate(int storeId, int userId, int? billId, CashReceiptBill bill, List<CashReceiptBillAccounting> accountingOptions, List<AccountingOption> accountings, CashReceiptBillUpdate data, List<CashReceiptItem> items, bool isAdmin = false, bool doAudit = true)
        {
            var uow = CashReceiptBillsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();
                bill.StoreId = storeId;
                if (!(bill.Id > 0))
                {
                    bill.MakeUserId = userId;
                }


                if (billId.HasValue && billId.Value != 0)
                {
                    #region �����տ

                    if (bill != null)
                    {
                        //�ͻ�
                        bill.CustomerId = data.CustomerId;
                        //�տ���
                        bill.Payeer = data.Payeer ?? 0;
                        //��ע
                        bill.Remark = data.Remark;

                        bill.OweCash = data.OweCash;
                        bill.ReceivableAmount = data.ReceivableAmount;
                        bill.PreferentialAmount = data.PreferentialAmount;

                        UpdateCashReceiptBill(bill);
                    }

                    #endregion
                }
                else
                {
                    #region ����տ

                    bill.StoreId = storeId;
                    //�ͻ�
                    bill.CustomerId = data.CustomerId;
                    //�տ���
                    bill.Payeer = data.Payeer ?? 0;
                    //��������
                    bill.CreatedOnUtc = DateTime.Now;
                    //���ݱ��
                    bill.BillNumber = string.IsNullOrEmpty(data.BillNumber) ? CommonHelper.GetBillNumber("SK", storeId) : data.BillNumber;
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

                    bill.OweCash = data.OweCash;
                    bill.ReceivableAmount = data.ReceivableAmount;
                    bill.PreferentialAmount = data.PreferentialAmount;

                    InsertCashReceiptBill(bill);

                    #endregion
                }

                #region �����տ���Ŀ

                data.Items.ForEach(p =>
                {
                    if (!string.IsNullOrEmpty(p.BillNumber) && p.BillTypeId != 0)
                    {
                        var sd = GetCashReceiptItemById(storeId, p.Id);
                        if (sd == null)
                        {
                            //׷����
                            if (bill.Items.Count(cp => cp.Id == p.Id) == 0)
                            {
                                var item = p;
                                item.BillId = p.BillId;
                                item.CashReceiptBillId = bill.Id;
                                item.CreatedOnUtc = DateTime.Now;
                                item.StoreId = storeId;
                                item.BillId = p.BillId;
                                InsertCashReceiptItem(item);

                                //���ų�
                                p.Id = item.Id;
                                if (!bill.Items.Select(s => s.Id).Contains(item.Id))
                                {
                                    bill.Items.Add(item);
                                }

                                #region ���޸ĵ��� �����տ�/���տ�  �߼��ƶ�����˹���
                                ////���տ�
                                //if (item.AmountOwedAfterReceipt == decimal.Zero)
                                //{
                                //    //���¿���״̬
                                //    switch (item.BillTypeEnum)
                                //    {
                                //        case BillTypeEnum.SaleBill:
                                //            _saleBillService.UpdateReceived(storeId, item.BillId, ReceiptStatus.Received);
                                //            break;
                                //        case BillTypeEnum.ReturnBill:
                                //            _returnBillService.UpdateReceived(storeId, item.BillId, ReceiptStatus.Received);
                                //            break;
                                //        case BillTypeEnum.AdvanceReceiptBill:
                                //            _advanceReceiptBillService.UpdateReceived(storeId, item.BillId, ReceiptStatus.Received);
                                //            break;
                                //        case BillTypeEnum.CostExpenditureBill:
                                //            _costExpenditureBillService.UpdateReceived(storeId, item.BillId, ReceiptStatus.Received);
                                //            break;
                                //        case BillTypeEnum.FinancialIncomeBill:
                                //            _financialIncomeBillService.UpdateReceived(storeId, item.BillId, ReceiptStatus.Received);
                                //            break;
                                //    }
                                //}
                                ////�����տ�
                                //else if (item.AmountOwedAfterReceipt <= item.ArrearsAmount)
                                //{
                                //    //���¿���״̬
                                //    switch (item.BillTypeEnum)
                                //    {
                                //        case BillTypeEnum.SaleBill:
                                //            _saleBillService.UpdateReceived(storeId, item.BillId, ReceiptStatus.Part);
                                //            break;
                                //        case BillTypeEnum.ReturnBill:
                                //            _returnBillService.UpdateReceived(storeId, item.BillId, ReceiptStatus.Part);
                                //            break;
                                //        case BillTypeEnum.AdvanceReceiptBill:
                                //            _advanceReceiptBillService.UpdateReceived(storeId, item.BillId, ReceiptStatus.Part);
                                //            break;
                                //        case BillTypeEnum.CostExpenditureBill:
                                //            _costExpenditureBillService.UpdateReceived(storeId, item.BillId, ReceiptStatus.Part);
                                //            break;
                                //        case BillTypeEnum.FinancialIncomeBill:
                                //            _financialIncomeBillService.UpdateReceived(storeId, item.BillId, ReceiptStatus.Part);
                                //            break;
                                //    }
                                //}
                                //else
                                //{

                                //}
                                #endregion
                            }
                        }
                        else
                        {
                            //���������
                            sd.BillNumber = p.BillNumber;
                            sd.BillTypeId = p.BillTypeId;//��������
                            sd.Amount = p.Amount;// ���ݽ��
                            sd.MakeBillDate = sd.MakeBillDate;//����ʱ��
                            sd.DiscountAmount = p.DiscountAmount;//�Żݽ��
                            sd.PaymentedAmount = p.PaymentedAmount;//���ս��
                            sd.ArrearsAmount = p.ArrearsAmount;//��Ƿ���
                            sd.DiscountAmountOnce = p.DiscountAmountOnce;//�����Żݽ��
                            sd.ReceivableAmountOnce = p.ReceivableAmountOnce;//�����տ���
                            sd.AmountOwedAfterReceipt = p.AmountOwedAfterReceipt;//�տ����Ƿ���
                            sd.Remark = p.Remark;//��ע
                            sd.BillId = p.BillId;//�տ��

                            #region ���޸ĵ��� �����տ�/���տ��߼��ƶ��� ��˹���
                            ////���տ�
                            //if (sd.AmountOwedAfterReceipt == decimal.Zero)
                            //{
                            //    //���¿���״̬
                            //    switch (sd.BillTypeEnum)
                            //    {
                            //        case BillTypeEnum.SaleBill:
                            //            _saleBillService.UpdateReceived(storeId, sd.BillId, ReceiptStatus.Received);
                            //            break;
                            //        case BillTypeEnum.ReturnBill:
                            //            _returnBillService.UpdateReceived(storeId, sd.BillId, ReceiptStatus.Received);
                            //            break;
                            //        case BillTypeEnum.AdvanceReceiptBill:
                            //            _advanceReceiptBillService.UpdateReceived(storeId, sd.BillId, ReceiptStatus.Received);
                            //            break;
                            //        case BillTypeEnum.CostExpenditureBill:
                            //            _costExpenditureBillService.UpdateReceived(storeId, sd.BillId, ReceiptStatus.Received);
                            //            break;
                            //        case BillTypeEnum.FinancialIncomeBill:
                            //            _financialIncomeBillService.UpdateReceived(storeId, sd.BillId, ReceiptStatus.Received);
                            //            break;
                            //    }
                            //}
                            ////�����տ�
                            //else if (sd.AmountOwedAfterReceipt <= sd.ArrearsAmount)
                            //{
                            //    //���¿���״̬
                            //    switch (sd.BillTypeEnum)
                            //    {
                            //        case BillTypeEnum.SaleBill:
                            //            _saleBillService.UpdateReceived(storeId, sd.BillId, ReceiptStatus.Part);
                            //            break;
                            //        case BillTypeEnum.ReturnBill:
                            //            _returnBillService.UpdateReceived(storeId, sd.BillId, ReceiptStatus.Part);
                            //            break;
                            //        case BillTypeEnum.AdvanceReceiptBill:
                            //            _advanceReceiptBillService.UpdateReceived(storeId, sd.BillId, ReceiptStatus.Part);
                            //            break;
                            //        case BillTypeEnum.CostExpenditureBill:
                            //            _costExpenditureBillService.UpdateReceived(storeId, sd.BillId, ReceiptStatus.Part);
                            //            break;
                            //        case BillTypeEnum.FinancialIncomeBill:
                            //            _financialIncomeBillService.UpdateReceived(storeId, sd.BillId, ReceiptStatus.Part);
                            //            break;
                            //    }
                            //}
                            //else
                            //{

                            //}
                            #endregion
                            UpdateCashReceiptItem(sd);
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
                        var item = GetCashReceiptItemById(storeId, p.Id);
                        if (item != null)
                        {
                            DeleteCashReceiptItem(item);
                        }
                    }
                });

                #endregion

                #region �տ��˻�ӳ��

                var cashReceiptBillAccountings = GetCashReceiptBillAccountingsByCashReceiptBillId(storeId, bill.Id);
                accountings.ToList().ForEach(c =>
                {
                    if (data.Accounting.Select(a => a.AccountingOptionId).Contains(c.Id))
                    {
                        if (!cashReceiptBillAccountings.Select(cc => cc.AccountingOptionId).Contains(c.Id))
                        {
                            var collection = data.Accounting.Select(a => a).Where(a => a.AccountingOptionId == c.Id).FirstOrDefault();
                            var cashReceiptBillAccounting = new CashReceiptBillAccounting()
                            {
                                //AccountingOption = c,
                                AccountingOptionId = c.Id,
                                CollectionAmount = collection != null ? collection.CollectionAmount : 0,
                                CashReceiptBill = bill,
                                BillId = bill.Id,
                                TerminalId = data.CustomerId,
                                StoreId = storeId
                            };
                            //����˻�
                            InsertCashReceiptBillAccounting(cashReceiptBillAccounting);
                        }
                        else
                        {
                            cashReceiptBillAccountings.ToList().ForEach(acc =>
                            {
                                var collection = data.Accounting.Select(a => a).Where(a => a.AccountingOptionId == acc.AccountingOptionId).FirstOrDefault();
                                acc.CollectionAmount = collection != null ? collection.CollectionAmount : 0;
                                acc.TerminalId = data.CustomerId;

                                //�����˻�
                                UpdateCashReceiptBillAccounting(acc);
                            });
                        }
                    }
                    else
                    {
                        if (cashReceiptBillAccountings.Select(cc => cc.AccountingOptionId).Contains(c.Id))
                        {
                            var saleaccountings = cashReceiptBillAccountings.Select(cc => cc).Where(cc => cc.AccountingOptionId == c.Id).ToList();
                            saleaccountings.ForEach(sa =>
                            {
                                DeleteCashReceiptBillAccounting(sa);
                            });
                        }
                    }
                });

                #endregion

                bool appBillAutoAudits = false;
                if (data.Operation == (int)OperationEnum.APP)
                {
                    appBillAutoAudits = _settingService.AppBillAutoAudits(storeId, BillTypeEnum.CashReceiptBill);
                }

                //����Ա�����Զ����
                if ((isAdmin && doAudit) || appBillAutoAudits) //�жϵ�ǰ��¼���Ƿ�Ϊ����Ա,��Ϊ����Ա�������Զ����
                {
                    AuditingNoTran(storeId, userId, bill);
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
                            Date = bill.CreatedOnUtc,
                            BillType = BillTypeEnum.CashReceiptBill,
                            BillNumber = bill.BillNumber,
                            BillId = bill.Id,
                            CreatedOnUtc = DateTime.Now
                        };
                        _queuedMessageService.InsertQueuedMessage(adminNumbers, queuedMessage);
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
            catch (Exception ex)
            {
                //������񲻴��ڻ���Ϊ����ع�
                transaction?.Rollback();
                return new BaseResult { Success = false, Message = ex.Message };
            }
            finally
            {
                //����������󶼻�رյ��������
                using (transaction) { }
            }
        }

        public BaseResult Auditing(int storeId, int userId, CashReceiptBill bill)
        {
            var uow = CashReceiptBillsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();

                bill.StoreId = storeId;
                //bill.MakeUserId = userId;


                //if (!CheckTerminalCashReceiptSettled(storeId, bill.CustomerId, bill.ReceivableAmount))
                //{
                //    return new BaseResult { Success = true, Message = "�ͻ�Ƿ���Ѿ����꣬���ʧ�ܣ�" };
                //}

                foreach (var item in bill.Items)
                {
                    if (GetBillIsReceipted(storeId, item.BillId, item.BillTypeEnum)) 
                    {
                        return new BaseResult { Success = true, Message = $"���ʧ��,����{item.BillNumber}�����ѽ��壡" };
                    }
                }

                AuditingNoTran(storeId, userId, bill);


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

        public BaseResult AuditingNoTran(int storeId, int userId, CashReceiptBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "������˳ɹ�" };
            var failed = new BaseResult { Success = false, Message = "�������ʧ��" };

            try
            {
                return _recordingVoucherService.CreateVoucher<CashReceiptBill, CashReceiptItem>(bill, storeId, userId, (voucherId) =>
                {
                    #region �޸ĵ������տ�/�����տ�
                    //��ȡ������Ŀ
                    bill.Items.ToList().ForEach(i=> 
                    {
                        var sd = GetCashReceiptItemById(storeId, i.Id);
                        if (sd != null)
                        {
                            var receiptStatus = ReceiptStatus.None;
                            if (sd.ArrearsAmount < 0)
                            {
                                if (sd.ReceivableAmountOnce < 0 && sd.AmountOwedAfterReceipt < 0)
                                    receiptStatus = ReceiptStatus.Part;
                                else if(sd.ReceivableAmountOnce<0 && sd.AmountOwedAfterReceipt >= 0)
                                    receiptStatus = ReceiptStatus.Received;
                            }
                            else 
                            {
                                if (sd.ReceivableAmountOnce > 0 && sd.AmountOwedAfterReceipt > 0)
                                    receiptStatus = ReceiptStatus.Part;
                                else if (sd.ReceivableAmountOnce > 0 && sd.AmountOwedAfterReceipt <= 0)
                                    receiptStatus = ReceiptStatus.Received;
                            }
                            //if (sd.AmountOwedAfterReceipt == decimal.Zero)
                            //    receiptStatus = ReceiptStatus.Received; //���տ�
                            //else if (sd.AmountOwedAfterReceipt <= sd.ArrearsAmount) //Ƿ��Ϊ
                            //    receiptStatus = ReceiptStatus.Part; //�����տ�
                            //���¿���״̬
                            switch (sd.BillTypeEnum)
                            {
                                case BillTypeEnum.SaleBill:
                                    _saleBillService.UpdateReceived(storeId, sd.BillId, receiptStatus);
                                    break;
                                case BillTypeEnum.ReturnBill:
                                    _returnBillService.UpdateReceived(storeId, sd.BillId, receiptStatus);
                                    break;
                                case BillTypeEnum.AdvanceReceiptBill:
                                    _advanceReceiptBillService.UpdateReceived(storeId, sd.BillId, receiptStatus);
                                    break;
                                case BillTypeEnum.CostExpenditureBill:
                                    _costExpenditureBillService.UpdateReceived(storeId, sd.BillId, receiptStatus);
                                    break;
                                case BillTypeEnum.FinancialIncomeBill:
                                    _financialIncomeBillService.UpdateReceived(storeId, sd.BillId, receiptStatus);
                                    break;
                            }
                        }
                    });
                    #endregion

                    #region �޸ĵ�����Ϣ
                    bill.VoucherId = voucherId;
                    bill.AuditedDate = DateTime.Now;
                    bill.AuditedUserId = userId;
                    bill.AuditedStatus = true;
                    UpdateCashReceiptBill(bill);
                    #endregion
                },
                () =>
                {
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
                            BillType = BillTypeEnum.CashReceiptBill,
                            BillNumber = bill.BillNumber,
                            BillId = bill.Id,
                            CreatedOnUtc = DateTime.Now
                        };
                        _queuedMessageService.InsertQueuedMessage(userNumbers.ToList(), queuedMessage);
                    }
                    catch (Exception ex)
                    {
                        _queuedMessageService.WriteLogs(ex.Message);
                    }
                    #endregion

                    return successful;
                },
                () => { return failed; });
            }
            catch (Exception)
            {
                return failed;
            }

        }
        #region �жϵ����Ƿ����տ�
        private bool GetBillIsReceipted(int storeId,int billId, BillTypeEnum billType) 
        {
            var isTrue = false;
            switch (billType)
            {
                case BillTypeEnum.SaleBill:
                    var saleBill = _saleBillService.GetSaleBillById(storeId,billId);
                    if (saleBill != null && saleBill.ReceivedStatus == ReceiptStatus.Received) 
                        isTrue = true;
                    break;
                case BillTypeEnum.ReturnBill:
                    var returnBill = _returnBillService.GetReturnBillById(storeId,billId);
                    if (returnBill != null && returnBill.ReceivedStatus == ReceiptStatus.Received)
                        isTrue = true;
                    break;
                case BillTypeEnum.AdvanceReceiptBill:
                    var advanceReceiptBill =_advanceReceiptBillService.GetAdvanceReceiptBillById(storeId,billId);
                    if (advanceReceiptBill != null && advanceReceiptBill.ReceivedStatus == ReceiptStatus.Received)
                        isTrue = true;
                    break;
                case BillTypeEnum.CostExpenditureBill:
                    var costExpenditureBill = _costExpenditureBillService.GetCostExpenditureBillById(storeId,billId);
                    if (costExpenditureBill != null && costExpenditureBill.ReceivedStatus == ReceiptStatus.Received)
                        isTrue = true;
                    break;
                case BillTypeEnum.FinancialIncomeBill:
                    var financialIncomeBill = _financialIncomeBillService.GetFinancialIncomeBillById(storeId,billId);
                    if (financialIncomeBill != null && financialIncomeBill.ReceivedStatus == ReceiptStatus.Received)
                        isTrue = true;
                    break;
            }
            return isTrue;
        }
        #endregion

        public BaseResult Reverse(int userId, CashReceiptBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "���ݺ��ɹ�" };
            var failed = new BaseResult { Success = false, Message = "���ݺ��ʧ��" };

            var uow = CashReceiptBillsRepository.UnitOfWork;
            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();

                #region ������ƾ֤

                _recordingVoucherService.CancleVoucher<CashReceiptBill, CashReceiptItem>(bill, () =>
                {
                    #region �޸ĵ����տ�״̬

                    if (bill != null && bill.Items != null && bill.Items.Count > 0)
                    {

                        bill.Items.ToList().ForEach(a =>
                        {
                            //�����տ�
                            if (a.PaymentedAmount != decimal.Zero) //����Ϊ����
                            {
                                //���¿���״̬
                                switch (a.BillTypeEnum)
                                {
                                    case BillTypeEnum.SaleBill:
                                        _saleBillService.UpdateReceived(bill.StoreId, a.BillId, ReceiptStatus.Part);
                                        break;
                                    case BillTypeEnum.ReturnBill:
                                        _returnBillService.UpdateReceived(bill.StoreId, a.BillId, ReceiptStatus.Part);
                                        break;
                                    case BillTypeEnum.AdvanceReceiptBill:
                                        _advanceReceiptBillService.UpdateReceived(bill.StoreId, a.BillId, ReceiptStatus.Part);
                                        break;
                                    case BillTypeEnum.CostExpenditureBill:
                                        _costExpenditureBillService.UpdateReceived(bill.StoreId, a.BillId, ReceiptStatus.Part);
                                        break;
                                    case BillTypeEnum.FinancialIncomeBill:
                                        _financialIncomeBillService.UpdateReceived(bill.StoreId, a.BillId, ReceiptStatus.Part);
                                        break;
                                }
                            }
                            //δ�տ�
                            else if (a.PaymentedAmount == decimal.Zero)
                            {
                                //���¿���״̬
                                switch (a.BillTypeEnum)
                                {
                                    case BillTypeEnum.SaleBill:
                                        _saleBillService.UpdateReceived(bill.StoreId, a.BillId, ReceiptStatus.None);
                                        break;
                                    case BillTypeEnum.ReturnBill:
                                        _returnBillService.UpdateReceived(bill.StoreId, a.BillId, ReceiptStatus.None);
                                        break;
                                    case BillTypeEnum.AdvanceReceiptBill:
                                        _advanceReceiptBillService.UpdateReceived(bill.StoreId, a.BillId, ReceiptStatus.None);
                                        break;
                                    case BillTypeEnum.CostExpenditureBill:
                                        _costExpenditureBillService.UpdateReceived(bill.StoreId, a.BillId, ReceiptStatus.None);
                                        break;
                                    case BillTypeEnum.FinancialIncomeBill:
                                        _financialIncomeBillService.UpdateReceived(bill.StoreId, a.BillId, ReceiptStatus.None);
                                        break;
                                }
                            }
                        });
                    }

                    #endregion

                    #region �޸ĵ��ݱ�״̬
                    bill.ReversedUserId = userId;
                    bill.ReversedDate = DateTime.Now;
                    bill.ReversedStatus = true;
                    //UpdateCashReceiptBill(bill);
                    #endregion

                    bill.VoucherId = 0;
                    UpdateCashReceiptBill(bill);
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


        /// <summary>
        /// ��֤�ն��Ƿ��Ѿ�����Ƿ��
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="terminalId"></param>
        /// <param name="oweCaseAmount">���</param>
        /// <returns></returns>
        public bool CheckTerminalCashReceiptSettled(int storeId, int? terminalId, decimal oweCaseAmount)
        {

            var queryString = @"(SELECT 0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                12 AS BillTypeId,'���۵�' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.ReceivableAmount AS Amount,
                                sb.PreferentialAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.SaleBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.TerminalId=t.Id
                            WHERE
                                sb.StoreId = " + storeId + "  AND  sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.TerminalId = " + terminalId + "";
            }


            queryString += @" ) UNION ALL (SELECT  0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                14 AS BillTypeId,'�˻���' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.ReceivableAmount AS Amount,
                                sb.PreferentialAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.ReturnBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.TerminalId=t.Id
                            WHERE
                                sb.StoreId = " + storeId + "  AND   sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.TerminalId = " + terminalId + "";
            }



            queryString += @") UNION ALL (SELECT  0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                43 AS BillTypeId,'Ԥ�տ' as BillTypeName,
                                sb.CustomerId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.Payeer AS BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.AdvanceAmount AS Amount,
                                sb.DiscountAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.AdvanceReceiptBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.CustomerId=t.Id
                            WHERE
                                 sb.StoreId = " + storeId + "  AND   sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.CustomerId = " + terminalId + "";
            }



            queryString += @" ) UNION ALL (SELECT 0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                45 AS BillTypeId,'����֧��' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.EmployeeId AS BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.SumAmount AS Amount,
                                sb.DiscountAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.CostExpenditureBills AS sb
                                inner join dcms.CostExpenditureItems cs on sb.Id=cs.CostExpenditureBillId
                                inner join dcms_crm.CRM_Terminals AS t on cs.CustomerId=t.Id
                            WHERE
                                 sb.StoreId = " + storeId + "  AND  sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND cs.CustomerId = " + terminalId + "";
            }



            queryString += @" ) UNION ALL (SELECT  0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                47 AS BillTypeId,'��������' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.SalesmanId AS BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.SumAmount AS Amount,
                                sb.DiscountAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.FinancialIncomeBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.TerminalId=t.Id
                            WHERE
                                 sb.StoreId = " + storeId + "  AND  sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.TerminalId = " + terminalId + "";
            }

            queryString += @" )";

            var bills = CashReceiptBillsRepository.QueryFromSql<BillCashReceiptSummary>(queryString).ToList();
            #region ����Ƿ�����߼�
            //��д���㣺 �Żݽ��	 ���ս��  ��Ƿ���
            foreach (var bill in bills)
            {
                //���۵�
                if (bill.BillTypeId == (int)BillTypeEnum.SaleBill)
                {
                    //���ݽ��
                    decimal calc_billAmount = bill.Amount ?? 0;
                    if (bill.Remark != null && bill.Remark.IndexOf("Ӧ�տ����۵�") != -1)
                    {
                        calc_billAmount = bill.ArrearsAmount ?? 0;
                    }

                    //�Żݽ�� 
                    decimal calc_discountAmount = 0;
                    //���ս��
                    decimal calc_paymentedAmount = 0;
                    //��Ƿ���
                    decimal calc_arrearsAmount = 0;

                    #region ��������

                    //���Ѿ��տ�ֵı����Żݺϼ�
                    var discountAmountOnce = _commonBillService.GetBillDiscountAmountOnce(storeId, bill.BillId);

                    //�Żݽ�� =  �����Żݽ��  + �����Ѿ��տ�ֵı����Żݺϼƣ�
                    calc_discountAmount = Convert.ToDecimal(Convert.ToDouble(bill.DiscountAmount ?? 0) + Convert.ToDouble(discountAmountOnce));

                    //�����տ���տ��˻���
                    var collectionAmount = _commonBillService.GetBillCollectionAmount(storeId, bill.BillId, BillTypeEnum.SaleBill);

                    //���Ѿ��տ�ֵı����տ�ϼ�
                    var receivableAmountOnce = _commonBillService.GetBillReceivableAmountOnce(storeId, bill.BillId);

                    //���ս�� = �����տ���տ��˻��� + �����Ѿ��տ�ֵı����տ�ϼƣ�
                    calc_paymentedAmount = collectionAmount + receivableAmountOnce;

                    //��Ƿ���
                    //Convert.ToDouble(bill.ArrearsAmount ?? 0) + 
                    calc_arrearsAmount = Convert.ToDecimal(Convert.ToDouble(calc_billAmount) - Convert.ToDouble(calc_discountAmount) - Convert.ToDouble(calc_paymentedAmount));

                    #endregion

                    //���¸�ֵ
                    bill.Amount = calc_billAmount;
                    bill.DiscountAmount = calc_discountAmount;
                    bill.PaymentedAmount = calc_paymentedAmount;
                    bill.ArrearsAmount = calc_arrearsAmount;

                }
                //�˻���
                else if (bill.BillTypeId == (int)BillTypeEnum.ReturnBill)
                {
                    //���ݽ��
                    decimal calc_billAmount = bill.Amount ?? 0;
                    //�Żݽ�� 
                    decimal calc_discountAmount = 0;
                    //���ս��
                    decimal calc_paymentedAmount = 0;
                    //��Ƿ���
                    decimal calc_arrearsAmount = 0;

                    #region ��������

                    //���Ѿ��տ�ֵı����Żݺϼ�
                    var discountAmountOnce = _commonBillService.GetBillDiscountAmountOnce(storeId, bill.BillId);

                    //�Żݽ�� =  �����Żݽ��  + �����Ѿ��տ�ֵı����Żݺϼƣ�
                    calc_discountAmount = bill.DiscountAmount ?? 0 + discountAmountOnce;

                    //�����տ���տ��˻���
                    var collectionAmount = _commonBillService.GetBillCollectionAmount(storeId, bill.BillId, BillTypeEnum.ReturnBill);

                    //���Ѿ��տ�ֵı����տ�ϼ�
                    var receivableAmountOnce = _commonBillService.GetBillReceivableAmountOnce(storeId, bill.BillId);

                    //���ս�� = �����տ���տ��˻��� + �����Ѿ��տ�ֵı����տ�ϼƣ�
                    calc_paymentedAmount = collectionAmount + receivableAmountOnce;

                    //��Ƿ���
                    calc_arrearsAmount = Convert.ToDecimal(Convert.ToDouble(calc_billAmount) - Convert.ToDouble(calc_discountAmount) - Math.Abs(Convert.ToDouble(calc_paymentedAmount)));

                    #endregion

                    //���¸�ֵ
                    bill.Amount = -calc_billAmount;
                    bill.DiscountAmount = -calc_discountAmount;
                    bill.PaymentedAmount = -calc_paymentedAmount;
                    bill.ArrearsAmount = -calc_arrearsAmount;

                }
                //Ԥ�տ
                else if (bill.BillTypeId == (int)BillTypeEnum.AdvanceReceiptBill)
                {

                    //���ݽ��
                    decimal calc_billAmount = bill.Amount ?? 0;
                    //�Żݽ�� 
                    decimal calc_discountAmount = 0;
                    //���ս��
                    decimal calc_paymentedAmount = 0;
                    //��Ƿ���
                    decimal calc_arrearsAmount = 0;

                    #region ��������

                    //���Ѿ��տ�ֵı����Żݺϼ�
                    var discountAmountOnce = _commonBillService.GetBillDiscountAmountOnce(storeId, bill.BillId);

                    //�Żݽ�� =  �����Żݽ��  + ���Ѿ��տ�ֵı����Żݺϼƣ�
                    calc_discountAmount = bill.DiscountAmount ?? 0 + discountAmountOnce;

                    //�����տ���տ��˻���
                    var collectionAmount = _commonBillService.GetBillCollectionAmount(storeId, bill.BillId, BillTypeEnum.AdvanceReceiptBill);

                    //���Ѿ��տ�ֵı����տ�ϼ�
                    var receivableAmountOnce = _commonBillService.GetBillReceivableAmountOnce(storeId, bill.BillId);

                    //���ս�� = �����տ���տ��˻��� + ���Ѿ��տ�ֵı����տ�ϼƣ�
                    calc_paymentedAmount = collectionAmount + receivableAmountOnce;

                    calc_arrearsAmount = Convert.ToDecimal(Convert.ToDouble(calc_billAmount) - Convert.ToDouble(calc_discountAmount) - Convert.ToDouble(calc_paymentedAmount));

                    #endregion

                    //���¸�ֵ
                    bill.Amount = calc_billAmount;
                    bill.DiscountAmount = calc_discountAmount;
                    bill.PaymentedAmount = calc_paymentedAmount;
                    bill.ArrearsAmount = calc_arrearsAmount;
                }
                //����֧��
                else if (bill.BillTypeId == (int)BillTypeEnum.CostExpenditureBill)
                {
                    //���ݽ��
                    decimal calc_billAmount = bill.Amount ?? 0;
                    //�Żݽ�� 
                    decimal calc_discountAmount = 0;
                    //���ս��
                    decimal calc_paymentedAmount = 0;
                    //��Ƿ���
                    decimal calc_arrearsAmount = 0;

                    #region ��������

                    //���Ѿ��տ�ֵı����Żݺϼ�
                    var discountAmountOnce = _commonBillService.GetBillDiscountAmountOnce(storeId, bill.BillId);

                    //�Żݽ�� =  �����Żݽ��  + �����Ѿ��տ�ֵı����Żݺϼƣ�
                    calc_discountAmount = bill.DiscountAmount ?? 0 + discountAmountOnce;

                    //�����տ���տ��˻���
                    var collectionAmount = _commonBillService.GetBillCollectionAmount(storeId, bill.BillId, BillTypeEnum.CostExpenditureBill);

                    //���Ѿ��տ�ֵı����տ�ϼ�
                    var receivableAmountOnce = _commonBillService.GetBillReceivableAmountOnce(storeId, bill.BillId);

                    //���ս�� = �����տ���տ��˻��� + �����Ѿ��տ�ֵı����տ�ϼƣ�
                    calc_paymentedAmount = collectionAmount + receivableAmountOnce;

                    //��Ƿ��� 
                    calc_arrearsAmount = Convert.ToDecimal(Convert.ToDouble(calc_billAmount) - Convert.ToDouble(calc_discountAmount) - Math.Abs(Convert.ToDouble(calc_paymentedAmount)));

                    #endregion

                    //���¸�ֵ
                    bill.Amount = -Math.Abs(calc_billAmount);
                    bill.DiscountAmount = -Math.Abs(calc_discountAmount);
                    bill.PaymentedAmount = -Math.Abs(calc_paymentedAmount);
                    bill.ArrearsAmount = -Math.Abs(calc_arrearsAmount);
                }
                //��������
                else if (bill.BillTypeId == (int)BillTypeEnum.FinancialIncomeBill)
                {
                    //���ݽ��
                    decimal calc_billAmount = bill.Amount ?? 0;
                    //�Żݽ�� 
                    decimal calc_discountAmount = 0;
                    //���ս��
                    decimal calc_paymentedAmount = 0;
                    //��Ƿ���
                    decimal calc_arrearsAmount = 0;

                    #region ��������

                    //���Ѿ��տ�ֵı����Żݺϼ�
                    var discountAmountOnce = _commonBillService.GetBillDiscountAmountOnce(storeId, bill.BillId);

                    //�Żݽ�� =  �����Żݽ��  + �����Ѿ��տ�ֵı����Żݺϼƣ�
                    calc_discountAmount = bill.DiscountAmount ?? 0 + discountAmountOnce;

                    //�����տ���տ��˻���
                    var collectionAmount = _commonBillService.GetBillCollectionAmount(storeId, bill.BillId, BillTypeEnum.FinancialIncomeBill);

                    //���Ѿ��տ�ֵı����տ�ϼ�
                    var receivableAmountOnce = _commonBillService.GetBillReceivableAmountOnce(storeId, bill.BillId);

                    //���ս�� = �����տ���տ��˻��� + �����Ѿ��տ�ֵı����տ�ϼƣ�
                    calc_paymentedAmount = collectionAmount + receivableAmountOnce;

                    //��Ƿ��� 
                    calc_arrearsAmount = Convert.ToDecimal(Convert.ToDouble(calc_billAmount) - Convert.ToDouble(calc_discountAmount) - Convert.ToDouble(calc_paymentedAmount));

                    #endregion

                    //���¸�ֵ
                    bill.Amount = calc_billAmount;
                    bill.DiscountAmount = calc_discountAmount;
                    bill.PaymentedAmount = calc_paymentedAmount;
                    bill.ArrearsAmount = calc_arrearsAmount;

                }
            }

            var totalArrearsAmount = bills.Sum(s => s.ArrearsAmount);

            if (oweCaseAmount <= totalArrearsAmount)
            {
                return true;
            }
            #endregion
            return false;
        }


        /// <summary>
        /// ��֤�����Ƿ��Ѿ��տ�
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="billTypeId"></param>
        /// <param name="billNumber"></param>
        /// <returns></returns>
        public Tuple<bool, string> CheckBillCashReceipt(int storeId, int billTypeId, string billNumber)
        {

            var query = from a in CashReceiptBillsRepository.Table
                        join b in CashReceiptItemsRepository.Table on a.Id equals b.CashReceiptBillId
                        where a.StoreId == storeId
                        && a.AuditedStatus == true
                        && a.ReversedStatus == false
                        && b.BillTypeId == billTypeId
                        && b.BillNumber == billNumber
                        select b;
            var lists = query.ToList();
            bool fg = false;
            string billNumbers = string.Empty;
            if (lists != null && lists.Count > 0)
            {
                fg = true;
                billNumbers = string.Join(",", lists);
            }
            return new Tuple<bool, string>(fg, billNumbers);

        }


        /// <summary>
        /// ��ȡ���տ�Ƿ���
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="payeer"></param>
        /// <param name="terminalId"></param>
        /// <param name="billTypeId"></param>
        /// <param name="billNumber"></param>
        /// <param name="remark"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public IList<BillCashReceiptSummary> GetBillCashReceiptSummary(int storeId, int? payeer,
            int? terminalId,
            int? billTypeId,
            string billNumber = "",
            string remark = "",
            DateTime? startTime = null,
            DateTime? endTime = null,
            int pageIndex = 0,
            int pageSize = int.MaxValue)
        {
            billNumber = CommonHelper.FilterSQLChar(billNumber);
            remark = CommonHelper.FilterSQLChar(remark);

            // sb.ReceivableAmount AS Amount,
            var queryString = @"(SELECT 0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                12 AS BillTypeId,'���۵�' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.SumAmount AS Amount,
                                sb.PreferentialAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.SaleBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.TerminalId=t.Id
                            WHERE
                                sb.StoreId = " + storeId + "  AND  sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.TerminalId = " + terminalId + "";
            }

            if (payeer.HasValue && payeer.Value > 0)
            {
                queryString += @" AND sb.BusinessUserId = " + payeer + "";
            }

            if (startTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc >= '" + startTime.Value.ToString("yyyy-MM-dd 00:00:00") + "'";
            }

            if (endTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc <= '" + endTime.Value.ToString("yyyy-MM-dd 23:59:59") + "'";
            }

            queryString += @" ) UNION ALL (SELECT  0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                14 AS BillTypeId,'�˻���' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.ReceivableAmount AS Amount,
                                sb.PreferentialAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.ReturnBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.TerminalId=t.Id
                            WHERE
                                sb.StoreId = " + storeId + "  AND   sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.TerminalId = " + terminalId + "";
            }

            if (payeer.HasValue && payeer.Value > 0)
            {
                queryString += @" AND sb.BusinessUserId = " + payeer + "";
            }

            if (startTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc >= '" + startTime.Value.ToString("yyyy-MM-dd 00:00:00") + "'";
            }

            if (endTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc <= '" + endTime.Value.ToString("yyyy-MM-dd 23:59:59") + "'";
            }

            queryString += @") UNION ALL (SELECT  0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                43 AS BillTypeId,'Ԥ�տ' as BillTypeName,
                                sb.CustomerId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.Payeer AS BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.AdvanceAmount AS Amount,
                                sb.DiscountAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.AdvanceReceiptBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.CustomerId=t.Id
                            WHERE
                                 sb.StoreId = " + storeId + "  AND   sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.CustomerId = " + terminalId + "";
            }

            if (payeer.HasValue && payeer.Value > 0)
            {
                queryString += @" AND sb.Payeer = " + payeer + "";
            }

            if (startTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc >= '" + startTime.Value.ToString("yyyy-MM-dd 00:00:00") + "'";
            }

            if (endTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc <= '" + endTime.Value.ToString("yyyy-MM-dd 23:59:59") + "'";
            }

            queryString += @" ) UNION ALL (SELECT 0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                45 AS BillTypeId,'����֧��' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.EmployeeId AS BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.SumAmount AS Amount,
                                sb.DiscountAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.CostExpenditureBills AS sb
                                inner join dcms.CostExpenditureItems cs on sb.Id=cs.CostExpenditureBillId
                                inner join dcms_crm.CRM_Terminals AS t on cs.CustomerId = t.Id
                            WHERE
                                 sb.StoreId = " + storeId + "  AND  sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND cs.CustomerId = " + terminalId + "";
            }

            if (payeer.HasValue && payeer.Value > 0)
            {
                queryString += @" AND sb.EmployeeId = " + payeer + "";
            }

            if (startTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc >= '" + startTime.Value.ToString("yyyy-MM-dd 00:00:00") + "'";
            }

            if (endTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc <= '" + endTime.Value.ToString("yyyy-MM-dd 23:59:59") + "'";
            }

            queryString += @" ) UNION ALL (SELECT  0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                47 AS BillTypeId,'��������' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.SalesmanId AS BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.SumAmount AS Amount,
                                sb.DiscountAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.FinancialIncomeBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.TerminalId=t.Id
                            WHERE
                                 sb.StoreId = " + storeId + "  AND  sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.TerminalId = " + terminalId + "";
            }

            if (payeer.HasValue && payeer.Value > 0)
            {
                queryString += @" AND sb.SalesmanId = " + payeer + "";
            }

            if (startTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc >= '" + startTime.Value.ToString("yyyy-MM-dd 00:00:00") + "'";
            }

            if (endTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc <= '" + endTime.Value.ToString("yyyy-MM-dd 23:59:59") + "'";
            }

            queryString += @" )";

            //var sbCount = $"SELECT COUNT(1) as `Value` FROM ({queryString}) as alls;";
            //int totalCount = ProductsRepository.QueryFromSql<IntQueryType>(sbCount.ToString()).ToList().FirstOrDefault().Value ?? 0;

            string sbQuery = $"SELECT * FROM(SELECT ROW_NUMBER() OVER(ORDER BY BillId) AS RowNum, alls.* FROM({queryString}) as alls ) AS result  WHERE RowNum >= {pageIndex * pageSize} AND RowNum <= {(pageIndex + 1) * pageSize} ORDER BY BillId asc";

            var query = CashReceiptBillsRepository.QueryFromSql<BillCashReceiptSummary>(sbQuery).ToList();

            if (billTypeId.HasValue && billTypeId.Value > 0)
                query = query.Where(s => s.BillTypeId == billTypeId).ToList();

            if (!string.IsNullOrEmpty(billNumber))
                query = query.Where(s => s.BillNumber == billNumber).ToList();

            return query;
        }


        /// <summary>
        /// �ж�ָ�������Ƿ�����Ƿ��(�Ƿ��Ѿ������)
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="billTypeId"></param>
        /// <param name="billId"></param>
        /// <returns></returns>
        public bool ThereAnyDebt(int storeId, int? billTypeId, int billId)
        {

            //���۵�
            if (billTypeId == (int)BillTypeEnum.SaleBill)
            {
                return SaleBillsRepository.Table
                    .Where(s => s.StoreId == storeId && s.Id == billId && (s.ReceiptStatus == 0 || s.ReceiptStatus == 1))
                    .Count() > 0;
            }
            //�˻���
            else if (billTypeId == (int)BillTypeEnum.ReturnBill)
            {
                return ReturnBillsRepository.Table
                    .Where(s => s.StoreId == storeId && s.Id == billId && (s.ReceiptStatus == 0 || s.ReceiptStatus == 1))
                    .Count() > 0;
            }
            //Ԥ�տ
            else if (billTypeId == (int)BillTypeEnum.AdvanceReceiptBill)
            {
                return AdvanceReceiptBillsRepository.Table.
                    Where(s => s.StoreId == storeId && s.Id == billId && (s.ReceiptStatus == 0 || s.ReceiptStatus == 1))
                    .Count() > 0;
            }
            //����֧��
            else if (billTypeId == (int)BillTypeEnum.CostExpenditureBill)
            {
                return CostExpenditureBillsRepository.Table.
                    Where(s => s.StoreId == storeId && s.Id == billId && (s.ReceiptStatus == 0 || s.ReceiptStatus == 1))
                    .Count() > 0;
            }
            //��������
            else if (billTypeId == (int)BillTypeEnum.FinancialIncomeBill)
            {
                return FinancialIncomeBillsRepository.Table.
                    Where(s => s.StoreId == storeId && s.Id == billId && (s.ReceiptStatus == 0 || s.ReceiptStatus == 1))
                    .Count() > 0;
            }
            else
            {
                return true;
            }
        }


        /// <summary>
        /// ���µ��ݽ���״̬
        /// </summary>
        /// <param name="store"></param>
        /// <param name="billId"></param>
        /// <param name="handInStatus"></param>
        public void UpdateHandInStatus(int? store, int billId, bool handInStatus)
        {
            var bill = GetCashReceiptBillById(store, billId, false);
            if (bill != null)
            {
                bill.HandInStatus = handInStatus;
                var uow = CashReceiptBillsRepository.UnitOfWork;
                CashReceiptBillsRepository.Update(bill);
                uow.SaveChanges();
                //֪ͨ
                _eventPublisher.EntityUpdated(bill);
            }
        }

        /// <summary>
        /// ��ȡ���տ�Ƿ���
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="payeer"></param>
        /// <param name="terminalId"></param>
        /// <param name="billTypeId"></param>
        /// <param name="billNumber"></param>
        /// <param name="remark"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public IList<BillCashReceiptSummary> GetBillCashReceiptList(int storeId, IList<int> userIds,
            int? terminalId,
            int? billTypeId,
            string billNumber = "",
            string remark = "",
            DateTime? startTime = null,
            DateTime? endTime = null,
            int pageIndex = 0,
            int pageSize = int.MaxValue)
        {
            billNumber = CommonHelper.FilterSQLChar(billNumber);
            remark = CommonHelper.FilterSQLChar(remark);

            // sb.ReceivableAmount AS Amount,
            var queryString = @"(SELECT 0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                12 AS BillTypeId,'���۵�' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.SumAmount AS Amount,
                                sb.PreferentialAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.SaleBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.TerminalId=t.Id
                            WHERE
                                sb.StoreId = " + storeId + "  AND  sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.TerminalId = " + terminalId + "";
            }

            if (userIds != null && userIds.Count>0)
            {
                queryString += @" AND sb.BusinessUserId in("+ string.Join(",", userIds) +")";
            }

            if (startTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc >= '" + startTime.Value.ToString("yyyy-MM-dd 00:00:00") + "'";
            }

            if (endTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc <= '" + endTime.Value.ToString("yyyy-MM-dd 23:59:59") + "'";
            }

            queryString += @" ) UNION ALL (SELECT  0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                14 AS BillTypeId,'�˻���' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.ReceivableAmount AS Amount,
                                sb.PreferentialAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.ReturnBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.TerminalId=t.Id
                            WHERE
                                sb.StoreId = " + storeId + "  AND   sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.TerminalId = " + terminalId + "";
            }

            if (userIds != null && userIds.Count > 0)
            {
                queryString += @" AND sb.BusinessUserId in("+ string.Join(",",userIds) +")";
            }

            if (startTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc >= '" + startTime.Value.ToString("yyyy-MM-dd 00:00:00") + "'";
            }

            if (endTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc <= '" + endTime.Value.ToString("yyyy-MM-dd 23:59:59") + "'";
            }

            queryString += @") UNION ALL (SELECT  0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                43 AS BillTypeId,'Ԥ�տ' as BillTypeName,
                                sb.CustomerId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.Payeer AS BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.AdvanceAmount AS Amount,
                                sb.DiscountAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.AdvanceReceiptBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.CustomerId=t.Id
                            WHERE
                                 sb.StoreId = " + storeId + "  AND   sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.CustomerId = " + terminalId + "";
            }

            if (userIds != null && userIds.Count > 0)
            {
                queryString += @" AND sb.Payeer in("+ string.Join(",",userIds) +")";
            }

            if (startTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc >= '" + startTime.Value.ToString("yyyy-MM-dd 00:00:00") + "'";
            }

            if (endTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc <= '" + endTime.Value.ToString("yyyy-MM-dd 23:59:59") + "'";
            }

            queryString += @" ) UNION ALL (SELECT 0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                45 AS BillTypeId,'����֧��' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.EmployeeId AS BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.SumAmount AS Amount,
                                sb.DiscountAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.CostExpenditureBills AS sb
                                inner join dcms.CostExpenditureItems cs on sb.Id=cs.CostExpenditureBillId
                                inner join dcms_crm.CRM_Terminals AS t on cs.CustomerId = t.Id
                            WHERE
                                 sb.StoreId = " + storeId + "  AND  sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND cs.CustomerId = " + terminalId + "";
            }

            if (userIds != null && userIds.Count > 0)
            {
                queryString += @" AND sb.EmployeeId in("+ string.Join(",",userIds) +")";
            }

            if (startTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc >= '" + startTime.Value.ToString("yyyy-MM-dd 00:00:00") + "'";
            }

            if (endTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc <= '" + endTime.Value.ToString("yyyy-MM-dd 23:59:59") + "'";
            }

            queryString += @" ) UNION ALL (SELECT  0 as Id,sb.StoreId,
                                sb.Id AS BillId,
                                sb.BillNumber AS BillNumber,
                                47 AS BillTypeId,'��������' as BillTypeName,
                                sb.TerminalId AS CustomerId,
                                t.Code AS CustomerPointCode,
                                sb.SalesmanId AS BusinessUserId,
                                sb.CreatedOnUtc AS MakeBillDate,
                                sb.SumAmount AS Amount,
                                sb.DiscountAmount AS DiscountAmount,
                                0 AS PaymentedAmount,
                                sb.OweCash AS ArrearsAmount,
                                sb.Remark AS Remark
                            FROM
                                dcms.FinancialIncomeBills AS sb
                                inner join dcms_crm.CRM_Terminals AS t on sb.TerminalId=t.Id
                            WHERE
                                 sb.StoreId = " + storeId + "  AND  sb.auditedStatus = 1 AND sb.ReversedStatus = 0 AND abs(sb.OweCash) > 0 AND (sb.ReceiptStatus = 0 or sb.ReceiptStatus = 1)";

            if (terminalId.HasValue && terminalId.Value > 0)
            {
                queryString += @" AND sb.TerminalId = " + terminalId + "";
            }

            if (userIds != null && userIds.Count > 0)
            {
                queryString += @" AND sb.SalesmanId in("+ string.Join(",",userIds) +")";
            }

            if (startTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc >= '" + startTime.Value.ToString("yyyy-MM-dd 00:00:00") + "'";
            }

            if (endTime.HasValue)
            {
                queryString += @" AND sb.CreatedOnUtc <= '" + endTime.Value.ToString("yyyy-MM-dd 23:59:59") + "'";
            }

            queryString += @" )";

            //var sbCount = $"SELECT COUNT(1) as `Value` FROM ({queryString}) as alls;";
            //int totalCount = ProductsRepository.QueryFromSql<IntQueryType>(sbCount.ToString()).ToList().FirstOrDefault().Value ?? 0;

            string sbQuery = $"SELECT * FROM(SELECT ROW_NUMBER() OVER(ORDER BY BillId) AS RowNum, alls.* FROM({queryString}) as alls ) AS result  WHERE RowNum >= {pageIndex * pageSize} AND RowNum <= {(pageIndex + 1) * pageSize} ORDER BY BillId asc";

            var query = CashReceiptBillsRepository.QueryFromSql<BillCashReceiptSummary>(sbQuery).ToList();

            if (billTypeId.HasValue && billTypeId.Value > 0)
                query = query.Where(s => s.BillTypeId == billTypeId).ToList();

            if (!string.IsNullOrEmpty(billNumber))
                query = query.Where(s => s.BillNumber == billNumber).ToList();

            return query;
        }

        public bool ExistsUnAuditedByBillNumber(int storeId, string billNumber,int id)
        {
            try
            {
                var query = from a in CashReceiptBillsRepository.Table
                            join b in CashReceiptItemsRepository.Table on a.Id equals b.CashReceiptBillId
                            where a.StoreId == storeId
                            && a.AuditedStatus == false
                            && b.BillNumber == billNumber
                            select a;
                //�޸ĵ���ʱ�ų��Լ�
                if (id>0) 
                {
                    query = query.Where(w=>w.Id != id);
                }
                return query.Count() == 0;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public List<CashReceiptItem> GetCashReceiptItemListByBillId(int storeId, int billId)
        {
            try
            {
                var query = from a in CashReceiptBillsRepository.Table
                            join b in CashReceiptItemsRepository.Table on a.Id equals b.CashReceiptBillId
                            where a.StoreId == storeId
                            && a.AuditedStatus == true
                            && a.ReversedStatus == false
                            && b.BillId == billId
                            select b;
                return query.ToList();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
