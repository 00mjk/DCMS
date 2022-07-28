using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Domain.Configuration;
using DCMS.Core.Domain.Products;
using DCMS.Core.Domain.Stores;
using DCMS.Core.Domain.Terminals;
using DCMS.Core.Domain.Users;
using DCMS.Core.Domain.WareHouses;
using DCMS.Core.Infrastructure;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Configuration;
using DCMS.Services.Events;
using DCMS.Services.Products;
using DCMS.Services.Security;
using DCMS.Services.Terminals;
using DCMS.Services.Users;
using DCMS.Services.WareHouses;
using System;
using System.Collections.Generic;
using System.Linq;
using DCMS.Services.Caching;

namespace DCMS.Services.Stores
{
    /// <summary>
    /// �����̷���
    /// </summary>
    public partial class StoreService : BaseService, IStoreService
    {
        //����ʹ��_cacheManager ��� cacheManager
        
        public StoreService(IServiceGetter getter,
            IStaticCacheManager cacheManager,
            IEventPublisher eventPublisher) : base(getter, cacheManager, eventPublisher)
        {
            
        }

        #region Methods


        /// <summary>
        ///  ɾ��
        /// </summary>
        /// <param name="store"></param>
        public virtual void DeleteStore(Store store)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (store is IEntityForCaching)
            {
                throw new ArgumentException("Cacheable entities are not supported by Entity Framework");
            }

            var allStores = GetAllStores(true);
            if (allStores.Count == 1)
            {
                throw new Exception("You cannot delete the only configured store");
            }

            var uow = StoreRepository.UnitOfWork;
            StoreRepository.Delete(store);
            uow.SaveChanges();

            //event notification
            _eventPublisher.EntityDeleted(store);
        }

        /// <summary>
        /// ��ȡȫ��������
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public virtual IPagedList<Store> GetAllStores(string name = null, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;
            var query = StoreRepository_RO.Table;

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(c => c.Name.Contains(name));
            }

            query = query.OrderByDescending(c => c.DisplayOrder);

