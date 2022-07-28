using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Data;
using DCMS.Core.Domain.Campaigns;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Events;
using DCMS.Services.Terminals;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using DCMS.Services.Caching;


namespace DCMS.Services.Campaigns
{
    /// <summary>
    /// ���������
    /// </summary>
    public partial class CampaignService : BaseService, ICampaignService
    {
        #region ����

        private readonly IChannelService _channelService;
        protected readonly ICacheKeyService _cacheKeyService;

        public CampaignService(IServiceGetter getter,
            IStaticCacheManager cacheManager,
            IEventPublisher eventPublisher,
             IChannelService channelService
            ) : base(getter, cacheManager, eventPublisher)
        {
            _channelService = channelService;
        }

        #endregion

        #region �


        /// <summary>
        /// 
        /// </summary>
        /// <param name="store"></param>
        /// <param name="name"></param>
        /// <param name="billNumber"></param>
        /// <param name="remark"></param>
        /// <param name="channelId"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="showExpire">չʾ���ڻ</param>
        /// <param name="enabled">չʾͣ�û</param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="showHidden"></param>
        /// <returns></returns>
        public virtual IPagedList<Campaign> GetAllCampaigns(int? store, string name = "", string billNumber = "", string remark = "", int channelId = 0, DateTime? start = null, DateTime? end = null, bool showExpire = false, bool? enabled = null,
            int pageIndex = 0, int pageSize = int.MaxValue, bool showHidden = false)
        {
            //var query = CampaignsRepository.Table;
            if (pageSize >= 50)
                pageSize = 50;
            var query = from pc in CampaignsRepository.Table
                          .Include(cr => cr.CampaignChannels)
                        select pc;

            if (store.HasValue)
            {
                query = query.Where(c => c.StoreId == store);
            }
            else
            {
                return null;
            }
            if (channelId > 0)
            {
                query = from a in query
                        join b in CampaignChannelMappingRepository.Table on a.Id equals b.CampaignId
                        where b.ChannelId == channelId
                        select a;
            }
            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(c => c.Name.Contains(name));
            }

            if (!string.IsNullOrEmpty(billNumber))
            {
                query = query.Where(c => c.BillNumber.Contains(billNumber));
            }

            if (!string.IsNullOrEmpty(remark))
            {
                query = query.Where(c => c.Remark.Contains(remark));
            }

            if (start != null)
            {
                query = query.Where(c => c.CreatedOnUtc >= start);
            }

            if (end != null)
            {
                query = query.Where(c => c.CreatedOnUtc <= end);
            }

            //չʾ���ڻ(��ǰʱ����ڽ���ʱ��)
            if (showExpire)
            {
                query = query.Where(c => c.EndTime < DateTime.Now);
            }

            if (enabled.HasValue)
            {
                query = query.Where(c => c.Enabled == enabled);
            }

            //query = query.OrderBy(c => c.Id);
            query = query.OrderByDescending(c => c.CreatedOnUtc);

            //��ҳ��
            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<Campaign>(plists, pageIndex, pageSize, totalCount);

        }


        /// <summary>
        /// ��ѯ����ʹ�õĴ����
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="key"></param>
        /// <param name="channelId"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public virtual IPagedList<Campaign> GetAvailableCampaigns(string key, int storeId, int channelId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            //��ѯ���ô����
            var queryIds = from a in CampaignsRepository.Table
                           join b in CampaignChannelMappingRepository.Table on a.Id equals b.CampaignId
                           join d in CampaignBuyProductsRepository.Table on a.Id equals d.CampaignId
                           join e in CampaignGiveProductsRepository.Table on a.Id equals e.CampaignId
                           where a.StoreId == storeId
                           && (channelId > 0? b.ChannelId == channelId : 1==1) 
                           && a.Enabled == true
                           && (DateTime.Now >= a.StartTime
                           && DateTime.Now <= a.EndTime)
                           && d.Quantity > 0
                           && e.Quantity > 0
                           select a.Id;

            var ids = queryIds.Distinct().ToList();

            var query = CampaignsRepository.Table;

            //�������ô����
            query = query.Where(q => ids.Contains(q.Id));

