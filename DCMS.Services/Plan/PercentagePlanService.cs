using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Data;
using DCMS.Core.Domain.Common;
using DCMS.Core.Domain.Plan;
using DCMS.Core.Domain.Tasks;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Events;
using DCMS.Services.Tasks;
using DCMS.Services.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using DCMS.Services.Caching;


namespace DCMS.Services.plan
{

    /// <summary>
    /// ��ʾ��ɷ���
    /// </summary>
    public partial class PercentagePlanService : BaseService, IPercentagePlanService
    {
        private readonly IUserService _userService;
        private readonly IQueuedMessageService _queuedMessageService;
        public PercentagePlanService(IStaticCacheManager cacheManager,
            IServiceGetter getter,
            IEventPublisher eventPublisher,
            IUserService userService,
            IQueuedMessageService queuedMessageService) : base(getter, cacheManager, eventPublisher)
        {
            _userService = userService;
            _queuedMessageService = queuedMessageService;
        }


        #region ����


        /// <summary>
        ///  ɾ��
        /// </summary>
        /// <param name="percentagePlans"></param>
        public virtual void DeletePercentagePlan(PercentagePlan percentagePlans)
        {
            if (percentagePlans == null)
            {
                throw new ArgumentNullException("percentagePlans");
            }

            var uow = PercentagePlanRepository.UnitOfWork;
            PercentagePlanRepository.Delete(percentagePlans);
            uow.SaveChanges();

            //event notification
            _eventPublisher.EntityDeleted(percentagePlans);
        }

        /// <summary>
        /// ��ȡȫ����ɷ���
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public virtual IPagedList<PercentagePlan> GetAllPercentagePlans(int? store, string name = null, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;
            var query = PercentagePlanRepository.Table;

            if (store.HasValue && store.Value != 0)
            {
                query = query.Where(c => c.StoreId == store);
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(c => c.Name.Contains(name));
            }

            query = query.OrderByDescending(c => c.CreatedOnUtc);
            //var percentagePlans = new PagedList<PercentagePlan>(query.ToList(), pageIndex, pageSize);
            //return percentagePlans;

            //��ҳ��
            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<PercentagePlan>(plists, pageIndex, pageSize, totalCount);

        }


        /// <summary>
        /// ��ȡȫ��
        /// </summary>
        /// <returns></returns>
        public virtual IList<PercentagePlan> GetAllPercentagePlans(int? store)
        {
            var key = DCMSDefaults.PLAN_ALL_KEY.FillCacheKey(store ?? 0);
            return _cacheManager.Get(key, () =>
            {
                var query = from s in PercentagePlanRepository.Table
                            where s.StoreId == store.Value
                            orderby s.CreatedOnUtc, s.Name
                            select s;
                var percentagePlan = query.ToList();
                return percentagePlan;
            });
        }

        /// <summary>
        /// ������ɷ���ID��ȡ��ɷ���
        /// </summary>
        /// <returns></returns>
        public virtual IList<Percentage> GetPercentagePlans(int PercentagePlansid)
        {
            var query = from s in PercentageRepository.Table
                        where s.PercentagePlanId == PercentagePlansid
                        select s;
            var Percentage = query.ToList();
            return Percentage;
        }

        /// <summary>
        /// ��ȡȫ��
        /// </summary>
        /// <returns></returns>
        public virtual IList<PercentagePlan> GetAllPercentagePlans()
        {
            var key = DCMSDefaults.PLAN_ALL_KEY.FillCacheKey();
            return _cacheManager.Get(key, () =>
            {
                var query = from s in PercentagePlanRepository.Table
                            orderby s.CreatedOnUtc, s.Name
                            select s;
                var percentagePlan = query.ToList();
                return percentagePlan;
            });
        }

        /// <summary>
        /// ��ȡ
        /// </summary>
        /// <param name="percentagePlansId"></param>
        /// <returns></returns>
        public virtual PercentagePlan GetPercentagePlanById(int? store, int percentagePlansId)
        {
            if (percentagePlansId == 0)
            {
                return null;
            }

            return PercentagePlanRepository.ToCachedGetById(percentagePlansId);
        }