            //��ҳ��
            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<Store>(plists, pageIndex, pageSize, totalCount);

        }

        /// <summary>
        /// ��ȡȫ��
        /// </summary>
        /// <returns></returns>
        public virtual IList<Store> GetAllStores(bool loadCacheableCopy = true)
        {
            IList<Store> LoadStoresFunc()
            {
                var query = from s in StoreRepository_RO.Table orderby s.DisplayOrder, s.Id select s;
                return query.ToList();
            }

            if (loadCacheableCopy)
            {
                return _cacheManager.Get(DCMSStoreDefaults.StoresByIdCacheKey.FillCacheKey(loadCacheableCopy), () =>
                {
                    var result = new List<Store>();
                    foreach (var store in LoadStoresFunc())
                    {
                        result.Add(new StoreForCaching(store));
                    }
                    return result;
                });
            }

            return LoadStoresFunc();
        }
        /// <summary>
        /// ��ȡȫ��
        /// </summary>
        /// <returns></returns>
        public virtual IList<Corporations> GetAllfactory()
        {
            var query = from s in CorporationsRepository_RO.Table
                        where s.FactoryId != 3 && s.ShortName.Contains("�ֹ�˾")
                        orderby s.FactoryId ascending
                        select s;
            return query.ToList();
        }
        /// <summary>
        /// �󶨾�������Ϣ
        /// </summary>
        /// <returns></returns>
        public virtual IList<Store> BindStoreList()
        {
            return _cacheManager.Get(DCMSDefaults.BINDSTORE_ALLLIST.FillCacheKey(0), () =>
             {
                 var query = from s in StoreRepository_RO.TableNoTracking
                             orderby s.DisplayOrder, s.Name
                             select s;
                 var result = query.Select(q => new { Id = q.Id, Name = q.Name }).ToList().Select(x => new Store { Id = x.Id, Name = x.Name }).ToList();
                 return result;
             });
        }

        /// <summary>
        /// ��ȡ
        /// </summary>
        /// <param name="storeId"></param>
        /// <returns></returns>
        public virtual Store GetStoreById(int storeId)
        {
            if (storeId == 0)
            {
                return null;
            }

            return StoreRepository_RO.GetById(storeId);
        }


        public virtual Store GetStoreByUserId(int userId)
        {
            if (userId == 0)
            {
                return null;
            }

            var user = UserRepository_RO.TableNoTracking.Where(u => u.Id == userId).FirstOrDefault();

            return GetStoreById(user?.StoreId ?? 0);
        }


        public virtual Store GetManageStore()
        {
            var store = StoreRepository_RO.TableNoTracking.Where(u => u.Code == "SYSTEM").FirstOrDefault();
            return store;
        }


        public virtual string GetStoreName(int storeId)
        {
            if (storeId == 0)
            {
                return "";
            }
            //var store = GetStoreById(storeId);
            //return store != null ? store.Name : "";
            var key = DCMSDefaults.STORE_NAME_BY_ID_KEY.FillCacheKey(storeId);
            return _cacheManager.Get(key, () =>
            {
                return StoreRepository_RO.Table.Where(a => a.Id == storeId).Select(a => a.Name).FirstOrDefault();
            });
        }

        public virtual IList<Store> GetStoresByIds(int[] sIds)
        {
            if (sIds == null || sIds.Length == 0)
            {
                return new List<Store>();
            }

            var query = from c in StoreRepository_RO.Table
                        where sIds.Contains(c.Id)
                        select c;
            var stores = query.ToList();

            var sortedStores = new List<Store>();
            foreach (int id in sIds)
            {
                var store = stores.Find(x => x.Id == id);
                if (store != null)
                {
                    sortedStores.Add(store);
                }
            }

            return sortedStores;
        }



        /// <summary>
        /// ���
        /// </summary>
        /// <param name="store"></param>
        public virtual void InsertStore(Store store)
        {
            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            var uow = StoreRepository.UnitOfWork;
            StoreRepository.Insert(store);
            uow.SaveChanges();

            //event notification
            _eventPublisher.EntityInserted(store);
        }

        /// <summary>
        /// ����
        /// </summary>
        /// <param name="store"></param>
        public virtual void UpdateStore(Store store)
        {
            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            var uow = StoreRepository.UnitOfWork;
            StoreRepository.Update(store);
            uow.SaveChanges();

            _cacheManager.RemoveByPrefix(DCMSDefaults.STORES_PK);
            //event notification
            _eventPublisher.EntityUpdated(store);
        }


        /// <summary>
        /// ��֤�����Ƿ����(������֤)
        /// </summary>
        /// <param name="storeCode"></param>
        /// <returns></returns>
        public bool CheckStoreCode(string storeCode)
        {
            var query = from s in StoreRepository_RO.Table
                        where s.Code == storeCode
                        orderby s.Id
                        select s;
            return query.ToList().Count() > 0;
        }




        #endregion


        #region �ն�


        /// <summary>
        /// ��ȡ�������ն�
        /// </summary>
        /// <returns></returns>
        public virtual IList<Terminal> GetTerminals(int? storeId)
        {
            if (storeId.HasValue)
            {
                var ids = new List<int>();
                var tids = from s in CRM_RELATIONRepository_RO.Table
                           where s.StoreId == storeId
                           orderby s.Id ascending
                           select s.TerminalId;

                if (tids != null && tids.Any())
                    ids = tids.ToList();

                var query = from s in TerminalsRepository_RO.Table
                            where ids.Contains(s.Id)
                            orderby s.Id ascending
                            select s;

                return query.ToList();
            }
            else
                return null;
        }

        #endregion


        /// <summary>
        /// �ű�����������
        /// </summary>
        /// <param name="store"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool AddStoreScript(Store store, User user)
        {

            bool fg = true;
            try
            {
                if (fg)
                {

                    var userService = EngineContext.Current.Resolve<IUserService>();

                    int storeId = store.Id;
                    int userId = user.Id;
                    int userRoleId = 0;
                    //step1:
                    #region �����ɫ

                    //��������Ա
                    UserRole userRole;
                    if (user.UserRoles.ToList().Where(u => u.SystemName == "Administrators").Count() == 0)
                    {
                        userRole = new UserRole
                        {
                            Name = "��������Ա",
                            StoreId = storeId,
                            Active = true,
                            IsSystemRole = false,
                            SystemName = "Administrators",
                            Description = "��������Ա"
                        };

                        userService.InsertUserRole(userRole);
                        userRoleId = userRole.Id;
                        user.UserRoles.Add(userRole);
                    }
                    else
                    {
                        userRole = user.UserRoles.ToList().Where(u => u.SystemName == "Administrators").FirstOrDefault();
                        userRoleId = userRole.Id;
                    }

                    //Ա��
                    if (user.UserRoles.ToList().Where(u => u.SystemName == "Employees").Count() == 0)
                    {
                        var userRole2 = new UserRole
                        {
                            Name = "Ա��",
                            StoreId = storeId,
                            Active = true,
                            IsSystemRole = false,
                            SystemName = "Employees",
                            Description = "Ա��"
                        };

                        userService.InsertUserRole(userRole2);
                        user.UserRoles.Add(userRole2);
                    }

                    //ҵ��Ա
                    if (user.UserRoles.ToList().Where(u => u.SystemName == "Salesmans").Count() == 0)
                    {
                        var userRole3 = new UserRole
                        {
                            Name = "ҵ��Ա",
                            StoreId = storeId,
                            Active = true,
                            IsSystemRole = false,
                            SystemName = "Salesmans",
                            Description = "ҵ��Ա"
                        };

                        userService.InsertUserRole(userRole3);
                        user.UserRoles.Add(userRole3);
                    }

                    //�ͻ�Ա
                    if (user.UserRoles.ToList().Where(u => u.SystemName == "Delivers").Count() == 0)
                    {
                        var userRole4 = new UserRole
                        {
                            Name = "�ͻ�Ա",
                            StoreId = storeId,
                            Active = true,
                            IsSystemRole = false,
                            SystemName = "Delivers",
                            Description = "�ͻ�Ա"
                        };

                        userService.InsertUserRole(userRole4);
                        user.UserRoles.Add(userRole4);
                    }

                    //�����̹���Ա
                    if (user.UserRoles.ToList().Where(u => u.SystemName == "Distributors").Count() == 0)
                    {
                        var userRole5 = new UserRole
                        {
                            Name = "�����̹���Ա",
                            StoreId = storeId,
                            Active = true,
                            IsSystemRole = false,
                            SystemName = "Distributors",
                            Description = "�����̹���Ա"
                        };

                        userService.InsertUserRole(userRole5);
                        user.UserRoles.Add(userRole5);
                    }

                    #endregion

                    //step2:
                    #region �û���ɫ��ϵ
                    //user.UserRoles.Add(userRole);
                    //user.UserRoles.Add(userRole2);
                    //user.UserRoles.Add(userRole3);
                    //user.UserRoles.Add(userRole4);
                    //user.UserRoles.Add(userRole5);
                    userService.UpdateUser(user);
                    #endregion

                    //step3:
                    #region ��ɫģ��(�ű�ֻ����������ԱĬ������Ȩ��)
                    var moduleService = EngineContext.Current.Resolve<IModuleService>();
                    var modules = moduleService.GetAllModules();
                    if (modules != null && modules.Count > 0)
                    {
                        modules.ToList().ForEach(m =>
                        {
                            if (userRole.ModuleRoles.Select(s => s.Module).Where(um => um.Id == m.Id).Count() == 0)
                            {
                                userRole.ModuleRoles.Add(new Core.Domain.Security.ModuleRole
                                {
                                    Module_Id = m.Id,
                                    Module = m,
                                    UserRole_Id = userRole.Id,
                                    UserRole = userRole
                                });
                            }
                        });
                    }
                    userService.UpdateUserRole(userRole);

                    #endregion

                    //step5:
                    #region Ĭ�ϻ������ݱ�

                    #region Ʒ�Ƶ��� Brand
                    var _brandService = EngineContext.Current.Resolve<IBrandService>();

                    List<Brand> brands = _brandService.GetAllBrands(storeId).ToList();

                    if (brands == null || brands.Where(b => b.Name == "ѩ��").Count() == 0)
                    {
                        Brand brand = new Brand()
                        {
                            StoreId = storeId,
                            Name = "ѩ��",
                            Status = true,
                            DisplayOrder = 0,
                            CreatedOnUtc = DateTime.Now
                        };
                        _brandService.InsertBrand(brand);
                    }

                    #endregion

                    #region ��Ʒ��� Category
                    var _categoryService = EngineContext.Current.Resolve<ICategoryService>();
                    List<Category> categorys = _categoryService.GetAllCategories(storeId).ToList();

                    Category category;
                    if (categorys == null || categorys.Where(c => c.Name == "ȫ��").Count() == 0)
                    {
                        category = new Category()
                        {
                            StoreId = storeId,
                            Name = "ȫ��",
                            ParentId = 0,
                            PathCode = "1",
                            StatisticalType = 1,
                            Status = 0,
                            OrderNo = 0,
                            BrandId = null,
                            BrandName = "",
                            Deleted = false,
                            Published = true,
                            PercentageId = null
                        };
                        _categoryService.InsertCategory(category);

                    }
                    else
                    {
                        category = categorys.Where(c => c.Name == "ȫ��").FirstOrDefault();
                    }

                    if (categorys == null || categorys.Where(c => c.Name == "ơ��").Count() == 0)
                    {
                        Category category2 = new Category()
                        {
                            StoreId = storeId,
                            Name = "ơ��",
                            ParentId = category.Id,
                            PathCode = null,
                            StatisticalType = 0,
                            Status = 0,
                            OrderNo = 0,
                            BrandId = null,
                            BrandName = "",
                            Deleted = false,
                            Published = true,
                            PercentageId = null
                        };
                        _categoryService.InsertCategory(category2);
                    }

                    #endregion

                    #region ���� Channel
                    var _channelService = EngineContext.Current.Resolve<IChannelService>();
                    List<Channel> channels = _channelService.GetAll(storeId).ToList();
                    if (channels == null || channels.Where(c => c.Name == "�̳�").Count() == 0)
                    {
                        Channel channel1 = new Channel()
                        {
                            StoreId = storeId,
                            OrderNo = 0,
                            Name = "�̳�",
                            Describe = "�̳�",
                            Attribute = 4,
                            Deleted = false,
                            CreateDate = DateTime.Now
                        };
                        _channelService.InsertChannel(channel1);
                    }

                    if (channels == null || channels.Where(c => c.Name == "����").Count() == 0)
                    {
                        Channel channel2 = new Channel()
                        {
                            StoreId = storeId,
                            OrderNo = 0,
                            Name = "����",
                            Describe = "����",
                            Attribute = 5,
                            Deleted = false,
                            CreateDate = DateTime.Now
                        };
                        _channelService.InsertChannel(channel2);
                    }

                    if (channels == null || channels.Where(c => c.Name == "����").Count() == 0)
                    {
                        Channel channel3 = new Channel()
                        {
                            StoreId = storeId,
                            OrderNo = 0,
                            Name = "����",
                            Describe = "����",
                            Attribute = 2,
                            Deleted = false,
                            CreateDate = DateTime.Now
                        };
                        _channelService.InsertChannel(channel3);
                    }

                    if (channels == null || channels.Where(c => c.Name == "��������").Count() == 0)
                    {
                        Channel channel4 = new Channel()
                        {
                            StoreId = storeId,
                            OrderNo = 0,
                            Name = "��������",
                            Describe = "��������",
                            Attribute = 1,
                            Deleted = false,
                            CreateDate = DateTime.Now
                        };
                        _channelService.InsertChannel(channel4);
                    }

                    #endregion

                    #region Ƭ�� District
                    var _districtService = EngineContext.Current.Resolve<IDistrictService>();
                    List<District> districts = _districtService.GetAll(storeId).ToList();

                    if (districts == null || districts.Where(b => b.Name == "ȫ��").Count() == 0)
                    {
                        District district = new District()
                        {
                            StoreId = storeId,
                            Name = "ȫ��",
                            ParentId = 0,
                            OrderNo = 0,
                            Describe = "",
                            Deleted = false
                        };
                        _districtService.InsertDistrict(district);
                    }

                    #endregion

                    #region ��ӡģ�� PrintTemplate

                    var _printTemplateService = EngineContext.Current.Resolve<IPrintTemplateService>();
                    List<PrintTemplate> printTemplates = _printTemplateService.GetAllPrintTemplates(storeId).ToList();

                    #region ���۶���

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.SaleReservationBill).Count() == 0)
                    {
                        string content1 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; font - family: ����, ���Ŀ���; \">@��������</span></p>    <p style=\"text - align: left; \"><span style=\"font - family: ����, ���ķ���; font - size: 10pt; \">�ͻ���<strong>@�ͻ�����</strong> &nbsp; &nbsp;�ͻ��绰��@�ͻ��绰 &nbsp; �ͻ���ַ��@�ͻ���ַ &nbsp;</span></p>    <p style=\"text - align: left; \"><span style=\"font - family: ����, ���ķ���; font - size: 10pt; \">���ݱ�ţ�@���ݱ�� �Ƶ���@�Ƶ�&nbsp; &nbsp; ���ڣ�@����&nbsp;ҵ��Ա��@ҵ��Ա &nbsp;ҵ��绰��@ҵ��绰</span></p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 729px; \" class=\"table table-bordered\">    <thead>  <tr style=\"height: 20px; \">    <td style=\"width: 35px; height: 20px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \"><strong>&nbsp;</strong></span></td>    <td style=\"width: 162px; height: 20px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \"><strong>��Ʒ����</strong></span></td>    <td style=\"width: 123px; height: 20px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \"><strong>������</strong></span></td>    <td style=\"width: 101px; height: 20px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \"><strong>��λ</strong></span></td>    <td style=\"width: 98px; height: 20px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \"><strong>��λ����</strong></span></td>    <td style=\"width: 65px; height: 20px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \"><strong>����</strong></span></td>    <td style=\"width: 61px; height: 20px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \"><strong>�۸�</strong></span></td>    <td style=\"width: 78px; height: 20px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \"><strong>���</strong></span></td>    <td style=\"width: 88px; height: 20px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \"><strong>��ע</strong></span></td>    </tr>    </thead>    <tbody>    <tr style=\"height: 17px; \">    <td style=\"width: 35px; height: 17px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;#���</span></td>    <td style=\"width: 162px; height: 17px; \"><strong><span style=\"font - family: ����, ���ķ���; \">#��Ʒ����</span></strong></td>    <td style=\"width: 123px; height: 17px; \"><span style=\"font - family: ����, ���ķ���; \">#������</span></td>    <td style=\"width: 101px; height: 17px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \">#��Ʒ��λ</span></td>    <td style=\"width: 98px; height: 17px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \">#��λ����</span></td>    <td style=\"width: 65px; height: 17px; text - align: right; \"><strong><span style=\"font - family: ����, ���ķ���; \">#����</span></strong></td>    <td style=\"width: 61px; height: 17px; text - align: right; \"><span style=\"font - family: ����, ���ķ���; \">#�۸�</span></td>    <td style=\"width: 78px; height: 17px; text - align: right; \"><strong><span style=\"font - family: ����, ���ķ���; \">#���</span></strong></td>    <td style=\"width: 88px; height: 17px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \">#��ע</span></td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 24px; \">    <td style=\"width: 35px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td>    <td style=\"width: 162px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \"><strong>�ܼ�</strong></span></td>    <td style=\"width: 123px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td>    <td style=\"width: 101px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td>    <td style=\"width: 98px; height: 24px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td>    <td style=\"width: 65px; height: 24px; text - align: right; \"><strong><span style=\"font - family: ����, ���ķ���; \">&nbsp;����:###</span></strong></td>    <td style=\"width: 61px; height: 24px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td>    <td style=\"width: 78px; height: 24px; text - align: right; \"><strong><span style=\"font - family: ����, ���ķ���; \">���:###</span></strong></td>    <td style=\"width: 88px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p style=\"text - align: left; \"><span style=\"font - family: ����, ���ķ���; font - size: 10pt; \">��˾��ַ��@��˾��ַ�ල�绰��15802908655</span><span style=\"font - family: ����, ���ķ���; font - size: 10pt; \">&nbsp;</span></p>    <p><span style=\"font - family: ����, ���ķ���; \"><strong>�ͻ��ˣ�&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;�ջ���:</strong>&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;<strong>�տʽ</strong>���ֽ𡾡�΢�š���֧��������ת�ˡ���&nbsp;</span></p>    </div>";

                        PrintTemplate printTemplate1 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.SaleReservationBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.SaleReservationBill),
                            Content = content1
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate1);
                    }

                    #endregion

                    #region ���۵�

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.SaleBill).Count() == 0)
                    {
                        string content2 = "<div id=\"theadid\"><p style=\"text - align: center; \"><span style=\"font - size: 24pt; font - family: ����, ���Ŀ���; \">@��������</span></p><p style=\"text - align: left; \"><span style=\"font - family: ����, ���ķ���; \">�ͻ���@�ͻ����� &nbsp;�ͻ��绰��@�ͻ��绰 &nbsp; �ͻ���ַ��@�ͻ���ַ &nbsp; &nbsp; &nbsp;</span></p><p style=\"text - align: left; \"><span style=\"font - family: ����, ���ķ���; \">���ݱ�ţ�@���ݱ���Ƶ���@�Ƶ�&nbsp; &nbsp;���ڣ�@��������&nbsp;ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰</span></p></div><div id=\"tbodyid\"><table style=\"width: 659px; \" cellpadding=\"0\" class=\"table table-bordered\"><thead><tr style=\"height: 1px; \"><td style=\"width: 36px; height: 1px; \"><span style=\"font - size: 10pt; font - family: ����, ���ķ���; \">&nbsp;</span></td><td style=\"width: 141px; height: 1px; text - align: center; \"><span style=\"font - size: 8pt; font - family: ����, ���ķ���; \"><strong>��Ʒ����</strong></span></td><td style=\"width: 67px; height: 1px; text - align: center; \"><span style=\"font - size: 8pt; font - family: ����, ���ķ���; \"><strong>������</strong></span></td><td style=\"width: 80px; height: 1px; text - align: center; \"><span style=\"font - size: 8pt; font - family: ����, ���ķ���; \"><strong>��λ</strong></span></td><td style=\"width: 118px; height: 1px; text - align: center; \"><span style=\"font - size: 8pt; font - family: ����, ���ķ���; \"><strong>��λ����</strong></span></td><td style=\"width: 93px; height: 1px; text - align: center; \"><span style=\"font - size: 8pt; font - family: ����, ���ķ���; \"><strong>����</strong></span></td><td style=\"width: 75px; height: 1px; text - align: center; \"><span style=\"font - size: 8pt; font - family: ����, ���ķ���; \"><strong>�۸�</strong></span></td><td style=\"width: 91px; height: 1px; text - align: center; \"><span style=\"font - size: 8pt; font - family: ����, ���ķ���; \"><strong>���</strong></span></td><td style=\"width: 100px; height: 1px; text - align: center; \"><span style=\"font - size: 8pt; font - family: ����, ���ķ���; \"><strong>��ע</strong></span></td></tr></thead><tbody><tr style=\"height: 5px; \"><td style=\"width: 36px; height: 5px; text - align: center; \"><span style=\"font - family: ����, ���ķ���; \">#���</span></td><td style=\"width: 141px; height: 5px; text - align: left; \"><p><span style=\"font - family: ����, ���ķ���; \">#��Ʒ����</span></p><p>&nbsp;</p></td><td style=\"width: 67px; height: 5px; \"><span style=\"font - size: 10pt; font - family: ����, ���ķ���; \">#������</span></td><td style=\"width: 80px; height: 5px; text - align: left; \">&nbsp;<span style=\"font - size: 8pt; font - family: ����, ���ķ���; \">#��Ʒ��λ</span></td><td style=\"width: 118px; height: 5px; text - align: left; \"><span style=\"font - size: 10pt; font - family: ����, ���ķ���; \">#��λ����</span></td><td style=\"width: 93px; height: 5px; text - align: right; \"><span style=\"font - size: 10pt; font - family: ����, ���ķ���; \">#����</span></td><td style=\"width: 75px; height: 5px; text - align: right; \"><span style=\"font - size: 10pt; font - family: ����, ���ķ���; \">#�۸�</span></td><td style=\"width: 91px; height: 5px; text - align: right; \"><span style=\"font - size: 10pt; font - family: ����, ���ķ���; \">#���</span></td><td style=\"width: 100px; height: 5px; \"><span style=\"font - size: 10pt; font - family: ����, ���ķ���; \">#��ע</span></td></tr></tbody><tfoot><tr style=\"height: 24px; \"><td style=\"width: 36px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td><td style=\"width: 141px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \"><strong>�ܼ�</strong></span></td><td style=\"width: 67px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td><td style=\"width: 80px; height: 24px; \">&nbsp;</td><td style=\"width: 118px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td><td style=\"width: 93px; height: 24px; text - align: right; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;����:###</span></td><td style=\"width: 75px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td><td style=\"width: 91px; height: 24px; text - align: right; \"><span style=\"font - family: ����, ���ķ���; \">���:###</span></td><td style=\"width: 100px; height: 24px; \"><span style=\"font - family: ����, ���ķ���; \">&nbsp;</span></td></tr></tfoot></table></div><div id=\"tfootid\"><p>&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;&nbsp;</p><p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;�ල�绰��15802908655&nbsp; &nbsp;</p><p>�ͻ��ˣ��ͻ��ˣ�</p><p>&nbsp;</p><div class=\"entry - mod - catalogue\">&nbsp;</div></div>";

                        PrintTemplate printTemplate2 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.SaleBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.SaleBill),
                            Content = content2
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate2);
                    }

                    #endregion

                    #region �˻�����

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.ReturnReservationBill).Count() == 0)
                    {
                        string content3 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ͻ���@�ͻ����� &nbsp; �ֿ⣺@�ֿ� &nbsp; &nbsp;ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰 &nbsp; &nbsp;</p>    <p style=\"text - align: left; \">���ݱ�ţ�@���ݱ�� &nbsp; &nbsp;&nbsp;�������ڣ�@��������</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 729px; \" class=\"table table-bordered\">    <thead>  <tr>    <td style=\"width: 32px; height: 20px; text - align: center; \"><strong>&nbsp;</strong></td>    <td style=\"width: 256px; height: 20px; text - align: center; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 94px; height: 20px; text - align: center; \"><strong>������</strong></td>    <td style=\"width: 50px; height: 20px; text - align: center; \"><strong>��λ</strong></td>    <td style=\"width: 98px; height: 20px; text - align: center; \"><strong>��λ����</strong></td>    <td style=\"width: 58px; height: 20px; text - align: center; \"><strong>����</strong></td>    <td style=\"width: 64px; height: 20px; text - align: center; \"><strong>�۸�</strong></td>    <td style=\"width: 79px; height: 20px; text - align: center; \"><strong>���</strong></td>    <td style=\"width: 80px; height: 20px; text - align: center; \"><strong>��ע</strong></td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 256px; height: 17px; \">#��Ʒ����</td>    <td style=\"width: 94px; height: 17px; \">#������</td>    <td style=\"width: 50px; height: 17px; text - align: center; \">#��Ʒ��λ</td>    <td style=\"width: 98px; height: 17px; \">#��λ����</td>    <td style=\"width: 58px; height: 17px; text - align: right; \">#����</td>    <td style=\"width: 64px; height: 17px; text - align: right; \">#�۸�</td>    <td style=\"width: 79px; height: 17px; text - align: right; \">#���</td>    <td style=\"width: 80px; height: 17px; \">#��ע</td>    </tr>    </tbody>    <tfoot>  <tr>    <td style=\"width: 32px; height: 24px; \">&nbsp;</td>    <td style=\"width: 256px; height: 24px; \"><strong>�ܼ�</strong></td>    <td style=\"width: 94px; height: 24px; \">&nbsp;</td>    <td style=\"width: 50px; height: 24px; \">&nbsp;</td>    <td style=\"width: 98px; height: 24px; \">&nbsp;</td>    <td style=\"width: 58px; height: 24px; text - align: right; \">&nbsp;����:###</td>    <td style=\"width: 64px; height: 24px; \">&nbsp;</td>    <td style=\"width: 79px; height: 24px; text - align: right; \">���:###</td>    <td style=\"width: 80px; height: 24px; \">&nbsp;</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;�����绰��@�����绰 &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;&nbsp;</p>    <p>��ע��@��ע</p>    </div>";

                        PrintTemplate printTemplate3 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.ReturnReservationBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.ReturnReservationBill),
                            Content = content3
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate3);
                    }

                    #endregion

                    #region �˻���

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.ReturnBill).Count() == 0)
                    {
                        string content4 = "<div id=\"theadid\"><p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>  <p style=\"text - align: left; \">�ͻ���@�ͻ����� &nbsp; �ֿ⣺@�ֿ� &nbsp; &nbsp;ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰 &nbsp; &nbsp;</p>  <p style=\"text - align: left; \">���ݱ�ţ�@���ݱ�� &nbsp;&nbsp;�������ڣ�@��������</p>  </div>  <div id=\"tbodyid\"><table style=\"width: 720px; \" class=\"table table-bordered\">  <thead><tr>  <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>  <td style=\"width: 211.656px; height: 20px; text - align: center; \"><strong>��Ʒ����</strong></td>  <td style=\"width: 101.344px; height: 20px; text - align: center; \"><strong>������</strong></td>  <td style=\"width: 43px; height: 20px; text - align: center; \"><strong>��λ</strong></td>  <td style=\"width: 74px; height: 20px; text - align: center; \"><strong>��λ����</strong></td>  <td style=\"width: 49px; height: 20px; text - align: center; \"><strong>����</strong></td>  <td style=\"width: 67px; height: 20px; text - align: center; \"><strong>�۸�</strong></td>  <td style=\"width: 71px; height: 20px; text - align: center; \"><strong>���</strong></td>  <td style=\"width: 89px; height: 20px; text - align: center; \"><strong>��ע</strong></td>  </tr>  </thead>  <tbody>  <tr>  <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>  <td style=\"width: 211.656px; height: 17px; \"><p>#��Ʒ����</p>  <p>#��������</p>  </td>  <td style=\"width: 101.344px; height: 17px; \">#������</td>  <td style=\"width: 43px; height: 17px; text - align: center; \">#��Ʒ��λ</td>  <td style=\"width: 74px; height: 17px; \">#��λ����</td>  <td style=\"width: 49px; height: 17px; text - align: right; \">#����</td>  <td style=\"width: 67px; height: 17px; text - align: right; \">#�۸�</td>  <td style=\"width: 71px; height: 17px; text - align: right; \">#���</td>  <td style=\"width: 89px; height: 17px; \">#��ע</td>  </tr>  </tbody>  <tfoot><tr>  <td style=\"width: 32px; height: 24px; \">&nbsp;</td>  <td style=\"width: 211.656px; height: 24px; \"><strong>�ܼ�</strong></td>  <td style=\"width: 101.344px; height: 24px; \">&nbsp;</td>  <td style=\"width: 43px; height: 24px; \">&nbsp;</td>  <td style=\"width: 74px; height: 24px; \">&nbsp;</td>  <td style=\"width: 49px; height: 24px; text - align: right; \">&nbsp;����:###</td>  <td style=\"width: 67px; height: 24px; \">&nbsp;</td>  <td style=\"width: 71px; height: 24px; text - align: right; \">���:###</td>  <td style=\"width: 89px; height: 24px; \">&nbsp;</td>  </tr>  </tfoot></table>  </div>  <div id=\"tfootid\"><p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>  <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;�����绰��@�����绰 &nbsp;</p>  <p>��ע��@��ע &nbsp;</p>  </div>";

                        PrintTemplate printTemplate4 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.ReturnBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.ReturnBill),
                            Content = content4
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate4);
                    }

                    #endregion

                    #region �����Ի���

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.CarGoodBill).Count() == 0)
                    {
                        string content5 = "<div id=\"theadid\">    <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>       </div>       <div id=\"tbodyid\">    <table style=\"width: 729px; \" class=\"table table-bordered\">         <thead>            <tr style=\"height: 20px; \">           <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>           <td style=\"width: 50px; height: 20px; \"><strong>���ݱ��</strong></td>           <td style=\"width: 50px; height: 20px; \"><strong>��������</strong></td>           <td style=\"width: 50px; height: 20px; \"><strong>�ͻ�</strong></td>           <td style=\"width: 50px; height: 20px; \"><strong>ת��ʱ��</strong></td>           <td style=\"width: 50px; height: 20px; \"><strong>�ֿ�</strong></td>           <td style=\"width: 50px; height: 20px; \"><strong>��Ʒ����</strong></td>           <td style=\"width: 50px; height: 20px; \"><strong>��������</strong></td>           <td style=\"width: 50px; height: 20px; \"><strong>�˶�����</strong></td>           <td style=\"width: 50px; height: 20px; \"><strong>����</strong></td>           <td style=\"width: 50px; height: 20px; \"><strong>�˻�</strong></td>          </tr>         </thead>         <tbody>          <tr style=\"height: 17px; \">           <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>           <td style=\"width: 100px; height: 17px; \">#���ݱ��</td>           <td style=\"width: 100px; height: 17px; \">#��������</td>           <td style=\"width: 100px; height: 17px; \">#�ͻ�</td>           <td style=\"width: 100px; height: 17px; \">#ת��ʱ��</td>           <td style=\"width: 100px; height: 17px; \">#�ֿ�</td>           <td style=\"width: 150px; height: 17px; \">#��Ʒ����</td>           <td style=\"width: 50px; height: 17px; \">#��������</td>           <td style=\"width: 50px; height: 17px; \">#�˶�����</td>           <td style=\"width: 50px; height: 17px; \">#����</td>           <td style=\"width: 50px; height: 17px; \">#�˻�</td>          </tr>         </tbody>         <tfoot>      <tr style=\"height: 17px; \">           <td style=\"width: 32px; height: 17px; \"><strong>&nbsp;</strong></td>           <td style=\"width: 50px; height: 17px; \"><strong>�ϼ�</strong></td>           <td style=\"width: 50px; height: 17px; \"></td>           <td style=\"width: 50px; height: 17px; \"></td>           <td style=\"width: 50px; height: 17px; \"></td>           <td style=\"width: 50px; height: 17px; \"></td>           <td style=\"width: 50px; height: 17px; \"></td>           <td style=\"width: 50px; height: 17px; \">��������:###</td>           <td style=\"width: 50px; height: 17px; \">�˶�����:###</td>           <td style=\"width: 50px; height: 17px; \">����:###</td>           <td style=\"width: 50px; height: 17px; \">�˻�:###</td>          </tr>         </tfoot>    </table>       </div>       <div id=\"tfootid\">   &nbsp;  </div>";

                        PrintTemplate printTemplate5 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.CarGoodBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.CarGoodBill),
                            Content = content5
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate5);
                    }

                    #endregion

                    #region �ɹ�����

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.PurchaseReservationBill).Count() == 0)
                    {
                        string content6 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">��Ӧ�̣�@��Ӧ�� &nbsp;&nbsp; &nbsp;ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰 &nbsp; &nbsp;</p>    <p style=\"text - align: left; \">���ݱ�ţ�@���ݱ�� &nbsp; &nbsp;&nbsp;�������ڣ�@��������</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 720px; \"  class=\"table table-bordered\">  <thead>  <tr>    <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 214px; height: 20px; text - align: center; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 121px; height: 20px; text - align: center; \"><strong>������</strong></td>    <td style=\"width: 62px; height: 20px; text - align: center; \"><strong>��λ</strong></td>    <td style=\"width: 101px; height: 20px; text - align: center; \"><strong>��λ����</strong></td>    <td style=\"width: 74px; height: 20px; text - align: center; \"><strong>����</strong></td>    <td style=\"width: 61px; height: 20px; text - align: center; \"><strong>�۸�</strong></td>    <td style=\"width: 73px; height: 20px; text - align: center; \"><strong>���</strong></td>    <td style=\"width: 63px; height: 20px; text - align: center; \"><strong>��ע</strong></td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 214px; height: 17px; \">#��Ʒ����</td>    <td style=\"width: 121px; height: 17px; \">#������</td>    <td style=\"width: 62px; height: 17px; text - align: center; \">#��Ʒ��λ</td>    <td style=\"width: 101px; height: 17px; text - align: center; \">#��λ����</td>    <td style=\"width: 74px; height: 17px; text - align: right; \">#����</td>    <td style=\"width: 61px; height: 17px; text - align: right; \">#�۸�</td>    <td style=\"width: 73px; height: 17px; text - align: right; \">#���</td>    <td style=\"width: 63px; height: 17px; \">#��ע</td>    </tr>    </tbody>    <tfoot>  <tr>    <td style=\"width: 32px; height: 24px; \">&nbsp;</td>    <td style=\"width: 214px; height: 24px; \"><strong>�ܼ�</strong></td>    <td style=\"width: 121px; height: 24px; \">&nbsp;</td>    <td style=\"width: 62px; height: 24px; \">&nbsp;</td>    <td style=\"width: 101px; height: 24px; \">&nbsp;</td>    <td style=\"width: 74px; height: 24px; text - align: right; \">&nbsp;����:###</td>    <td style=\"width: 61px; height: 24px; \">&nbsp;</td>    <td style=\"width: 73px; height: 24px; text - align: right; \">���:###</td>    <td style=\"width: 63px; height: 24px; \">&nbsp;</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;&nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;�����绰��@�����绰 &nbsp; &nbsp;</p>    <p>��ע��@��ע &nbsp;</p>    </div>";

                        PrintTemplate printTemplate6 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.PurchaseReservationBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.PurchaseReservationBill),
                            Content = content6
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate6);
                    }

                    #endregion

                    #region �ɹ���

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.PurchaseBill).Count() == 0)
                    {
                        string content7 = "<!DOCTYPE html>  <html>  <head>  </head>  <body>  <div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>  <p style=\"text - align: left; \">��Ӧ�̣�@��Ӧ�� &nbsp; ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰 &nbsp; &nbsp;</p>  <p style=\"text - align: left; \">���ݱ�ţ�@���ݱ�� &nbsp; &nbsp; �������ڣ�@�������� &nbsp; &nbsp; &nbsp;�ֿ⣺@�ֿ�</p>  </div>  <div id=\"tbodyid\">  <table style=\"width: 720px; \">  <thead>  <tr style=\"height: 20.179px; \">  <td style=\"width: 32px; height: 20.179px; \"><strong>&nbsp;</strong></td>  <td style=\"width: 216.474px; height: 20.179px; \"><strong>��Ʒ����</strong></td>  <td style=\"width: 118.526px; height: 20.179px; \"><strong>������</strong></td>  <td style=\"width: 54px; height: 20.179px; \"><strong>��λ</strong></td>  <td style=\"width: 81px; height: 20.179px; \"><strong>��λ����</strong></td>  <td style=\"width: 57px; height: 20.179px; \"><strong>����</strong></td>  <td style=\"width: 75px; height: 20.179px; \"><strong>�۸�</strong></td>  <td style=\"width: 79px; height: 20.179px; \"><strong>���</strong></td>  <td style=\"width: 87px; height: 20.179px; \"><strong>��ע</strong></td>  </tr>  </thead>  <tbody>  <tr style=\"height: 17px; \">  <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>  <td style=\"width: 216.474px; height: 17px; \">  <p>#��Ʒ����</p>  <p>#��������</p>  </td>  <td style=\"width: 118.526px; height: 17px; \">#������</td>  <td style=\"width: 54px; height: 17px; text - align: center; \">#��Ʒ��λ</td>  <td style=\"width: 81px; height: 17px; text - align: center; \">#��λ����</td>  <td style=\"width: 57px; height: 17px; text - align: right; \">#����</td>  <td style=\"width: 75px; height: 17px; text - align: right; \">#�۸�</td>  <td style=\"width: 79px; height: 17px; text - align: right; \">#���</td>  <td style=\"width: 87px; height: 17px; \">#��ע</td>  </tr>  </tbody>  <tfoot>  <tr style=\"height: 24px; \">  <td style=\"width: 32px; height: 24px; \">&nbsp;</td>  <td style=\"width: 216.474px; height: 24px; \"><strong>�ܼ�</strong></td>  <td style=\"width: 118.526px; height: 24px; \">&nbsp;</td>  <td style=\"width: 54px; height: 24px; \">&nbsp;</td>  <td style=\"width: 81px; height: 24px; \">&nbsp;</td>  <td style=\"width: 57px; height: 24px; text - align: right; \">����:###&nbsp;</td>  <td style=\"width: 75px; height: 24px; \">&nbsp;</td>  <td style=\"width: 79px; height: 24px; text - align: right; \">���:###</td>  <td style=\"width: 87px; height: 24px; \">&nbsp;</td>  </tr>  </tfoot>  </table>  </div>  <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;&nbsp;</p>  <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;�����绰��@�����绰 &nbsp;</p>  <p>��ע��@��ע</p>  </div>  </body>  </html>";

                        PrintTemplate printTemplate7 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.PurchaseBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.PurchaseBill),
                            Content = content7
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate7);
                    }

                    #endregion

                    #region �ɹ��˻���

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.PurchaseReturnBill).Count() == 0)
                    {
                        string content8 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">��Ӧ�̣�@��Ӧ�� &nbsp; ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰 &nbsp;</p>    <p style=\"text - align: left; \">&nbsp;���ݱ�ţ�@���ݱ�� &nbsp; &nbsp;�������ڣ�@�������� &nbsp; &nbsp;�ֿ⣺@�ֿ�</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 720px; \"  class=\"table table-bordered\">  <thead>  <tr>    <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 213.489px; height: 20px; text - align: center; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 95.5114px; height: 20px; text - align: center; \"><strong>������</strong></td>    <td style=\"width: 43px; height: 20px; text - align: center; \"><strong>��λ</strong></td>    <td style=\"width: 89px; height: 20px; text - align: center; \"><strong>��λ����</strong></td>    <td style=\"width: 54px; height: 20px; text - align: center; \"><strong>����</strong></td>    <td style=\"width: 51px; height: 20px; text - align: center; \"><strong>�۸�</strong></td>    <td style=\"width: 63px; height: 20px; text - align: center; \"><strong>���</strong></td>    <td style=\"width: 85px; height: 20px; text - align: center; \"><strong>��ע</strong></td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 213.489px; height: 17px; \">  <p>#��Ʒ����</p>    <p>#��������</p>    </td>    <td style=\"width: 95.5114px; height: 17px; \">#������</td>    <td style=\"width: 43px; height: 17px; text - align: center; \">#��Ʒ��λ</td>    <td style=\"width: 89px; height: 17px; text - align: center; \">#��λ����</td>    <td style=\"width: 54px; height: 17px; text - align: right; \">#����</td>    <td style=\"width: 51px; height: 17px; text - align: right; \">#�۸�</td>    <td style=\"width: 63px; height: 17px; text - align: right; \">#���</td>    <td style=\"width: 85px; height: 17px; \">#��ע</td>    </tr>    </tbody>    <tfoot>  <tr>    <td style=\"width: 32px; height: 24px; \">&nbsp;</td>    <td style=\"width: 213.489px; height: 24px; \"><strong>�ܼ�</strong></td>    <td style=\"width: 95.5114px; height: 24px; \">&nbsp;</td>    <td style=\"width: 43px; height: 24px; \">&nbsp;</td>    <td style=\"width: 89px; height: 24px; \">&nbsp;</td>    <td style=\"width: 54px; height: 24px; text - align: right; \">&nbsp;����:###</td>    <td style=\"width: 51px; height: 24px; \">&nbsp;</td>    <td style=\"width: 63px; height: 24px; text - align: right; \">���:###</td>    <td style=\"width: 85px; height: 24px; \">&nbsp;</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;&nbsp;</p>    <p>��ע��@��ע&nbsp;</p>    </div>";

                        PrintTemplate printTemplate8 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.PurchaseReturnBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.PurchaseReturnBill),
                            Content = content8
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate8);
                    }

                    #endregion

                    #region ������

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.AllocationBill).Count() == 0)
                    {
                        string content9 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�����ֿ⣺@�����ֿ� &nbsp; &nbsp; ����ֿ⣺@����ֿ� &nbsp;&nbsp;�������ڣ�@��������</p>    <p style=\"text - align: left; \">ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰 &nbsp; &nbsp;���ݱ�ţ�@���ݱ�� &nbsp;&nbsp;</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 720px; \"  class=\"table table-bordered\">  <thead>  <tr style=\"height: 20px; \">    <td style=\"width: 32px; height: 20px; text - align: center; \"><strong>&nbsp;</strong></td>    <td style=\"width: 260.6px; height: 20px; text - align: center; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 96.4px; height: 20px; text - align: center; \"><strong>����</strong></td>    <td style=\"width: 112px; height: 20px; text - align: center; \"><strong>������</strong></td>    <td style=\"width: 44px; height: 20px; text - align: center; \"><strong>��λ</strong></td>    <td style=\"width: 85px; height: 20px; text - align: center; \"><strong>��λ����</strong></td>    <td style=\"width: 70px; height: 20px; text - align: center; \"><strong>����</strong></td>    </tr>    </thead>    <tbody>    <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 260.6px; height: 17px; \">  <p>#��Ʒ����</p>    <p>#��������</p>    </td>    <td style=\"width: 96.4px; height: 17px; \">  <p style=\"text - align: right; \">#����</p>    </td>    <td style=\"width: 112px; height: 17px; \">#������</td>    <td style=\"width: 44px; height: 17px; text - align: center; \">#��Ʒ��λ</td>    <td style=\"width: 85px; height: 17px; text - align: center; \">#��λ����</td>    <td style=\"width: 70px; height: 17px; text - align: right; \">#������</td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; \">&nbsp;</td>    <td style=\"width: 260.6px; height: 17px; \">  <p>�ϼ�</p>    </td>    <td style=\"width: 96.4px; height: 17px; \">  <p style=\"text - align: right; \">����:###</p>    </td>    <td style=\"width: 112px; height: 17px; \">&nbsp;</td>    <td style=\"width: 44px; height: 17px; text - align: center; \">&nbsp;</td>    <td style=\"width: 85px; height: 17px; text - align: center; \">&nbsp;</td>    <td style=\"width: 70px; height: 17px; text - align: right; \">&nbsp;</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ��ע��@��ע &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>�����绰��@�����绰 &nbsp;</p>    </div>";

                        PrintTemplate printTemplate9 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.AllocationBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.AllocationBill),
                            Content = content9
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate9);
                    }

                    #endregion

                    #region �̵�ӯ����

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.InventoryProfitLossBill).Count() == 0)
                    {
                        string content10 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ֿ⣺@�ֿ� &nbsp; &nbsp; �����ˣ�@������ &nbsp; &nbsp;���ݱ�ţ�@���ݱ��</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 720px; \" class=\"table table-bordered\">    <thead>  <tr>    <td style=\"width: 32px; height: 20px; text - align: center; \"><strong>&nbsp;</strong></td>    <td style=\"width: 201.29px; height: 20px; text - align: center; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 70.7102px; height: 20px; text - align: center; \"><strong>��λ</strong></td>    <td style=\"width: 94px; height: 20px; text - align: center; \"><strong>��λ����</strong></td>    <td style=\"width: 56px; height: 20px; text - align: center; \"><strong>����</strong></td>    <td style=\"width: 54px; height: 20px; text - align: center; \"><strong>�ɱ���</strong></td>    <td style=\"width: 130px; height: 20px; text - align: center; \"><strong>�ɱ����</strong></td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 201.29px; height: 17px; \">  <p>#��Ʒ����</p>    <p>#��������</p>    </td>    <td style=\"width: 70.7102px; height: 17px; text - align: center; \">#��Ʒ��λ</td>    <td style=\"width: 94px; height: 17px; \">#��λ����</td>    <td style=\"width: 56px; height: 17px; text - align: right; \">#����</td>    <td style=\"width: 54px; height: 17px; text - align: right; \">#�ɱ���</td>    <td style=\"width: 130px; height: 17px; text - align: right; \">#�ɱ����</td>    </tr>    </tbody>    <tfoot>  <tr>    <td style=\"width: 32px; height: 24px; \">&nbsp;</td>    <td style=\"width: 201.29px; height: 24px; \"><strong>�ܼ�</strong></td>    <td style=\"width: 70.7102px; height: 24px; \">&nbsp;</td>    <td style=\"width: 94px; height: 24px; \">&nbsp;</td>    <td style=\"width: 56px; height: 24px; \">&nbsp;</td>    <td style=\"width: 54px; height: 24px; \">&nbsp;</td>    <td style=\"width: 130px; height: 24px; text - align: right; \">&nbsp;���:###</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;&nbsp;�����绰��@�����绰 &nbsp;</p>    <p>��ע��@��ע &nbsp; &nbsp; &nbsp;</p>    </div>";

                        PrintTemplate printTemplate10 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.InventoryProfitLossBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.InventoryProfitLossBill),
                            Content = content10
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate10);
                    }

                    #endregion

                    #region �ɱ����۵�

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.CostAdjustmentBill).Count() == 0)
                    {
                        string content11 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ֿ⣺@�ֿ� &nbsp; &nbsp; �����ˣ�@������ &nbsp; &nbsp;���ݱ�ţ�@���ݱ��</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 720px; \" class=\"table table-bordered\">    <thead>  <tr>    <td style=\"width: 32px; height: 20px; text - align: center; \"><strong>&nbsp;</strong></td>    <td style=\"width: 201.29px; height: 20px; text - align: center; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 70.7102px; height: 20px; text - align: center; \"><strong>��λ</strong></td>    <td style=\"width: 94px; height: 20px; text - align: center; \"><strong>��λ����</strong></td>    <td style=\"width: 56px; height: 20px; text - align: center; \"><strong>����</strong></td>    <td style=\"width: 54px; height: 20px; text - align: center; \"><strong>�ɱ���</strong></td>    <td style=\"width: 130px; height: 20px; text - align: center; \"><strong>�ɱ����</strong></td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 201.29px; height: 17px; \">  <p>#��Ʒ����</p>    <p>#��������</p>    </td>    <td style=\"width: 70.7102px; height: 17px; text - align: center; \">#��Ʒ��λ</td>    <td style=\"width: 94px; height: 17px; \">#��λ����</td>    <td style=\"width: 56px; height: 17px; text - align: right; \">#����</td>    <td style=\"width: 54px; height: 17px; text - align: right; \">#�ɱ���</td>    <td style=\"width: 130px; height: 17px; text - align: right; \">#�ɱ����</td>    </tr>    </tbody>    <tfoot>  <tr>    <td style=\"width: 32px; height: 24px; \">&nbsp;</td>    <td style=\"width: 201.29px; height: 24px; \"><strong>�ܼ�</strong></td>    <td style=\"width: 70.7102px; height: 24px; \">&nbsp;</td>    <td style=\"width: 94px; height: 24px; \">&nbsp;</td>    <td style=\"width: 56px; height: 24px; \">&nbsp;</td>    <td style=\"width: 54px; height: 24px; \">&nbsp;</td>    <td style=\"width: 130px; height: 24px; text - align: right; \">&nbsp;���:###</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;&nbsp;�����绰��@�����绰 &nbsp;</p>    <p>��ע��@��ע &nbsp; &nbsp; &nbsp;</p>    </div>";

                        PrintTemplate printTemplate11 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.CostAdjustmentBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.CostAdjustmentBill),
                            Content = content11
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate11);
                    }

                    #endregion

                    #region ����

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.ScrapProductBill).Count() == 0)
                    {
                        string content12 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ֿ⣺@�ֿ� &nbsp; &nbsp; �����ˣ�@������ &nbsp; &nbsp;���ݱ�ţ�@���ݱ��</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 722px; \" class=\"table table-bordered\">    <thead>  <tr>    <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 214px; height: 20px; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 52px; height: 20px; \"><strong>��λ</strong></td>    <td style=\"width: 91px; height: 20px; \"><strong>��λ����</strong></td>    <td style=\"width: 66px; height: 20px; \"><strong>����</strong></td>    <td style=\"width: 69px; height: 20px; \"><strong>�ɱ���</strong></td>    <td style=\"width: 115px; height: 20px; \"><strong>�ɱ����</strong></td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 214px; height: 17px; \">  <p>#��Ʒ����</p>    <p>#��������</p>    </td>    <td style=\"width: 52px; height: 17px; text - align: center; \">#��Ʒ��λ</td>    <td style=\"width: 91px; height: 17px; text - align: center; \">#��λ����</td>    <td style=\"width: 66px; height: 17px; text - align: right; \">#����</td>    <td style=\"width: 69px; height: 17px; text - align: right; \">#�ɱ���</td>    <td style=\"width: 115px; height: 17px; text - align: right; \">#�ɱ����</td>    </tr>    </tbody>    <tfoot>  <tr>    <td style=\"width: 32px; height: 24px; \">&nbsp;</td>    <td style=\"width: 214px; height: 24px; \"><strong>�ܼ�</strong></td>    <td style=\"width: 52px; height: 24px; \">&nbsp;</td>    <td style=\"width: 91px; height: 24px; \">&nbsp;</td>    <td style=\"width: 66px; height: 24px; text - align: right; \">����:###&nbsp;</td>    <td style=\"width: 69px; height: 24px; \">&nbsp;</td>    <td style=\"width: 115px; height: 24px; text - align: right; \">&nbsp;���:###</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ��ע��@��ע &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>�����绰��@�����绰 &nbsp;</p>    </div>";

                        PrintTemplate printTemplate12 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.ScrapProductBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.ScrapProductBill),
                            Content = content12
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate12);
                    }

                    #endregion

                    #region �̵��������֣�

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.InventoryAllTaskBill).Count() == 0)
                    {
                        string content13 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ֿ⣺@�ֿ� &nbsp; &nbsp;&nbsp;�̵���Ա��@�̵���Ա</p>    <p style=\"text - align: left; \">ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰 &nbsp; &nbsp;���ݱ�ţ�@���ݱ��</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 727px; \"  class=\"table table-bordered\">  <thead>  <tr>    <td style=\"width: 32px; height: 20px; text - align: center; \">&nbsp;</td>    <td style=\"width: 187px; height: 20px; text - align: center; \">��Ʒ����</td>    <td style=\"width: 91px; height: 20px; text - align: center; \">��λ����</td>    <td style=\"width: 94px; height: 20px; text - align: center; \">��ǰ�������</td>    <td style=\"width: 102px; height: 20px; text - align: center; \">�̵���������</td>    <td style=\"width: 68px; height: 20px; text - align: center; \">��ӯ����</td>    <td style=\"width: 84px; height: 20px; text - align: center; \">�̿�����</td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 187px; height: 17px; \">  <p>#��Ʒ����</p>    <p>#��������</p>    </td>    <td style=\"width: 91px; height: 17px; text - align: center; \">#��λ����</td>    <td style=\"width: 94px; height: 17px; text - align: right; \">#��ǰ�������</td>    <td style=\"width: 102px; height: 17px; text - align: right; \">#�̵�������</td>    <td style=\"width: 68px; height: 17px; text - align: right; \">#��ӯ����</td>    <td style=\"width: 84px; height: 17px; text - align: right; \">#�̿�����</td>    </tr>    </tbody>    </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ��ע��@��ע &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>�����绰��@�����绰 &nbsp;</p>    </div>";

                        PrintTemplate printTemplate13 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.InventoryAllTaskBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.InventoryAllTaskBill),
                            Content = content13
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate13);
                    }

                    #endregion

                    #region �̵����񣨲��֣�

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.InventoryPartTaskBill).Count() == 0)
                    {
                        string content14 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ֿ⣺@�ֿ� &nbsp; &nbsp;&nbsp;�̵���Ա��@�̵���Ա</p>    <p style=\"text - align: left; \">ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰 &nbsp; &nbsp;���ݱ�ţ�@���ݱ��</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 727px; \"  class=\"table table-bordered\">  <thead>  <tr>    <td style=\"width: 32px; height: 20px; text - align: center; \">&nbsp;</td>    <td style=\"width: 187px; height: 20px; text - align: center; \">��Ʒ����</td>    <td style=\"width: 91px; height: 20px; text - align: center; \">��λ����</td>    <td style=\"width: 94px; height: 20px; text - align: center; \">��ǰ�������</td>    <td style=\"width: 102px; height: 20px; text - align: center; \">�̵���������</td>    <td style=\"width: 68px; height: 20px; text - align: center; \">��ӯ����</td>    <td style=\"width: 84px; height: 20px; text - align: center; \">�̿�����</td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 187px; height: 17px; \">  <p>#��Ʒ����</p>    <p>#��������</p>    </td>    <td style=\"width: 91px; height: 17px; text - align: center; \">#��λ����</td>    <td style=\"width: 94px; height: 17px; text - align: right; \">#��ǰ�������</td>    <td style=\"width: 102px; height: 17px; text - align: right; \">#�̵�������</td>    <td style=\"width: 68px; height: 17px; text - align: right; \">#��ӯ����</td>    <td style=\"width: 84px; height: 17px; text - align: right; \">#�̿�����</td>    </tr>    </tbody>    </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ��ע��@��ע &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>�����绰��@�����绰 &nbsp;</p>    </div>";

                        PrintTemplate printTemplate14 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.InventoryPartTaskBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.InventoryPartTaskBill),
                            Content = content14
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate14);
                    }

                    #endregion

                    #region ��ϵ�

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.CombinationProductBill).Count() == 0)
                    {
                        string content15 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ֿ⣺@�ֿ� &nbsp; ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ����Ʒ��@����Ʒ &nbsp; ������@���� &nbsp;���ݱ�ţ�@���ݱ��</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 729px; \" class=\"table table-bordered\">    <thead>  <tr>    <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 153px; height: 20px; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 105px; height: 20px; \"><strong>����Ʒ/����Ʒ</strong></td>    <td style=\"width: 58px; height: 20px; \"><strong>��������</strong></td>    <td style=\"width: 56px; height: 20px; \"><strong>����</strong></td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 153px; height: 17px; \">#��Ʒ����</td>    <td style=\"width: 105px; height: 17px; \">#����Ʒ/����Ʒ</td>    <td style=\"width: 58px; height: 17px; \">#��������</td>    <td style=\"width: 56px; height: 17px; \">#����</td>    </tr>    </tbody>    </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;�����绰��@�����绰 &nbsp;&nbsp;</p>    <p>��ע��@��ע</p>    <p>&nbsp;</p>    </div>";

                        PrintTemplate printTemplate15 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.CombinationProductBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.CombinationProductBill),
                            Content = content15
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate15);
                    }

                    #endregion

                    #region ��ֵ�

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.SplitProductBill).Count() == 0)
                    {
                        string content16 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ֿ⣺@�ֿ� &nbsp; ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ����Ʒ��@����Ʒ &nbsp; ������@���� &nbsp;���ݱ�ţ�@���ݱ��</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 729px; \" class=\"table table-bordered\">    <thead>  <tr>    <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 153px; height: 20px; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 105px; height: 20px; \"><strong>����Ʒ/����Ʒ</strong></td>    <td style=\"width: 58px; height: 20px; \"><strong>��������</strong></td>    <td style=\"width: 56px; height: 20px; \"><strong>����</strong></td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 153px; height: 17px; \">#��Ʒ����</td>    <td style=\"width: 105px; height: 17px; \">#����Ʒ/����Ʒ</td>    <td style=\"width: 58px; height: 17px; \">#��������</td>    <td style=\"width: 56px; height: 17px; \">#����</td>    </tr>    </tbody>    </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;�����绰��@�����绰 &nbsp;&nbsp;</p>    <p>��ע��@��ע</p>    <p>&nbsp;</p>    </div>";

                        PrintTemplate printTemplate16 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.SplitProductBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.SplitProductBill),
                            Content = content16
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate16);
                    }

                    #endregion

                    #region �տ

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.CashReceiptBill).Count() == 0)
                    {
                        string content17 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ͻ���@�ͻ����� &nbsp; ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰 &nbsp; &nbsp;���ݱ�ţ�@���ݱ��</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 728px; \" class=\"table table-bordered\">    <thead>  <tr style=\"height: 20px; \">    <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 125px; height: 20px; text - align: center; \"><strong>���ݱ��</strong></td>    <td style=\"width: 82px; height: 20px; text - align: center; \"><strong>��������</strong></td>    <td style=\"width: 110px; height: 20px; text - align: center; \"><strong>����ʱ��</strong></td>    <td style=\"width: 122px; height: 20px; text - align: center; \"><strong>���ݽ��</strong></td>    <td style=\"width: 77px; height: 20px; text - align: center; \"><strong>����</strong></td>    <td style=\"width: 88px; height: 20px; text - align: center; \"><strong>��Ƿ</strong></td>    <td style=\"width: 93px; height: 20px; text - align: center; \"><strong>�����տ�</strong></td>    </tr>    </thead>    <tbody>    <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 125px; height: 17px; \">#���ݱ��</td>    <td style=\"width: 82px; height: 17px; \">#��������</td>    <td style=\"width: 110px; height: 17px; \">#����ʱ��</td>    <td style=\"width: 122px; height: 17px; text - align: right; \">#���ݽ��</td>    <td style=\"width: 77px; height: 17px; text - align: right; \">#���ս��</td>    <td style=\"width: 88px; height: 17px; text - align: right; \">#��Ƿ���</td>    <td style=\"width: 93px; height: 17px; text - align: right; \">#�����տ���</td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 24px; \">    <td style=\"width: 32px; height: 24px; \">&nbsp;</td>    <td style=\"width: 125px; height: 24px; \"><strong>�ܼ�</strong></td>    <td style=\"width: 82px; height: 24px; \">&nbsp;</td>    <td style=\"width: 110px; height: 24px; \">&nbsp;</td>    <td style=\"width: 122px; height: 24px; text - align: right; \">&nbsp;���ݽ��:###</td>    <td style=\"width: 77px; height: 24px; text - align: right; \">&nbsp;���ս��:###</td>    <td style=\"width: 88px; height: 24px; text - align: right; \">��Ƿ���:###</td>    <td style=\"width: 93px; height: 24px; text - align: right; \">�����տ���:###</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ��ע��@��ע &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>    <p>�����绰��@�����绰 &nbsp;</p>    </div>";

                        PrintTemplate printTemplate17 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.CashReceiptBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.CashReceiptBill),
                            Content = content17
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate17);
                    }

                    #endregion

                    #region ���

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.PaymentReceiptBill).Count() == 0)
                    {
                        string content18 = "<!DOCTYPE html>  <html>  <head>  </head>  <body>  <div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>  <p style=\"text - align: left; \">�ͻ���@�ͻ����� &nbsp; ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; ҵ��绰��@ҵ��绰 &nbsp; &nbsp;���ݱ�ţ�@���ݱ��</p>  </div>  <div id=\"tbodyid\">  <table style=\"width: 720px; \">  <thead>  <tr>  <td style=\"width: 32px; height: 20px; text - align: center; \"><strong>&nbsp;</strong></td>  <td style=\"width: 123px; height: 20px; text - align: center; \"><strong>���ݱ��</strong></td>  <td style=\"width: 78px; height: 20px; text - align: center; \"><strong>��������</strong></td>  <td style=\"width: 104.23px; height: 20px; text - align: center; \"><strong>����ʱ��</strong></td>  <td style=\"width: 82.7699px; height: 20px; text - align: center; \"><strong>���ݽ��</strong></td>  <td style=\"width: 77px; height: 20px; text - align: center; \"><strong>�Ѹ�</strong></td>  <td style=\"width: 85px; height: 20px; text - align: center; \"><strong>��Ƿ</strong></td>  <td style=\"width: 134px; height: 20px; text - align: center; \"><strong>���θ���</strong></td>  </tr>  </thead>  <tbody>  <tr>  <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>  <td style=\"width: 123px; height: 17px; \">#���ݱ��</td>  <td style=\"width: 78px; height: 17px; \">#��������</td>  <td style=\"width: 104.23px; height: 17px; \">#����ʱ��</td>  <td style=\"width: 82.7699px; height: 17px; text - align: right; \">#���ݽ��</td>  <td style=\"width: 77px; height: 17px; text - align: right; \">#���ս��</td>  <td style=\"width: 85px; height: 17px; text - align: right; \">#��Ƿ���</td>  <td style=\"width: 134px; height: 17px; text - align: right; \">#�����տ���</td>  </tr>  </tbody>  <tfoot>  <tr>  <td style=\"width: 32px; height: 24px; \">&nbsp;</td>  <td style=\"width: 123px; height: 24px; \"><strong>�ܼ�</strong></td>  <td style=\"width: 78px; height: 24px; \">&nbsp;</td>  <td style=\"width: 104.23px; height: 24px; \">&nbsp;</td>  <td style=\"width: 82.7699px; height: 24px; \">���ݽ��:###</td>  <td style=\"width: 77px; height: 24px; text - align: right; \">���ս��:###</td>  <td style=\"width: 85px; height: 24px; text - align: right; \">��Ƿ���:###</td>  <td style=\"width: 134px; height: 24px; text - align: right; \">�����տ���:###</td>  </tr>  </tfoot>  </table>  </div>  <div id=\"tfootid\">  <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ڣ�@���� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ��ע��@��ע &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>  <p>��ַ��@��˾��ַ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</p>  <p>�����绰��@�����绰 &nbsp;</p>  </div>  </body>  </html>";

                        PrintTemplate printTemplate18 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.PaymentReceiptBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.PaymentReceiptBill),
                            Content = content18
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate18);
                    }

                    #endregion

                    #region Ԥ�տ�

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.AdvanceReceiptBill).Count() == 0)
                    {
                        string content19 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 720px; \" class=\"table table-bordered\">    <thead>  <tr>    <td style=\"width: 63px; height: 20px; \">�ͻ�</td>    <td style=\"width: 93px; height: 20px; \">#�ͻ�</td>    <td style=\"width: 69px; height: 20px; \">ҵ��Ա</td>    <td style=\"width: 106.011px; height: 20px; \">#ҵ��Ա</td>    <td style=\"width: 96.9886px; height: 20px; \">�տ�����</td>    <td style=\"width: 112.727px; height: 20px; \" colspan=\"2\">#�տ�����</td>    <td style=\"width: 65px; height: 20px; \">Ԥ�տ�</td>    <td style=\"width: 125px; height: 20px; \">#Ԥ�տ��˻�</td>    </tr>    </thead>    <tbody>    <tr>    <td style=\"width: 63px; height: 17px; \">&nbsp;</td>    <td style=\"width: 93px; height: 17px; \">&nbsp;</td>    <td style=\"width: 69px; height: 17px; \">Ԥ�տ���</td>    <td style=\"width: 106.011px; height: 17px; \">#Ԥ�տ���</td>    <td style=\"width: 96.9886px; height: 17px; \">��ע</td>    <td style=\"width: 302.727px; height: 17px; \" colspan=\"4\">&nbsp;#��ע</td>    </tr>    </tbody>    </table>    </div>    <div id=\"tfootid\">  <p>&nbsp;</p>    [�Żݣ�@�Żݽ�� ]&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;֧����Ϣ��@֧����Ϣ&nbsp;</div>";

                        PrintTemplate printTemplate19 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.AdvanceReceiptBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.AdvanceReceiptBill),
                            Content = content19
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate19);
                    }

                    #endregion

                    #region Ԥ����

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.AdvancePaymentBill).Count() == 0)
                    {
                        string content20 = "<!DOCTYPE html>  <html>  <head>  </head>  <body>  <div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>  </div>  <div id=\"tbodyid\">  <table style=\"width: 720px; \">  <thead>  <tr style=\"height: 20.0881px; \">  <td style=\"width: 56.821px; height: 20.0881px; \">&nbsp;��Ӧ��</td>  <td style=\"width: 127.179px; height: 20.0881px; \">#��Ӧ��</td>  <td style=\"width: 62px; height: 20.0881px; \">ҵ��Ա</td>  <td style=\"width: 102px; height: 20.0881px; \">#ҵ��Ա</td>  <td style=\"width: 87px; height: 20.0881px; \">��������</td>  <td style=\"width: 114.545px; height: 20.0881px; \" colspan=\"2\">#��������</td>  <td style=\"width: 50px; height: 20.0881px; \">Ԥ����</td>  <td style=\"width: 131px; height: 20.0881px; \">#Ԥ����</td>  </tr>  </thead>  <tbody>  <tr style=\"height: 17px; \">  <td style=\"width: 56.821px; height: 17px; \">�����˻�</td>  <td style=\"width: 127.179px; height: 17px; \">#�����˻�</td>  <td style=\"width: 62px; height: 17px; \">������</td>  <td style=\"width: 102px; height: 17px; \">#������</td>  <td style=\"width: 87px; height: 17px; \">��ע</td>  <td style=\"width: 295.545px; height: 17px; \" colspan=\"4\">&nbsp;#��ע</td>  </tr>  </tbody>  </table>  </div>  <div id=\"tfootid\">&nbsp;</div>  </body>  </html>";

                        PrintTemplate printTemplate20 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.AdvancePaymentBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.AdvancePaymentBill),
                            Content = content20
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate20);
                    }

                    #endregion

                    #region ����֧����

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.CostExpenditureBill).Count() == 0)
                    {
                        string content21 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 32px; \">����֧����</span></p>    <p style=\"text - align: left; \">&nbsp;ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;��ӡʱ�䣺@��ӡʱ�� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ݱ�ţ�@���ݱ�� &nbsp; &nbsp;&nbsp;</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 728px; \" class=\"table table-bordered\">    <thead>  <tr style=\"height: 20.2px; \">    <td style=\"width: 32px; height: 20.2px; text - align: center; \"><strong>&nbsp;</strong></td>    <td style=\"width: 256px; height: 20.2px; text - align: center; \">�������</td>    <td style=\"width: 150px; height: 20.2px; text - align: center; \">���</td>    <td style=\"width: 147px; height: 20.2px; text - align: center; \">�ͻ�</td>    <td style=\"width: 121px; height: 20.2px; text - align: center; \">��ע</td>    </tr>    </thead>    <tbody>    <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; text - align: left; \">&nbsp;#���</td>    <td style=\"width: 256px; height: 17px; text - align: center; \">#�������</td>    <td style=\"width: 150px; height: 17px; text - align: right; \">#���</td>    <td style=\"width: 147px; height: 17px; text - align: center; \">#�ͻ�</td>    <td style=\"width: 121px; height: 17px; \">#��ע</td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 24px; \">    <td style=\"width: 32px; height: 24px; \">&nbsp;</td>    <td style=\"width: 256px; height: 24px; \">�ܼ�</td>    <td style=\"width: 150px; height: 24px; text - align: right; \">&nbsp;@�ϼ�</td>    <td style=\"width: 147px; height: 24px; text - align: right; \">&nbsp;</td>    <td style=\"width: 121px; height: 24px; \">&nbsp;</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�������ڣ�@�������� &nbsp; &nbsp; &nbsp; &nbsp; ���ʽ��@���ʽ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;֧����@֧����� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ���ڣ�@����</p>    <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ��ע��@��ע &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;����ˣ�@�����</p>    </div>";

                        PrintTemplate printTemplate21 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.CostExpenditureBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.CostExpenditureBill),
                            Content = content21
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate21);
                    }

                    #endregion

                    #region ��������

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.FinancialIncomeBill).Count() == 0)
                    {
                        string content22 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 32px; \">��������</span></p>    <p style=\"text - align: left; \">&nbsp;ҵ��Ա��@ҵ��Ա &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;��ӡʱ�䣺@��ӡʱ�� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;���ݱ�ţ�@���ݱ�� &nbsp; &nbsp;&nbsp;</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 728px; \" class=\"table table-bordered\">    <thead>  <tr style=\"height: 20.2px; \">    <td style=\"width: 32px; height: 20.2px; text - align: center; \"><strong>&nbsp;</strong></td>    <td style=\"width: 256px; height: 20.2px; text - align: center; \">�������</td>    <td style=\"width: 150px; height: 20.2px; text - align: center; \">���</td>    <td style=\"width: 147px; height: 20.2px; text - align: center; \">�ͻ�/��Ӧ��</td>    <td style=\"width: 121px; height: 20.2px; text - align: center; \">��ע</td>    </tr>    </thead>    <tbody>    <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; text - align: left; \">&nbsp;#���</td>    <td style=\"width: 256px; height: 17px; text - align: center; \">#�������</td>    <td style=\"width: 150px; height: 17px; text - align: right; \">#���</td>    <td style=\"width: 147px; height: 17px; text - align: center; \">#�ͻ�/��Ӧ��</td>    <td style=\"width: 121px; height: 17px; \">#��ע</td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 24px; \">    <td style=\"width: 32px; height: 24px; \">&nbsp;</td>    <td style=\"width: 256px; height: 24px; \">�ܼ�</td>    <td style=\"width: 150px; height: 24px; text - align: right; \">&nbsp;@�ϼ�</td>    <td style=\"width: 147px; height: 24px; text - align: right; \">&nbsp;</td>    <td style=\"width: 121px; height: 24px; \">&nbsp;</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�������ڣ�@�������� &nbsp; &nbsp; &nbsp; &nbsp; ���ʽ��@���ʽ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;�����@������ &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ���ڣ�@����</p>    <p>�Ƶ���@�Ƶ� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ��ע��@��ע &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;����ˣ�@�����</p>    </div>";

                        PrintTemplate printTemplate22 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.FinancialIncomeBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.FinancialIncomeBill),
                            Content = content22
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate22);
                    }

                    #endregion

                    #region ����װ����

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.AllLoadBill).Count() == 0)
                    {
                        string content23 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ֿ⣺@�ֿ� &nbsp; ������@���� &nbsp; &nbsp; ҵ��Ա��@ҵ��Ա</p>    <p style=\"text - align: left; \">������ţ�@�������</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 729px; \" class=\"table table-bordered\">    <thead>  <tr style=\"height: 20px; \">    <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 300px; height: 20px; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 105px; height: 20px; \"><strong>����</strong></td>    </tr>    </thead>    <tbody>    <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 300px; height: 17px; \">#��Ʒ����</td>    <td style=\"width: 105px; height: 17px; \">#����</td>    </tr>    </tbody>    </table>    </div>    <div id=\"tfootid\">&nbsp;</div>";

                        PrintTemplate printTemplate23 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.AllLoadBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.AllLoadBill),
                            Content = content23
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate23);
                    }

                    #endregion

                    #region ����װ����

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.ZeroLoadBill).Count() == 0)
                    {
                        string content24 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ֿ⣺@�ֿ� &nbsp; ������@���� &nbsp; &nbsp; ҵ��Ա��@ҵ��Ա</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 729px; \" class=\"table table-bordered\">    <thead>  <tr style=\"height: 20px; \">    <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 153px; height: 20px; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 105px; height: 20px; \"><strong>����</strong></td>    </tr>    </thead>    <tbody>    <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 153px; height: 17px; \">#��Ʒ����</td>    <td style=\"width: 105px; height: 17px; \">#����</td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 153px; height: 17px; \"><strong>�ϼ�</strong></td>    <td style=\"width: 105px; height: 17px; \">����(����С):###</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">&nbsp;</div>";

                        PrintTemplate printTemplate24 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.ZeroLoadBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.ZeroLoadBill),
                            Content = content24
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate24);
                    }

                    #endregion

                    #region �������ϲ���

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.ZeroLoadBill).Count() == 0)
                    {
                        string content25 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">�ֿ⣺@�ֿ� &nbsp; ������@���� &nbsp; &nbsp; ҵ��Ա��@ҵ��Ա</p>    <p style=\"text - align: left; \">������ţ�@�������</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 729px; \" class=\"table table-bordered\">    <thead>  <tr style=\"height: 20px; \">    <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 300px; height: 20px; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 105px; height: 20px; \"><strong>����</strong></td>    </tr>    </thead>    <tbody>    <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 300px; height: 17px; \">#��Ʒ����</td>    <td style=\"width: 105px; height: 17px; \">#����</td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 153px; height: 17px; \"><strong>�ϼ�</strong></td>    <td style=\"width: 105px; height: 17px; \">����(����С):###</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">&nbsp;</div>";

                        PrintTemplate printTemplate25 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.AllZeroMergerBill,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.AllZeroMergerBill),
                            Content = content25
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate25);
                    }

                    #endregion

                    #region ����ƾ֤

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.AccountingVoucher).Count() == 0)
                    {
                        string content26 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 24pt; \">@��������</span></p>    <p style=\"text - align: left; \">���ڣ�@���� &nbsp; &nbsp;���ݱ�ţ�@���ݱ�� &nbsp; &nbsp; ���ɷ�ʽ��@���ɷ�ʽ &nbsp; &nbsp; ƾ֤�ţ�@ƾ֤��</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 728px; \" class=\"table table-bordered\">    <thead>  <tr style=\"height: 20px; \">    <td style=\"width: 32px; height: 20px; \"><strong>&nbsp;</strong></td>    <td style=\"width: 125px; height: 20px; text - align: center; \"><strong>ժҪ</strong></td>    <td style=\"width: 82px; height: 20px; text - align: center; \"><strong>��Ŀ</strong></td>    <td style=\"width: 110px; height: 20px; text - align: center; \"><strong>�跽���</strong></td>    <td style=\"width: 122px; height: 20px; text - align: center; \"><strong>�������</strong></td>    </tr>    </thead>    <tbody>    <tr style=\"height: 17px; \">    <td style=\"width: 32px; height: 17px; \">&nbsp;#���</td>    <td style=\"width: 125px; height: 17px; \">#ժҪ</td>    <td style=\"width: 82px; height: 17px; \">#��Ŀ</td>    <td style=\"width: 110px; height: 17px; text - align: right; \">#�跽���</td>    <td style=\"width: 122px; height: 17px; text - align: right; \">#�������</td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 24px; \">    <td style=\"width: 32px; height: 24px; \">&nbsp;</td>    <td style=\"width: 125px; height: 24px; text - align:center\" colspan=\"2\"><strong>�ϼ�:&nbsp;�ϼ��ܽ��:###</strong></td>    <td style=\"width: 122px; height: 24px; text - align: right; \">&nbsp;�跽�ܽ��:###</td>    <td style=\"width: 77px; height: 24px; text - align: right; \">&nbsp;�����ܽ��:###</td>    </tr>    </tfoot>  </table>    </div>    <div id=\"tfootid\">  <p>�Ƶ��ˣ�@�Ƶ��� &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;����ˣ�@�����</p>    </div>";

                        PrintTemplate printTemplate26 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 0,
                            BillType = (int)BillTypeEnum.AccountingVoucher,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.AccountingVoucher),
                            Content = content26
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate26);
                    }

                    #endregion

                    #region ����

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.StockReport).Count() == 0)
                    {
                        string content27 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 20pt; \">����</span></p>    <p style=\"text - align: left; \">�ֿ⣺�ֿ�@&nbsp;&nbsp;��Ʒ���ƣ���Ʒ����@&nbsp;&nbsp;��Ʒ�����Ʒ���@&nbsp;&nbsp;</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 729px; \" cellpadding=\"0\" class=\"table table-bordered\">    <thead>  <tr style=\"height: 1px; \">    <td style=\"width: 36px; height: 1px; \"><strong>���</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��Ʒ���</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��Ʒ���</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>������</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>ʵ�ʿ������</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��λ����</strong></td>    </tr>    </thead>    <tbody>    <tr style=\"height: 5px; \">    <td style=\"width: 36px; height: 1px; \">���#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��Ʒ���#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��Ʒ���#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��Ʒ����#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">������#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">ʵ�ʿ������#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��λ����#</td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 24px; \">    <td style=\"width: 36px; height: 1px; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�ϼ�</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">ʵ�ʿ������##</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">&nbsp;</td>    </tr>    </tfoot>  </table>    </div>";

                        PrintTemplate printTemplate27 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 1,
                            BillType = (int)BillTypeEnum.StockReport,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.StockReport),
                            Content = content27
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate27);
                    }

                    #endregion

                    #region ���ۻ���(�ͻ�/��Ʒ)

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.SaleSummeryReport).Count() == 0)
                    {
                        string content28 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 20pt; \">���ۻ���(�ͻ�/��Ʒ)</span></p>    <p style=\"text - align: left; \">�������ڣ���������@&nbsp;&nbsp;�ֿ⣺�ֿ�@&nbsp;&nbsp;��Ʒ���ƣ���Ʒ����@&nbsp;&nbsp;��Ʒ�����Ʒ���@&nbsp;&nbsp;</p>    <p style=\"text - align: left; \">ҵ��Ա��ҵ��Ա@&nbsp;&nbsp;�ͻ����ͻ�@</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 729px; \" cellpadding=\"0\" class=\"table table-bordered\">    <thead>  <tr style=\"height: 1px; \">    <td style=\"width: 36px; height: 1px; \"><strong>���</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�ͻ�����</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��������</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>���۽��</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�˻�����</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�˻����</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��������</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�������</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>������</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�ܽ��</strong></td>    </tr>    </thead>    <tbody>    <tr style=\"height: 5px; \">    <td style=\"width: 36px; height: 1px; \">���#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">�ͻ�����#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��Ʒ����#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��������#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">���۽��#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">�˻�����#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">�˻����#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��������#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">�������#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">������#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">�ܽ��#</td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 24px; \">    <td style=\"width: 36px; height: 1px; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�ܼ�</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��������##</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">���۽��##</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">�˻�����##</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">�˻����##</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��������##</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">�������##</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">������##</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">�ܽ��##</td>    </tr>    </tfoot>  </table>    </div>";

                        PrintTemplate printTemplate28 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 1,
                            BillType = (int)BillTypeEnum.SaleSummeryReport,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.SaleSummeryReport),
                            Content = content28
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate28);
                    }

                    #endregion

                    #region �������ܱ�(����Ʒ)

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.TransferSummaryReport).Count() == 0)
                    {
                        string content29 = "<div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 20pt; \">�������ܱ�(����Ʒ)</span></p>    <p style=\"text - align: left; \">�����ֿ⣺�����ֿ�@&nbsp;&nbsp;����ֿ⣺����ֿ�@&nbsp;&nbsp;��Ʒ���ƣ���Ʒ����@&nbsp;&nbsp;��Ʒ�����Ʒ���@&nbsp;&nbsp;</p>    <p style=\"text - align: left; \">�������ڣ���������@&nbsp;&nbsp;</p>    </div>    <div id=\"tbodyid\">  <table style=\"width: 729px; \" cellpadding=\"0\" class=\"table table-bordered\"><thead>  <tr style=\"height: 1px; \">    <td style=\"width: 36px; height: 1px; \"><strong>���</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��Ʒ����</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��λ����</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�����ֿ�</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>����ֿ�</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>����</strong></td>    </tr>    </thead>    <tbody>    <tr style=\"height: 5px; \">    <td style=\"width: 36px; height: 1px; \">���#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��Ʒ����#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��Ʒ����#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">��λ����#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">�����ֿ�#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">����ֿ�#</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">����#</td>    </tr>    </tbody>    <tfoot>  <tr style=\"height: 24px; \">    <td style=\"width: 36px; height: 1px; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�ϼ�</strong></td>    <td style=\"width: 100px; height: 1px; text - align: center; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">&nbsp;</td>    <td style=\"width: 100px; height: 1px; text - align: center; \">����##</td>    </tr>    </tfoot>  </table>    </div>";

                        PrintTemplate printTemplate29 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 1,
                            BillType = (int)BillTypeEnum.TransferSummaryReport,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.TransferSummaryReport),
                            Content = content29
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate29);
                    }

                    #endregion

                    #region ���ۻ���(����Ʒ)

                    if (printTemplates == null || printTemplates.Where(p => p.BillType == (int)BillTypeEnum.SaleSummeryProductReport).Count() == 0)
                    {
                        string content30 = "<!DOCTYPE html>  <html>  <head>  </head>  <body>  <div id=\"theadid\">  <p style=\"text - align: center; \"><span style=\"font - size: 20pt; \">���ۻ���(����Ʒ)</span></p>  <p style=\"text - align: left; \">�������ڣ���������@&nbsp;&nbsp;�ֿ⣺�ֿ�@&nbsp;&nbsp;��Ʒ���ƣ���Ʒ����@&nbsp;&nbsp;��Ʒ�����Ʒ���@&nbsp;&nbsp;</p>  <p style=\"text - align: left; \">ҵ��Ա��ҵ��Ա@&nbsp;&nbsp;�ͻ����ͻ�@</p>  </div>  <div id=\"tbodyid\">  <table style=\"width: 729px; \" cellpadding=\"0\" class=\"table table-bordered\"><thead>  <tr style=\"height: 1px; \">  <td style=\"width: 36px; height: 1px; \"><strong>���</strong></td>  <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��Ʒ����</strong></td>  <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>������</strong></td>  <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��������</strong></td>  <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>���۽��</strong></td>  <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��������</strong></td>  <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�˻�����</strong></td>  <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�˻����</strong></td>  <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>��������</strong></td>  <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�����۶�</strong></td>  </tr>  </thead>  <tbody>  <tr style=\"height: 5px; \">  <td style=\"width: 36px; height: 1px; \">���#</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">��Ʒ����#</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">������#</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">��������#</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">���۽��#</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">��������#</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">�˻�����#</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">�˻����#</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">��������#</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">���۾���#</td>  </tr>  </tbody>  <tfoot>  <tr style=\"height: 24px; \">  <td style=\"width: 36px; height: 1px; \">&nbsp;</td>  <td style=\"width: 100px; height: 1px; text - align: center; \"><strong>�ϼ�</strong></td>  <td style=\"width: 100px; height: 1px; text - align: center; \">&nbsp;</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">��������##</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">���۽��##</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">��������##</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">�˻�����##</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">�˻����##</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">��������##</td>  <td style=\"width: 100px; height: 1px; text - align: center; \">���۾���##</td>  </tr>  </tfoot>  </table>  </div>  </body>  </html>";

                        PrintTemplate printTemplate30 = new PrintTemplate()
                        {
                            StoreId = storeId,
                            TemplateType = 1,
                            BillType = (int)BillTypeEnum.SaleSummeryProductReport,
                            Title = CommonHelper.GetEnumDescription(BillTypeEnum.SaleSummeryProductReport),
                            Content = content30
                        };
                        _printTemplateService.InsertPrintTemplate(printTemplate30);
                    }

                    #endregion

                    #endregion

                    #region �ȼ� Rank
                    var _rankService = EngineContext.Current.Resolve<IRankService>();
                    List<Rank> ranks = _rankService.GetAll(storeId).ToList();

                    if (ranks == null || ranks.Where(r => r.Name == "A��").Count() == 0)
                    {
                        Rank rank1 = new Rank()
                        {
                            StoreId = storeId,
                            Name = "A��",
                            Deleted = false,
                            CreateDate = DateTime.Now
                        };
                        _rankService.InsertRank(rank1);
                    }

                    if (ranks == null || ranks.Where(r => r.Name == "B��").Count() == 0)
                    {
                        Rank rank2 = new Rank()
                        {
                            StoreId = storeId,
                            Name = "B��",
                            Deleted = false,
                            CreateDate = DateTime.Now
                        };
                        _rankService.InsertRank(rank2);
                    }

                    if (ranks == null || ranks.Where(r => r.Name == "C��").Count() == 0)
                    {
                        Rank rank3 = new Rank()
                        {
                            StoreId = storeId,
                            Name = "C��",
                            Deleted = false,
                            CreateDate = DateTime.Now
                        };
                        _rankService.InsertRank(rank3);
                    }

                    if (ranks == null || ranks.Where(r => r.Name == "D��").Count() == 0)
                    {
                        Rank rank4 = new Rank()
                        {
                            StoreId = storeId,
                            Name = "D��",
                            Deleted = false,
                            CreateDate = DateTime.Now
                        };
                        _rankService.InsertRank(rank4);
                    }

                    if (ranks == null || ranks.Where(r => r.Name == "E��").Count() == 0)
                    {
                        Rank rank5 = new Rank()
                        {
                            StoreId = storeId,
                            Name = "E��",
                            Deleted = false,
                            CreateDate = DateTime.Now
                        };
                        _rankService.InsertRank(rank5);
                    }

                    #endregion

                    #region �������ѡ�� SpecificationAttributeOptions,��ʾ������� SpecificationAttributes
                    var _specificationAttributeService = EngineContext.Current.Resolve<ISpecificationAttributeService>();
                    List<SpecificationAttribute> specificationAttributes = _specificationAttributeService.GetSpecificationAttributesBtStore(storeId).ToList();

                    SpecificationAttribute specificationAttribute1;
                    if (specificationAttributes == null || specificationAttributes.Where(s => s.Name == "��װ").Count() == 0)
                    {
                        specificationAttribute1 = new SpecificationAttribute()
                        {
                            StoreId = storeId,
                            Name = "��װ",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttribute(specificationAttribute1);
                    }
                    else
                    {
                        specificationAttribute1 = specificationAttributes.Where(s => s.Name == "��װ").FirstOrDefault();
                    }

                    SpecificationAttribute specificationAttribute2;
                    if (specificationAttributes == null || specificationAttributes.Where(s => s.Name == "����").Count() == 0)
                    {
                        specificationAttribute2 = new SpecificationAttribute()
                        {
                            StoreId = storeId,
                            Name = "����",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttribute(specificationAttribute2);
                    }
                    else
                    {
                        specificationAttribute2 = specificationAttributes.Where(s => s.Name == "����").FirstOrDefault();
                    }

                    SpecificationAttribute specificationAttribute3;
                    if (specificationAttributes == null || specificationAttributes.Where(s => s.Name == "С��λ").Count() == 0)
                    {
                        specificationAttribute3 = new SpecificationAttribute()
                        {
                            StoreId = storeId,
                            Name = "С��λ",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttribute(specificationAttribute3);
                    }
                    else
                    {
                        specificationAttribute3 = specificationAttributes.Where(s => s.Name == "С��λ").FirstOrDefault();
                    }

                    SpecificationAttribute specificationAttribute4;
                    if (specificationAttributes == null || specificationAttributes.Where(s => s.Name == "��λ").Count() == 0)
                    {
                        specificationAttribute4 = new SpecificationAttribute()
                        {
                            StoreId = storeId,
                            Name = "��λ",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttribute(specificationAttribute4);
                    }
                    else
                    {
                        specificationAttribute4 = specificationAttributes.Where(s => s.Name == "��λ").FirstOrDefault();
                    }

                    SpecificationAttribute specificationAttribute5;
                    if (specificationAttributes == null || specificationAttributes.Where(s => s.Name == "�е�λ").Count() == 0)
                    {
                        specificationAttribute5 = new SpecificationAttribute()
                        {
                            StoreId = storeId,
                            Name = "�е�λ",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttribute(specificationAttribute5);
                    }
                    else
                    {
                        specificationAttribute5 = specificationAttributes.Where(s => s.Name == "�е�λ").FirstOrDefault();
                    }

                    List<SpecificationAttributeOption> specificationAttributeOptions = _specificationAttributeService.GetSpecificationAttributeOptionsByStore(storeId).ToList();

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "���װ" && s.SpecificationAttributeId == specificationAttribute1.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption1 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute1.Id,
                            Name = "���װ",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption1);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "���װ" && s.SpecificationAttributeId == specificationAttribute1.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption2 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute1.Id,
                            Name = "���װ",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption2);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "ƿװ" && s.SpecificationAttributeId == specificationAttribute1.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption3 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute1.Id,
                            Name = "ƿװ",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption3);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��װ" && s.SpecificationAttributeId == specificationAttribute1.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption4 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute1.Id,
                            Name = "��װ",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption4);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "200ML" && s.SpecificationAttributeId == specificationAttribute2.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption5 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute2.Id,
                            Name = "200ML",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption5);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "300ML" && s.SpecificationAttributeId == specificationAttribute2.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption6 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute2.Id,
                            Name = "300ML",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption6);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "400ML" && s.SpecificationAttributeId == specificationAttribute2.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption7 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute2.Id,
                            Name = "400ML",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption7);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "500ML" && s.SpecificationAttributeId == specificationAttribute2.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption8 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute2.Id,
                            Name = "500ML",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption8);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "ƿ" && s.SpecificationAttributeId == specificationAttribute3.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption9 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute3.Id,
                            Name = "ƿ",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption9);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute3.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption10 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute3.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption10);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute3.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption11 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute3.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption11);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute3.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption12 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute3.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption12);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute3.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption13 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute3.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption13);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute3.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption14 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute3.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption14);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute4.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption15 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute4.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption15);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute4.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption16 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute4.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption16);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute4.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption17 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute4.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption17);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute5.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption18 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute5.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption18);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute5.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption19 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute5.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption19);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute5.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption20 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute5.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption20);
                    }

                    if (specificationAttributeOptions == null || specificationAttributeOptions.Where(s => s.Name == "��" && s.SpecificationAttributeId == specificationAttribute5.Id).Count() == 0)
                    {
                        SpecificationAttributeOption specificationAttributeOption21 = new SpecificationAttributeOption()
                        {
                            SpecificationAttributeId = specificationAttribute5.Id,
                            Name = "��",
                            DisplayOrder = 0
                        };
                        _specificationAttributeService.InsertSpecificationAttributeOption(specificationAttributeOption21);
                    }

                    #endregion

                    #region ͳ����� StatisticalTypes

                    var _statisticalTypeService = EngineContext.Current.Resolve<IStatisticalTypeService>();
                    List<StatisticalTypes> statisticalTypess = _statisticalTypeService.GetAllStatisticalTypess(storeId).ToList();

                    if (statisticalTypess == null || statisticalTypess.Where(s => s.Name == "��ţ").Count() == 0)
                    {
                        StatisticalTypes statisticalTypes1 = new StatisticalTypes()
                        {
                            StoreId = storeId,
                            Name = "��ţ",
                            Value = "0",
                            CreatedOnUtc = DateTime.Now
                        };
                        _statisticalTypeService.InsertStatisticalTypes(statisticalTypes1);
                    }

                    if (statisticalTypess == null || statisticalTypess.Where(s => s.Name == "����").Count() == 0)
                    {
                        StatisticalTypes statisticalTypes2 = new StatisticalTypes()
                        {
                            StoreId = storeId,
                            Name = "����",
                            Value = "1",
                            CreatedOnUtc = DateTime.Now
                        };
                        _statisticalTypeService.InsertStatisticalTypes(statisticalTypes2);
                    }

                    if (statisticalTypess == null || statisticalTypess.Where(s => s.Name == "ѩ��").Count() == 0)
                    {
                        StatisticalTypes statisticalTypes3 = new StatisticalTypes()
                        {
                            StoreId = storeId,
                            Name = "ѩ��",
                            Value = "2",
                            CreatedOnUtc = DateTime.Now
                        };
                        _statisticalTypeService.InsertStatisticalTypes(statisticalTypes3);
                    }

                    if (statisticalTypess == null || statisticalTypess.Where(s => s.Name == "����").Count() == 0)
                    {
                        StatisticalTypes statisticalTypes4 = new StatisticalTypes()
                        {
                            StoreId = storeId,
                            Name = "����",
                            Value = "3",
                            CreatedOnUtc = DateTime.Now
                        };
                        _statisticalTypeService.InsertStatisticalTypes(statisticalTypes4);
                    }

                    if (statisticalTypess == null || statisticalTypess.Where(s => s.Name == "����").Count() == 0)
                    {
                        StatisticalTypes statisticalTypes5 = new StatisticalTypes()
                        {
                            StoreId = storeId,
                            Name = "����",
                            Value = "4",
                            CreatedOnUtc = DateTime.Now
                        };
                        _statisticalTypeService.InsertStatisticalTypes(statisticalTypes5);
                    }

                    #endregion

                    #region �ֿ� WareHouse

                    var _wareHouseService = EngineContext.Current.Resolve<IWareHouseService>();
                    List<WareHouse> wareHouses = _wareHouseService.GetWareHouseList(storeId).ToList();
                    if (wareHouses == null || wareHouses.Where(w => w.Name == "���ֿ�").Count() == 0)
                    {
                        WareHouse wareHouse1 = new WareHouse()
                        {
                            StoreId = storeId,
                            Code = "zck",
                            Name = "���ֿ�",
                            Type = 1,
                            AllowNegativeInventory = true,
                            Status = true,
                            Deleted = false,
                            CreatedUserId = userId,
                            CreatedOnUtc = DateTime.Now
                        };
                        _wareHouseService.InsertWareHouse(wareHouse1);
                    }


                    #endregion

                    #endregion

                    //step6:
                    #region ����

                    var _settingService = EngineContext.Current.Resolve<ISettingService>();

                    _settingService.ClearCache(store.Id);
                    #region APP��ӡ����

                    var aPPPrintSetting = _settingService.LoadSetting<APPPrintSetting>(storeId);
                    aPPPrintSetting.AllowPrintPackPrice = false;
                    aPPPrintSetting.PrintMode = 1;
                    aPPPrintSetting.PrintingNumber = 1;
                    aPPPrintSetting.AllowAutoPrintSalesAndReturn = false;
                    aPPPrintSetting.AllowAutoPrintOrderAndReturn = false;
                    aPPPrintSetting.AllowAutoPrintAdvanceReceipt = false;
                    aPPPrintSetting.AllowAutoPrintArrears = false;
                    aPPPrintSetting.AllowPrintOnePass = false;
                    aPPPrintSetting.AllowPrintProductSummary = false;
                    aPPPrintSetting.AllowPringMobile = false;
                    aPPPrintSetting.AllowPrintingTimeAndNumber = false;
                    aPPPrintSetting.AllowPrintCustomerBalance = false;
                    aPPPrintSetting.PageHeaderText = "";
                    aPPPrintSetting.PageFooterText1 = "";
                    aPPPrintSetting.PageFooterText2 = "";
                    aPPPrintSetting.PageHeaderImage = "";
                    aPPPrintSetting.PageFooterImage = "";

                    _settingService.SaveSetting(aPPPrintSetting, x => x.AllowPrintPackPrice, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.PrintMode, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.PrintingNumber, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.AllowAutoPrintSalesAndReturn, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.AllowAutoPrintOrderAndReturn, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.AllowAutoPrintAdvanceReceipt, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.AllowAutoPrintArrears, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.AllowPrintOnePass, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.AllowPrintProductSummary, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.AllowPringMobile, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.AllowPrintingTimeAndNumber, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.AllowPrintCustomerBalance, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.PageHeaderText, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.PageFooterText1, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.PageFooterText2, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.PageHeaderImage, storeId, false);
                    _settingService.SaveSetting(aPPPrintSetting, x => x.PageFooterImage, storeId, false);
                    #endregion

                    #region ���Դ�ӡ����

                    var pcPrintSetting = _settingService.LoadSetting<PCPrintSetting>(storeId);
                    pcPrintSetting.StoreName = store.Name;
                    pcPrintSetting.Address = "";
                    pcPrintSetting.PlaceOrderTelphone = "";
                    pcPrintSetting.PrintMethod = 1;
                    pcPrintSetting.PaperType = 1;
                    pcPrintSetting.PaperWidth = 100;
                    pcPrintSetting.PaperHeight = 100;
                    pcPrintSetting.BorderType = 1;
                    pcPrintSetting.MarginTop = 1;
                    pcPrintSetting.MarginBottom = 1;
                    pcPrintSetting.MarginLeft = 1;
                    pcPrintSetting.MarginRight = 1;
                    pcPrintSetting.IsPrintPageNumber = false;
                    pcPrintSetting.PrintHeader = false;
                    pcPrintSetting.PrintFooter = false;
                    pcPrintSetting.IsFixedRowNumber = false;
                    pcPrintSetting.FixedRowNumber = 30;
                    pcPrintSetting.PrintSubtotal = false;
                    pcPrintSetting.PrintPort = 8000;

                    _settingService.SaveSetting(pcPrintSetting, x => x.StoreName, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.Address, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.PlaceOrderTelphone, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.PrintMethod, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.PaperType, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.PaperWidth, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.PaperHeight, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.BorderType, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.MarginTop, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.MarginBottom, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.MarginLeft, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.MarginRight, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.IsPrintPageNumber, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.PrintHeader, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.PrintFooter, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.IsFixedRowNumber, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.FixedRowNumber, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.PrintSubtotal, storeId, false);
                    _settingService.SaveSetting(pcPrintSetting, x => x.PrintPort, storeId, false);

                    #endregion

                    #region ��ƿ�Ŀ
                    //var accountingOptionService = EngineContext.Current.Resolve<IAccountingService>();
                    //List<AccountingOption> accountingOptions = accountingOptionService.GetAllAccountingOptionsByStore(storeId);

                    //#region 1�ʲ���
                    ////����ֽ�
                    //AccountingOption accountingOption1;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "����ֽ�").Count() == 0)
                    //{
                    //    accountingOption1 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = 0,
                    //        Name = "����ֽ�",
                    //        Code = "1001",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption1);
                    //}
                    //else
                    //{
                    //    accountingOption1 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "����ֽ�").FirstOrDefault();
                    //}

                    ////���д��
                    //AccountingOption accountingOption2;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "���д��").Count() == 0)
                    //{
                    //    accountingOption2 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = 0,
                    //        Name = "���д��",
                    //        Code = "1002",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption2);
                    //}
                    //else
                    //{
                    //    accountingOption2 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "���д��").FirstOrDefault();
                    //}

                    ////Ӧ���˿�
                    //AccountingOption accountingOption3;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "Ӧ���˿�").Count() == 0)
                    //{
                    //    accountingOption3 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = 0,
                    //        Name = "Ӧ���˿�",
                    //        Code = "1004",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.AccountsReceivable.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption3);
                    //}
                    //else
                    //{
                    //    accountingOption3 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "Ӧ���˿�").FirstOrDefault();
                    //}

                    ////Ԥ���˿�
                    //AccountingOption accountingOption4;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "Ԥ���˿�").Count() == 0)
                    //{
                    //    accountingOption4 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = 0,
                    //        Name = "Ԥ���˿�",
                    //        Code = "1005",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption4);
                    //}
                    //else
                    //{
                    //    accountingOption4 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "Ԥ���˿�").FirstOrDefault();
                    //}

                    ////Ӧ����Ϣ
                    //AccountingOption accountingOption5;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "Ӧ����Ϣ").Count() == 0)
                    //{
                    //    accountingOption5 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = 0,
                    //        Name = "Ӧ����Ϣ",
                    //        Code = "1006",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.InterestReceivable.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption5);
                    //}
                    //else
                    //{
                    //    accountingOption5 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "Ӧ����Ϣ").FirstOrDefault();
                    //}

                    ////�����Ʒ
                    //AccountingOption accountingOption6;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "�����Ʒ").Count() == 0)
                    //{
                    //    accountingOption6 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = 0,
                    //        Name = "�����Ʒ",
                    //        Code = "1007",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.InventoryGoods.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption6);
                    //}
                    //else
                    //{
                    //    accountingOption6 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "�����Ʒ").FirstOrDefault();
                    //}

                    ////�̶��ʲ�
                    //AccountingOption accountingOption7;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "�̶��ʲ�").Count() == 0)
                    //{
                    //    accountingOption7 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = 0,
                    //        Name = "�̶��ʲ�",
                    //        Code = "1008",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.FixedAssets.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption7);
                    //}
                    //else
                    //{
                    //    accountingOption7 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "�̶��ʲ�").FirstOrDefault();
                    //}

                    ////�ۼ��۾�
                    //AccountingOption accountingOption8;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "�ۼ��۾�").Count() == 0)
                    //{
                    //    accountingOption8 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = 0,
                    //        Name = "�ۼ��۾�",
                    //        Code = "1009",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.AccumulatedDepreciation.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption8);
                    //}
                    //else
                    //{
                    //    accountingOption8 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "�ۼ��۾�").FirstOrDefault();
                    //}

                    ////�̶��ʲ�����
                    //AccountingOption accountingOption9;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "�̶��ʲ�����").Count() == 0)
                    //{
                    //    accountingOption9 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = 0,
                    //        Name = "�̶��ʲ�����",
                    //        Code = "1010",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.LiquidationFixedAssets.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption9);
                    //}
                    //else
                    //{
                    //    accountingOption9 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "�̶��ʲ�����").FirstOrDefault();
                    //}

                    ////�����˻�
                    //AccountingOption accountingOption10;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "�����˻�").Count() == 0)
                    //{
                    //    accountingOption10 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = 0,
                    //        Name = "�����˻�",
                    //        Code = "1003",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.OtherAccount.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption10);
                    //}
                    //else
                    //{
                    //    accountingOption10 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == 0 && a.Name == "�����˻�").FirstOrDefault();
                    //}

                    ////�ֽ�
                    //AccountingOption accountingOption11;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == accountingOption1.Id && a.Name == "�ֽ�").Count() == 0)
                    //{
                    //    accountingOption11 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.Cash.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption11);
                    //}
                    //else
                    //{
                    //    accountingOption11 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == accountingOption1.Id && a.Name == "�ֽ�").FirstOrDefault();
                    //}

                    ////����
                    //AccountingOption accountingOption12;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == accountingOption2.Id && a.Name == "����").Count() == 0)
                    //{
                    //    accountingOption12 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.Bank.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption12);
                    //}
                    //else
                    //{
                    //    accountingOption12 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == accountingOption2.Id && a.Name == "����").FirstOrDefault();
                    //}

                    ////΢��
                    //AccountingOption accountingOption13;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == accountingOption2.Id && a.Name == "΢��").Count() == 0)
                    //{
                    //    accountingOption13 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "΢��",
                    //        Code = "100202",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.WChat.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption13);
                    //}
                    //else
                    //{
                    //    accountingOption13 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == accountingOption2.Id && a.Name == "΢��").FirstOrDefault();
                    //}

                    ////֧����
                    //AccountingOption accountingOption14;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == accountingOption2.Id && a.Name == "֧����").Count() == 0)
                    //{
                    //    accountingOption14 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "֧����",
                    //        Code = "100203",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.PayTreasure.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption14);
                    //}
                    //else
                    //{
                    //    accountingOption14 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == accountingOption2.Id && a.Name == "֧����").FirstOrDefault();
                    //}

                    ////Ԥ����
                    //AccountingOption accountingOption15;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == accountingOption4.Id && a.Name == "Ԥ����").Count() == 0)
                    //{
                    //    accountingOption15 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption4.Id,
                    //        Name = "Ԥ����",
                    //        Code = "100501",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.AdvancePayment.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption15);
                    //}
                    //else
                    //{
                    //    accountingOption15 = accountingOptions.Where(a => a.AccountingTypeId == 1 && a.ParentId == accountingOption4.Id && a.Name == "Ԥ����").FirstOrDefault();
                    //}

                    //#endregion

                    //#region 2��ծ��
                    ////���ڽ��
                    //AccountingOption accountingOption16;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "���ڽ��").Count() == 0)
                    //{
                    //    accountingOption16 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = 0,
                    //        Name = "���ڽ��",
                    //        Code = "2001",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.ShortBorrowing.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption16);
                    //}
                    //else
                    //{
                    //    accountingOption16 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "���ڽ��").FirstOrDefault();
                    //}

                    ////Ӧ���˿�
                    //AccountingOption accountingOption17;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "Ӧ���˿�").Count() == 0)
                    //{
                    //    accountingOption17 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = 0,
                    //        Name = "Ӧ���˿�",
                    //        Code = "2002",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.AccountsPayable.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption17);
                    //}
                    //else
                    //{
                    //    accountingOption17 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "Ӧ���˿�").FirstOrDefault();
                    //}

                    ////Ԥ���˿�
                    //AccountingOption accountingOption18;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "Ԥ���˿�").Count() == 0)
                    //{
                    //    accountingOption18 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = 0,
                    //        Name = "Ԥ���˿�",
                    //        Code = "2003",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption18);
                    //}
                    //else
                    //{
                    //    accountingOption18 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "Ԥ���˿�").FirstOrDefault();
                    //}

                    ////������
                    //AccountingOption accountingOption19;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "������").Count() == 0)
                    //{
                    //    accountingOption19 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = 0,
                    //        Name = "������",
                    //        Code = "2004",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.Order.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption19);
                    //}
                    //else
                    //{
                    //    accountingOption19 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "������").FirstOrDefault();
                    //}

                    ////Ӧ��ְ��н��
                    //AccountingOption accountingOption20;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "Ӧ��ְ��н��").Count() == 0)
                    //{
                    //    accountingOption20 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = 0,
                    //        Name = "Ӧ��ְ��н��",
                    //        Code = "2005",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.EmployeePayable.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption20);
                    //}
                    //else
                    //{
                    //    accountingOption20 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "Ӧ��ְ��н��").FirstOrDefault();
                    //}

                    ////Ӧ��˰��
                    //AccountingOption accountingOption21;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "Ӧ��˰��").Count() == 0)
                    //{
                    //    accountingOption21 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = 0,
                    //        Name = "Ӧ��˰��",
                    //        Code = "2006",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption21);
                    //}
                    //else
                    //{
                    //    accountingOption21 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "Ӧ��˰��").FirstOrDefault();
                    //}

                    ////Ӧ����Ϣ
                    //AccountingOption accountingOption22;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "Ӧ����Ϣ").Count() == 0)
                    //{
                    //    accountingOption22 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = 0,
                    //        Name = "Ӧ����Ϣ",
                    //        Code = "2007",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.InterestPayable.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption22);
                    //}
                    //else
                    //{
                    //    accountingOption22 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "Ӧ����Ϣ").FirstOrDefault();
                    //}

                    ////����Ӧ����
                    //AccountingOption accountingOption23;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "����Ӧ����").Count() == 0)
                    //{
                    //    accountingOption23 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = 0,
                    //        Name = "����Ӧ����",
                    //        Code = "2008",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.OtherPayables.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption23);
                    //}
                    //else
                    //{
                    //    accountingOption23 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "����Ӧ����").FirstOrDefault();
                    //}

                    ////���ڽ��
                    //AccountingOption accountingOption24;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "���ڽ��").Count() == 0)
                    //{
                    //    accountingOption24 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = 0,
                    //        Name = "���ڽ��",
                    //        Code = "2009",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.LongBorrowing.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption24);
                    //}
                    //else
                    //{
                    //    accountingOption24 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == 0 && a.Name == "���ڽ��").FirstOrDefault();
                    //}

                    ////Ԥ�տ�
                    //AccountingOption accountingOption25;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption18.Id && a.Name == "Ԥ�տ�").Count() == 0)
                    //{
                    //    accountingOption25 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = accountingOption18.Id,
                    //        Name = "Ԥ�տ�",
                    //        Code = "200301",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.AdvanceReceipt.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption25);
                    //}
                    //else
                    //{
                    //    accountingOption25 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption18.Id && a.Name == "Ԥ�տ�").FirstOrDefault();
                    //}

                    ////Ӧ����ֵ˰
                    //AccountingOption accountingOption26;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption21.Id && a.Name == "Ӧ����ֵ˰").Count() == 0)
                    //{
                    //    accountingOption26 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = accountingOption21.Id,
                    //        Name = "Ӧ����ֵ˰",
                    //        Code = "200601",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption26);
                    //}
                    //else
                    //{
                    //    accountingOption26 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption21.Id && a.Name == "Ӧ����ֵ˰").FirstOrDefault();
                    //}

                    ////����˰��
                    //AccountingOption accountingOption27;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption26.Id && a.Name == "����˰��").Count() == 0)
                    //{
                    //    accountingOption27 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = accountingOption26.Id,
                    //        Name = "����˰��",
                    //        Code = "20060101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.InputTax.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption27);
                    //}
                    //else
                    //{
                    //    accountingOption27 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption26.Id && a.Name == "����˰��").FirstOrDefault();
                    //}

                    ////�ѽ�˰��
                    //AccountingOption accountingOption28;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption26.Id && a.Name == "�ѽ�˰��").Count() == 0)
                    //{
                    //    accountingOption28 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = accountingOption26.Id,
                    //        Name = "�ѽ�˰��",
                    //        Code = "20060102",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.PayTaxes.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption28);
                    //}
                    //else
                    //{
                    //    accountingOption28 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption26.Id && a.Name == "�ѽ�˰��").FirstOrDefault();
                    //}

                    ////ת��δ����ֵ˰
                    //AccountingOption accountingOption29;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption26.Id && a.Name == "ת��δ����ֵ˰").Count() == 0)
                    //{
                    //    accountingOption29 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = accountingOption26.Id,
                    //        Name = "ת��δ����ֵ˰",
                    //        Code = "20060103",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.TransferTaxes.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption29);
                    //}
                    //else
                    //{
                    //    accountingOption29 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption26.Id && a.Name == "ת��δ����ֵ˰").FirstOrDefault();
                    //}

                    ////����˰��
                    //AccountingOption accountingOption30;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption26.Id && a.Name == "����˰��").Count() == 0)
                    //{
                    //    accountingOption30 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = accountingOption26.Id,
                    //        Name = "����˰��",
                    //        Code = "20060104",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.OutputTax.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption30);
                    //}
                    //else
                    //{
                    //    accountingOption30 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption26.Id && a.Name == "����˰��").FirstOrDefault();
                    //}

                    ////δ����ֵ˰
                    //AccountingOption accountingOption31;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption21.Id && a.Name == "δ����ֵ˰").Count() == 0)
                    //{
                    //    accountingOption31 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 2,
                    //        ParentId = accountingOption21.Id,
                    //        Name = "δ����ֵ˰",
                    //        Code = "200602",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.UnpaidVAT.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption31);
                    //}
                    //else
                    //{
                    //    accountingOption31 = accountingOptions.Where(a => a.AccountingTypeId == 2 && a.ParentId == accountingOption21.Id && a.Name == "δ����ֵ˰").FirstOrDefault();
                    //}

                    //#endregion

                    //#region 3Ȩ����
                    ////ʵ���ʱ�
                    //AccountingOption accountingOption32;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == 0 && a.Name == "ʵ���ʱ�").Count() == 0)
                    //{
                    //    accountingOption32 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 3,
                    //        ParentId = 0,
                    //        Name = "ʵ���ʱ�",
                    //        Code = "3001",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.PaidCapital.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption32);
                    //}
                    //else
                    //{
                    //    accountingOption32 = accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == 0 && a.Name == "ʵ���ʱ�").FirstOrDefault();
                    //}

                    ////�ʱ�����
                    //AccountingOption accountingOption33;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == 0 && a.Name == "�ʱ�����").Count() == 0)
                    //{
                    //    accountingOption33 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 3,
                    //        ParentId = 0,
                    //        Name = "�ʱ�����",
                    //        Code = "3002",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.CapitalReserves.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption33);
                    //}
                    //else
                    //{
                    //    accountingOption33 = accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == 0 && a.Name == "�ʱ�����").FirstOrDefault();
                    //}


                    ////ӯ�๫��
                    //AccountingOption accountingOption34;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == 0 && a.Name == "ӯ�๫��").Count() == 0)
                    //{
                    //    accountingOption34 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 3,
                    //        ParentId = 0,
                    //        Name = "ӯ�๫��",
                    //        Code = "3003",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption34);
                    //}
                    //else
                    //{
                    //    accountingOption34 = accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == 0 && a.Name == "ӯ�๫��").FirstOrDefault();
                    //}

                    ////��������
                    //AccountingOption accountingOption35;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == 0 && a.Name == "��������").Count() == 0)
                    //{
                    //    accountingOption35 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 3,
                    //        ParentId = 0,
                    //        Name = "��������",
                    //        Code = "3004",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.ThisYearProfits.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption35);
                    //}
                    //else
                    //{
                    //    accountingOption35 = accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == 0 && a.Name == "��������").FirstOrDefault();
                    //}

                    ////�������
                    //AccountingOption accountingOption36;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == 0 && a.Name == "�������").Count() == 0)
                    //{
                    //    accountingOption36 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 3,
                    //        ParentId = 0,
                    //        Name = "�������",
                    //        Code = "3005",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption36);
                    //}
                    //else
                    //{
                    //    accountingOption36 = accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == 0 && a.Name == "�������").FirstOrDefault();
                    //}

                    ////����ӯ�๫��
                    //AccountingOption accountingOption37;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == accountingOption34.Id && a.Name == "����ӯ�๫��").Count() == 0)
                    //{
                    //    accountingOption37 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 3,
                    //        ParentId = accountingOption34.Id,
                    //        Name = "����ӯ�๫��",
                    //        Code = "300301",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.LegalSurplus.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption37);
                    //}
                    //else
                    //{
                    //    accountingOption37 = accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == accountingOption34.Id && a.Name == "����ӯ�๫��").FirstOrDefault();
                    //}

                    ////����ӯ�๫��
                    //AccountingOption accountingOption38;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == accountingOption34.Id && a.Name == "����ӯ�๫��").Count() == 0)
                    //{
                    //    accountingOption38 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 3,
                    //        ParentId = accountingOption34.Id,
                    //        Name = "����ӯ�๫��",
                    //        Code = "300302",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.ArbitrarySurplus.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption38);
                    //}
                    //else
                    //{
                    //    accountingOption38 = accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == accountingOption34.Id && a.Name == "����ӯ�๫��").FirstOrDefault();
                    //}

                    ////δ��������
                    //AccountingOption accountingOption39;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == accountingOption36.Id && a.Name == "δ��������").Count() == 0)
                    //{
                    //    accountingOption39 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 3,
                    //        ParentId = accountingOption36.Id,
                    //        Name = "δ��������",
                    //        Code = "300501",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.UndistributedProfit.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption39);
                    //}
                    //else
                    //{
                    //    accountingOption39 = accountingOptions.Where(a => a.AccountingTypeId == 3 && a.ParentId == accountingOption36.Id && a.Name == "δ��������").FirstOrDefault();
                    //}

                    //#endregion

                    //#region 4�����ࣨ���룩

                    ////��Ӫҵ������
                    //AccountingOption accountingOption40;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == 0 && a.Name == "��Ӫҵ������").Count() == 0)
                    //{
                    //    accountingOption40 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 4,
                    //        ParentId = 0,
                    //        Name = "��Ӫҵ������",
                    //        Code = "4001",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.MainIncome.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption40);
                    //}
                    //else
                    //{
                    //    accountingOption40 = accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == 0 && a.Name == "��Ӫҵ������").FirstOrDefault();
                    //}

                    ////����ҵ������
                    //AccountingOption accountingOption41;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == 0 && a.Name == "����ҵ������").Count() == 0)
                    //{
                    //    accountingOption41 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 4,
                    //        ParentId = 0,
                    //        Name = "����ҵ������",
                    //        Code = "4002",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption41);
                    //}
                    //else
                    //{
                    //    accountingOption41 = accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == 0 && a.Name == "����ҵ������").FirstOrDefault();
                    //}

                    ////�̵㱨������
                    //AccountingOption accountingOption42;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == accountingOption41.Id && a.Name == "�̵㱨������").Count() == 0)
                    //{
                    //    accountingOption42 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 4,
                    //        ParentId = accountingOption41.Id,
                    //        Name = "�̵㱨������",
                    //        Code = "400201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.TakeStockIncome.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption42);
                    //}
                    //else
                    //{
                    //    accountingOption42 = accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == accountingOption41.Id && a.Name == "�̵㱨������").FirstOrDefault();
                    //}

                    ////�ɱ���������
                    //AccountingOption accountingOption43;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == accountingOption41.Id && a.Name == "�ɱ���������").Count() == 0)
                    //{
                    //    accountingOption43 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 4,
                    //        ParentId = accountingOption41.Id,
                    //        Name = "�ɱ���������",
                    //        Code = "400202",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.CostIncome.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption43);
                    //}
                    //else
                    //{
                    //    accountingOption43 = accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == accountingOption41.Id && a.Name == "�ɱ���������").FirstOrDefault();
                    //}

                    ////���ҷ���
                    //AccountingOption accountingOption44;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == accountingOption41.Id && a.Name == "���ҷ���").Count() == 0)
                    //{
                    //    accountingOption44 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 4,
                    //        ParentId = accountingOption41.Id,
                    //        Name = "���ҷ���",
                    //        Code = "400203",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.ManufacturerRebates.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption44);
                    //}
                    //else
                    //{
                    //    accountingOption44 = accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == accountingOption41.Id && a.Name == "���ҷ���").FirstOrDefault();
                    //}

                    ////��Ʒ��װ����
                    //AccountingOption accountingOption45;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == accountingOption41.Id && a.Name == "��Ʒ��װ����").Count() == 0)
                    //{
                    //    accountingOption45 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 4,
                    //        ParentId = accountingOption41.Id,
                    //        Name = "��Ʒ��װ����",
                    //        Code = "400204",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.GoodsIncome.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption45);
                    //}
                    //else
                    //{
                    //    accountingOption45 = accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == accountingOption41.Id && a.Name == "��Ʒ��װ����").FirstOrDefault();
                    //}

                    ////�ɹ��˻�����
                    //AccountingOption accountingOption46;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == accountingOption41.Id && a.Name == "�ɹ��˻�����").Count() == 0)
                    //{
                    //    accountingOption46 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 4,
                    //        ParentId = accountingOption41.Id,
                    //        Name = "�ɹ��˻�����",
                    //        Code = "400205",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.PurchaseIncome.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption46);
                    //}
                    //else
                    //{
                    //    accountingOption46 = accountingOptions.Where(a => a.AccountingTypeId == 4 && a.ParentId == accountingOption41.Id && a.Name == "�ɹ��˻�����").FirstOrDefault();
                    //}

                    //#endregion

                    //#region 5�����֧ࣨ����
                    ////��Ӫҵ��ɱ�
                    //AccountingOption accountingOption47;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == 0 && a.Name == "��Ӫҵ��ɱ�").Count() == 0)
                    //{
                    //    accountingOption47 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = 0,
                    //        Name = "��Ӫҵ��ɱ�",
                    //        Code = "5001",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.MainCost.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption47);
                    //}
                    //else
                    //{
                    //    accountingOption47 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == 0 && a.Name == "��Ӫҵ��ɱ�").FirstOrDefault();
                    //}

                    ////����ҵ��ɱ�
                    //AccountingOption accountingOption48;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == 0 && a.Name == "����ҵ��ɱ�").Count() == 0)
                    //{
                    //    accountingOption48 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = 0,
                    //        Name = "����ҵ��ɱ�",
                    //        Code = "5002",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption48);
                    //}
                    //else
                    //{
                    //    accountingOption48 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == 0 && a.Name == "����ҵ��ɱ�").FirstOrDefault();
                    //}

                    ////���۷���
                    //AccountingOption accountingOption49;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == 0 && a.Name == "���۷���").Count() == 0)
                    //{
                    //    accountingOption49 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = 0,
                    //        Name = "���۷���",
                    //        Code = "5003",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption49);
                    //}
                    //else
                    //{
                    //    accountingOption49 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == 0 && a.Name == "���۷���").FirstOrDefault();
                    //}

                    ////�������
                    //AccountingOption accountingOption50;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == 0 && a.Name == "�������").Count() == 0)
                    //{
                    //    accountingOption50 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = 0,
                    //        Name = "�������",
                    //        Code = "5004",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption50);
                    //}
                    //else
                    //{
                    //    accountingOption50 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == 0 && a.Name == "�������").FirstOrDefault();
                    //}

                    ////�������
                    //AccountingOption accountingOption51;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == 0 && a.Name == "�������").Count() == 0)
                    //{
                    //    accountingOption51 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = 0,
                    //        Name = "�������",
                    //        Code = "5005",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption51);
                    //}
                    //else
                    //{
                    //    accountingOption51 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == 0 && a.Name == "�������").FirstOrDefault();
                    //}

                    ////�̵����
                    //AccountingOption accountingOption52;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption48.Id && a.Name == "�̵����").Count() == 0)
                    //{
                    //    accountingOption52 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption48.Id,
                    //        Name = "�̵����",
                    //        Code = "500201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.InventoryLoss.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption52);
                    //}
                    //else
                    //{
                    //    accountingOption52 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption48.Id && a.Name == "�̵����").FirstOrDefault();
                    //}

                    ////�ɱ�������ʧ
                    //AccountingOption accountingOption53;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption48.Id && a.Name == "�ɱ�������ʧ").Count() == 0)
                    //{
                    //    accountingOption53 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption48.Id,
                    //        Name = "�ɱ�������ʧ",
                    //        Code = "500202",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.CostLoss.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption53);
                    //}
                    //else
                    //{
                    //    accountingOption53 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption48.Id && a.Name == "�ɱ�������ʧ").FirstOrDefault();
                    //}

                    ////�ɹ��˻���ʧ
                    //AccountingOption accountingOption54;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption48.Id && a.Name == "�ɹ��˻���ʧ").Count() == 0)
                    //{
                    //    accountingOption54 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption48.Id,
                    //        Name = "�ɹ��˻���ʧ",
                    //        Code = "500203",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.PurchaseLoss.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption54);
                    //}
                    //else
                    //{
                    //    accountingOption54 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption48.Id && a.Name == "�ɹ��˻���ʧ").FirstOrDefault();
                    //}

                    ////�Ż�
                    //AccountingOption accountingOption55;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "�Ż�").Count() == 0)
                    //{
                    //    accountingOption55 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "�Ż�",
                    //        Code = "500301",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.Preferential.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption55);
                    //}
                    //else
                    //{
                    //    accountingOption55 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "�Ż�").FirstOrDefault();
                    //}

                    ////ˢ��������
                    //AccountingOption accountingOption56;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "ˢ��������").Count() == 0)
                    //{
                    //    accountingOption56 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "ˢ��������",
                    //        Code = "500302",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.CardFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption56);
                    //}
                    //else
                    //{
                    //    accountingOption56 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "ˢ��������").FirstOrDefault();
                    //}

                    ////���з�
                    //AccountingOption accountingOption57;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "���з�").Count() == 0)
                    //{
                    //    accountingOption57 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "���з�",
                    //        Code = "500303",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.DisplayFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption57);
                    //}
                    //else
                    //{
                    //    accountingOption57 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "���з�").FirstOrDefault();
                    //}

                    ////�ͷ�
                    //AccountingOption accountingOption58;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "�ͷ�").Count() == 0)
                    //{
                    //    accountingOption58 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "�ͷ�",
                    //        Code = "500304",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.OilFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption58);
                    //}
                    //else
                    //{
                    //    accountingOption58 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "�ͷ�").FirstOrDefault();
                    //}

                    ////������
                    //AccountingOption accountingOption59;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "������").Count() == 0)
                    //{
                    //    accountingOption59 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "������",
                    //        Code = "500305",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.CarFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption59);
                    //}
                    //else
                    //{
                    //    accountingOption59 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "������").FirstOrDefault();
                    //}

                    ////�òͷ�
                    //AccountingOption accountingOption60;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "�òͷ�").Count() == 0)
                    //{
                    //    accountingOption60 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "�òͷ�",
                    //        Code = "500306",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.MealsFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption60);
                    //}
                    //else
                    //{
                    //    accountingOption60 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "�òͷ�").FirstOrDefault();
                    //}

                    ////�˷�
                    //AccountingOption accountingOption61;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "�˷�").Count() == 0)
                    //{
                    //    accountingOption61 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "�˷�",
                    //        Code = "500307",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.TransferFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption61);
                    //}
                    //else
                    //{
                    //    accountingOption61 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "�˷�").FirstOrDefault();
                    //}

                    ////�۾ɷ���
                    //AccountingOption accountingOption62;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "�۾ɷ���").Count() == 0)
                    //{
                    //    accountingOption62 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "�۾ɷ���",
                    //        Code = "500308",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.OldFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption62);
                    //}
                    //else
                    //{
                    //    accountingOption62 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "�۾ɷ���").FirstOrDefault();
                    //}

                    ////0.5Ԫ����
                    //AccountingOption accountingOption63;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "0.5Ԫ����").Count() == 0)
                    //{
                    //    accountingOption63 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "0.5Ԫ����",
                    //        Code = "500309",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.BottleCapsFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption63);
                    //}
                    //else
                    //{
                    //    accountingOption63 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "0.5Ԫ����").FirstOrDefault();
                    //}

                    ////2Ԫƿ��
                    //AccountingOption accountingOption64;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "2Ԫƿ��").Count() == 0)
                    //{
                    //    accountingOption64 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "2Ԫƿ��",
                    //        Code = "500310",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.TwoCapsFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption64);
                    //}
                    //else
                    //{
                    //    accountingOption64 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "2Ԫƿ��").FirstOrDefault();
                    //}

                    ////50Ԫƿ��
                    //AccountingOption accountingOption65;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "50Ԫƿ��").Count() == 0)
                    //{
                    //    accountingOption65 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption49.Id,
                    //        Name = "50Ԫƿ��",
                    //        Code = "500311",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.FiftyCapsFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption65);
                    //}
                    //else
                    //{
                    //    accountingOption65 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption49.Id && a.Name == "50Ԫƿ��").FirstOrDefault();
                    //}

                    ////�칫��
                    //AccountingOption accountingOption66;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption50.Id && a.Name == "�칫��").Count() == 0)
                    //{
                    //    accountingOption66 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption50.Id,
                    //        Name = "�칫��",
                    //        Code = "500401",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.OfficeFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption66);
                    //}
                    //else
                    //{
                    //    accountingOption66 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption50.Id && a.Name == "�칫��").FirstOrDefault();
                    //}

                    ////����
                    //AccountingOption accountingOption67;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption50.Id && a.Name == "����").Count() == 0)
                    //{
                    //    accountingOption67 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption50.Id,
                    //        Name = "����",
                    //        Code = "500402",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.HouseFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption67);
                    //}
                    //else
                    //{
                    //    accountingOption67 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption50.Id && a.Name == "����").FirstOrDefault();
                    //}

                    ////��ҵ�����
                    //AccountingOption accountingOption68;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption50.Id && a.Name == "��ҵ�����").Count() == 0)
                    //{
                    //    accountingOption68 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption50.Id,
                    //        Name = "��ҵ�����",
                    //        Code = "500403",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.ManagementFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption68);
                    //}
                    //else
                    //{
                    //    accountingOption68 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption50.Id && a.Name == "��ҵ�����").FirstOrDefault();
                    //}

                    ////ˮ���
                    //AccountingOption accountingOption69;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption50.Id && a.Name == "ˮ���").Count() == 0)
                    //{
                    //    accountingOption69 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption50.Id,
                    //        Name = "ˮ���",
                    //        Code = "500404",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.WaterFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption69);
                    //}
                    //else
                    //{
                    //    accountingOption69 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption50.Id && a.Name == "ˮ���").FirstOrDefault();
                    //}

                    ////�ۼ��۾�
                    //AccountingOption accountingOption70;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption50.Id && a.Name == "�ۼ��۾�").Count() == 0)
                    //{
                    //    accountingOption70 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption50.Id,
                    //        Name = "�ۼ��۾�",
                    //        Code = "500405",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.AccumulatedFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption70);
                    //}
                    //else
                    //{
                    //    accountingOption70 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption50.Id && a.Name == "�ۼ��۾�").FirstOrDefault();
                    //}

                    ////�������
                    //AccountingOption accountingOption71;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption51.Id && a.Name == "�������").Count() == 0)
                    //{
                    //    accountingOption71 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption51.Id,
                    //        Name = "�������",
                    //        Code = "500501",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.ExchangeLoss.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption71);
                    //}
                    //else
                    //{
                    //    accountingOption71 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption51.Id && a.Name == "�������").FirstOrDefault();
                    //}

                    ////��Ϣ
                    //AccountingOption accountingOption72;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption51.Id && a.Name == "��Ϣ").Count() == 0)
                    //{
                    //    accountingOption72 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption51.Id,
                    //        Name = "��Ϣ",
                    //        Code = "500502",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.Interest.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption72);
                    //}
                    //else
                    //{
                    //    accountingOption72 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption51.Id && a.Name == "��Ϣ").FirstOrDefault();
                    //}

                    ////������
                    //AccountingOption accountingOption73;
                    //if (accountingOptions == null || accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption51.Id && a.Name == "������").Count() == 0)
                    //{
                    //    accountingOption73 = new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 5,
                    //        ParentId = accountingOption51.Id,
                    //        Name = "������",
                    //        Code = "500503",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = false,
                    //        AccountCodeTypeId = int.Parse(AccountingCodeEnum.PoundageFees.GetTypeCode().ToString())
                    //    };
                    //    accountingOptionService.InsertAccountingOption(accountingOption73);
                    //}
                    //else
                    //{
                    //    accountingOption73 = accountingOptions.Where(a => a.AccountingTypeId == 5 && a.ParentId == accountingOption51.Id && a.Name == "������").FirstOrDefault();
                    //}

                    //#endregion

                    //6������


                    #endregion

                    #region ��˾����

                    var companySetting = _settingService.LoadSetting<CompanySetting>(storeId);

                    //��Ʒ��ϸ��
                    companySetting.OpenBillMakeDate = 0;
                    companySetting.MulProductPriceUnit = 0;
                    companySetting.AllowCreateMulSameBarcode = false;

                    //����ѡ��
                    companySetting.DefaultPurchasePrice = 0;
                    companySetting.VariablePriceCommodity = 0;
                    companySetting.AccuracyRounding = 0;
                    companySetting.MakeBillDisplayBarCode = 0;
                    companySetting.AllowSelectionDateRange = 0;
                    companySetting.DockingTicketPassSystem = false;
                    companySetting.AllowReturnInSalesAndOrders = false;
                    companySetting.AppMaybeDeliveryPersonnel = false;
                    companySetting.AppSubmitOrderAutoAudits = false;
                    companySetting.AppSubmitTransferAutoAudits = false;
                    companySetting.AppSubmitExpenseAutoAudits = false;
                    companySetting.AppSubmitBillReturnAutoAudits = false;
                    companySetting.AppAllowWriteBack = false;
                    companySetting.AllowAdvancePaymentsNegative = false;
                    companySetting.ShowOnlyPrepaidAccountsWithPrepaidReceipts = false;
                    companySetting.TasteByTasteAccountingOnlyPrintMainProduct = false;
                    companySetting.AutoApproveConsumerPaidBill = true;

                    //������
                    companySetting.APPOnlyShowHasStockProduct = false;
                    companySetting.APPShowOrderStock = false;

                    //ҵ��Ա����
                    companySetting.OnStoreStopSeconds = 0;
                    companySetting.EnableSalesmanTrack = false;
                    companySetting.Start = "7:00";
                    companySetting.End = "19:00";
                    companySetting.FrequencyTimer = 1;
                    companySetting.SalesmanOnlySeeHisCustomer = false;
                    companySetting.SalesmanVisitStoreBefore = false;
                    companySetting.SalesmanVisitMustPhotographed = false;
                    companySetting.DoorheadPhotoNum = 1;
                    companySetting.DisplayPhotoNum = 4;
                    companySetting.EnableBusinessTime = false;
                    companySetting.BusinessStart = "7:00";
                    companySetting.BusinessEnd = "19:00";
                    companySetting.EnableBusinessVisitLine = false;

                    //�������
                    companySetting.ReferenceCostPrice = 0;
                    companySetting.AveragePurchasePriceCalcNumber = 0;
                    companySetting.AllowNegativeInventoryMonthlyClosure = false;

                    //��������
                    companySetting.EnableTaxRate = false;
                    companySetting.TaxRate = 0;
                    companySetting.PhotographedWater = "";

                    //�������
                    companySetting.ClearArchiveDatas = false;
                    companySetting.ClearBillDatas = false;

                    _settingService.SaveSetting(companySetting, x => x.OpenBillMakeDate, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.MulProductPriceUnit, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AllowCreateMulSameBarcode, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.DefaultPurchasePrice, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.VariablePriceCommodity, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AccuracyRounding, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.MakeBillDisplayBarCode, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AllowSelectionDateRange, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.DockingTicketPassSystem, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AllowReturnInSalesAndOrders, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AppMaybeDeliveryPersonnel, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AppSubmitOrderAutoAudits, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AppSubmitTransferAutoAudits, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AppSubmitExpenseAutoAudits, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AppSubmitBillReturnAutoAudits, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AppAllowWriteBack, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AllowAdvancePaymentsNegative, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.ShowOnlyPrepaidAccountsWithPrepaidReceipts, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.TasteByTasteAccountingOnlyPrintMainProduct, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.APPOnlyShowHasStockProduct, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.APPShowOrderStock, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.OnStoreStopSeconds, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.EnableSalesmanTrack, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.Start, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.End, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.FrequencyTimer, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.SalesmanOnlySeeHisCustomer, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.SalesmanVisitStoreBefore, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.SalesmanVisitMustPhotographed, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.ReferenceCostPrice, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AveragePurchasePriceCalcNumber, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.AllowNegativeInventoryMonthlyClosure, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.EnableTaxRate, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.TaxRate, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.PhotographedWater, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.ClearArchiveDatas, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.ClearBillDatas, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.DisplayPhotoNum, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.DoorheadPhotoNum, storeId, false);

                    _settingService.SaveSetting(companySetting, x => x.EnableBusinessTime, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.BusinessStart, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.BusinessEnd, storeId, false);
                    _settingService.SaveSetting(companySetting, x => x.EnableBusinessVisitLine, storeId, false);
                    #endregion

                    #region ��ע���� RemarkConfig
                    var _remarkConfigService = EngineContext.Current.Resolve<IRemarkConfigService>();
                    List<RemarkConfig> remarkConfigs = _remarkConfigService.GetAllRemarkConfigs(storeId).ToList();

                    if (remarkConfigs == null || remarkConfigs.Where(r => r.Name == "����").Count() == 0)
                    {
                        RemarkConfig remarkConfig1 = new RemarkConfig()
                        {
                            StoreId = storeId,
                            Name = "����",
                            RemberPrice = true
                        };
                        _remarkConfigService.InsertRemarkConfig(remarkConfig1);
                    }

                    if (remarkConfigs == null || remarkConfigs.Where(r => r.Name == "����").Count() == 0)
                    {
                        RemarkConfig remarkConfig2 = new RemarkConfig()
                        {
                            StoreId = storeId,
                            Name = "����",
                            RemberPrice = true
                        };
                        _remarkConfigService.InsertRemarkConfig(remarkConfig2);
                    }

                    if (remarkConfigs == null || remarkConfigs.Where(r => r.Name == "����").Count() == 0)
                    {
                        RemarkConfig remarkConfig3 = new RemarkConfig()
                        {
                            StoreId = storeId,
                            Name = "����",
                            RemberPrice = true
                        };
                        _remarkConfigService.InsertRemarkConfig(remarkConfig3);
                    }

                    if (remarkConfigs == null || remarkConfigs.Where(r => r.Name == "��ƿ��").Count() == 0)
                    {
                        RemarkConfig remarkConfig4 = new RemarkConfig()
                        {
                            StoreId = storeId,
                            Name = "��ƿ��",
                            RemberPrice = true
                        };
                        _remarkConfigService.InsertRemarkConfig(remarkConfig4);
                    }

                    if (remarkConfigs == null || remarkConfigs.Where(r => r.Name == "������").Count() == 0)
                    {
                        RemarkConfig remarkConfig5 = new RemarkConfig()
                        {
                            StoreId = storeId,
                            Name = "������",
                            RemberPrice = true
                        };
                        _remarkConfigService.InsertRemarkConfig(remarkConfig5);
                    }

                    if (remarkConfigs == null || remarkConfigs.Where(r => r.Name == "����").Count() == 0)
                    {
                        RemarkConfig remarkConfig6 = new RemarkConfig()
                        {
                            StoreId = storeId,
                            Name = "����",
                            RemberPrice = true
                        };
                        _remarkConfigService.InsertRemarkConfig(remarkConfig6);
                    }

                    if (remarkConfigs == null || remarkConfigs.Where(r => r.Name == "����������").Count() == 0)
                    {
                        RemarkConfig remarkConfig7 = new RemarkConfig()
                        {
                            StoreId = storeId,
                            Name = "����������",
                            RemberPrice = true
                        };
                        _remarkConfigService.InsertRemarkConfig(remarkConfig7);
                    }

                    if (remarkConfigs == null || remarkConfigs.Where(r => r.Name == "ר��").Count() == 0)
                    {
                        RemarkConfig remarkConfig8 = new RemarkConfig()
                        {
                            StoreId = storeId,
                            Name = "ר��",
                            RemberPrice = true
                        };
                        _remarkConfigService.InsertRemarkConfig(remarkConfig8);
                    }

                    if (remarkConfigs == null || remarkConfigs.Where(r => r.Name == "�����ߴ���").Count() == 0)
                    {
                        RemarkConfig remarkConfig9 = new RemarkConfig()
                        {
                            StoreId = storeId,
                            Name = "�����ߴ���",
                            RemberPrice = true
                        };
                        _remarkConfigService.InsertRemarkConfig(remarkConfig9);
                    }



                    #endregion

                    #region ��Ʒ����
                    var productSetting = _settingService.LoadSetting<ProductSetting>(storeId);
                    productSetting.SmallUnitSpecificationAttributeOptionsMapping = specificationAttribute3.Id;
                    productSetting.StrokeUnitSpecificationAttributeOptionsMapping = specificationAttribute5.Id;
                    productSetting.BigUnitSpecificationAttributeOptionsMapping = specificationAttribute4.Id;

                    _settingService.SaveSetting(productSetting, x => x.SmallUnitSpecificationAttributeOptionsMapping, storeId, false);
                    _settingService.SaveSetting(productSetting, x => x.StrokeUnitSpecificationAttributeOptionsMapping, storeId, false);
                    _settingService.SaveSetting(productSetting, x => x.BigUnitSpecificationAttributeOptionsMapping, storeId, false);

                    #endregion

                    //#region ��������

                    //var financeSetting = _settingService.LoadSetting<FinanceSetting>(storeId);

                    ////���۵�(�տ��˻�)
                    //FinanceAccountingMap saleFinanceAccountingMap = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (saleFinanceAccountingMap.Options == null || saleFinanceAccountingMap.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    saleFinanceAccountingMap.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (saleFinanceAccountingMap.Options == null || saleFinanceAccountingMap.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    saleFinanceAccountingMap.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //saleFinanceAccountingMap.DefaultOption = accountingOption11.Id;
                    //saleFinanceAccountingMap.DebitOption = accountingOption11.Id;
                    //saleFinanceAccountingMap.CreditOption = accountingOption11.Id;
                    //financeSetting.SaleBillAccountingOptionConfiguration = JsonConvert.SerializeObject(saleFinanceAccountingMap);

                    ////���۶���(�տ��˻�)
                    //FinanceAccountingMap saleReservationBillFinanceAccountingMap = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (saleReservationBillFinanceAccountingMap.Options == null || saleReservationBillFinanceAccountingMap.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    saleReservationBillFinanceAccountingMap.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (saleReservationBillFinanceAccountingMap.Options == null || saleReservationBillFinanceAccountingMap.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    saleReservationBillFinanceAccountingMap.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //saleReservationBillFinanceAccountingMap.DefaultOption = accountingOption11.Id;
                    //saleReservationBillFinanceAccountingMap.DebitOption = accountingOption11.Id;
                    //saleReservationBillFinanceAccountingMap.CreditOption = accountingOption11.Id;
                    //financeSetting.SaleReservationBillAccountingOptionConfiguration = JsonConvert.SerializeObject(saleReservationBillFinanceAccountingMap);

                    ////�˻���(�տ��˻�)
                    //FinanceAccountingMap returnBillAccountingOptionConfiguration = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (returnBillAccountingOptionConfiguration.Options == null || returnBillAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    returnBillAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (returnBillAccountingOptionConfiguration.Options == null || returnBillAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    returnBillAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //returnBillAccountingOptionConfiguration.DefaultOption = accountingOption11.Id;
                    //returnBillAccountingOptionConfiguration.DebitOption = accountingOption11.Id;
                    //returnBillAccountingOptionConfiguration.CreditOption = accountingOption11.Id;
                    //financeSetting.ReturnBillAccountingOptionConfiguration = JsonConvert.SerializeObject(returnBillAccountingOptionConfiguration);

                    ////�˻�����(�տ��˻�)
                    //FinanceAccountingMap returnReservationBillAccountingOptionConfiguration = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (returnReservationBillAccountingOptionConfiguration.Options == null || returnReservationBillAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    returnReservationBillAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (returnReservationBillAccountingOptionConfiguration.Options == null || returnReservationBillAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    returnReservationBillAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //returnReservationBillAccountingOptionConfiguration.DefaultOption = accountingOption11.Id;
                    //returnReservationBillAccountingOptionConfiguration.DebitOption = accountingOption11.Id;
                    //returnReservationBillAccountingOptionConfiguration.CreditOption = accountingOption11.Id;
                    //financeSetting.ReturnReservationBillAccountingOptionConfiguration = JsonConvert.SerializeObject(returnReservationBillAccountingOptionConfiguration);

                    ////�տ(�տ��˻�)
                    //FinanceAccountingMap receiptAccountingOptionConfiguration = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (receiptAccountingOptionConfiguration.Options == null || receiptAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    receiptAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (receiptAccountingOptionConfiguration.Options == null || receiptAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    receiptAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //receiptAccountingOptionConfiguration.DefaultOption = accountingOption11.Id;
                    //receiptAccountingOptionConfiguration.DebitOption = accountingOption11.Id;
                    //receiptAccountingOptionConfiguration.CreditOption = accountingOption11.Id;
                    //financeSetting.ReceiptAccountingOptionConfiguration = JsonConvert.SerializeObject(receiptAccountingOptionConfiguration);

                    ////���(�����˻�)
                    //FinanceAccountingMap paymentAccountingOptionConfiguration = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (paymentAccountingOptionConfiguration.Options == null || paymentAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    paymentAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (paymentAccountingOptionConfiguration.Options == null || paymentAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    paymentAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //paymentAccountingOptionConfiguration.DefaultOption = accountingOption11.Id;
                    //paymentAccountingOptionConfiguration.DebitOption = accountingOption11.Id;
                    //paymentAccountingOptionConfiguration.CreditOption = accountingOption11.Id;
                    //financeSetting.PaymentAccountingOptionConfiguration = JsonConvert.SerializeObject(paymentAccountingOptionConfiguration);

                    ////Ԥ�տ(�տ��˻�)
                    //FinanceAccountingMap advanceReceiptAccountingOptionConfiguration = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (advanceReceiptAccountingOptionConfiguration.Options == null || advanceReceiptAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    advanceReceiptAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (advanceReceiptAccountingOptionConfiguration.Options == null || advanceReceiptAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    advanceReceiptAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //advanceReceiptAccountingOptionConfiguration.DefaultOption = accountingOption11.Id;
                    //advanceReceiptAccountingOptionConfiguration.DebitOption = accountingOption11.Id;
                    //advanceReceiptAccountingOptionConfiguration.CreditOption = accountingOption11.Id;
                    //financeSetting.AdvanceReceiptAccountingOptionConfiguration = JsonConvert.SerializeObject(advanceReceiptAccountingOptionConfiguration);

                    ////Ԥ���(�����˻�)
                    //FinanceAccountingMap advancePaymentAccountingOptionConfiguration = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (advancePaymentAccountingOptionConfiguration.Options == null || advancePaymentAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    advancePaymentAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (advancePaymentAccountingOptionConfiguration.Options == null || advancePaymentAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    advancePaymentAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //advancePaymentAccountingOptionConfiguration.DefaultOption = accountingOption11.Id;
                    //advancePaymentAccountingOptionConfiguration.DebitOption = accountingOption11.Id;
                    //advancePaymentAccountingOptionConfiguration.CreditOption = accountingOption11.Id;
                    //financeSetting.AdvancePaymentAccountingOptionConfiguration = JsonConvert.SerializeObject(advancePaymentAccountingOptionConfiguration);

                    ////�ɹ���(�����˻�)
                    //FinanceAccountingMap purchaseBillAccountingOptionConfiguration = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (purchaseBillAccountingOptionConfiguration.Options == null || purchaseBillAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    purchaseBillAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (purchaseBillAccountingOptionConfiguration.Options == null || purchaseBillAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    purchaseBillAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //purchaseBillAccountingOptionConfiguration.DefaultOption = accountingOption11.Id;
                    //purchaseBillAccountingOptionConfiguration.DebitOption = accountingOption11.Id;
                    //purchaseBillAccountingOptionConfiguration.CreditOption = accountingOption11.Id;
                    //financeSetting.PurchaseBillAccountingOptionConfiguration = JsonConvert.SerializeObject(purchaseBillAccountingOptionConfiguration);

                    ////�ɹ��˻���(�����˻�)
                    //FinanceAccountingMap purchaseReturnBillAccountingOptionConfiguration = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (purchaseReturnBillAccountingOptionConfiguration.Options == null || purchaseReturnBillAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    purchaseReturnBillAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (purchaseReturnBillAccountingOptionConfiguration.Options == null || purchaseReturnBillAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    purchaseReturnBillAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //purchaseReturnBillAccountingOptionConfiguration.DefaultOption = accountingOption11.Id;
                    //purchaseReturnBillAccountingOptionConfiguration.DebitOption = accountingOption11.Id;
                    //purchaseReturnBillAccountingOptionConfiguration.CreditOption = accountingOption11.Id;
                    //financeSetting.PurchaseReturnBillAccountingOptionConfiguration = JsonConvert.SerializeObject(purchaseReturnBillAccountingOptionConfiguration);

                    ////����֧��(֧���˻�)
                    //FinanceAccountingMap costExpenditureAccountingOptionConfiguration = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (costExpenditureAccountingOptionConfiguration.Options == null || costExpenditureAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    costExpenditureAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (costExpenditureAccountingOptionConfiguration.Options == null || costExpenditureAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    costExpenditureAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //costExpenditureAccountingOptionConfiguration.DefaultOption = accountingOption11.Id;
                    //costExpenditureAccountingOptionConfiguration.DebitOption = accountingOption11.Id;
                    //costExpenditureAccountingOptionConfiguration.CreditOption = accountingOption11.Id;
                    //financeSetting.CostExpenditureAccountingOptionConfiguration = JsonConvert.SerializeObject(costExpenditureAccountingOptionConfiguration);

                    ////�������루�տ��˻���
                    //FinanceAccountingMap financialIncomeAccountingOptionConfiguration = new FinanceAccountingMap();
                    ////�ֽ�
                    //if (financialIncomeAccountingOptionConfiguration.Options == null || financialIncomeAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption1.Id && so.Name == "�ֽ�").Count() == 0)
                    //{
                    //    financialIncomeAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption1.Id,
                    //        Name = "�ֽ�",
                    //        Code = "100101",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    ////����
                    //if (financialIncomeAccountingOptionConfiguration.Options == null || financialIncomeAccountingOptionConfiguration.Options.Where(so => so.AccountingTypeId == 1 && so.ParentId == accountingOption2.Id && so.Name == "����").Count() == 0)
                    //{
                    //    financialIncomeAccountingOptionConfiguration.Options.Add(new AccountingOption()
                    //    {
                    //        StoreId = storeId,
                    //        AccountingTypeId = 1,
                    //        ParentId = accountingOption2.Id,
                    //        Name = "����",
                    //        Code = "100201",
                    //        DisplayOrder = 0,
                    //        Enabled = true,
                    //        IsDefault = true
                    //    });
                    //}

                    //financialIncomeAccountingOptionConfiguration.DefaultOption = accountingOption11.Id;
                    //financialIncomeAccountingOptionConfiguration.DebitOption = accountingOption11.Id;
                    //financialIncomeAccountingOptionConfiguration.CreditOption = accountingOption11.Id;
                    //financeSetting.FinancialIncomeAccountingOptionConfiguration = JsonConvert.SerializeObject(financialIncomeAccountingOptionConfiguration);

                    //_settingService.SaveSetting(financeSetting, x => x.SaleBillAccountingOptionConfiguration, storeId, false);
                    //_settingService.SaveSetting(financeSetting, x => x.SaleReservationBillAccountingOptionConfiguration, storeId, false);

                    //_settingService.SaveSetting(financeSetting, x => x.ReturnBillAccountingOptionConfiguration, storeId, false);
                    //_settingService.SaveSetting(financeSetting, x => x.ReturnReservationBillAccountingOptionConfiguration, storeId, false);

                    //_settingService.SaveSetting(financeSetting, x => x.ReceiptAccountingOptionConfiguration, storeId, false);
                    //_settingService.SaveSetting(financeSetting, x => x.PaymentAccountingOptionConfiguration, storeId, false);

                    //_settingService.SaveSetting(financeSetting, x => x.AdvanceReceiptAccountingOptionConfiguration, storeId, false);
                    //_settingService.SaveSetting(financeSetting, x => x.AdvancePaymentAccountingOptionConfiguration, storeId, false);

                    //_settingService.SaveSetting(financeSetting, x => x.PurchaseBillAccountingOptionConfiguration, storeId, false);
                    //_settingService.SaveSetting(financeSetting, x => x.PurchaseReturnBillAccountingOptionConfiguration, storeId, false);

                    //_settingService.SaveSetting(financeSetting, x => x.CostExpenditureAccountingOptionConfiguration, storeId, false);
                    //_settingService.SaveSetting(financeSetting, x => x.FinancialIncomeAccountingOptionConfiguration, storeId, false);

                    //#endregion

                    #endregion

                    //scope.Complete();
                }

            }
            catch (Exception)
            {
                fg = false;
            }
            return fg;

        }

        public string[] GetNotExistingStores(string[] storeIdsNames)
        {
            if (storeIdsNames == null)
            {
                throw new ArgumentNullException(nameof(storeIdsNames));
            }

            var query = StoreRepository_RO.Table;
            var queryFilter = storeIdsNames.Distinct().ToArray();
            //filtering by name
            var filter = query.Select(store => store.Name).Where(store => queryFilter.Contains(store)).ToList();
            queryFilter = queryFilter.Except(filter).ToArray();

            //if some names not found
            if (!queryFilter.Any())
            {
                return queryFilter.ToArray();
            }

            //filtering by IDs
            filter = query.Select(store => store.Id.ToString()).Where(store => queryFilter.Contains(store)).ToList();
            queryFilter = queryFilter.Except(filter).ToArray();

            return queryFilter.ToArray();
        }

    }
}