            if (!string.IsNullOrWhiteSpace(key))
            {
                query = query.Where(c => c.Name.Contains(key));
            }

            query = query.OrderByDescending(c => c.CreatedOnUtc);

            //��ҳ��
            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<Campaign>(plists, pageIndex, pageSize, totalCount);
        }

        public virtual IList<CampaignBuyProduct> GetCampaignBuyByCampaignId(int campaignId)
        {
            var query = from c in CampaignBuyProductsRepository.Table
                        where c.CampaignId == campaignId
                        select c;
            var list = query.ToList();
            return list;
        }
        public virtual IList<CampaignGiveProduct> GetCampaignGiveByCampaignId(int campaignId)
        {
            var query = from c in CampaignGiveProductsRepository.Table
                        where c.CampaignId == campaignId
                        select c;
            var list = query.ToList();
            return list;
        }

        public virtual IList<Campaign> GetAllCampaigns()
        {
            var query = from c in CampaignsRepository.Table
                        orderby c.Id
                        select c;

            var categories = query.ToList();
            return categories;
        }

        public virtual Campaign GetCampaignById(int? storeId, int campaignId)
        {
            if (campaignId == 0)
            {
                return null;
            }
            var key = DCMSDefaults.CAMPAIGN_BY_ID_KEY.FillCacheKey(storeId ?? 0, campaignId);
            return _cacheManager.Get(key, () =>
            {
                var campaign = CampaignsRepository.Table
                .Where(s => s.StoreId == storeId && s.Id == campaignId)
                .Include(s => s.BuyProducts)
                .Include(s => s.GiveProducts).FirstOrDefault();

                var query = from c in CampaignChannelMappingRepository.Table
                            where c.StoreId == storeId && c.CampaignId == campaignId
                            select c;

                query = query.Include(s => s.Channel).Include(s => s.Campaign);
                
                campaign?.SetCampaignChannels(query.ToList());
                
                return campaign;
            });
        }

        public virtual void InsertCampaign(Campaign campaign)
        {
            if (campaign == null)
            {
                throw new ArgumentNullException("campaign");
            }

            var uow = CampaignsRepository.UnitOfWork;
            CampaignsRepository.Insert(campaign);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(campaign);
        }

        public virtual void UpdateCampaign(Campaign campaign)
        {
            if (campaign == null)
            {
                throw new ArgumentNullException("campaign");
            }

            var uow = CampaignsRepository.UnitOfWork;
            CampaignsRepository.Update(campaign);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(campaign);
        }

        public virtual void DeleteCampaign(Campaign campaign)
        {
            if (campaign == null)
            {
                throw new ArgumentNullException("campaign");
            }

            var uow = CampaignsRepository.UnitOfWork;
            CampaignsRepository.Delete(campaign);
            uow.SaveChanges();

            //event notification
            _eventPublisher.EntityDeleted(campaign);
        }


        #endregion



        #region �����ӳ��


        public virtual IPagedList<CampaignChannel> GetCampaignChannelsByCampaignId(int campaignId, int? userId, int? storeId, int pageIndex, int pageSize)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (campaignId == 0)
            {
                return new PagedList<CampaignChannel>(new List<CampaignChannel>(), pageIndex, pageSize, 0);
            }

            var key = DCMSDefaults.CAMPAIGN_CHANNEL_ALLBY_CAMPAIGNID_KEY.FillCacheKey(storeId, campaignId, pageIndex, pageSize, userId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CampaignChannelMappingRepository.Table
                                //join p in ChannelsRepository.Table on pc.ChannelId equals p.Id
                            where pc.StoreId == storeId && pc.CampaignId == campaignId
                            orderby pc.Id
                            select pc;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CampaignChannel>(plists, pageIndex, pageSize, totalCount);

            });
        }


        public virtual IList<CampaignChannel> GetCampaignChannelsByCampaignId(int? store, int campaignId)
        {

            var key = DCMSDefaults.CAMPAIGN_CHANNEL_BY_AMPAIGNID_KEY.FillCacheKey(store ?? 0, campaignId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CampaignChannelMappingRepository.Table
                                //join p in ChannelsRepository.Table on pc.ChannelId equals p.Id 
                            where pc.StoreId == store && pc.CampaignId == campaignId
                            orderby pc.Id
                            select pc;

                return query.ToList();
            });
        }

        public virtual CampaignChannel GetCampaignChannelById(int productCampaignId)
        {
            if (productCampaignId == 0)
            {
                return null;
            }

            return CampaignChannelMappingRepository.ToCachedGetById(productCampaignId);
        }

        public virtual void InsertCampaignChannel(CampaignChannel productCampaign)
        {
            if (productCampaign == null)
            {
                throw new ArgumentNullException("productCampaign");
            }

            var uow = CampaignChannelMappingRepository.UnitOfWork;
            CampaignChannelMappingRepository.Insert(productCampaign);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(productCampaign);
        }

        public virtual void UpdateCampaignChannel(CampaignChannel productCampaign)
        {
            if (productCampaign == null)
            {
                throw new ArgumentNullException("productCampaign");
            }

            var uow = CampaignChannelMappingRepository.UnitOfWork;
            CampaignChannelMappingRepository.Update(productCampaign);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(productCampaign);
        }

        public virtual void DeleteCampaignChannel(CampaignChannel productCampaign)
        {
            if (productCampaign == null)
            {
                throw new ArgumentNullException("productCampaign");
            }

            var uow = CampaignChannelMappingRepository.UnitOfWork;
            CampaignChannelMappingRepository.Delete(productCampaign);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(productCampaign);
        }


        #endregion


        #region ������Ʒ


        public virtual IList<CampaignBuyProduct> GetCampaignBuyProductsByCampaignId(int campaignId, int? userId, int? storeId, int pageIndex, int pageSize)
        {
            if (campaignId == 0)
            {
                return new PagedList<CampaignBuyProduct>(new List<CampaignBuyProduct>(), pageIndex, pageSize, 0);
            }

            var key = DCMSDefaults.CAMPAIGN_PRODUCT_ALLBY_CAMPAIGNID_KEY.FillCacheKey(storeId, campaignId, pageIndex, pageSize, userId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CampaignBuyProductsRepository.Table
                            where pc.CampaignId == campaignId
                            orderby pc.Id
                            select pc;
                //var productCampaigns = new PagedList<CampaignBuyProduct>(query.ToList(), pageIndex, pageSize);
                //return productCampaigns;

                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CampaignBuyProduct>(plists, pageIndex, pageSize, totalCount);

            });
        }

        public virtual CampaignBuyProduct GetCampaignBuyProductById(int campaignBuyProductId)
        {
            if (campaignBuyProductId == 0)
            {
                return null;
            }

            return CampaignBuyProductsRepository.ToCachedGetById(campaignBuyProductId);
        }

        public virtual void InsertCampaignBuyProduct(CampaignBuyProduct campaignBuyProduct)
        {
            if (campaignBuyProduct == null)
            {
                throw new ArgumentNullException("campaignBuyProduct");
            }

            var uow = CampaignBuyProductsRepository.UnitOfWork;
            CampaignBuyProductsRepository.Insert(campaignBuyProduct);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(campaignBuyProduct);
        }

        public virtual void UpdateCampaignBuyProduct(CampaignBuyProduct campaignBuyProduct)
        {
            if (campaignBuyProduct == null)
            {
                throw new ArgumentNullException("campaignBuyProduct");
            }

            var uow = CampaignBuyProductsRepository.UnitOfWork;
            CampaignBuyProductsRepository.Update(campaignBuyProduct);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(campaignBuyProduct);
        }

        public virtual void DeleteCampaignBuyProduct(CampaignBuyProduct campaignBuyProduct)
        {
            if (campaignBuyProduct == null)
            {
                throw new ArgumentNullException("campaignBuyProduct");
            }

            var uow = CampaignBuyProductsRepository.UnitOfWork;
            CampaignBuyProductsRepository.Delete(campaignBuyProduct);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(campaignBuyProduct);
        }


        #endregion



        #region ������Ʒ

        public virtual IList<CampaignGiveProduct> GetCampaignGiveProductByCampaignId(int campaignId, int? userId, int? storeId, int pageIndex, int pageSize)
        {
            if (campaignId == 0)
            {
                return new PagedList<CampaignGiveProduct>(new List<CampaignGiveProduct>(), pageIndex, pageSize, 0);
            }

            var key = DCMSDefaults.CAMPAIGN_GIVEPRODUCT_ALLBY_CAMPAIGNID_KEY.FillCacheKey(storeId, campaignId, pageIndex, pageSize, userId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in CampaignGiveProductsRepository.Table
                            where pc.CampaignId == campaignId
                            orderby pc.Id
                            select pc;
                //var productCampaigns = new PagedList<CampaignGiveProduct>(query.ToList(), pageIndex, pageSize);
                //return productCampaigns;

                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CampaignGiveProduct>(plists, pageIndex, pageSize, totalCount);

            });
        }

        /// <summary>
        /// ������Ʒ
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="channelId"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public virtual IList<CampaignBuyProduct> GetCampaignBuyProducts(int? storeId, int? channelId, int pageIndex, int pageSize)
        {
            var key = DCMSDefaults.CAMPAIGN_BUYPRODUCTS_KEY.FillCacheKey(storeId, channelId, pageIndex, pageSize);
            return _cacheManager.Get(key, () =>
            {
                var query = from cs in CampaignsRepository.Table
                            join cc in CampaignChannelMappingRepository.Table on cs.Id equals cc.CampaignId
                            join cp in CampaignBuyProductsRepository.Table on cs.Id equals cp.CampaignId
                            where cs.StoreId == storeId && cc.ChannelId == channelId && (DateTime.Now >= cs.StartTime && DateTime.Now <= cs.EndTime) && cs.Enabled == true
                            orderby cp.Id
                            select cp;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CampaignBuyProduct>(plists, pageIndex, pageSize, totalCount);

            });
        }

        /// <summary>
        /// ������Ʒ
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="channelId"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public virtual IList<CampaignGiveProduct> GetCampaignGiveProducts(int? storeId, int? channelId, int pageIndex, int pageSize)
        {

            var key = DCMSDefaults.CAMPAIGN_GIVEPRODUCTS_KEY.FillCacheKey(storeId, channelId, pageIndex, pageSize);
            return _cacheManager.Get(key, () =>
            {
                var query = from cs in CampaignsRepository.Table
                            join cc in CampaignChannelMappingRepository.Table on cs.Id equals cc.CampaignId
                            join cp in CampaignGiveProductsRepository.Table on cs.Id equals cp.CampaignId
                            where cs.StoreId == storeId && cc.ChannelId == channelId && (DateTime.Now >= cs.StartTime && DateTime.Now <= cs.EndTime) && cs.Enabled == true
                            orderby cp.Id
                            select cp;
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<CampaignGiveProduct>(plists, pageIndex, pageSize, totalCount);

            });
        }

        /// <summary>
        /// ��ȡ���� ��Ʒ�����Ϣ
        /// </summary>
        /// <param name="storeId">������Id</param>
        /// <param name="channelId">����Id</param>
        /// <returns></returns>
        public IList<Campaign> GetAvailableCampaigns(int storeId, int channelId)
        {
            var key = DCMSDefaults.CAMPAIGN_GETAVAILABLECAMPAIGNS.FillCacheKey(storeId, channelId);
            return _cacheManager.Get(key, () =>
            {
                var query = from cs in CampaignsRepository.Table
                            join cc in CampaignChannelMappingRepository.Table on cs.Id equals cc.CampaignId
                            where cs.StoreId == storeId
                            && cc.ChannelId == channelId
                            && (DateTime.Now >= cs.StartTime
                            && DateTime.Now <= cs.EndTime)
                            && cs.Enabled == true
                            && cs.BuyProducts.Count > 0
                            && cs.GiveProducts.Count > 0
                            select cs;

                return query.ToList();

            });
        }

        public virtual Tuple<List<CampaignBuyProduct>, List<CampaignGiveProduct>> GetAvailableCampaigns(string key, int storeId, int channelId, int pageIndex, int pageSize, out int totalCount)
        {
            totalCount = 0;
            var query = from cs in CampaignsRepository.Table
                        join cc in CampaignChannelMappingRepository.Table on cs.Id equals cc.CampaignId
                        join a in CampaignBuyProductsRepository.Table on cs.Id equals a.CampaignId
                        join b in CampaignGiveProductsRepository.Table on cs.Id equals b.CampaignId

                        join ap in ProductsRepository.Table on a.ProductId equals ap.Id
                        join bp in ProductsRepository.Table on b.ProductId equals bp.Id

                        where cs.StoreId == storeId
                        && cc.ChannelId == channelId
                        && (DateTime.Now >= cs.StartTime
                        && DateTime.Now <= cs.EndTime)
                        && cs.Enabled == true
                        && a.Quantity > 0
                        && b.Quantity > 0

                        select new
                        {
                            CampaignId = cs.Id,

                            BuyId = a.Id,
                            BuyProductId = a.ProductId,
                            BuyProductName = ap.Name,
                            BuyQuantity = a.Quantity,
                            BuyUnitId = a.UnitId,
                            BuyPrice = a.Price,

                            GiveId = b.Id,
                            GiveProductId = b.ProductId,
                            GiveProductName = bp.Name,
                            GiveQuantity = b.Quantity,
                            GiveUnitId = b.UnitId,
                            GivePrice = b.Price,
                        };

            //��������Ʒ����
            if (!string.IsNullOrEmpty(key))
            {
                query = query.Where(q => q.BuyProductName.Contains(key) || q.GiveProductName.Contains(key));
            }

            //�����Ժ���ܻ���� ��������
            query = query.OrderByDescending(q => q.CampaignId);

            totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();

            List<CampaignBuyProduct> buys = new List<CampaignBuyProduct>();
            List<CampaignGiveProduct> gives = new List<CampaignGiveProduct>();
            foreach (var item in plists)
            {
                //��ֹ�ظ����
                if (buys.Where(bu => bu.Id == item.BuyId).Count() == 0)
                {
                    buys.Add(new CampaignBuyProduct()
                    {
                        Id = item.BuyId,
                        CampaignId = item.CampaignId,
                        ProductId = item.BuyProductId,
                        Quantity = item.BuyQuantity,
                        UnitId = item.BuyUnitId,
                        Price = item.BuyPrice
                    });
                }
                //��ֹ�ظ����
                if (gives.Where(gi => gi.Id == item.GiveId).Count() == 0)
                {
                    gives.Add(new CampaignGiveProduct()
                    {
                        Id = item.GiveId,
                        CampaignId = item.CampaignId,
                        ProductId = item.GiveProductId,
                        Quantity = item.GiveQuantity,
                        UnitId = item.GiveUnitId,
                        Price = item.GivePrice
                    });
                }
            }

            return new Tuple<List<CampaignBuyProduct>, List<CampaignGiveProduct>>(buys, gives);

        }

        public virtual IList<Campaign> GetCampaignsByIds(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return new List<Campaign>();
            }

            var query = from c in CampaignsRepository.Table
                        .Include(c => c.CampaignChannels)
                        where ids.Contains(c.Id)
                        select c;
            var list = query.ToList();

            var result = new List<Campaign>();
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

        public virtual IList<CampaignBuyProduct> GetCampaignBuyByIds(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return new List<CampaignBuyProduct>();
            }

            var query = from c in CampaignBuyProductsRepository.Table
                        where ids.Contains(c.Id)
                        select c;
            var list = query.ToList();

            var result = new List<CampaignBuyProduct>();
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
        public virtual IList<CampaignGiveProduct> GetCampaignGiveByIds(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return new List<CampaignGiveProduct>();
            }

            var query = from c in CampaignGiveProductsRepository.Table
                        where ids.Contains(c.Id)
                        select c;
            var list = query.ToList();

            var result = new List<CampaignGiveProduct>();
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

        public virtual IList<CampaignBuyProduct> GetCampaignBuyByCampaignIds(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return new List<CampaignBuyProduct>();
            }

            var query = from c in CampaignBuyProductsRepository.Table
                        where ids.Contains(c.CampaignId)
                        select c;
            var list = query.ToList();

            return list;
        }
        public virtual IList<CampaignGiveProduct> GetCampaignGiveByCampaignIds(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return new List<CampaignGiveProduct>();
            }

            var query = from c in CampaignGiveProductsRepository.Table
                        where ids.Contains(c.CampaignId)
                        select c;
            var list = query.ToList();

            return list;
        }



        public virtual CampaignGiveProduct GetCampaignGiveProductById(int campaignGiveProductId)
        {
            if (campaignGiveProductId == 0)
            {
                return null;
            }

            return CampaignGiveProductsRepository.ToCachedGetById(campaignGiveProductId);
        }

        public virtual void InsertCampaignGiveProduct(CampaignGiveProduct campaignGiveProduct)
        {
            if (campaignGiveProduct == null)
            {
                throw new ArgumentNullException("campaignGiveProduct");
            }

            var uow = CampaignGiveProductsRepository.UnitOfWork;
            CampaignGiveProductsRepository.Insert(campaignGiveProduct);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(campaignGiveProduct);
        }

        public virtual void UpdateCampaignGiveProduct(CampaignGiveProduct campaignGiveProduct)
        {
            if (campaignGiveProduct == null)
            {
                throw new ArgumentNullException("campaignGiveProduct");
            }

            var uow = CampaignGiveProductsRepository.UnitOfWork;
            CampaignGiveProductsRepository.Update(campaignGiveProduct);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(campaignGiveProduct);
        }

        public virtual void DeleteCampaignGiveProduct(CampaignGiveProduct campaignGiveProduct)
        {
            if (campaignGiveProduct == null)
            {
                throw new ArgumentNullException("campaignGiveProduct");
            }

            var uow = CampaignGiveProductsRepository.UnitOfWork;
            CampaignGiveProductsRepository.Delete(campaignGiveProduct);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(campaignGiveProduct);
        }


        #endregion


        public BaseResult BillCreateOrUpdate(int storeId, int userId, int? campaignId, Campaign campaign, CampaignUpdate data, List<CampaignBuyProduct> buyItems, List<CampaignGiveProduct> giveItems, bool isAdmin = false)
        {
            var uow = CampaignsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();



                #region ����������Ϣ
                if (campaignId.HasValue && campaignId.Value != 0)
                {
                    if (campaign != null)
                    {
                        campaign.Name = data.Name;
                        campaign.StartTime = data.StartTime;
                        campaign.EndTime = data.EndTime;
                        campaign.Remark = data.Remark;
                        campaign.SaleRemark = "";
                        campaign.Enabled = data.Enabled;
                        campaign.ProtocolNum = data.ProtocolNum;

                        //���»
                        UpdateCampaign(campaign);

                        var chennels = _channelService.GetAll(storeId);

                        //��������ӳ��
                        var campaignChannels = GetCampaignChannelsByCampaignId(storeId, campaign.Id);
                        chennels.ToList().ForEach(c =>
                        {
                            if (data.SelectedChannelIds.Contains(c.Id))
                            {
                                if (!campaignChannels.Select(cc => cc.ChannelId).Contains(c.Id))
                                {
                                    var campaignChannel = new CampaignChannel()
                                    {
                                        ChannelId = c.Id,
                                        CampaignId = campaign.Id,
                                        StoreId = storeId
                                    };
                                    //�������ӳ��
                                    InsertCampaignChannel(campaignChannel);
                                }
                            }
                            else
                            {
                                if (campaignChannels.Select(cc => cc.ChannelId).Contains(c.Id))
                                {
                                    var channels = campaignChannels.Select(cc => cc).Where(cc => cc.ChannelId == c.Id).ToList();
                                    channels.ForEach(ch =>
                                    {
                                        DeleteCampaignChannel(ch);
                                    });
                                }
                            }
                        });
                    }
                }
                else
                {
                    campaign.StoreId = storeId;
                    campaign.Name = data.Name;
                    campaign.StartTime = data.StartTime;
                    campaign.EndTime = data.EndTime;
                    campaign.Remark = data.Remark;
                    campaign.SaleRemark = "";
                    campaign.BillNumber = CommonHelper.GetBillNumber("CX", storeId);
                    campaign.MakeUserId = userId;
                    campaign.CreatedOnUtc = DateTime.Now;
                    campaign.Enabled = data.Enabled;
                    campaign.ProtocolNum = data.ProtocolNum;

                    //��ӻ
                    InsertCampaign(campaign);
                    campaignId = campaign.Id;

                    data.SelectedChannelIds.ToList().ForEach(c =>
                    {
                        var channel = _channelService.GetChannelById(storeId, c);
                        if (channel != null)
                        {
                            var campaignChannel = new CampaignChannel()
                            {
                                StoreId = storeId,
                                ChannelId = channel.Id,
                                CampaignId = campaign.Id
                            };
                            //�������ӳ��
                            InsertCampaignChannel(campaignChannel);
                        }
                    });
                }

                #endregion

                #region ���¹�����Ŀ

                if (buyItems != null)
                {
                    if (campaignId.HasValue && campaignId.Value != 0)
                    {
                        buyItems.ForEach(p =>
                        {
                            //��campaign�Ѿ�����BuyProducts��ֱ��������
                            //var op = GetCampaignBuyProductById(p.Id);
                            var op = campaign.BuyProducts.Where(w => w.Id == p.Id).FirstOrDefault();
                            if (op == null)
                            {
                                //׷����
                                if (campaign.BuyProducts.Count(cp => cp.Id == p.Id) == 0)
                                {
                                    var product = p;
                                    product.CampaignId = campaignId.Value;
                                    product.StoreId = storeId;
                                    InsertCampaignBuyProduct(product);
                                    //���ų�
                                    p.Id = product.Id;
                                    if (!campaign.BuyProducts.Select(s => s.Id).Contains(product.Id))
                                    {
                                        campaign.BuyProducts.Add(product);
                                    }
                                }
                            }
                            else
                            {
                                //���������
                                op.ProductId = p.ProductId;
                                op.UnitId = p.UnitId;
                                op.Quantity = p.Quantity;
                                UpdateCampaignBuyProduct(op);
                            }
                        });

                        //�ӿ��Ƴ�ɾ����
                        campaign.BuyProducts.ToList().ForEach(p =>
                        {
                            if (buyItems.Count(cp => cp.Id == p.Id) == 0)
                            {
                                campaign.BuyProducts.Remove(p);
                                var op = GetCampaignBuyProductById(p.Id);
                                if (op != null)
                                {
                                    DeleteCampaignBuyProduct(op);
                                }
                            }
                        });
                    }
                }

                #endregion

                #region ����������Ŀ
                if (giveItems != null)
                {
                    if (campaignId.HasValue && campaignId.Value != 0)
                    {
                        giveItems.ForEach(p =>
                        {
                            //��campaign�Ѿ�����GiveProducts��ֱ��������
                            //var op = GetCampaignGiveProductById(p.Id);
                            var op = campaign.GiveProducts.Where(w => w.Id == p.Id).FirstOrDefault();
                            if (op == null)
                            {
                                //׷����
                                if (campaign.GiveProducts.Count(cp => cp.Id == p.Id) == 0)
                                {
                                    var product = p;
                                    product.CampaignId = campaignId.Value;
                                    product.StoreId = storeId;
                                    InsertCampaignGiveProduct(product);
                                    //���ų�
                                    p.Id = product.Id;
                                    //campaign.GiveProducts.Add(product);
                                    if (!campaign.GiveProducts.Select(s => s.Id).Contains(product.Id))
                                    {
                                        campaign.GiveProducts.Add(product);
                                    }
                                }
                            }
                            else
                            {
                                //���������
                                op.ProductId = p.ProductId;
                                op.UnitId = p.UnitId;
                                op.Quantity = p.Quantity;
                                UpdateCampaignGiveProduct(op);
                            }
                        });

                        //�ӿ��Ƴ�ɾ����
                        campaign.GiveProducts.ToList().ForEach(p =>
                        {
                            if (giveItems.Count(cp => cp.Id == p.Id) == 0)
                            {
                                campaign.GiveProducts.Remove(p);
                                var op = GetCampaignGiveProductById(p.Id);
                                if (op != null)
                                {
                                    DeleteCampaignGiveProduct(op);
                                }
                            }
                        });

                    }
                }
                #endregion


                //��������
                transaction.Commit();

                return new BaseResult { Success = true, Return = campaignId ?? 0, Message = "���ݴ���/���³ɹ�", Code = campaign.Id };
            }
            catch (Exception ex)
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



    }
}
