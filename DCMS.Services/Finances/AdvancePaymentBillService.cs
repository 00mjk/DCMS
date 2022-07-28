using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Data;
using DCMS.Core.Domain.Configuration;
using DCMS.Core.Domain.Finances;
using DCMS.Core.Domain.Tasks;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Events;
using DCMS.Services.Tasks;
using DCMS.Services.Users;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using DCMS.Services.Caching;

namespace DCMS.Services.Finances
{
    /// <summary>
    /// ��ʾԤ����ݷ���
    /// </summary>
    public partial class AdvancePaymentBillService : BaseService, IAdvancePaymentBillService
    {
        private readonly IUserService _userService;
        private readonly IQueuedMessageService _queuedMessageService;
        private readonly IRecordingVoucherService _recordingVoucherService;

        public AdvancePaymentBillService(IServiceGetter getter,
            IStaticCacheManager cacheManager,
            IEventPublisher eventPublisher,
            IUserService userService,
            IQueuedMessageService queuedMessageService,
            IRecordingVoucherService recordingVoucherService
            ) : base(getter, cacheManager, eventPublisher)
        {
            _userService = userService;
            _queuedMessageService = queuedMessageService;
            _recordingVoucherService = recordingVoucherService;
        }

        #region ����


        public bool Exists(int billId)
        {
            return AdvancePaymentBillsRepository.TableNoTracking.Where(a => a.Id == billId).Count() > 0;
        }