        public virtual IList<PercentagePlan> GetPercentagePlansByIds(int[] sIds)
        {
            if (sIds == null || sIds.Length == 0)
            {
                return new List<PercentagePlan>();
            }

            var query = from c in PercentagePlanRepository.Table
                        where sIds.Contains(c.Id)
                        select c;
            var percentagePlan = query.ToList();

            var sortedPercentagePlans = new List<PercentagePlan>();
            foreach (int id in sIds)
            {
                var percentagePlans = percentagePlan.Find(x => x.Id == id);
                if (percentagePlans != null)
                {
                    sortedPercentagePlans.Add(percentagePlans);
                }
            }
            return sortedPercentagePlans;
        }

        //ee
        /// <summary>
        /// ����ɷ�����Ϣ ������ɷ���
        /// </summary>
        /// <param name="store"></param>
        /// <returns></returns>
        public virtual List<PercentagePlan> BindPercentagePlanList(int? store)
        {
            return _cacheManager.Get(DCMSDefaults.BINDPERCENTAGEPLANLIST_STORE_KEY.FillCacheKey(store), () =>
           {
               var query = from s in PercentagePlanRepository.Table
                           where s.StoreId == store.Value
                           orderby s.CreatedOnUtc, s.Name
                           select s;
               var result = query.Select(q => new { q.Id, q.Name }).ToList().Select(x => new PercentagePlan { Id = x.Id, Name = x.Name }).ToList();
               return result;
           });
        }

        /// <summary>
        /// ��ҵ��Ա��ɷ�����Ϣ ��������Ϊ ȫ�� + ҵ��Ա
        /// </summary>
        /// <param name="store"></param>
        /// <returns></returns>
        public virtual List<PercentagePlan> BindBusinessPercentagePlanList(int? store)
        {
            return _cacheManager.Get(DCMSDefaults.BINDBUSINESSPERCENTAGEPLANLIST_STORE_KEY.FillCacheKey(store), () =>
           {
               var query = from s in PercentagePlanRepository.Table
                           where s.StoreId == store.Value
                           && (s.PlanTypeId == (int)PercentagePlanType.AllPlan || s.PlanTypeId == (int)PercentagePlanType.BusinessExtractPlan)
                           orderby s.CreatedOnUtc, s.Name
                           select s;
               var result = query.Select(q => new { q.Id, q.Name }).ToList().Select(x => new PercentagePlan { Id = x.Id, Name = x.Name }).ToList();
               return result;
           });
        }

        /// <summary>
        /// ���ͻ�Ա��ɷ�����Ϣ ��������Ϊ ȫ�� + �ͻ�Ա
        /// </summary>
        /// <param name="store"></param>
        /// <returns></returns>
        public virtual List<PercentagePlan> BindDeliverPercentagePlanList(int? store)
        {
            return _cacheManager.Get(DCMSDefaults.BINDDELIVERPERCENTAGEPLANLIST_STORE_KEY.FillCacheKey(store), () =>
           {
               var query = from s in PercentagePlanRepository.Table
                           where s.StoreId == store.Value
                           && (s.PlanTypeId == (int)PercentagePlanType.AllPlan || s.PlanTypeId == (int)PercentagePlanType.DeliveryExtractPlan)
                           orderby s.CreatedOnUtc, s.Name
                           select s;
               var result = query.Select(q => new { q.Id, q.Name }).ToList().Select(x => new PercentagePlan { Id = x.Id, Name = x.Name }).ToList();
               return result;
           });
        }
        //ee



        /// <summary>
        /// ���
        /// </summary>
        /// <param name="percentagePlans"></param>
        public virtual void InsertPercentagePlan(PercentagePlan percentagePlans)
        {
            if (percentagePlans == null)
            {
                throw new ArgumentNullException("percentagePlans");
            }

            var uow = PercentagePlanRepository.UnitOfWork;
            PercentagePlanRepository.Insert(percentagePlans);
            uow.SaveChanges();

            //event notification
            _eventPublisher.EntityInserted(percentagePlans);
        }

        /// <summary>
        /// ����
        /// </summary>
        /// <param name="percentagePlans"></param>
        public virtual void UpdatePercentagePlan(PercentagePlan percentagePlans)
        {
            if (percentagePlans == null)
            {
                throw new ArgumentNullException("percentagePlans");
            }

            var uow = PercentagePlanRepository.UnitOfWork;
            PercentagePlanRepository.Update(percentagePlans);
            uow.SaveChanges();

            //event notification
            _eventPublisher.EntityUpdated(percentagePlans);
        }


