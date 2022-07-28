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
    /// ��ʾ�������뵥�ݷ���
    /// </summary>
    public partial class FinancialIncomeBillService : BaseService, IFinancialIncomeBillService
    {

        #region ����

        private readonly IUserService _userService;
        private readonly IQueuedMessageService _queuedMessageService;
        private readonly IRecordingVoucherService _recordingVoucherService;

        public FinancialIncomeBillService(IServiceGetter getter,
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

        #endregion

        #region ����

        public bool Exists(int billId)
        {
            return FinancialIncomeBillsRepository.TableNoTracking.Where(a => a.Id == billId).Count() > 0;
        }

        public virtual IPagedList<FinancialIncomeBill> GetAllFinancialIncomeBills(int? store, int? makeuserId, int? salesmanId, int? customerId, int? manufacturerId, string billNumber = "", bool? status = null, DateTime? start = null, DateTime? end = null, bool? isShowReverse = null, bool? sortByAuditedTime = null, string remark = "", int pageIndex = 0, int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;

            DateTime.TryParse(start?.ToString("yyyy-MM-dd 00:00:00"), out DateTime startDate);
            DateTime.TryParse(end?.ToString("yyyy-MM-dd 23:59:59"), out DateTime endDate);

            var query = from pc in FinancialIncomeBillsRepository.Table
                         .Include(cr => cr.Items)
                         //.ThenInclude(cr => cr.FinancialIncomeBill)
                         .Include(cr => cr.FinancialIncomeBillAccountings)
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

            if (salesmanId.HasValue && salesmanId.Value > 0)
            {
                query = query.Where(c => c.SalesmanId == salesmanId);
            }

            if (customerId.HasValue && customerId.Value > 0)
            {
                query = query.Where(c => c.Items.Count(s => s.CustomerOrManufacturerId == customerId) > 0);
            }

            if (manufacturerId.HasValue && manufacturerId.Value > 0)
            {
                query = query.Where(c => c.Items.Count(s => s.CustomerOrManufacturerId == manufacturerId) > 0);
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

            if (!string.IsNullOrEmpty(remark))
            {
                query = query.Where(c => c.Remark.Contains(remark));
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

            //��ҳ��
            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<FinancialIncomeBill>(plists, pageIndex, pageSize, totalCount);
        }

        public virtual IList<FinancialIncomeBill> GetAllFinancialIncomeBills()
        {
            var query = from c in FinancialIncomeBillsRepository.Table
                        orderby c.Id
                        select c;

            var categories = query.ToList();
            return categories;
        }

        public virtual FinancialIncomeBill GetFinancialIncomeBillById(int? store, int financialIncomeBillId)
        {
            if (financialIncomeBillId == 0)
            {
                return null;
            }

            var key = DCMSDefaults.FINANCIALINCOMEBILL_BY_ID_KEY.FillCacheKey(store ?? 0, financialIncomeBillId);
            return _cacheManager.Get(key, () =>
            {
                return FinancialIncomeBillsRepository.ToCachedGetById(financialIncomeBillId);
            });
        }


        public virtual FinancialIncomeBill GetFinancialIncomeBillByNumber(int? store, string billNumber)
        {
            var query = FinancialIncomeBillsRepository.Table;
            var bill = query.Where(a => a.StoreId == store && a.BillNumber == billNumber).FirstOrDefault();
            return bill;
        }


        public virtual FinancialIncomeBill GetFinancialIncomeBillById(int? store, int financialIncomeBillId, bool isInclude = false)
        {
            if (financialIncomeBillId == 0)
            {
                return null;
            }

            if (isInclude)
            {
                var query = FinancialIncomeBillsRepository.Table
                .Include(f => f.Items)
                //.ThenInclude(f => f.FinancialIncomeBill)
                .Include(pb => pb.FinancialIncomeBillAccountings)
                .ThenInclude(ao => ao.AccountingOption);

                return query.FirstOrDefault(f => f.Id == financialIncomeBillId);
            }
            return FinancialIncomeBillsRepository.ToCachedGetById(financialIncomeBillId);
        }

        public virtual void InsertFinancialIncomeBill(FinancialIncomeBill financialIncomeBill)
        {
            if (financialIncomeBill == null)
            {
                throw new ArgumentNullException("financialIncomeBill");
            }

            var uow = FinancialIncomeBillsRepository.UnitOfWork;
            FinancialIncomeBillsRepository.Insert(financialIncomeBill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(financialIncomeBill);
        }

        public virtual void UpdateFinancialIncomeBill(FinancialIncomeBill financialIncomeBill)
        {
            if (financialIncomeBill == null)
            {
                throw new ArgumentNullException("financialIncomeBill");
            }

            var uow = FinancialIncomeBillsRepository.UnitOfWork;
            FinancialIncomeBillsRepository.Update(financialIncomeBill);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(financialIncomeBill);
        }



        public void UpdatePaymented(int? store, int billId, PayStatus payStatus)
        {
            var bill = GetFinancialIncomeBillById(store, billId, false);
            if (bill != null)
            {
                var uow = FinancialIncomeBillsRepository.UnitOfWork;
                bill.PayStatus = (int)payStatus;
                FinancialIncomeBillsRepository.Update(bill);
                uow.SaveChanges();
            }
        }

        public void UpdateReceived(int? store, int billId, ReceiptStatus receiptStatus)
        {
            var bill = GetFinancialIncomeBillById(store, billId, false);
            if (bill != null)
            {
                var uow = FinancialIncomeBillsRepository.UnitOfWork;
                bill.ReceiptStatus = (int)receiptStatus;
                FinancialIncomeBillsRepository.Update(bill);
                uow.SaveChanges();
            }
        }



        public virtual void DeleteFinancialIncomeBill(FinancialIncomeBill financialIncomeBill)
        {
            if (financialIncomeBill == null)
            {
                throw new ArgumentNullException("financialIncomeBill");
            }

            var uow = FinancialIncomeBillsRepository.UnitOfWork;
            FinancialIncomeBillsRepository.Delete(financialIncomeBill);
            uow.SaveChanges();

            //event notification
            _eventPublisher.EntityDeleted(financialIncomeBill);
        }
        #endregion

        #region ������Ŀ

        public virtual IPagedList<FinancialIncomeItem> GetFinancialIncomeItemsByFinancialIncomeBillId(int financialIncomeBillId, int? userId, int? storeId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (financialIncomeBillId == 0)
            {
                return new PagedList<FinancialIncomeItem>(new List<FinancialIncomeItem>(), pageIndex, pageSize);
            }

            var key = DCMSDefaults.FINANCIALINCOMEBILLITEM_ALL_KEY.FillCacheKey(storeId, financialIncomeBillId, pageIndex, pageSize, userId);

            return _cacheManager.Get(key, () =>
            {
                var query = from pc in FinancialIncomeItemsRepository.Table
                            .Include(f => f.FinancialIncomeBill)
                            where pc.FinancialIncomeBillId == financialIncomeBillId
                            orderby pc.Id
                            select pc;
                //var productFinancialIncomeBills = new PagedList<FinancialIncomeItem>(query.ToList(), pageIndex, pageSize);
                //return productFinancialIncomeBills;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<FinancialIncomeItem>(plists, pageIndex, pageSize, totalCount);
            });
        }

        public virtual List<FinancialIncomeItem> GetFinancialIncomeItemList(int financialIncomeBillId)
        {
            List<FinancialIncomeItem> financialIncomeItems = null;
            var query = FinancialIncomeItemsRepository_RO.Table.Include(s => s.FinancialIncomeBill);
            financialIncomeItems = query.Where(a => a.FinancialIncomeBillId == financialIncomeBillId).ToList();
            return financialIncomeItems;
        }

        public virtual FinancialIncomeItem GetFinancialIncomeItemById(int? store, int financialIncomeItemId)
        {
            if (financialIncomeItemId == 0)
            {
                return null;
            }

            var key = DCMSDefaults.FINANCIALINCOMEBILLITEM_BY_ID_KEY.FillCacheKey(store ?? 0, financialIncomeItemId);
            return _cacheManager.Get(key, () => { return FinancialIncomeItemsRepository.ToCachedGetById(financialIncomeItemId); });
        }

        public virtual void InsertFinancialIncomeItem(FinancialIncomeItem financialIncomeItem)
        {
            if (financialIncomeItem == null)
            {
                throw new ArgumentNullException("financialIncomeItem");
            }

            var uow = FinancialIncomeItemsRepository.UnitOfWork;
            FinancialIncomeItemsRepository.Insert(financialIncomeItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(financialIncomeItem);
        }

        public virtual void UpdateFinancialIncomeItem(FinancialIncomeItem financialIncomeItem)
        {
            if (financialIncomeItem == null)
            {
                throw new ArgumentNullException("financialIncomeItem");
            }

            var uow = FinancialIncomeItemsRepository.UnitOfWork;
            FinancialIncomeItemsRepository.Update(financialIncomeItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(financialIncomeItem);
        }

        public virtual void DeleteFinancialIncomeItem(FinancialIncomeItem financialIncomeItem)
        {
            if (financialIncomeItem == null)
            {
                throw new ArgumentNullException("financialIncomeItem");
            }

            var uow = FinancialIncomeItemsRepository.UnitOfWork;
            FinancialIncomeItemsRepository.Delete(financialIncomeItem);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(financialIncomeItem);
        }


        #endregion

        #region �տ��˻�ӳ��

        public virtual IPagedList<FinancialIncomeBillAccounting> GetFinancialIncomeBillAccountingsByFinancialIncomeBillId(int storeId, int userId, int financialIncomeBillId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (financialIncomeBillId == 0)
            {
                return new PagedList<FinancialIncomeBillAccounting>(new List<FinancialIncomeBillAccounting>(), pageIndex, pageSize);
            }

            //string key = string.Format(FINANCIALINCOMEBILL_ACCOUNTINGL_BY_BILLID_KEY.FillCacheKey( financialIncomeBillId, pageIndex, pageSize, _workContext.CurrentUser.Id, _workContext.CurrentStore.Id);
            var key = DCMSDefaults.FINANCIALINCOMEBILL_ACCOUNTING_ALLBY_BILLID_KEY.FillCacheKey(storeId, financialIncomeBillId, pageIndex, pageSize, userId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in FinancialIncomeBillAccountingMappingRepository.Table
                            join p in AccountingOptionsRepository.Table on pc.AccountingOptionId equals p.Id
                            where pc.BillId == financialIncomeBillId
                            orderby pc.Id
                            select pc;


                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<FinancialIncomeBillAccounting>(plists, pageIndex, pageSize, totalCount);
            });
        }

        public virtual IList<FinancialIncomeBillAccounting> GetFinancialIncomeBillAccountingsByFinancialIncomeBillId(int? store, int financialIncomeBillId)
        {

            var key = DCMSDefaults.FINANCIALINCOMEBILL_ACCOUNTINGL_BY_BILLID_KEY.FillCacheKey(store ?? 0, financialIncomeBillId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in FinancialIncomeBillAccountingMappingRepository.Table
                            join p in AccountingOptionsRepository.Table on pc.AccountingOptionId equals p.Id
                            where pc.BillId == financialIncomeBillId
                            orderby pc.Id
                            select pc;

                return query.ToList();
            });
        }

        public virtual FinancialIncomeBillAccounting GetFinancialIncomeBillAccountingById(int financialIncomeBillAccountingId)
        {
            if (financialIncomeBillAccountingId == 0)
            {
                return null;
            }

            return FinancialIncomeBillAccountingMappingRepository.ToCachedGetById(financialIncomeBillAccountingId);
        }

        public virtual void InsertFinancialIncomeBillAccounting(FinancialIncomeBillAccounting financialIncomeBillAccounting)
        {
            if (financialIncomeBillAccounting == null)
            {
                throw new ArgumentNullException("financialIncomeBillAccounting");
            }

            var uow = FinancialIncomeBillAccountingMappingRepository.UnitOfWork;
            FinancialIncomeBillAccountingMappingRepository.Insert(financialIncomeBillAccounting);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(financialIncomeBillAccounting);
        }

        public virtual void UpdateFinancialIncomeBillAccounting(FinancialIncomeBillAccounting financialIncomeBillAccounting)
        {
            if (financialIncomeBillAccounting == null)
            {
                throw new ArgumentNullException("financialIncomeBillAccounting");
            }

            var uow = FinancialIncomeBillAccountingMappingRepository.UnitOfWork;
            FinancialIncomeBillAccountingMappingRepository.Update(financialIncomeBillAccounting);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(financialIncomeBillAccounting);
        }

        public virtual void DeleteFinancialIncomeBillAccounting(FinancialIncomeBillAccounting financialIncomeBillAccounting)
        {
            if (financialIncomeBillAccounting == null)
            {
                throw new ArgumentNullException("financialIncomeBillAccounting");
            }

            var uow = FinancialIncomeBillAccountingMappingRepository.UnitOfWork;
            FinancialIncomeBillAccountingMappingRepository.Delete(financialIncomeBillAccounting);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(financialIncomeBillAccounting);
        }


        #endregion

        public BaseResult BillCreateOrUpdate(int storeId, int userId, int? billId, FinancialIncomeBill bill, List<FinancialIncomeBillAccounting> accountingOptions, List<AccountingOption> accountings, FinancialIncomeBillUpdate data, List<FinancialIncomeItem> items, bool isAdmin = false,bool doAudit = true)
        {
            var uow = FinancialIncomeBillsRepository.UnitOfWork;
            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();



                if (billId.HasValue && billId.Value != 0)
                {
                    #region ���²������뵥
                    if (bill != null)
                    {
                        bill.SalesmanId = data.SalesmanId;
                        bill.Remark = data.Remark;
                        bill.TerminalId = data.TerminalId;
                        bill.ManufacturerId = data.ManufacturerId;
                        bill.SumAmount = data.Items.Sum(s => s.Amount ?? 0);
                        bill.OweCash = data.OweCash;

                        UpdateFinancialIncomeBill(bill);
                    }

                    #endregion
                }
                else
                {
                    #region ��Ӳ������뵥

                    bill.StoreId = storeId;
                    bill.SalesmanId = data.SalesmanId;
                    bill.CreatedOnUtc = DateTime.Now;
                    bill.TerminalId = data.TerminalId;
                    bill.ManufacturerId = data.ManufacturerId;
                    bill.SumAmount = data.Items.Sum(s => s.Amount ?? 0);
                    bill.OweCash = data.OweCash;

                    //���ݱ��
                    bill.BillNumber = string.IsNullOrEmpty(data.BillNumber) ? CommonHelper.GetBillNumber("CWSR", storeId): data.BillNumber;

                    var sb = GetFinancialIncomeBillByNumber(storeId, bill.BillNumber);
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
                    bill.Operation = 0;
                    if (data.TerminalId > 0)
                    {
                        if (accountingOptions.Sum(s => s.CollectionAmount ) > 0 && data.OweCash > 0)
                        {
                            bill.ReceivedStatus = ReceiptStatus.Part;
                        }
                        if (accountingOptions.Sum(s => s.CollectionAmount) > 0 && data.OweCash == 0)
                        {
                            bill.ReceivedStatus = ReceiptStatus.Received;
                        }
                        if (accountingOptions.Sum(s => s.CollectionAmount) == 0)
                        {
                            bill.ReceivedStatus = ReceiptStatus.None;
                        }
                    }
                    if (data.ManufacturerId > 0)
                    {
                        if (accountingOptions.Sum(s => s.CollectionAmount) > 0 && data.OweCash > 0)
                        {
                            bill.PaymentStatus = PayStatus.Part;
                        }
                        if (accountingOptions.Sum(s => s.CollectionAmount) > 0 && data.OweCash == 0)
                        {
                            bill.PaymentStatus = PayStatus.Paid;
                        }
                        if (accountingOptions.Sum(s => s.CollectionAmount) == 0)
                        {
                            bill.PaymentStatus = PayStatus.None;
                        }
                    }

                    //��ʶ����Դ

                    InsertFinancialIncomeBill(bill);

                    #endregion
                }

                #region �����տ���Ŀ

                data.Items.ForEach(p =>
                {
                    if (p.AccountingOptionId != 0)
                    {
                        var sd = GetFinancialIncomeItemById(storeId, p.Id);
                        if (sd == null)
                        {
                            //׷����
                            if (bill.Items.Count(cp => cp.Id == p.Id) == 0)
                            {
                                var item = p;
                                item.FinancialIncomeBillId = bill.Id;
                                item.CreatedOnUtc = DateTime.Now;
                                item.StoreId = storeId;

                                if (bill.ManufacturerId > 0)
                                {
                                    item.CustomerOrManufacturerId = bill.ManufacturerId;
                                    item.CustomerOrManufacturerType = 2;
                                }
                                else if (bill.TerminalId > 0)
                                {
                                    item.CustomerOrManufacturerId = bill.TerminalId;
                                    item.CustomerOrManufacturerType = 1;
                                }

                                InsertFinancialIncomeItem(item);

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
                            sd.AccountingOptionId = p.AccountingOptionId;//�������
                            sd.Amount = p.Amount;//���
                            sd.FinancialIncomeBillId = sd.FinancialIncomeBillId;
                            sd.Remark = p.Remark;//��ע

                            if (bill.ManufacturerId > 0)
                            {
                                sd.CustomerOrManufacturerId = bill.ManufacturerId;
                                sd.CustomerOrManufacturerType = 2;
                            }
                            else if (bill.TerminalId > 0)
                            {
                                sd.CustomerOrManufacturerId = bill.TerminalId;
                                sd.CustomerOrManufacturerType = 1;
                            }

                            UpdateFinancialIncomeItem(sd);
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
                        var item = GetFinancialIncomeItemById(storeId, p.Id);
                        if (item != null)
                        {
                            DeleteFinancialIncomeItem(item);
                        }
                    }
                });

                #endregion

                #region �տ��˻�ӳ��

                var financialIncomeBillAccountings = GetFinancialIncomeBillAccountingsByFinancialIncomeBillId(storeId, bill.Id);
                accountings.ToList().ForEach(c =>
                {
                    if (data.Accounting.Select(a => a.AccountingOptionId).Contains(c.Id))
                    {
                        if (!financialIncomeBillAccountings.Select(cc => cc.AccountingOptionId).Contains(c.Id))
                        {
                            var collection = data.Accounting.Select(a => a).Where(a => a.AccountingOptionId == c.Id).FirstOrDefault();
                            var financialIncomeBillAccounting = new FinancialIncomeBillAccounting()
                            {
                                //AccountingOption = c,
                                AccountingOptionId = c.Id,
                                CollectionAmount = collection != null ? collection.CollectionAmount : 0,
                                FinancialIncomeBill = bill,
                                BillId = bill.Id,
                                StoreId = storeId
                            };
                            //����˻�
                            InsertFinancialIncomeBillAccounting(financialIncomeBillAccounting);
                        }
                        else
                        {
                            financialIncomeBillAccountings.ToList().ForEach(acc =>
                            {
                                var collection = data.Accounting.Select(a => a).Where(a => a.AccountingOptionId == acc.AccountingOptionId).FirstOrDefault();
                                acc.CollectionAmount = collection != null ? collection.CollectionAmount : 0;
                                //�����˻�
                                UpdateFinancialIncomeBillAccounting(acc);
                            });
                        }
                    }
                    else
                    {
                        if (financialIncomeBillAccountings.Select(cc => cc.AccountingOptionId).Contains(c.Id))
                        {
                            var saleaccountings = financialIncomeBillAccountings.Select(cc => cc).Where(cc => cc.AccountingOptionId == c.Id).ToList();
                            saleaccountings.ForEach(sa =>
                            {
                                DeleteFinancialIncomeBillAccounting(sa);
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
                            BillType = BillTypeEnum.FinancialIncomeBill,
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

        public BaseResult Auditing(int storeId, int userId, FinancialIncomeBill bill)
        {
            var uow = FinancialIncomeBillsRepository.UnitOfWork;

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

        public BaseResult AuditingNoTran(int storeId, int userId, FinancialIncomeBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "������˳ɹ�" };
            var failed = new BaseResult { Success = false, Message = "�������ʧ��" };

            try
            {
                return _recordingVoucherService.CreateVoucher<FinancialIncomeBill, FinancialIncomeItem>(bill, storeId, userId, (voucherId) =>
                {
                    #region �޸ĵ��ݱ�״̬
                    bill.VoucherId = voucherId;
                    bill.AuditedUserId = userId;
                    bill.AuditedDate = DateTime.Now;
                    bill.AuditedStatus = true;
                    UpdateFinancialIncomeBill(bill);

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
                            BillType = BillTypeEnum.FinancialIncomeBill,
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

        public BaseResult Reverse(int userId, FinancialIncomeBill bill)
        {
            var successful = new BaseResult { Success = true, Message = "���ݺ��ɹ�" };
            var failed = new BaseResult { Success = false, Message = "���ݺ��ʧ��" };

            var uow = FinancialIncomeBillsRepository.UnitOfWork;
            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();

                #region ������ƾ֤

                _recordingVoucherService.CancleVoucher<FinancialIncomeBill, FinancialIncomeItem>(bill, () =>
                {
                    #region �޸ĵ��ݱ�״̬
                    bill.ReversedUserId = userId;
                    bill.ReversedDate = DateTime.Now;
                    bill.ReversedStatus = true;

                    //UpdateFinancialIncomeBill(bill);
                    #endregion

                    bill.VoucherId = 0;
                    UpdateFinancialIncomeBill(bill);
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
