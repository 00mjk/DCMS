using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Data;
using DCMS.Core.Domain.Finances;
using DCMS.Core.Domain.Purchases;
using DCMS.Core.Domain.Sales;
using DCMS.Core.Domain.Tasks;
using DCMS.Core.Domain.WareHouses;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Events;
using DCMS.Services.Settings;
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
    /// ���ڱ�ʾ����ƾ֤����
    /// </summary>
    public partial class RecordingVoucherService : BaseService, IRecordingVoucherService
    {

        private readonly IUserService _userService;
        private readonly IQueuedMessageService _queuedMessageService;
        private readonly IAccountingService _accountingService;

        public RecordingVoucherService(IServiceGetter getter,
            IStaticCacheManager cacheManager,
            IEventPublisher eventPublisher,
            IUserService userService,
            IQueuedMessageService queuedMessageService,
            IAccountingService accountingService
            ) : base(getter, cacheManager, eventPublisher)
        {
            _userService = userService;
            _queuedMessageService = queuedMessageService;
            _accountingService = accountingService;
        }

        #region ����


        public virtual IPagedList<RecordingVoucher> GetAllRecordingVouchers(int? store, int? makeuserId, int? generateMode, string billNumber = "", string summary = "", bool? status = null, DateTime? start = null, DateTime? end = null, int? billTypeId = null, string recordName = "", int? accountingOptionId = null, int pageIndex = 0, int pageSize = 30)
        {
            if (pageSize >= 50)
                pageSize = 50;
            DateTime.TryParse(start.Value.ToString("yyyy-MM-dd 00:00:00"), out DateTime first);
            DateTime.TryParse(end.Value.ToString("yyyy-MM-dd 23:59:59"), out DateTime last);

            var query = from rv in RecordingVouchersRepository.Table
                        .Include(r => r.Items)
                        select rv;

            if (store.HasValue)
            {
                query = query.Where(x => x.StoreId == store);
            }

            if (start.HasValue)
            {
                query = query.Where(x => x.RecordTime >= first);
            }

            if (end.HasValue)
            {
                query = query.Where(x => x.RecordTime <= last);
            }

            if (makeuserId.HasValue && makeuserId > 0)
            {
                query = query.Where(x => x.MakeUserId == makeuserId);
            }

            if (generateMode.HasValue)
            {
                query = query.Where(c => c.GenerateMode == generateMode);
            }

            if (!string.IsNullOrWhiteSpace(billNumber))
            {
                query = query.Where(c => c.BillNumber.Contains(billNumber));
            }

            if (status.HasValue)
            {
                query = query.Where(c => c.AuditedStatus == status);
            }

            if (billTypeId.HasValue && billTypeId > 0)
            {
                query = query.Where(t => t.BillTypeId == billTypeId);
            }

            if (!string.IsNullOrWhiteSpace(recordName))
            {
                //query = query.Where(t => (t.RecordName + "-" + t.RecordNumber).Contains(recordName));
                query = query.Where(t => recordName.Contains(t.RecordNumber.ToString()));
            }

            if (accountingOptionId.HasValue && accountingOptionId.Value > 0)
            {
                query = query.Where(x => x.Items.Count(s => s.AccountingOptionId == accountingOptionId) > 0);
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                query = query.Where(x => x.Items.Count(s => s.Summary.Contains(summary)) > 0);
            }

            query = query.OrderByDescending(c => c.RecordTime);

            var plists = query.ToList();

            //��ҳ
            return new PagedList<RecordingVoucher>(plists, pageIndex, pageSize);
        }

        public virtual IList<RecordingVoucher> GetAllRecordingVouchers(int? store)
        {
            var query = from c in RecordingVouchersRepository.Table
                        where c.StoreId == store
                        orderby c.Id
                        select c;

            var categories = query.ToList();
            return categories;
        }

        /// <summary>
        /// ���ݾ����̡�����״̬���������ͻ�ȡ����ƾ֤
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="deleteStatus"></param>
        /// <param name="billTypeEnum"></param>
        /// <returns></returns>
        public virtual IList<RecordingVoucher> GetAllRecordingVouchersByStoreIdBillType(int storeId, bool? deleteStatus, BillTypeEnum billTypeEnum)
        {
            var query = RecordingVouchersRepository.Table;

            //������
            query = query.Where(c => c.StoreId == storeId);

            //����δɾ��
            if (deleteStatus == null || deleteStatus == false)
            {
                query = query.Where(c => c.Deleted == false);
            }
            //����ɾ��
            else
            {
                query = query.Where(c => c.Deleted == deleteStatus);
            }

            //��������
            query = query.Where(c => c.BillTypeId == (int)billTypeEnum);

            var categories = query.ToList();
            return categories;
        }

        public virtual RecordingVoucher GetRecordingVoucherById(int? store, int recordingVoucherId, bool isInclulude = false)
        {
            if (recordingVoucherId == 0)
            {
                return null;
            }

            var key = DCMSDefaults.RECORDINGVOUCHER_BY_ID_KEY.FillCacheKey(store ?? 0, recordingVoucherId);
            return _cacheManager.Get(key, () => 
            {
                if (isInclulude)
                {
                    var query = RecordingVouchersRepository_RO.Table.Include(rv => rv.Items);
                    return query.FirstOrDefault(r => r.Id == recordingVoucherId);
                }

                return RecordingVouchersRepository.ToCachedGetById(recordingVoucherId); 
            });
        }

        public virtual RecordingVoucher GetRecordingVoucher(int storeId, int billTypeId, string billNumber)
        {
            if (storeId == 0 || billTypeId == 0 || string.IsNullOrEmpty(billNumber))
            {
                return null;
            }

            var query = RecordingVouchersRepository.Table;
            query = query.Where(a => a.StoreId == storeId);
            query = query.Where(a => a.BillTypeId == billTypeId);
            query = query.Where(a => a.BillNumber == billNumber);
            return query.FirstOrDefault();

        }

        public virtual List<RecordingVoucher> GetRecordingVouchers(int storeId, int billTypeId, string billNumber)
        {
            if (storeId == 0 || billTypeId == 0 || string.IsNullOrEmpty(billNumber))
            {
                return null;
            }

            var query = RecordingVouchersRepository.Table;
            query = query.Where(a => a.StoreId == storeId);
            query = query.Where(a => a.BillTypeId == billTypeId);
            query = query.Where(a => a.BillNumber == billNumber);
            return query.ToList();

        }

        public virtual void InsertRecordingVoucher(RecordingVoucher recordingVoucher)
        {
            if (recordingVoucher == null)
            {
                throw new ArgumentNullException("recordingVoucher");
            }

            var uow = RecordingVouchersRepository.UnitOfWork;
            RecordingVouchersRepository.Insert(recordingVoucher);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(recordingVoucher);
        }

        public virtual void UpdateRecordingVoucher(RecordingVoucher recordingVoucher)
        {
            if (recordingVoucher == null)
            {
                throw new ArgumentNullException("recordingVoucher");
            }

            var uow = RecordingVouchersRepository.UnitOfWork;
            RecordingVouchersRepository.Update(recordingVoucher);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(recordingVoucher);
        }

        public virtual void DeleteRecordingVoucher(RecordingVoucher recordingVoucher)
        {
            if (recordingVoucher == null)
            {
                throw new ArgumentNullException("recordingVoucher");
            }

            var uow = RecordingVouchersRepository.UnitOfWork;
            RecordingVouchersRepository.Delete(recordingVoucher);
            uow.SaveChanges();


            //event notification
            _eventPublisher.EntityDeleted(recordingVoucher);
        }


        public virtual void RollBackRecordingVoucher(RecordingVoucher recordingVoucher)
        {
            if (recordingVoucher != null)
            {
                DeleteRecordingVoucher(recordingVoucher);
                DeleteVoucherItemWithVoucher(recordingVoucher);
            }
        }




        public virtual void DeleteRecordingVoucherFromPeriod(int? storeId, DateTime? period, string billNumber)
        {
            var uow = RecordingVouchersRepository.UnitOfWork;
            var rvs = GetLikeRecordingVoucherFromPeriod(storeId, period, billNumber);
            var rvs_ids = rvs.Select(s => s.Id);

            var query = from pc in VoucherItemsRepository.Table
                        where rvs_ids.Contains(pc.RecordingVoucherId)
                        orderby pc.Id
                        select pc;
            var vis = query.ToList();

            if (vis != null && vis.Any())
            {
                VoucherItemsRepository.Delete(vis);
            }

            if (rvs != null && rvs.Any())
            {
                RecordingVouchersRepository.Delete(rvs);
            }

            if (vis != null && vis.Any() || rvs != null && rvs.Any())
            {
                uow.SaveChanges();

                //event notification
                rvs.ToList().ForEach(r =>
                {
                    _eventPublisher.EntityDeleted(r);
                });
            }
        }

        /// <summary>
        /// ��ȡָ���ڼ��ƾ֤
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        public IList<RecordingVoucher> GetRecordingVoucherFromPeriod(int? storeId, DateTime? period)
        {
            if (period.HasValue)
            {
                //���µ�һ��
                var first = period.Value.AddDays(1 - period.Value.Day);
                //�������һ��
                var last = period.Value.AddDays(1 - period.Value.Day).AddMonths(1).AddDays(-1);

                DateTime.TryParse(first.ToString("yyyy-MM-dd 00:00:00"), out DateTime start);
                DateTime.TryParse(last.ToString("yyyy-MM-dd 23:59:59"), out DateTime end);

                var query = from rv in RecordingVouchersRepository.TableNoTracking
                            where rv.StoreId == storeId
                            && rv.RecordTime >= start
                            && rv.RecordTime <= end
                            orderby rv.Id
                            select rv;
                return query.ToList();
            }

            return null;
        }

        /// <summary>
        /// ��ȡָ���ڼ��ƾ֤
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="period"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public IList<RecordingVoucher> GetLikeRecordingVoucherFromPeriod(int? storeId, DateTime? period, string billNumber)
        {
            if (period.HasValue)
            {
                //���µ�һ��
                var first = period.Value.AddDays(1 - period.Value.Day);
                //�������һ��
                var last = period.Value.AddDays(1 - period.Value.Day).AddMonths(1).AddDays(-1);

                DateTime.TryParse(first.ToString("yyyy-MM-dd 00:00:00"), out DateTime start);
                DateTime.TryParse(last.ToString("yyyy-MM-dd 23:59:59"), out DateTime end);

                var query = from rv in RecordingVouchersRepository.TableNoTracking
                            where rv.StoreId == storeId
                            && rv.RecordTime >= start
                            && rv.RecordTime <= end
                            && rv.BillNumber.Contains(billNumber)
                            orderby rv.Id
                            select rv;
                return query.ToList();
            }

            return null;
        }


        #endregion

        #region ������Ŀ


        /// <summary>
        /// ��ȡָ����Ŀ���ڳ����
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="accountingOptionId"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public Tuple<decimal, decimal, decimal> GetInitiallBalance(int? storeId, int accountingOptionId, DateTime? first, DateTime? last, decimal balance)
        {
            if (!storeId.HasValue)
            {
                return new Tuple<decimal, decimal, decimal>(0, 0, 0);
            }

            if (accountingOptionId == 0)
            {
                return new Tuple<decimal, decimal, decimal>(0, 0, 0);
            }

            var query = from pc in VoucherItemsRepository.TableNoTracking
                        join aop in AccountingOptionsRepository.TableNoTracking on pc.AccountingOptionId equals aop.Id
                        where pc.StoreId == storeId && (accountingOptionId == pc.AccountingOptionId || accountingOptionId == (aop.ParentId ?? 0))
                        orderby pc.Id
                        select pc;

            if (first.HasValue)
            {
                DateTime.TryParse(first.Value.ToString("yyyy-MM-dd 00:00:00"), out DateTime start);
                query = query.Where(c => c.RecordTime >= start);
            }

            if (last.HasValue)
            {
                DateTime.TryParse(last.Value.ToString("yyyy-MM-dd 23:59:59"), out DateTime end);
                query = query.Where(c => c.RecordTime <= end);
            }

            var items = query.ToList();

            decimal balance_debit = 0;
            decimal balance_credit = 0;

            if (items != null && items.Any())
            {
                balance_debit = items.Sum(s => s.DebitAmount ?? 0);
                balance_credit = items.Sum(s => s.CreditAmount ?? 0);
                balance += balance_debit - balance_credit;
            }

            return new Tuple<decimal, decimal, decimal>(balance, balance_debit, balance_credit);
        }

        public virtual IPagedList<VoucherItem> GetVoucherItemsByRecordingVoucherId(int recordingVoucherId, int? userId, int? storeId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (recordingVoucherId == 0)
            {
                return new PagedList<VoucherItem>(new List<VoucherItem>(), pageIndex, pageSize);
            }

            var key = DCMSDefaults.RECORDINGVOUCHERITEM_ALL_KEY.FillCacheKey(storeId, recordingVoucherId, pageIndex, pageSize, userId);

            return _cacheManager.Get(key, () =>
            {
                var query = from pc in VoucherItemsRepository.Table
                            where pc.RecordingVoucherId == recordingVoucherId
                            orderby pc.Id
                            select pc;
                //var recordingVouchers = new PagedList<VoucherItem>(query.ToList(), pageIndex, pageSize);
                //return recordingVouchers;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<VoucherItem>(plists, pageIndex, pageSize, totalCount);
            });
        }


        public virtual IList<VoucherItem> GetVoucherItemsByRecordingVoucherId(int recordingVoucherId)
        {
            var query = from pc in VoucherItemsRepository.Table
                        where pc.RecordingVoucherId == recordingVoucherId
                        orderby pc.Id
                        select pc;

            return query.ToList();
        }

        public virtual VoucherItem GetVoucherItemById(int? store, int voucherItemId)
        {
            if (voucherItemId == 0)
            {
                return null;
            }

            var key = DCMSDefaults.RECORDINGVOUCHERITEM_BY_ID_KEY.FillCacheKey(store ?? 0, voucherItemId);
            return _cacheManager.Get(key, () => { return VoucherItemsRepository.ToCachedGetById(voucherItemId); });
        }

        public virtual void InsertVoucherItem(VoucherItem voucherItem)
        {
            if (voucherItem == null)
            {
                throw new ArgumentNullException("voucherItem");
            }

            var uow = VoucherItemsRepository.UnitOfWork;

            VoucherItemsRepository.Insert(voucherItem);

            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(voucherItem);
        }

        public virtual void InsertVoucherItems(List<VoucherItem> voucherItems)
        {
            if (voucherItems == null)
            {
                throw new ArgumentNullException("voucherItems");
            }

            var uow = VoucherItemsRepository.UnitOfWork;
            VoucherItemsRepository.Insert(voucherItems);
            uow.SaveChanges();


            voucherItems.ForEach(s => { _eventPublisher.EntityInserted(s); });

        }



        public virtual void UpdateVoucherItem(VoucherItem voucherItem)
        {
            if (voucherItem == null)
            {
                throw new ArgumentNullException("voucherItem");
            }

            var uow = VoucherItemsRepository.UnitOfWork;
            VoucherItemsRepository.Update(voucherItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(voucherItem);
        }

        public virtual void DeleteVoucherItem(VoucherItem voucherItem)
        {
            if (voucherItem == null)
            {
                throw new ArgumentNullException("voucherItem");
            }

            var uow = VoucherItemsRepository.UnitOfWork;
            VoucherItemsRepository.Delete(voucherItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(voucherItem);
        }

        public virtual void DeleteVoucherItemWithVoucher(RecordingVoucher recordingVoucher)
        {
            var uow = VoucherItemsRepository.UnitOfWork;
            var items = VoucherItemsRepository.Table.Where(s => s.StoreId == recordingVoucher.StoreId && s.RecordingVoucherId == recordingVoucher.Id);
            VoucherItemsRepository.Delete(items);
            uow.SaveChanges();

            //֪ͨ
            items.ToList().ForEach(s => { _eventPublisher.EntityInserted(s); });
        }

        /// <summary>
        /// ��ȡָ���ڼ��Ŀ��ϸ
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="accountsIds"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public virtual IList<VoucherItem> GetVoucherItemsByAccounts(int? storeId, int[] accountsIds, DateTime? first, DateTime? last)
        {
            if (!first.HasValue || !first.HasValue)
            {
                return null;
            }

            DateTime.TryParse(first.Value.ToString("yyyy-MM-dd 00:00:00"), out DateTime start);
            DateTime.TryParse(last.Value.ToString("yyyy-MM-dd 23:59:59"), out DateTime end);

            var query = from pc in VoucherItemsRepository.TableNoTracking
                        where pc.StoreId == storeId
                        && accountsIds.Contains(pc.AccountingOptionId)
                        && pc.RecordTime >= start
                        && pc.RecordTime <= end
                        orderby pc.Id
                        select pc;

            return query.ToList();
        }

        public virtual IList<VoucherItem> GetVoucherItemsByRecordingVoucherId(int? storeId, int recordingVoucherId)
        {
            var query = from pc in VoucherItemsRepository.TableNoTracking
                        where pc.StoreId == storeId
                        && pc.RecordingVoucherId == recordingVoucherId
                        orderby pc.Id
                        select pc;

            return query.ToList();
        }

        /// <summary>
        /// ���ݿ�ĿId��ȡָ���ڼ�ƾ֤��ϸ
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="accountingOptionId"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        public IList<VoucherItem> GetVoucherItemsByAccountingOptionIdFromPeriod(int? storeId, int accountingOptionId, DateTime? period)
        {
            if (period.HasValue)
            {
                //���µ�һ��
                var first = period.Value.AddDays(1 - period.Value.Day);
                //�������һ��
                var last = period.Value.AddDays(1 - period.Value.Day).AddMonths(1).AddDays(-1);

                DateTime.TryParse(first.ToString("yyyy-MM-dd 00:00:00"), out DateTime start);
                DateTime.TryParse(last.ToString("yyyy-MM-dd 23:59:59"), out DateTime end);

                var query = from rv in RecordingVouchersRepository.TableNoTracking
                            join vi in VoucherItemsRepository.TableNoTracking on rv.Id equals vi.RecordingVoucherId
                            where rv.StoreId == storeId
                            && vi.AccountingOptionId == accountingOptionId
                            && rv.RecordTime >= start
                            && rv.RecordTime <= end
                            orderby vi.Id
                            select vi;

                return query.ToList();
            }

            return null;
        }

        /// <summary>
        /// ���ݿ�Ŀö���������ȡָ���ڼ�ƾ֤��ϸ
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="accountCodeTypeId"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        public IList<VoucherItem> GetVoucherItemsByAccountCodeTypeIdFromPeriod(int? storeId, int accountCodeTypeId, DateTime? period)
        {
            if (period.HasValue)
            {
                //���µ�һ��
                var first = period.Value.AddDays(1 - period.Value.Day);
                //�������һ��
                var last = period.Value.AddDays(1 - period.Value.Day).AddMonths(1).AddDays(-1);

                DateTime.TryParse(first.ToString("yyyy-MM-dd 00:00:00"), out DateTime start);
                DateTime.TryParse(last.ToString("yyyy-MM-dd 23:59:59"), out DateTime end);

                var query = from rv in RecordingVouchersRepository.TableNoTracking
                            join vi in VoucherItemsRepository.TableNoTracking on rv.Id equals vi.RecordingVoucherId
                            join aor in AccountingOptionsRepository.TableNoTracking on vi.AccountingOptionId equals aor.Id
                            where rv.StoreId == storeId
                            && aor.AccountCodeTypeId == accountCodeTypeId
                            && rv.RecordTime >= start
                            && rv.RecordTime <= end
                            orderby vi.Id
                            select vi;

                return query.ToList();
            }

            return null;
        }

        /// <summary>
        /// ��ȡָ����Ŀ�Ľ�תƾ֤��Ŀ
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="accountingOptionId"></param>
        /// <param name="period"></param>
        /// <param name="numberName"></param>
        /// <returns></returns>
        public IList<VoucherItem> GetVoucherItemsByAccountingOptionIdFromPeriod(int? storeId, int accountingOptionId, DateTime? period, string numberName)
        {
            if (period.HasValue)
            {
                //���µ�һ��
                var first = period.Value.AddDays(1 - period.Value.Day);
                //�������һ��
                var last = period.Value.AddDays(1 - period.Value.Day).AddMonths(1).AddDays(-1);

                DateTime.TryParse(first.ToString("yyyy-MM-dd 00:00:00"), out DateTime start);
                DateTime.TryParse(last.ToString("yyyy-MM-dd 23:59:59"), out DateTime end);

                var query = from rv in RecordingVouchersRepository.TableNoTracking
                            join vi in VoucherItemsRepository.TableNoTracking on rv.Id equals vi.RecordingVoucherId
                            where rv.StoreId == storeId
                            && rv.BillNumber.Contains(numberName)
                            && vi.AccountingOptionId == accountingOptionId
                            && rv.RecordTime >= start
                            && rv.RecordTime <= end
                            orderby vi.Id
                            select vi;

                return query.ToList();
            }

            return null;
        }


        public IList<VoucherItem> GetVoucherItemsByAccountingOptionIdFromPeriod(int? storeId, int accountingOptionId, DateTime? _start, DateTime? _end, string numberName)
        {
            if (!_start.HasValue)
            {
                return null;
            }

            if (!_end.HasValue)
            {
                return null;
            }

            var first = _start.Value.AddDays(1 - _start.Value.Day);
            var last = _end.Value.AddDays(1 - _end.Value.Day).AddMonths(1).AddDays(-1);

            DateTime.TryParse(first.ToString("yyyy-MM-dd 00:00:00"), out DateTime start);
            DateTime.TryParse(last.ToString("yyyy-MM-dd 23:59:59"), out DateTime end);

            var query = from rv in RecordingVouchersRepository.TableNoTracking
                        join vi in VoucherItemsRepository.TableNoTracking on rv.Id equals vi.RecordingVoucherId
                        where rv.StoreId == storeId
                        && rv.BillNumber.Contains(numberName)
                        && vi.AccountingOptionId == accountingOptionId
                        && rv.RecordTime >= start
                        && rv.RecordTime <= end
                        orderby vi.Id
                        select vi;

            return query.ToList();

        }


        /// <summary>
        /// ��ȡָ����Ŀָ���ڼ���ĩ�����תƾ֤��
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="accountingOptionId"></param>
        /// <param name="period"></param>
        /// <param name="numberName"></param>
        /// <returns></returns>
        public VoucherItem GetPeriodLossSettle(int? storeId, int accountingOptionId, DateTime? period)
        {
            if (period.HasValue)
            {
                var query = from rv in RecordingVouchersRepository.TableNoTracking
                            join vi in VoucherItemsRepository.TableNoTracking on rv.Id equals vi.RecordingVoucherId
                            where rv.StoreId == storeId
                            && rv.BillNumber.Contains($"settle{period.Value.ToString("yyyyMM")}")
                            && vi.AccountingOptionId == accountingOptionId
                            && rv.RecordTime.ToString("yyyy-MM") == period.Value.ToString("yyyy-MM")
                            orderby vi.Id
                            select vi;

                return query.FirstOrDefault();
            }
            return null;
        }
        #endregion

        #region ����


        /// <summary>
        /// ��ȡƾ֤��
        /// </summary>
        /// <param name="store"></param>
        /// <returns></returns>
        public int GetRecordingVoucherNumber(int? store, DateTime period)
        {
            if (period == null)
            {
                period = DateTime.Now;
            }

            //���µ�һ��
            var first = period.AddDays(1 - period.Day);
            //�������һ��
            var last = period.AddDays(1 - period.Day).AddMonths(1).AddDays(-1);

            DateTime.TryParse(first.ToString("yyyy-MM-dd 00:00:00"), out DateTime start);
            DateTime.TryParse(last.ToString("yyyy-MM-dd 23:59:59"), out DateTime end);

            var maxRecordingVoucher = RecordingVouchersRepository.TableNoTracking
                .Where(r => r.StoreId == store && r.RecordTime >= start && r.RecordTime <= end)
                .OrderByDescending(r => r.Id)
                .Count();

            return maxRecordingVoucher += 1;
        }

        /// <summary>
        /// ����¼��ƾ֤
        /// </summary>
        /// <param name="store"></param>
        /// <param name="makeUserId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool CreateRecordingVoucher(int? store, int? makeUserId, RecordingVoucher recordingVoucher)
        {
            try
            {
                #region ���ƾ֤

                //������
                recordingVoucher.StoreId = store ?? 0;
                //ƾ֤��(��)
                recordingVoucher.RecordName = string.IsNullOrEmpty(recordingVoucher.RecordName) ? "��" : recordingVoucher.RecordName;
                //ƾ֤��
                recordingVoucher.RecordNumber = GetRecordingVoucherNumber(store, recordingVoucher.RecordTime);

                //��������
                if (recordingVoucher.RecordTime == null)
                {
                    recordingVoucher.RecordTime = DateTime.Now;
                }

                //���ݱ��
                recordingVoucher.BillNumber = recordingVoucher.BillNumber;
                //�Ƶ���
                recordingVoucher.MakeUserId = makeUserId ?? 0;

                if (recordingVoucher.AuditedStatus)
                {
                    recordingVoucher.AuditedUserId = makeUserId ?? 0;
                    //״̬(���)
                    recordingVoucher.AuditedStatus = true;
                    //���ʱ��
                    recordingVoucher.AuditedDate = DateTime.Now;
                }

                //��������
                recordingVoucher.BillTypeId = recordingVoucher.BillTypeId;
                //�ֹ�����
                recordingVoucher.GenerateMode = recordingVoucher.GenerateMode;

                InsertRecordingVoucher(recordingVoucher);

                #endregion

                #region ƾ֤��Ŀ

                var voucherItems = recordingVoucher.Items;
                foreach (var item in voucherItems)
                {
                    if (!recordingVoucher.Items.Select(s => s.Id).Contains(item.Id))
                    {
                        item.StoreId = store ?? 0;
                        item.RecordingVoucherId = recordingVoucher.Id;
                        item.RecordTime = item.RecordTime == null ? DateTime.Now : item.RecordTime;
                        item.AccountingOptionName = string.IsNullOrEmpty(item.AccountingOptionName) ? "" : item.AccountingOptionName;

                        //if (item.AccountingOptionId != 0)
                        //    InsertVoucherItem(item);
                        //�衢�� ���ܶ�Ϊ0
                        if (item.AccountingOptionId != 0 && (item.DebitAmount != 0 || item.CreditAmount != 0))
                        {
                            InsertVoucherItem(item);
                        }

                    }
                }

                #endregion

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region ������

        public BaseResult BillCreateOrUpdate(int storeId, int userId, int? voucherId, RecordingVoucher recordingVoucher, RecordingVoucherUpdate data, List<VoucherItem> items, bool isAdmin = false)
        {
            var uow = RecordingVouchersRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();



                if (voucherId.HasValue && voucherId.Value != 0)
                {
                    #region ����ƾ֤ 
                    if (recordingVoucher != null)
                    {

                        //ƾ֤��(��)
                        recordingVoucher.RecordName = data.RecordName;
                        //ƾ֤��
                        recordingVoucher.RecordNumber = data.RecordNumber;
                        //��������
                        recordingVoucher.RecordTime = data.RecordTime == null ? DateTime.Now : data.RecordTime;

                        UpdateRecordingVoucher(recordingVoucher);
                    }

                    #endregion
                }
                else
                {
                    #region ���ƾ֤

                    recordingVoucher.StoreId = storeId;
                    //ƾ֤��(��)
                    recordingVoucher.RecordName = data.RecordName;
                    //ƾ֤��
                    recordingVoucher.RecordNumber = data.RecordNumber;
                    //��������
                    recordingVoucher.RecordTime = data.RecordTime == null ? DateTime.Now : data.RecordTime;
                    //���ݱ��
                    recordingVoucher.BillNumber = CommonHelper.GetBillNumber("PZ", storeId);
                    //�Ƶ���
                    recordingVoucher.MakeUserId = userId;
                    //״̬(���)
                    recordingVoucher.AuditedStatus = false;
                    recordingVoucher.AuditedDate = null;
                    //��������
                    recordingVoucher.BillTypeId = 0;
                    //�ֹ�����
                    recordingVoucher.GenerateMode = 0;
                    InsertRecordingVoucher(recordingVoucher);

                    #endregion
                }

                #region ����ƾ֤��Ŀ

                data.Items.ForEach(p =>
                {
                    if (p.AccountingOptionId != 0)
                    {
                        var sd = GetVoucherItemById(storeId, p.Id);
                        if (sd == null)
                        {
                            //׷����
                            if (recordingVoucher.Items.Count(cp => cp.Id == p.Id) == 0)
                            {
                                var item = p;
                                item.StoreId = storeId;
                                item.RecordTime = DateTime.Now;
                                item.RecordingVoucherId = recordingVoucher.Id;
                                item.RecordTime = recordingVoucher.RecordTime;
                                InsertVoucherItem(item);
                                //���ų�
                                p.Id = item.Id;
                                //recordingVoucher.Items.Add(item);
                                if (!recordingVoucher.Items.Select(s => s.Id).Contains(item.Id))
                                {
                                    recordingVoucher.Items.Add(item);
                                }
                            }
                        }
                        else
                        {
                            //���������
                            sd.Summary = p.Summary;
                            sd.AccountingOptionId = p.AccountingOptionId;
                            sd.DebitAmount = p.DebitAmount;
                            sd.CreditAmount = sd.CreditAmount;
                            UpdateVoucherItem(sd);
                        }
                    }
                });

                #endregion

                #region Grid �Ƴ���ӿ��Ƴ�ɾ����

                recordingVoucher.Items.ToList().ForEach(p =>
                {
                    if (data.Items.Count(cp => cp.Id == p.Id) == 0)
                    {
                        recordingVoucher.Items.Remove(p);
                        var item = GetVoucherItemById(storeId, p.Id);
                        if (item != null)
                        {
                            DeleteVoucherItem(item);
                        }
                    }
                });

                #endregion

                //1.����Ա���� �Զ����
                //2.�Զ�����ƾ֤ �Զ����
                if (isAdmin || recordingVoucher.GenerateMode == (int)GenerateMode.Auto) //�жϵ�ǰ��¼���Ƿ�Ϊ����Ա,��Ϊ����Ա�������Զ����
                {

                    #region �޸ĵ��ݱ�״̬
                    recordingVoucher.AuditedUserId = userId;
                    recordingVoucher.AuditedDate = DateTime.Now;
                    recordingVoucher.AuditedStatus = true;

                    UpdateRecordingVoucher(recordingVoucher);
                    #endregion

                    #region ����֪ͨ �Ƶ���
                    try
                    {
                        //�Ƶ���
                        var userNumbers = _userService.GetAllUserMobileNumbersByUserIds(new List<int> { recordingVoucher.MakeUserId });
                        var queuedMessage = new QueuedMessage()
                        {
                            StoreId = storeId,
                            MType = MTypeEnum.Audited,
                            Title = CommonHelper.GetEnumDescription<MTypeEnum>(MTypeEnum.Audited),
                            Date = recordingVoucher.AuditedDate ?? DateTime.Now,
                            BillType = BillTypeEnum.RecordingVoucher,
                            BillNumber = recordingVoucher.BillNumber,
                            BillId = recordingVoucher.Id,
                            CreatedOnUtc = DateTime.Now
                        };
                        _queuedMessageService.InsertQueuedMessage(userNumbers.ToList(),queuedMessage);
                    }
                    catch (Exception ex)
                    {
                        _queuedMessageService.WriteLogs(ex.Message);
                    }
                    #endregion
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
                            Date = recordingVoucher.AuditedDate ?? DateTime.Now,
                            BillType = BillTypeEnum.PurchaseBill,
                            BillNumber = recordingVoucher.BillNumber,
                            BillId = recordingVoucher.Id,
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

                return new BaseResult { Success = true, Return = voucherId ?? 0, Message = Resources.Bill_CreateOrUpdateSuccessful };
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

        public BaseResult Auditing(int storeId, int userId, RecordingVoucher recordingVoucher)
        {
            var uow = RecordingVouchersRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();



                #region �޸ĵ��ݱ�״̬
                recordingVoucher.AuditedUserId = userId;
                recordingVoucher.AuditedDate = DateTime.Now;
                recordingVoucher.AuditedStatus = true;

                UpdateRecordingVoucher(recordingVoucher);
                #endregion

                #region ����֪ͨ
                try
                {
                    //�Ƶ���
                    var userNumbers = _userService.GetAllUserMobileNumbersByUserIds(new List<int> { recordingVoucher.MakeUserId });
                    QueuedMessage queuedMessage = new QueuedMessage()
                    {
                        StoreId = storeId,
                        MType = MTypeEnum.Audited,
                        Title = CommonHelper.GetEnumDescription<MTypeEnum>(MTypeEnum.Audited),
                        Date = recordingVoucher.AuditedDate ?? DateTime.Now,
                        BillType = BillTypeEnum.RecordingVoucher,
                        BillNumber = recordingVoucher.BillNumber,
                        BillId = recordingVoucher.Id,
                        CreatedOnUtc = DateTime.Now
                    };
                    _queuedMessageService.InsertQueuedMessage(userNumbers.ToList(),queuedMessage);
                }
                catch (Exception ex)
                {
                    _queuedMessageService.WriteLogs(ex.Message);
                }
                #endregion


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

        public BaseResult Reverse(int userId, RecordingVoucher recordingVoucher)
        {
            var uow = RecordingVouchersRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();





                //��������
                transaction.Commit();
                return new BaseResult { Success = true, Message = "���ݺ��ɹ�" };
            }
            catch (Exception)
            {
                //������񲻴��ڻ���Ϊ����ع�
                transaction?.Rollback();
                return new BaseResult { Success = false, Message = "���ݺ��ʧ��" };
            }
            finally
            {
                //����������󶼻�رյ��������
                using (transaction) { }
            }
        }

        #endregion

        #region �������


        /// <summary>
        /// ҵ�񵥾ݼ���
        /// ���۵� SaleBills
        /// �˻��� ReturnBills
        /// �ɹ��� PurchaseBills
        /// �ɹ��˻��� PurchaseReturnBills
        /// �տ CashReceiptBills
        /// Ԥ�տ AdvanceReceiptBills
        /// ��� PaymentReceiptBills
        /// Ԥ��� AdvancePaymentBills
        /// �������� FinancialIncomeBills
        /// �ɱ����۵� CostAdjustmentBills
        /// ���� ScrapProductBills
        /// ����֧�� CostExpenditureBills
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bill">����</param>
        public BaseResult CreateVoucher<T, T1>(T bill, int storeId, int makeUserId, Action<int> update, Func<BaseResult> successful, Func<BaseResult> failed) where T : BaseBill<T1> where T1 : BaseEntity
        {
            try
            {
                //���۵�
                if (bill is SaleBill sb)
                {
                    #region
                    /*
                    ���۵�:  ����ʱ���տ��˻���ĿĬ��ѡ���б��� ����ֽ�Ĭ�ϣ��� ���д�   �����˻���ָ������֧������  Ԥ���˿
                    */
                    if (sb != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = sb.Id,
                            //���ݱ��
                            BillNumber = sb.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.SaleBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };

                        #region �跽���տ��˻����̶�����

                        //1.�Ż�
                        var preferential = _accountingService.Parse(storeId, AccountingCodeEnum.Preferential);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 0,
                            RecordTime = DateTime.Now,
                            Summary = preferential?.Name,
                            AccountingOptionName = preferential?.Name,
                            AccountingOptionId = preferential?.Id ?? 0,
                            DebitAmount = sb.PreferentialAmount
                        });

                        if (sb.OweCash > 0)
                        {
                            //2.Ӧ���˿�յ���Ƿ�
                            var accountsReceivable = _accountingService.Parse(storeId, AccountingCodeEnum.AccountsReceivable);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 0,
                                RecordTime = DateTime.Now,
                                Summary = accountsReceivable?.Name,
                                AccountingOptionName = accountsReceivable?.Name,
                                AccountingOptionId = accountsReceivable?.Id ?? 0,
                                //DebitAmount = sb.ReceivableAmount
                                DebitAmount = sb.OweCash
                            });
                        }

                        //3.�տ��˻�������ֽ����д������˻���ָ������֧������Ԥ���˿
                        if (sb.SaleBillAccountings?.Any() ?? false)
                        {
                            sb.SaleBillAccountings.ToList().ForEach(a =>
                            {
                                if (a.CollectionAmount != 0)
                                {
                                    //�����ϸ
                                    recordingVoucher.Items.Add(new VoucherItem()
                                    {
                                        StoreId = storeId,
                                        Direction = 0,
                                        RecordTime = DateTime.Now,
                                        Summary = a.AccountingOption?.Name,
                                        AccountingOptionName = a.AccountingOption?.Name,
                                        AccountingOptionId = a.AccountingOptionId,
                                        DebitAmount = a.CollectionAmount
                                    });
                                }
                            });
                        }

                        #endregion

                        #region �������̶�����

                        //1.��Ӫҵ������
                        var mainIncome = _accountingService.Parse(storeId, AccountingCodeEnum.MainIncome);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 1,
                            RecordTime = DateTime.Now,
                            Summary = mainIncome?.Name,
                            AccountingOptionName = mainIncome?.Name,
                            AccountingOptionId = mainIncome?.Id ?? 0,
                            CreditAmount = sb.SumAmount
                        });

                        //2.����˰�����˰�ʺ� 
                        var outputTax = _accountingService.Parse(storeId, AccountingCodeEnum.OutputTax);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 1,
                            RecordTime = DateTime.Now,
                            Summary = outputTax?.Name,
                            AccountingOptionName = outputTax?.Name,
                            AccountingOptionId = outputTax?.Id ?? 0,
                            CreditAmount = sb.TaxAmount
                        });

                        #endregion

                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);


                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }

                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }

                    #endregion
                }
                //�˻���
                else if (bill is ReturnBill rb)
                {
                    #region
                    /*
                    �����˻���:  ����ʱ���տ��˻���ĿĬ��ѡ���б��� ����ֽ�Ĭ�ϣ��� ���д�   �����˻���ָ������֧������  Ԥ���˿
                    */
                    if (rb != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = rb.Id,
                            //���ݱ��
                            BillNumber = rb.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.ReturnBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };


                        var accountings = rb.ReturnBillAccountings;

                        #region �跽

                        //1.��Ӫҵ������
                        var mainincome = _accountingService.Parse(storeId, AccountingCodeEnum.MainIncome);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 0,
                            RecordTime = DateTime.Now,
                            Summary = mainincome?.Name,
                            AccountingOptionName = mainincome?.Name,
                            AccountingOptionId = mainincome?.Id ?? 0,
                            DebitAmount = rb.SumAmount
                        });

                        //2.����˰�����˰�ʺ�   
                        var outputtax = _accountingService.Parse(storeId, AccountingCodeEnum.OutputTax);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 0,
                            RecordTime = DateTime.Now,
                            Summary = outputtax?.Name,
                            AccountingOptionName = outputtax?.Name,
                            AccountingOptionId = outputtax?.Id ?? 0,
                            DebitAmount = rb.TaxAmount
                        });
                        #endregion

                        #region ����

                        //1.�Ż�
                        var preferential = _accountingService.Parse(storeId, AccountingCodeEnum.Preferential);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 1,
                            RecordTime = DateTime.Now,
                            Summary = preferential?.Name,
                            AccountingOptionName = preferential?.Name,
                            AccountingOptionId = preferential?.Id ?? 0,
                            CreditAmount = rb.PreferentialAmount
                        });

                        if (rb.OweCash > 0)
                        {
                            //2.Ӧ���˿�յ���Ƿ�
                            var accountsreceivable = _accountingService.Parse(storeId, AccountingCodeEnum.AccountsReceivable);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 1,
                                RecordTime = DateTime.Now,
                                Summary = accountsreceivable?.Name,
                                AccountingOptionName = accountsreceivable?.Name,
                                AccountingOptionId = accountsreceivable?.Id ?? 0,
                                CreditAmount = rb.OweCash
                            });
                        }

                        //3.�տ��˻�������ֽ����д������˻���ָ������֧������Ԥ���˿   
                        if (rb.ReturnBillAccountings?.Any() ?? false)
                        {
                            rb.ReturnBillAccountings.ToList().ForEach(a =>
                            {
                                if (a.CollectionAmount != 0)
                                {
                                    //�����ϸ
                                    recordingVoucher.Items.Add(new VoucherItem()
                                    {
                                        StoreId = storeId,
                                        Direction = 1,
                                        RecordTime = DateTime.Now,
                                        Summary = a.AccountingOption?.Name,
                                        AccountingOptionName = a.AccountingOption?.Name,
                                        AccountingOptionId = a.AccountingOptionId,
                                        CreditAmount = a.CollectionAmount
                                    });
                                }
                            });
                        }
                        #endregion


                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);
                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }

                    #endregion
                }
                //�ɹ��� 
                else if (bill is PurchaseBill pb)
                {
                    #region
                    /*
                    �ɹ���������ʱ�������˻���ĿĬ��ѡ���б��� ����ֽ� ���д�   �����˻���ָ������֧������  Ԥ���˿ 
                    */
                    if (pb != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = pb.Id,
                            //���ݱ��
                            BillNumber = pb.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.PurchaseBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };


                        var accountings = pb.PurchaseBillAccountings;

                        #region �跽

                        //1.����˰�����˰�ʺ� 
                        var inputtax = _accountingService.Parse(storeId, AccountingCodeEnum.InputTax);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 0,
                            RecordTime = DateTime.Now,
                            Summary = inputtax?.Name,
                            AccountingOptionName = inputtax?.Name,
                            AccountingOptionId = inputtax?.Id ?? 0,
                            DebitAmount = pb.TaxAmount
                        });

                        //2.�����Ʒ
                        var inventorygoods = _accountingService.Parse(storeId, AccountingCodeEnum.InventoryGoods);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 0,
                            RecordTime = DateTime.Now,
                            Summary = inventorygoods?.Name,
                            AccountingOptionName = inventorygoods?.Name,
                            AccountingOptionId = inventorygoods?.Id ?? 0,
                            DebitAmount = pb.SumAmount
                        });

                        #endregion

                        #region ����

                        //--2020.04.15  pxh
                        //1.�Ż�
                        if (pb.PreferentialAmount > 0)
                        {
                            var preferential = _accountingService.Parse(storeId, AccountingCodeEnum.Preferential);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 1,
                                RecordTime = DateTime.Now,
                                Summary = preferential?.Name,
                                AccountingOptionName = preferential?.Name,
                                AccountingOptionId = preferential?.Id ?? 0,
                                CreditAmount = pb.PreferentialAmount
                            });
                        }
                        //--pxh

                        if (pb.OweCash > 0)
                        {
                            //1.Ӧ���˿�
                            var accountspayable = _accountingService.Parse(storeId, AccountingCodeEnum.AccountsPayable);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 1,
                                RecordTime = DateTime.Now,
                                Summary = accountspayable?.Name,
                                AccountingOptionName = accountspayable?.Name,
                                AccountingOptionId = accountspayable?.Id ?? 0,
                                CreditAmount = pb.OweCash
                            });

                        }
                        
                        //2.�����˻�������ֽ����д������˻���ָ������֧������Ԥ���˿   
                        if (pb.PurchaseBillAccountings?.Any() ?? false)
                        {
                            pb.PurchaseBillAccountings.ToList().ForEach(a =>
                            {
                                if (a.CollectionAmount != 0)
                                {
                                    //�����ϸ
                                    recordingVoucher.Items.Add(new VoucherItem()
                                    {
                                        StoreId = storeId,
                                        Direction = 1,
                                        RecordTime = DateTime.Now,
                                        Summary = a.AccountingOption?.Name,
                                        AccountingOptionName = a.AccountingOption?.Name,
                                        AccountingOptionId = a.AccountingOptionId,
                                        CreditAmount = a.CollectionAmount
                                    });
                                }
                            });
                        }
                        #endregion


                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);

                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }

                    #endregion
                }
                //�ɹ��˻��� 
                else if (bill is PurchaseReturnBill prb)
                {
                    #region
                    /*
                     �ɹ��˻���������ʱ�������˻���ĿĬ��ѡ���б��� ����ֽ� ���д�   �����˻���ָ������֧������  Ԥ���˿
                     */
                    if (prb != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = prb.Id,
                            //���ݱ��
                            BillNumber = prb.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.PurchaseReturnBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };

                        #region �跽

                        //--2020.04.15  pxh
                        //1.�Ż�
                        if (prb.PreferentialAmount > 0)
                        {
                            var preferential = _accountingService.Parse(storeId, AccountingCodeEnum.Preferential);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 0,
                                RecordTime = DateTime.Now,
                                Summary = preferential?.Name,
                                AccountingOptionName = preferential?.Name,
                                AccountingOptionId = preferential?.Id ?? 0,
                                DebitAmount = prb.PreferentialAmount
                            });
                        }
                        //--pxh

                        if (prb.OweCash > 0)
                        {
                            //1.Ӧ���˿�
                            var accountspayable = _accountingService.Parse(storeId, AccountingCodeEnum.AccountsPayable);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 0,
                                RecordTime = DateTime.Now,
                                Summary = accountspayable?.Name,
                                AccountingOptionName = accountspayable?.Name,
                                AccountingOptionId = accountspayable?.Id ?? 0,
                                DebitAmount = prb.OweCash
                            });
                        }

                        //2.�����˻�������ֽ����д������˻���ָ������֧������Ԥ���˿
                        if (prb.PurchaseReturnBillAccountings?.Any() ?? false)
                        {
                            prb.PurchaseReturnBillAccountings.ToList().ForEach(a =>
                            {
                                if (a.CollectionAmount != 0)
                                {
                                    //�����ϸ
                                    recordingVoucher.Items.Add(new VoucherItem()
                                    {
                                        StoreId = storeId,
                                        Direction = 0,
                                        RecordTime = DateTime.Now,
                                        Summary = a.AccountingOption?.Name,
                                        AccountingOptionName = a.AccountingOption?.Name,
                                        AccountingOptionId = a.AccountingOptionId,
                                        DebitAmount = a.CollectionAmount
                                    });
                                }
                            });
                        }
                        #endregion

                        #region ����

                        //1.����˰�����˰�ʺ�   
                        var inputtax = _accountingService.Parse(storeId, AccountingCodeEnum.InputTax);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 1,
                            RecordTime = DateTime.Now,
                            Summary = inputtax?.Name,
                            AccountingOptionName = inputtax?.Name,
                            AccountingOptionId = inputtax?.Id ?? 0,
                            CreditAmount = prb.TaxAmount
                        });

                        //2.�����Ʒ
                        var inventorygoods = _accountingService.Parse(storeId, AccountingCodeEnum.InventoryGoods);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 1,
                            RecordTime = DateTime.Now,
                            Summary = inventorygoods?.Name,
                            AccountingOptionName = inventorygoods?.Name,
                            AccountingOptionId = inventorygoods?.Id ?? 0,
                            CreditAmount = prb.SumAmount
                        });

                        #endregion

                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);
                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }
                    #endregion
                }
                //�տ 
                else if (bill is CashReceiptBill crb)
                {
                    #region
                    /*
                    �տ������ʱ���տ��˻���ĿĬ��ѡ���б��� ����ֽ�Ĭ�ϣ��� ���д�   �����˻���ָ������֧������  Ԥ���˿
                    */
                    if (crb != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = crb.Id,
                            //���ݱ��
                            BillNumber = crb.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.CashReceiptBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };

                        #region �跽

                        //1.�Ż�
                        var preferential = _accountingService.Parse(storeId, AccountingCodeEnum.Preferential);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 0,
                            RecordTime = DateTime.Now,
                            Summary = preferential?.Name,
                            AccountingOptionName = preferential?.Name,
                            AccountingOptionId = preferential?.Id ?? 0,
                            //DebitAmount = crb.CashReceiptBillAccountings.Sum(s => s.CollectionAmount ?? 0)
                            DebitAmount = crb.Items.Sum(s => s.DiscountAmountOnce ?? 0)
                        });

                        //2.�տ��˻�������ֽ����д������˻���ָ������֧������Ԥ���˿
                        if (crb.CashReceiptBillAccountings?.Any() ?? false)
                        {
                            crb.CashReceiptBillAccountings.ToList().ForEach(a =>
                            {
                                if (a.CollectionAmount != 0)
                                {
                                    //�����ϸ
                                    recordingVoucher.Items.Add(new VoucherItem()
                                    {
                                        StoreId = storeId,
                                        Direction = 0,
                                        RecordTime = DateTime.Now,
                                        Summary = a.AccountingOption?.Name,
                                        AccountingOptionName = a.AccountingOption?.Name,
                                        AccountingOptionId = a.AccountingOptionId,
                                        DebitAmount = a.CollectionAmount
                                    });
                                }
                            });
                        }

                        #endregion

                        #region ����




                        //1.Ӧ���˿�
                        var accountsreceivable = _accountingService.Parse(storeId, AccountingCodeEnum.AccountsReceivable);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 1,
                            RecordTime = DateTime.Now,
                            Summary = accountsreceivable?.Name,
                            AccountingOptionName = accountsreceivable?.Name,
                            AccountingOptionId = accountsreceivable?.Id ?? 0,
                            CreditAmount = crb.CashReceiptBillAccountings.Sum(s => s.CollectionAmount ) + crb.Items.Sum(s => s.DiscountAmountOnce ?? 0)
                        });

                        #endregion

                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);
                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }
                    #endregion
                }
                //Ԥ�տ 
                else if (bill is AdvanceReceiptBill arb)
                {
                    #region
                    /*
                    Ԥ�տ������ʱ���տ��˻���ĿĬ��ѡ���б��� ����ֽ�Ĭ�ϣ��� ���д�   �����˻���ָ������֧������
                    */
                    if (arb != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = arb.Id,
                            //���ݱ��
                            BillNumber = arb.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.AdvanceReceiptBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };

                        #region �跽

                        //--2020.04.15  pxh
                        //1.�Ż�
                        if (arb.DiscountAmount > 0)
                        {
                            var preferential = _accountingService.Parse(storeId, AccountingCodeEnum.Preferential);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 0,
                                RecordTime = DateTime.Now,
                                Summary = preferential?.Name,
                                AccountingOptionName = preferential?.Name,
                                AccountingOptionId = preferential?.Id ?? 0,
                                DebitAmount = arb.DiscountAmount
                            });
                        }
                        //--pxh

                        //2.�տ��˻�������ֽ����д������˻���ָ������֧������
                        if (arb.Items?.Any() ?? false)
                        {
                            arb.Items.Where(s => s.Copy == false).ToList().ForEach(a =>
                            {
                                if (a.CollectionAmount != 0)
                                {
                                    //�����ϸ
                                    recordingVoucher.Items.Add(new VoucherItem()
                                    {
                                        StoreId = storeId,
                                        Direction = 0,
                                        RecordTime = DateTime.Now,
                                        Summary = a.AccountingOption?.Name,
                                        AccountingOptionName = a.AccountingOption?.Name,
                                        AccountingOptionId = a.AccountingOptionId,
                                        DebitAmount = a.CollectionAmount
                                    });
                                }
                            });
                        }

                        #endregion

                        #region ����

                        //1.Ԥ���˿� ���ÿ�Ŀ���ӿ�Ŀ�� 
                        var advancereceipt = _accountingService.ParseChilds(storeId, AccountingCodeEnum.AdvanceReceipt, arb.AccountingOptionId ?? 0);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 1,
                            RecordTime = DateTime.Now,
                            Summary = advancereceipt?.Name,
                            AccountingOptionName = advancereceipt?.Name,
                            AccountingOptionId = advancereceipt?.Id ?? 0,
                            CreditAmount = arb.AdvanceAmount
                        });

                        #endregion

                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);
                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }

                    #endregion
                }
                //��� 
                else if (bill is PaymentReceiptBill prcb)
                {
                    #region
                    /*
                    ���������ʱ�������˻���ĿĬ��ѡ���б��� ����ֽ�Ĭ�ϣ��� ���д�   �����˻���ָ������֧������  Ԥ���˿
                    */
                    if (prcb != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = prcb.Id,
                            //���ݱ��
                            BillNumber = prcb.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.PaymentReceiptBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };

                        #region �跽

                        //1.Ӧ���˿�
                        var accountspayable = _accountingService.Parse(storeId, AccountingCodeEnum.AccountsPayable);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 0,
                            RecordTime = DateTime.Now,
                            Summary = accountspayable?.Name,
                            AccountingOptionName = accountspayable?.Name,
                            AccountingOptionId = accountspayable?.Id ?? 0,
                            DebitAmount = prcb.AmountOwedAfterReceipt
                        });

                        #endregion

                        #region ����

                        //1.�Ż�
                        if (prcb.DiscountAmount > 0)
                        {
                            var preferential = _accountingService.Parse(storeId, AccountingCodeEnum.Preferential);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 1,
                                RecordTime = DateTime.Now,
                                Summary = preferential?.Name,
                                AccountingOptionName = preferential?.Name,
                                AccountingOptionId = preferential?.Id ?? 0,
                                CreditAmount = prcb.DiscountAmount
                            });
                        }

                        //1.�����˻�������ֽ����д������˻���ָ������֧������Ԥ���˿
                        if (prcb.PaymentReceiptBillAccountings?.Any() ?? false)
                        {
                            prcb.PaymentReceiptBillAccountings.ToList().ForEach(a =>
                            {
                                if (a.CollectionAmount != 0)
                                {
                                    //�����ϸ
                                    recordingVoucher.Items.Add(new VoucherItem()
                                    {
                                        StoreId = storeId,
                                        Direction = 1,
                                        RecordTime = DateTime.Now,
                                        Summary = a.AccountingOption?.Name,
                                        AccountingOptionName = a.AccountingOption?.Name,
                                        AccountingOptionId = a.AccountingOptionId,
                                        CreditAmount = a.CollectionAmount
                                    });
                                }
                            });
                        }

                        #endregion

                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);
                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }

                    #endregion

                }
                //Ԥ��� 
                else if (bill is AdvancePaymentBill apb)
                {
                    #region
                    /*
                    Ԥ�տ������ʱ�������˻���ĿĬ��ѡ���б��� ����ֽ�Ĭ�ϣ��� ���д�   �����˻���ָ������֧������
                    */
                    if (apb != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = apb.Id,
                            //���ݱ��
                            BillNumber = apb.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.AdvancePaymentBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };

                        #region �跽

                        //1.Ԥ���� ���ÿ�Ŀ���ӿ�Ŀ��
                        //��Ԥ����Imprest�޸�ΪԤ���˿�AdvancePayment,��Ȼȡ�����ӿ�Ŀ
                        var imprest = _accountingService.ParseChilds(storeId, AccountingCodeEnum.AdvancePayment, apb.AccountingOptionId ?? 0);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 0,
                            RecordTime = DateTime.Now,
                            Summary = imprest?.Name,
                            AccountingOptionName = imprest?.Name,
                            AccountingOptionId = imprest?.Id ?? 0,
                            DebitAmount = apb.AdvanceAmount
                        });

                        #endregion

                        #region ����

                        //1.�����˻�������ֽ����д������˻���ָ������֧������ 
                        if (apb.Items?.Any() ?? false)
                        {
                            apb.Items.Where(s => s.Copy == false).ToList().ForEach(a =>
                                {
                                    if (a.CollectionAmount != 0)
                                    {
                                        //�����ϸ
                                        recordingVoucher.Items.Add(new VoucherItem()
                                        {
                                            StoreId = storeId,
                                            Direction = 1,
                                            RecordTime = DateTime.Now,
                                            Summary = a.AccountingOption?.Name,
                                            AccountingOptionName = a.AccountingOption?.Name,
                                            AccountingOptionId = a.AccountingOptionId,
                                            CreditAmount = a.CollectionAmount
                                        });
                                    }
                                });
                        }

                        #endregion

                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);
                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }
                    #endregion
                }
                //�������� 
                else if (bill is FinancialIncomeBill fib)
                {
                    #region
                    /*
                    �������뵥������ʱ���տʽ Ĭ��ѡ���б��� ����ֽ�Ĭ�ϣ��� ���д�   �����˻���ָ������֧������
                    */
                    if (fib != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = fib.Id,
                            //���ݱ��
                            BillNumber = fib.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.FinancialIncomeBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };

                        #region �跽

                        if (fib.OweCash > 0)
                        {
                            //1.Ӧ���˿�
                            var accountspayable = _accountingService.Parse(storeId, AccountingCodeEnum.AccountsPayable);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 0,
                                RecordTime = DateTime.Now,
                                Summary = accountspayable?.Name,
                                AccountingOptionName = accountspayable?.Name,
                                AccountingOptionId = accountspayable?.Id ?? 0,
                                DebitAmount = fib.OweCash
                            });
                        }

                        //1.�տʽ ������ֽ����д������˻���ָ������֧������
                        if (fib.FinancialIncomeBillAccountings?.Any() ?? false)
                        {
                            fib.FinancialIncomeBillAccountings.ToList().ForEach(a =>
                            {
                                if (a.CollectionAmount != 0)
                                {
                                    //�����ϸ
                                    recordingVoucher.Items.Add(new VoucherItem()
                                    {
                                        StoreId = storeId,
                                        Direction = 0,
                                        RecordTime = DateTime.Now,
                                        Summary = a.AccountingOption?.Name,
                                        AccountingOptionName = a.AccountingOption?.Name,
                                        AccountingOptionId = a.AccountingOptionId,
                                        DebitAmount = a.CollectionAmount
                                    });
                                }
                            });
                        }

                        #endregion

                        #region ����

                        //1.����ҵ������ ���ÿ�Ŀ���ӿ�Ŀ(��ϸ)��    
                        if (fib.Items?.Any() ?? false)
                        {
                            fib.Items.ToList().ForEach(a =>
                            {
                                var otherincome = _accountingService.GetAccountingOptionById(a.AccountingOptionId);
                                //�����ϸ
                                recordingVoucher.Items.Add(new VoucherItem()
                                {
                                    StoreId = storeId,
                                    Direction = 1,
                                    RecordTime = DateTime.Now,
                                    Summary = otherincome?.Name,
                                    AccountingOptionName = otherincome?.Name,
                                    AccountingOptionId = a.AccountingOptionId,
                                    //DebitAmount = a.Amount
                                    CreditAmount = a.Amount
                                });
                            });
                        }

                        #endregion

                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);
                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }
                    #endregion
                }
                //�ɱ����۵� 
                else if (bill is CostAdjustmentBill cab)
                {
                    #region

                    /*
                     �ɱ����۵�������ʱ�������ˣ�����ʱ

                    ������ʧ������ƾ֤Ϊ��

  	                    �跽���̶�����
                            1.�����Ʒ

                        �������̶�����
 	                        1.�ɱ�������ʧ


                     �������룬����ƾ֤Ϊ��

  	                   �跽���̶�����
                            1.�����Ʒ

                       �������̶�����
       	                    1.�ɱ���������

                     */
                    if (cab != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = cab.Id,
                            //���ݱ��
                            BillNumber = cab.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.CostAdjustmentBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };


                        var adjustmentPriceBefore = cab.Items.Sum(s => s.AdjustmentPriceBefore ?? 0);
                        var adjustedPrice = cab.Items.Sum(s => s.AdjustedPrice ?? 0);
                        var check = adjustedPrice - adjustmentPriceBefore;

                        #region �跽

                        //1.�����Ʒ
                        var inventorygoods = _accountingService.Parse(storeId, AccountingCodeEnum.InventoryGoods);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 0,
                            RecordTime = DateTime.Now,
                            Summary = inventorygoods?.Name,
                            AccountingOptionName = inventorygoods?.Name,
                            AccountingOptionId = inventorygoods?.Id ?? 0,
                            CreditAmount = check
                        });

                        #endregion

                        #region ����

                        //����
                        if (check > 0)
                        {
                            //1.�ɱ���������
                            var costincome = _accountingService.Parse(storeId, AccountingCodeEnum.CostIncome);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 1,
                                RecordTime = DateTime.Now,
                                Summary = costincome?.Name,
                                AccountingOptionName = costincome?.Name,
                                AccountingOptionId = costincome?.Id ?? 0,
                                CreditAmount = check
                            });

                        }
                        //��ʧ
                        else
                        {
                            //1.�ɱ�������ʧ
                            var costloss = _accountingService.Parse(storeId, AccountingCodeEnum.CostLoss);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 1,
                                RecordTime = DateTime.Now,
                                Summary = costloss?.Name,
                                AccountingOptionName = costloss?.Name,
                                AccountingOptionId = costloss?.Id ?? 0,
                                DebitAmount = check
                            });
                        }

                        #endregion

                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);
                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }
                    #endregion
                }
                //���� 
                else if (bill is ScrapProductBill spb)
                {
                    #region
                    /*
                    ���𵥣�����ʱ

                    �����������Ϊ��Ӫҵ��

                                ����ƾ֤Ϊ��

                    �跽���̶�����
                            1.�Զ�����ÿ�Ŀ����������ÿ�Ŀ�£�

                                �������̶�����
                    1.�����Ʒ

                    �����������Ϊ��Ӫҵ��

                                ����ƾ֤Ϊ��

                    �跽���̶�����
                            1.Ӫҵ��֧��

                                �������̶�����
                    1.�����Ʒ    
                    */
                    if (spb != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = spb.Id,
                            //���ݱ��
                            BillNumber = spb.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.ScrapProductBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };

                        var costAmount = spb.Items.Sum(s => s.CostAmount ?? 0);

                        #region �跽
                        //Ӫҵ��
                        if (spb.Reason == 0)
                        {
                            // 1.�Զ�����ÿ�Ŀ����������ÿ�Ŀ�£�
                            var managefees = _accountingService.Parse(storeId, AccountingCodeEnum.ManageFees);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 0,
                                RecordTime = DateTime.Now,
                                Summary = managefees?.Name,
                                AccountingOptionName = managefees?.Name,
                                AccountingOptionId = managefees?.Id ?? 0,
                                DebitAmount = costAmount
                            });
                        }
                        //Ӫҵ��
                        else if (spb.Reason == 1)
                        {
                            //1.Ӫҵ��֧��
                            var nonOperatingExpenses = _accountingService.Parse(storeId, AccountingCodeEnum.NonOperatingExpenses);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 0,
                                RecordTime = DateTime.Now,
                                Summary = nonOperatingExpenses?.Name,
                                AccountingOptionName = nonOperatingExpenses?.Name,
                                AccountingOptionId = nonOperatingExpenses?.Id ?? 0,
                                DebitAmount = costAmount
                            });
                        }

                        #endregion

                        #region ����

                        //1.�����Ʒ
                        var inventoryGoods = _accountingService.Parse(storeId, AccountingCodeEnum.InventoryGoods);
                        recordingVoucher.Items.Add(new VoucherItem()
                        {
                            StoreId = storeId,
                            Direction = 1,
                            RecordTime = DateTime.Now,
                            Summary = inventoryGoods?.Name,
                            AccountingOptionName = inventoryGoods?.Name,
                            AccountingOptionId = inventoryGoods?.Id ?? 0,
                            CreditAmount = costAmount
                        });


                        #endregion

                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);
                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }
                    #endregion
                }
                //����֧�� 
                else if (bill is CostExpenditureBill ceb)
                {
                    #region
                    /*
                    ����֧����������ʱ�����ʽ Ĭ��ѡ���б��� ����ֽ�Ĭ�ϣ��� ���д�   �����˻���ָ������֧������
                   
                    */
                    if (ceb != null)
                    {
                        //ƾ֤
                        var recordingVoucher = new RecordingVoucher()
                        {
                            BillId = ceb.Id,
                            //���ݱ��
                            BillNumber = ceb.BillNumber,
                            //��������
                            BillTypeId = (int)BillTypeEnum.CostExpenditureBill,
                            //ϵͳ����
                            GenerateMode = (int)GenerateMode.Auto,
                            //���ʱ��
                            AuditedDate = DateTime.Now,
                            RecordTime = DateTime.Now,
                            //�Զ����
                            AuditedStatus = true,
                            //�����
                            AuditedUserId = makeUserId
                        };

                        #region �跽

                        //1.���۷��ã�������ã��������(�κ��ӿ�Ŀ)
                        if (ceb.Items?.Any() ?? false)
                        {
                            ceb.Items.ToList().ForEach(a =>
                            {
                                var cost = _accountingService.GetAccountingOptionById(a.AccountingOptionId);
                                //��ӷ���
                                recordingVoucher.Items.Add(new VoucherItem()
                                {
                                    StoreId = storeId,
                                    Direction = 0,
                                    RecordTime = DateTime.Now,
                                    Summary = cost?.Name,
                                    AccountingOptionName = cost?.Name,
                                    AccountingOptionId = cost?.Id ?? 0,
                                    DebitAmount = a.Amount
                                });
                            });
                        }

                        #endregion

                        #region ����

                        if (ceb.OweCash > 0)
                        {
                            //1.Ӧ���˿�
                            var accountspayable = _accountingService.Parse(storeId, AccountingCodeEnum.AccountsPayable);
                            recordingVoucher.Items.Add(new VoucherItem()
                            {
                                StoreId = storeId,
                                Direction = 1,
                                RecordTime = DateTime.Now,
                                Summary = accountspayable?.Name,
                                AccountingOptionName = accountspayable?.Name,
                                AccountingOptionId = accountspayable?.Id ?? 0,
                                CreditAmount = ceb.OweCash
                            });
                        }

                        //1.���ʽ������ֽ�Ĭ�ϣ��� ���д�   �����˻���ָ������֧������
                        if (ceb.CostExpenditureBillAccountings?.Any() ?? false)
                        {
                            ceb.CostExpenditureBillAccountings.ToList().ForEach(a =>
                            {
                                if (a.CollectionAmount != 0)
                                {
                                    //�����ϸ
                                    recordingVoucher.Items.Add(new VoucherItem()
                                    {
                                        StoreId = storeId,
                                        Direction = 1,
                                        RecordTime = DateTime.Now,
                                        Summary = a.AccountingOption?.Name,
                                        AccountingOptionName = a.AccountingOption?.Name,
                                        AccountingOptionId = a.AccountingOptionId,
                                        CreditAmount = a.CollectionAmount
                                    });
                                }
                            });
                        }
                        #endregion

                        //����
                        CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);
                        try
                        {
                            //����
                            update?.Invoke(recordingVoucher.Id);
                        }
                        catch (Exception)
                        {
                            //����ʱ�ع�ƾ֤
                            RollBackRecordingVoucher(recordingVoucher);
                            return failed?.Invoke();
                        }
                        //����ƾ֤
                        var result = recordingVoucher.Id > 0;

                        if (!result)
                        {
                            return failed?.Invoke();
                        }
                        else
                        {
                            return successful?.Invoke();
                        }
                    }
                    #endregion
                }

                return failed?.Invoke();

            }
            catch (Exception)
            {
                return failed?.Invoke();
            }
        }

        /// <summary>
        /// ɾ��ƾ֤�ͼ�¼��ϸ
        /// </summary>
        /// <param name="voucherId"></param>
        /// <returns></returns>
        public bool DeleteVoucher(int voucherId)
        {
            //var voucher = RecordingVouchersRepository.ToCachedGetById(voucherId);
            //if (voucher != null)
            //{
            //    DeleteVoucherItemWithVoucher(voucher);
            //    DeleteRecordingVoucher(voucher);
            //    return true;
            //}
            //else
            //{
            //    return false;
            //}
            var voucher = RecordingVouchersRepository.ToCachedGetById(voucherId);
            if (voucher != null)
            {
                try
                {
                    DeleteVoucherItemWithVoucher(voucher);
                    DeleteRecordingVoucher(voucher);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
            {
                //���ƾ֤�����ڣ�����Ҫɾ��
                return true;
            }


        }

        /// <summary>
        /// ҵ�񵥾�ȡ������
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bill"></param>
        /// <param name="voucherId"></param>
        /// <param name="update"></param>
        /// <returns></returns>
        public bool CancleVoucher<T, T1>(T bill, Action update) where T : BaseBill<T1> where T1 : BaseEntity
        {
            try
            {

                var result = false;

                //���۵�
                if (bill is SaleBill sb)
                {
                    if (sb != null)
                    {
                        result = DeleteVoucher(sb.VoucherId);
                    }
                }
                //�˻���
                else if (bill is ReturnBill rb)
                {
                    if (rb != null)
                    {
                        result = DeleteVoucher(rb.VoucherId);
                    }
                }
                //�ɹ��� 
                else if (bill is PurchaseBill pb)
                {
                    if (pb != null)
                    {
                        result = DeleteVoucher(pb.VoucherId);
                    }
                }
                //�ɹ��˻��� 
                else if (bill is PurchaseReturnBill prb)
                {
                    if (prb != null)
                    {
                        result = DeleteVoucher(prb.VoucherId);
                    }
                }
                //�տ 
                else if (bill is CashReceiptBill crb)
                {
                    if (crb != null)
                    {
                        result = DeleteVoucher(crb.VoucherId);
                    }
                }
                //Ԥ�տ 
                else if (bill is AdvanceReceiptBill arb)
                {
                    if (arb != null)
                    {
                        result = DeleteVoucher(arb.VoucherId);
                    }
                }
                //��� 
                else if (bill is PaymentReceiptBill prcb)
                {
                    if (prcb != null)
                    {
                        result = DeleteVoucher(prcb.VoucherId);
                    }
                }
                //Ԥ��� 
                else if (bill is AdvancePaymentBill apb)
                {
                    if (apb != null)
                    {
                        result = DeleteVoucher(apb.VoucherId);
                    }
                }
                //�������� 
                else if (bill is FinancialIncomeBill fib)
                {
                    if (fib != null)
                    {
                        result = DeleteVoucher(fib.VoucherId);
                    }
                }
                //�ɱ����۵� 
                else if (bill is CostAdjustmentBill cab)
                {
                    if (cab != null)
                    {
                        result = DeleteVoucher(cab.VoucherId);
                    }
                }
                //���� 
                else if (bill is ScrapProductBill spb)
                {
                    if (spb != null)
                    {
                        result = DeleteVoucher(spb.VoucherId);
                    }
                }
                //����֧�� 
                else if (bill is CostExpenditureBill ceb)
                {
                    if (ceb != null)
                    {
                        result = DeleteVoucher(ceb.VoucherId);
                    }
                }
                //�̵�ӯ����
                else if (bill is InventoryProfitLossBill ipb)
                {
                    if (ipb != null)
                    {
                        result = DeleteVoucher(ipb.VoucherId);
                    }
                }

                //ƾ֤ɾ���ɹ�ִ�лص�����
                if (result)
                {
                    update?.Invoke();
                }

                return result;

            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// �ɱ���ת����
        /// </summary>
        /// <param name="type"></param>
        /// <param name="storeId"></param>
        /// <param name="makeUserId"></param>
        /// <param name="debit"></param>
        /// <param name="credit"></param>
        /// <param name="debitAmount"></param>
        /// <param name="creditAmount"></param>
        /// <param name="period"></param>
        /// <param name="close"></param>
        /// <returns></returns>
        public bool CreateCostVoucher(CostOfSettleEnum type, int storeId, int makeUserId, AccountingCodeEnum debit, AccountingCodeEnum credit, decimal? debitAmount, decimal? creditAmount, ClosingAccounts period, bool reserve = false)
        {
            try
            {
                var date = period.ClosingAccountDate;
                //ƾ֤
                var recordingVoucher = new RecordingVoucher()
                {
                    BillId = 0,
                    //��������
                    BillTypeId = 0,
                    //ϵͳ����
                    GenerateMode = (int)GenerateMode.Auto,
                    RecordTime = date,
                    //���ʱ��
                    AuditedDate = DateTime.Now,
                    //�Զ����
                    AuditedStatus = true
                };

                switch (type)
                {
                    case CostOfSettleEnum.CostOfPriceAdjust:
                        {
                            recordingVoucher.BillTypeId = (int)CostOfSettleEnum.CostOfPriceAdjust;
                            recordingVoucher.BillNumber = $"CostOfPriceAdjust{date.ToString("yyyyMM")}";
                            if (reserve)
                            {
                                recordingVoucher.BillNumber = $"CostOfPriceAdjustReserve{date.ToString("yyyyMM")}";
                            }
                        }
                        break;
                    case CostOfSettleEnum.CostOfPurchaseReject:
                        {
                            recordingVoucher.BillTypeId = (int)CostOfSettleEnum.CostOfPurchaseReject;
                            recordingVoucher.BillNumber = $"CostOfPurchaseReject{date.ToString("yyyyMM")}";
                            if (reserve)
                            {
                                recordingVoucher.BillNumber = $"CostOfPurchaseRejectReserve{date.ToString("yyyyMM")}";
                            }
                        }
                        break;
                    case CostOfSettleEnum.CostOfJointGoods:
                        {
                            recordingVoucher.BillTypeId = (int)CostOfSettleEnum.CostOfJointGoods;
                            recordingVoucher.BillNumber = $"CostOfJointGoods{date.ToString("yyyyMM")}";
                            if (reserve)
                            {
                                recordingVoucher.BillNumber = $"CostOfJointGoodsReserve{date.ToString("yyyyMM")}";
                            }
                        }
                        break;
                    case CostOfSettleEnum.CostOfStockAdjust:
                        {
                            recordingVoucher.BillTypeId = (int)CostOfSettleEnum.CostOfStockAdjust;
                            recordingVoucher.BillNumber = $"CostOfStockAdjust{date.ToString("yyyyMM")}";
                            if (reserve)
                            {
                                recordingVoucher.BillNumber = $"CostOfStockAdjustReserve{date.ToString("yyyyMM")}";
                            }
                        }
                        break;
                    case CostOfSettleEnum.CostOfStockLoss:
                        {
                            recordingVoucher.BillTypeId = (int)CostOfSettleEnum.CostOfStockLoss;
                            recordingVoucher.BillNumber = $"CostOfStockLoss{date.ToString("yyyyMM")}";
                            if (reserve)
                            {
                                recordingVoucher.BillNumber = $"CostOfStockLossReserve{date.ToString("yyyyMM")}";
                            }
                        }
                        break;
                    //������
                    case CostOfSettleEnum.CostOfSales:
                        {
                            recordingVoucher.BillTypeId = (int)CostOfSettleEnum.CostOfSales;
                            recordingVoucher.BillNumber = $"CostOfSales{date.ToString("yyyyMM")}";
                            if (reserve)
                            {
                                recordingVoucher.BillNumber = $"CostOfSalesReserve{date.ToString("yyyyMM")}";
                            }
                        }
                        break;
                }


                #region �跽

                var debitAcc = _accountingService.Parse(storeId, debit);
                recordingVoucher.Items.Add(new VoucherItem()
                {
                    StoreId = storeId,
                    RecordTime = date,
                    Direction = 0,
                    Summary = reserve == false ? $"{date.ToString("yyyyMM")}�½�" : $"{date.ToString("yyyyMM")}�����½�",
                    AccountingOptionName = $"{debitAcc?.Name}:{debitAcc?.Code}",
                    AccountingOptionId = debitAcc?.Id ?? 0,
                    DebitAmount = debitAmount ?? 0
                });

                #endregion

                #region ����


                var creditAcc = _accountingService.Parse(storeId, credit);
                recordingVoucher.Items.Add(new VoucherItem()
                {
                    StoreId = storeId,
                    RecordTime = date,
                    Direction = 1,
                    Summary = reserve == false ? $"{date.ToString("yyyyMM")}�½�" : $"{date.ToString("yyyyMM")}�����½�",
                    AccountingOptionName = $"{creditAcc?.Name}:{creditAcc?.Code}",
                    AccountingOptionId = creditAcc?.Id ?? 0,
                    CreditAmount = creditAmount ?? 0
                });

                #endregion

                //����
                CreateRecordingVoucher(storeId, makeUserId, recordingVoucher);

                //����ƾ֤
                return recordingVoucher.Id > 0;

            }
            catch (Exception)
            {
                return false;
            }

        }

        #endregion


    }
}