        public BaseResult BillCreateOrUpdate(int storeId, int userId, int? billId, PercentagePlan bill, PercentagePlan data, bool isAdmin = false)
        {
            var uow = ReturnBillsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();
                bill.StoreId = storeId;
                //bill.MakeUserId = userId;


                if (billId.HasValue && billId.Value != 0)
                {
                    #region ���·���


                    bill.Name = data.Name;
                    bill.PlanTypeId = data.PlanTypeId;

                    bill.IsByReturn = data.IsByReturn;
                    bill.CreatedOnUtc = data.CreatedOnUtc;
                    UpdatePercentagePlan(bill);
                    #endregion
                }
                else
                {
                    #region ����˻�

                    bill.StoreId = storeId;
                    bill.Name = data.Name;
                    bill.PlanTypeId = data.PlanTypeId;
                    bill.IsByReturn = data.IsByReturn;
                    bill.CreatedOnUtc = DateTime.UtcNow;


                    InsertPercentagePlan(bill);
                    //����Id
                    billId = bill.Id;

                    #endregion

                }


                //����Ա�����Զ����
                if (isAdmin) //�жϵ�ǰ��¼���Ƿ�Ϊ����Ա,��Ϊ����Ա�������Զ����
                {
                    AuditingNoTran(userId, bill);
                }
                else
                {
                    #region ����֪ͨ ����Ա
                    try
                    {
                        //�Ƶ��ˡ�����Ա
                        var userNumbers = _userService.GetAllAdminUserMobileNumbersByStore(storeId).ToList();

                        QueuedMessage queuedMessage = new QueuedMessage()
                        {
                            StoreId = storeId,
                            MType = MTypeEnum.Message,
                            Title = CommonHelper.GetEnumDescription<MTypeEnum>(MTypeEnum.Message),
                            Date = DateTime.Now,
                            BillType = BillTypeEnum.ReturnBill,
                            BillNumber = "",
                            BillId = bill.Id,
                            CreatedOnUtc = DateTime.Now
                        };
                        _queuedMessageService.InsertQueuedMessage(userNumbers,queuedMessage);
                    }
                    catch (Exception ex)
                    {
                        _queuedMessageService.WriteLogs(ex.Message);
                    }
                    #endregion
                }


                //��������
                transaction.Commit();

                return new BaseResult { Success = true, Return = billId ?? 0, Message = "���ݴ���/���³ɹ�" };
            }
            catch (Exception)
            {
                //������񲻴��ڻ���Ϊ����ع�
                transaction?.Rollback();
                return new BaseResult { Success = false, Message = "���ݴ���/����ʧ��" };
            }
            finally
            {
                //����������󶼻�رյ��������
                using (transaction) { }
            }
        }
        public BaseResult AuditingNoTran(int userId, PercentagePlan bill)
        {
            try
            {

                #region ����֪ͨ
                try
                {
                    //�Ƶ��ˡ�����Ա
                    var userNumbers = new List<string>() { _userService.GetMobileNumberByUserId(userId) };
                    var queuedMessage = new QueuedMessage()
                    {
                        StoreId = bill.StoreId,
                        MType = MTypeEnum.Audited,
                        Title = CommonHelper.GetEnumDescription<MTypeEnum>(MTypeEnum.Audited),
                        Date = DateTime.Now,
                        BillType = BillTypeEnum.ReturnBill,
                        BillNumber = "",
                        BillId = bill.Id,
                        CreatedOnUtc = DateTime.Now
                    };
                    _queuedMessageService.InsertQueuedMessage(userNumbers,queuedMessage);
                }
                catch (Exception ex)
                {
                    _queuedMessageService.WriteLogs(ex.Message);
                }
                #endregion

                return new BaseResult { Success = true, Message = "������˳ɹ�" };
            }
            catch (Exception)
            {
                return new BaseResult { Success = false, Message = "�������ʧ��" };
            }

        }

        public virtual int PercentagePlanId(int store, string Name)
        {
            var query = PercentagePlanRepository.Table;

            if (string.IsNullOrWhiteSpace(Name))
            {
                return 0;
            }

            return query.Where(s => s.StoreId == store && s.Name == Name).Select(s => s.Id).FirstOrDefault();
        }
        #endregion
    }
}