        public virtual IPagedList<AdvancePaymentBill> GetAllAdvancePaymentBills(int? store, int? makeuserId, int? draweer, int? manufacturerId, string billNumber = "", bool? status = null, DateTime? start = null, DateTime? end = null, bool? isShowReverse = null, bool? sortByAuditedTime = null, int? accountingOptionId = null, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;

            DateTime.TryParse(start?.ToString("yyyy-MM-dd 00:00:00"), out DateTime startDate);
            DateTime.TryParse(end?.ToString("yyyy-MM-dd 23:59:59"), out DateTime endDate);

            var query = from pc in AdvancePaymentBillsRepository.Table
                           .Include(cr => cr.Items)
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

            if (draweer.HasValue && draweer.Value > 0)
            {
                query = query.Where(c => c.Draweer == draweer);
            }

            if (manufacturerId.HasValue && manufacturerId.Value > 0)
            {
                query = query.Where(c => c.ManufacturerId == manufacturerId);
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
                query = query.Where(o => startDate <= o.CreatedOnUtc);
            }

            if (end.HasValue)
            {
                query = query.Where(o => endDate >= o.CreatedOnUtc);
            }

            if (isShowReverse.HasValue)
            {
                query = query.Where(c => c.ReversedStatus == isShowReverse);
            }

            if (accountingOptionId.HasValue && accountingOptionId.Value > 0)
            {
                query = query.Where(c => c.AccountingOptionId == accountingOptionId);
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

            //var unsortedAdvancePaymentBills = query.ToList();
            ////��ҳ
            //return new PagedList<AdvancePaymentBill>(unsortedAdvancePaymentBills, pageIndex, pageSize);
            //��ҳ��
            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<AdvancePaymentBill>(plists, pageIndex, pageSize, totalCount);
        }

        public virtual IList<AdvancePaymentBill> GetAllAdvancePaymentBills()
        {
            var query = from c in AdvancePaymentBillsRepository.Table
                        orderby c.Id
                        select c;

            var categories = query.ToList();
            return categories;
        }

        public virtual AdvancePaymentBill GetAdvancePaymentBillById(int? store, int advancePaymentBillId)
        {
            if (advancePaymentBillId == 0)
            {
                return null;
            }

            var key = DCMSDefaults.ADVANCEPAYMENTBILL_BY_ID_KEY.FillCacheKey(store ?? 0, advancePaymentBillId);
            return _cacheManager.Get(key, () =>
            {
                return AdvancePaymentBillsRepository.ToCachedGetById(advancePaymentBillId);
            });
        }

        public virtual AdvancePaymentBill GetAdvancePaymentBillById(int? store, int advancePaymentBillId, bool isInclude = false)
        {
            if (advancePaymentBillId == 0)
            {
                return null;
            }

            if (isInclude)
            {
                var query = AdvancePaymentBillsRepository.Table
                .Include(ap => ap.Items)
                .ThenInclude(ao => ao.AccountingOption);

                return query.FirstOrDefault(a => a.Id == advancePaymentBillId);
            }
            return AdvancePaymentBillsRepository.ToCachedGetById(advancePaymentBillId);
        }


        public virtual AdvancePaymentBill GetAdvancePaymentBillByNumber(int? store, string billNumber)
        {
            var query = AdvancePaymentBillsRepository.Table;
            var bill = query.Where(a => a.StoreId == store && a.BillNumber == billNumber).FirstOrDefault();
            return bill;
        }

        /// <summary>
        /// ���ݾ����̡���Ӧ�� ��ȡ Ԥ����
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="manufacturerId"></param>
        /// <returns></returns>
        public virtual IList<AdvancePaymentBill> GetAdvancePaymentBillByStoreIdManufacturerId(int storeId, int manufacturerId)
        {
            var query = AdvancePaymentBillsRepository.Table;

            query = query.Where(a => a.AuditedStatus == true);
            query = query.Where(a => a.ReversedStatus == false);

            query = query.Where(a => a.StoreId == storeId);
            query = query.Where(a => a.ManufacturerId == manufacturerId);

            return query.ToList();
        }

        public virtual decimal GetAdvanceAmountByManufacturerId(int storeId, int manufacturerId)
        {
            var query = AdvancePaymentBillsRepository.Table;
            query = query.Where(a => a.AuditedStatus == true);
            query = query.Where(a => a.ReversedStatus == false);
            query = query.Where(a => a.StoreId == storeId);
            query = query.Where(a => a.ManufacturerId == manufacturerId);
            return query.Sum(s => s.AdvanceAmount ?? 0);
        }

        public virtual void InsertAdvancePaymentBill(AdvancePaymentBill bill)
        {
            if (bill == null)
            {
                throw new ArgumentNullException("bill");
            }

            var uow = AdvancePaymentBillsRepository.UnitOfWork;
            AdvancePaymentBillsRepository.Insert(bill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(bill);
        }

        public virtual void UpdateAdvancePaymentBill(AdvancePaymentBill bill)
        {
            if (bill == null)
            {
                throw new ArgumentNullException("bill");
            }

            var uow = AdvancePaymentBillsRepository.UnitOfWork;
            AdvancePaymentBillsRepository.Update(bill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(bill);
        }

        public virtual void DeleteAdvancePaymentBill(AdvancePaymentBill bill)
        {
            if (bill == null)
            {
                throw new ArgumentNullException("bill");
            }

            var uow = AdvancePaymentBillsRepository.UnitOfWork;
            AdvancePaymentBillsRepository.Delete(bill);
            uow.SaveChanges();

            _eventPublisher.EntityDeleted(bill);
        }


        #endregion

        #region �տ��˻�ӳ��

        public virtual IPagedList<AdvancePaymentBillAccounting> GetAdvancePaymentBillAccountingsByAdvancePaymentBillId(int storeId, int userId, int advancePaymentBillId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (advancePaymentBillId == 0)
            {
                return new PagedList<AdvancePaymentBillAccounting>(new List<AdvancePaymentBillAccounting>(), pageIndex, pageSize);
            }

            //var key = DCMSDefaults.ADVANCEPAYMENTBILL_ACCOUNTINGL_BY_BILLID_KEY.FillCacheKey( advancePaymentBillId, pageIndex, pageSize, _workContext.CurrentUser.Id, _workContext.CurrentStore.Id);
            var key = DCMSDefaults.ADVANCEPAYMENTBILL_ACCOUNTING_ALLBY_BILLID_KEY.FillCacheKey(storeId, advancePaymentBillId, pageIndex, pageSize, userId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in AdvancePaymentBillAccountingMappingRepository.Table
                            join p in AccountingOptionsRepository.Table on pc.AccountingOptionId equals p.Id
                            where pc.BillId == advancePaymentBillId
                            orderby pc.Id
                            select pc;

                //var saleAccountings = new PagedList<AdvancePaymentBillAccounting>(query.ToList(), pageIndex, pageSize);
                //return saleAccountings;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<AdvancePaymentBillAccounting>(plists, pageIndex, pageSize, totalCount);
            });
        }

        public virtual IList<AdvancePaymentBillAccounting> GetAdvancePaymentBillAccountingsByAdvancePaymentBillId(int? store, int advancePaymentBillId)
        {

            var key = DCMSDefaults.ADVANCEPAYMENTBILL_ACCOUNTINGL_BY_BILLID_KEY.FillCacheKey(store ?? 0, advancePaymentBillId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in AdvancePaymentBillAccountingMappingRepository.Table
                            join p in AccountingOptionsRepository.Table on pc.AccountingOptionId equals p.Id
                            where pc.BillId == advancePaymentBillId
                            orderby pc.Id
                            select pc;


                return query.ToList();
            });
        }

        public virtual AdvancePaymentBillAccounting GetAdvancePaymentBillAccountingById(int advancePaymentBillAccountingId)
        {
            if (advancePaymentBillAccountingId == 0)
            {
                return null;
            }

            return AdvancePaymentBillAccountingMappingRepository.ToCachedGetById(advancePaymentBillAccountingId);
        }

        public virtual void InsertAdvancePaymentBillAccounting(AdvancePaymentBillAccounting advancePaymentBillAccounting)
        {
            if (advancePaymentBillAccounting == null)
            {
                throw new ArgumentNullException("advancePaymentBillAccounting");
            }

            var uow = AdvancePaymentBillAccountingMappingRepository.UnitOfWork;
            AdvancePaymentBillAccountingMappingRepository.Insert(advancePaymentBillAccounting);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(advancePaymentBillAccounting);
        }

        public virtual void UpdateAdvancePaymentBillAccounting(AdvancePaymentBillAccounting advancePaymentBillAccounting)
        {
            if (advancePaymentBillAccounting == null)
            {
                throw new ArgumentNullException("advancePaymentBillAccounting");
            }

            var uow = AdvancePaymentBillAccountingMappingRepository.UnitOfWork;
            AdvancePaymentBillAccountingMappingRepository.Update(advancePaymentBillAccounting);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(advancePaymentBillAccounting);
        }

        public virtual void DeleteAdvancePaymentBillAccounting(AdvancePaymentBillAccounting advancePaymentBillAccounting)
        {
            if (advancePaymentBillAccounting == null)
            {
                throw new ArgumentNullException("advancePaymentBillAccounting");
            }

            var uow = AdvancePaymentBillAccountingMappingRepository.UnitOfWork;
            AdvancePaymentBillAccountingMappingRepository.Delete(advancePaymentBillAccounting);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(advancePaymentBillAccounting);
        }


        #endregion

        public BaseResult BillCreateOrUpdate(int storeId, int userId, int? billId, AdvancePaymentBill bill, List<AdvancePaymentBillAccounting> accountingOptions, List<AccountingOption> accountings, AdvancePaymenBillUpdate data, bool isAdmin = false,bool doAudit = true)
        {
            var uow = AdvancePaymentBillsRepository.UnitOfWork;

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
                    #region ����Ԥ���
                    if (bill != null)
                    {
                        bill.ManufacturerId = data.ManufacturerId;
                        bill.Draweer = data.Draweer;
                        bill.AdvanceAmount = data?.Accounting.Where(s => s.Copy == false)?.Sum(ac => ac.CollectionAmount );
                        bill.AccountingOptionId = data.AccountingOptionId;
                        //��ע
                        bill.Remark = data.Remark;

                        UpdateAdvancePaymentBill(bill);
                    }

                    #endregion
                }
                else
                {
                    #region ���Ԥ���

                    bill.StoreId = storeId;
                    bill.ManufacturerId = data.ManufacturerId;
                    bill.Draweer = data.Draweer;
                    bill.AdvanceAmount = data?.Accounting.Where(s => s.Copy == false)?.Sum(ac => ac.CollectionAmount );
                    bill.AccountingOptionId = data.AccountingOptionId;
                    //��������
                    bill.CreatedOnUtc = DateTime.Now;
                    //���ݱ��
                    bill.BillNumber = string.IsNullOrEmpty(data.BillNumber) ? CommonHelper.GetBillNumber("YFK", storeId) : data.BillNumber;

                    var sb = GetAdvancePaymentBillByNumber(storeId, bill.BillNumber);
                    if (sb != null)
                    {
                        return new BaseResult { Success = false, Message = "����ʧ�ܣ��ظ��ύ" };
                    }

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

                    InsertAdvancePaymentBill(bill);

                    #endregion
                }

                #region �����˻�ӳ��

                var advancePaymentBillAccountings = GetAdvancePaymentBillAccountingsByAdvancePaymentBillId(storeId, bill.Id);
                accountings.ToList().ForEach(c =>
                {
                    if (data.Accounting.Select(a => a.AccountingOptionId).Contains(c.Id))
                    {
                        if (!advancePaymentBillAccountings.Select(cc => cc.AccountingOptionId).Contains(c.Id))
                        {
                            var collection = data.Accounting.Select(a => a).Where(a => a.AccountingOptionId == c.Id).FirstOrDefault();
                            var advancePaymentBillAccounting = new AdvancePaymentBillAccounting()
                            {
                                //AccountingOption = c,
                                AccountingOptionId = c.Id,
                                CollectionAmount = collection != null ? collection.CollectionAmount : 0,
                                AdvancePaymentBill = bill,
                                BillId = bill.Id,
                                ManufacturerId = data.ManufacturerId,
                                Copy = collection.Copy,
                                StoreId = storeId
                            };
                            //����˻�
                            InsertAdvancePaymentBillAccounting(advancePaymentBillAccounting);
                        }
                        else
                        {
                            advancePaymentBillAccountings.ToList().ForEach(acc =>
                            {
                                var collection = data.Accounting.Select(a => a).Where(a => a.AccountingOptionId == acc.AccountingOptionId).FirstOrDefault();
                                acc.CollectionAmount = collection != null ? collection.CollectionAmount : 0;
                                acc.ManufacturerId = data.ManufacturerId;
                                acc.Copy = collection.Copy;
                                //�����˻�
                                UpdateAdvancePaymentBillAccounting(acc);
                            });
                        }
                    }
                    else
                    {
                        if (advancePaymentBillAccountings.Select(cc => cc.AccountingOptionId).Contains(c.Id))
                        {
                            var saleaccountings = advancePaymentBillAccountings.Select(cc => cc).Where(cc => cc.AccountingOptionId == c.Id).ToList();
                            saleaccountings.ForEach(sa =>
                            {
                                DeleteAdvancePaymentBillAccounting(sa);
                            });
                        }
                    }
                });

                #endregion

                //����Ա�����Զ����
                if (isAdmin && doAudit) //�жϵ�ǰ��¼���Ƿ�Ϊ����Ա,��Ϊ����Ա�������Զ����
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
                            BillType = BillTypeEnum.AdvancePaymentBill,
                            BillNumber = bill.BillNumber,
                            BillId = bill.Id,
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

        public BaseResult Auditing(int storeId, int userId, AdvancePaymentBill bill)
        {
            var uow = AdvancePaymentBillsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();

                bill.StoreId = storeId;

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


        public BaseResult AuditingNoTran(int storeId, int userId, AdvancePaymentBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "������˳ɹ�" };
            var failed = new BaseResult { Success = false, Message = "�������ʧ��" };

            try
            {
                return _recordingVoucherService.CreateVoucher<AdvancePaymentBill, AdvancePaymentBillAccounting>(bill, storeId, userId, (voucherId) =>
                {
                    bill.VoucherId = voucherId;
                    bill.AuditedUserId = userId;
                    bill.AuditedDate = DateTime.Now;
                    bill.AuditedStatus = true;
                    UpdateAdvancePaymentBill(bill);
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
                            BillType = BillTypeEnum.AdvancePaymentBill,
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
                },
                () => { return failed; });
            }
            catch (Exception)
            {
                return failed;
            }

        }

        public BaseResult Reverse(int userId, AdvancePaymentBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "���ݺ��ɹ�" };
            var failed = new BaseResult { Success = false, Message = "���ݺ��ʧ��" };

            var uow = AdvancePaymentBillsRepository.UnitOfWork;
            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();

                #region ������ƾ֤

                _recordingVoucherService.CancleVoucher<AdvancePaymentBill, AdvancePaymentBillAccounting>(bill, () =>
                {

                    #region �޸ĵ��ݱ�״̬
                    bill.ReversedUserId = userId;
                    bill.ReversedDate = DateTime.Now;
                    bill.ReversedStatus = true;
                    //UpdateAdvancePaymentBill(bill);
                    #endregion

                    bill.VoucherId = 0;
                    UpdateAdvancePaymentBill(bill);
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
