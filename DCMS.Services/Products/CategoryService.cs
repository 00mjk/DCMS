using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Data;
using DCMS.Core.Domain.Common;
using DCMS.Core.Domain.Products;
using DCMS.Core.Domain.Tasks;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Events;
using DCMS.Services.Tasks;
using DCMS.Services.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using DCMS.Services.Caching;


namespace DCMS.Services.Products
{
    /// <summary>
    ///  ������
    /// </summary>
    public partial class CategoryService : BaseService, ICategoryService
    {
        private readonly IUserService _userService;
        private readonly IQueuedMessageService _queuedMessageService;
        
        public CategoryService(IServiceGetter getter,
            IStaticCacheManager cacheManager,
           
            IEventPublisher eventPublisher,
            IUserService userService,
            IQueuedMessageService queuedMessageService
            ) : base(getter, cacheManager, eventPublisher)
        {
            
            _userService = userService;
            _queuedMessageService = queuedMessageService;
        }

        #region ����


        /// <summary>
        /// ɾ�����
        /// </summary>
        /// <param name="category"></param>
        public virtual void DeleteCategory(Category category)
        {
            if (category == null)
            {
                throw new ArgumentNullException("category");
            }

            if (!category.IsPreset)
            {
                category.Deleted = true;
                UpdateCategory(category);

                //�Ӽ�Ҳɾ��
                var subcategories = GetAllCategoriesByParentCategoryId(category.StoreId, category.Id);
                foreach (var subcategory in subcategories)
                {
                    //subcategory.ParentId = 0;
                    subcategory.Deleted = true;
                    UpdateCategory(subcategory);
                }
            }

        }

        /// <summary>
        /// ��ȡȫ�����
        /// </summary>
        /// <param name="categoryName">�������</param>
        /// <param name="pageIndex">ҳ</param>
        /// <param name="pageSize">ҳ��</param>
        /// <param name="showHidden">�Ƿ���ʾ</param>
        /// <returns></returns>
        public virtual IPagedList<Category> GetAllCategories(int? store, string categoryName = "",
            int pageIndex = 0, int pageSize = int.MaxValue, bool showHidden = false)
        {
            if (pageSize >= 50)
                pageSize = 50;
            var query = CategoriesRepository.Table;

            if (store.HasValue && store.Value != 0)
            {
                query = query.Where(c => c.StoreId == store);
            }

            if (!showHidden)
            {
                query = query.Where(c => c.Published);
            }

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(c => c.Name.Contains(categoryName));
            }

            query = query.Where(c => !c.Deleted);
            query = query.OrderBy(c => c.ParentId).ThenBy(c => c.OrderNo);

            if (!showHidden)
            {
                query = query.OrderBy(c => c.ParentId).ThenBy(c => c.OrderNo);
            }

            var unsortedCategories = query.ToList();

            //������
            var sortedCategories = unsortedCategories.SortCategoriesForTree();

            //��ҳ
            return new PagedList<Category>(sortedCategories, pageIndex, pageSize);
        }



        /// <summary>
        /// ��ȡȫ���������
        /// </summary>
        /// <param name="categoryName">�������</param>
        /// <param name="pageIndex">ҳ</param>
        /// <param name="pageSize">ҳ��</param>
        /// <param name="showHidden">�Ƿ���ʾ</param>
        /// <returns></returns>
        public virtual List<Category> GetAllCategories(int? store)
        {
            if (!store.HasValue)
            {
                return new List<Category>();
            }

            var query = CategoriesRepository.Table.Where(c => c.StoreId == store && c.Deleted == false);
            query = query.OrderBy(c => c.ParentId).ThenBy(c => c.OrderNo);

            var key = DCMSDefaults.GETALLCATEGORIES_KEY.FillCacheKey(store);
            return _cacheManager.Get(key, () => query.ToList());
        }


        /// <summary>
        /// ���ݸ�������ȡȫ�����
        /// </summary>
        /// <param name="parentCategoryId"></param>
        /// <param name="showHidden"></param>
        /// <returns></returns>
        public IList<Category> GetAllCategoriesByParentCategoryId(int? store, int parentCategoryId,
            bool showHidden = false)
        {
            var key = DCMSDefaults.CATEGORIES_BY_PARENT_CATEGORY_ID_KEY.FillCacheKey(store.Value, parentCategoryId, showHidden, 0, store);

            return _cacheManager.Get(key, () =>
            {
                try
                {
                    var query = CategoriesRepository.TableNoTracking;

                    if (store.HasValue)
                    {
                        query = query.Where(c => c.StoreId == store.Value);
                    }

                    if (!showHidden)
                    {
                        query = query.Where(c => c.Published);
                    }

                    query = query.Where(c => c.ParentId == parentCategoryId);
                    query = query.Where(c => !c.Deleted);
                    query = query.OrderBy(c => c.OrderNo);

                    //���ʿ��ƣ��Ƿ�����ָ���˲鿴��CurrentUser ȡֵ,�Ժ�����������߼�...��

                    //var grops = query
                    //.Select((xx, index) => new { Index = index, Value = xx })
                    //.GroupBy(group => group.Index)
                    //.Select(group => group.Select(xx => xx.Value)
                    //    .ToList()).ToList();

                    query = query.OrderBy(c => c.OrderNo);

                    return query.ToList();
                }
                catch (Exception)
                {
                    return new List<Category>();
                }
            });
            //try
            //{
            //    var query = CategoriesRepository.TableNoTracking;

            //    if (store.HasValue)
            //    {
            //        query = query.Where(c => c.StoreId == store.Value);
            //    }

            //    if (!showHidden)
            //    {
            //        query = query.Where(c => c.Published);
            //    }

            //    query = query.Where(c => c.ParentId == parentCategoryId);
            //    query = query.Where(c => !c.Deleted);
            //    query = query.OrderBy(c => c.OrderNo);

            //    //���ʿ��ƣ��Ƿ�����ָ���˲鿴��CurrentUser ȡֵ,�Ժ�����������߼�...��

            //    query1 = query1.OrderBy(c => c.OrderNo);

            //    return query1.ToList();
            //}
            //catch (Exception)
            //{
            //    return new List<Category>();
            //}

        }


        public IList<Category> GetAllCategoriesByParentCategoryId(int? store, List<Category> allCategoies, int parentCategoryId, bool showHidden = false)
        {
            try
            {
                var query = allCategoies.Where(c => c.StoreId == store.Value);

                if (!showHidden)
                {
                    query = query.Where(c => c.Published);
                }

                query = query.Where(c => c.ParentId == parentCategoryId);
                query = query.Where(c => !c.Deleted);
                query = query.OrderBy(c => c.OrderNo);

                //���ʿ��ƣ��Ƿ�����ָ���˲鿴��CurrentUser ȡֵ,�Ժ�����������߼�...��
                query = query.OrderBy(c => c.OrderNo);

                return query.ToList();
            }
            catch (Exception)
            {
                return new List<Category>();
            }

        }


        /// <summary>
        /// ��ȡȫ��������ʾ�����
        /// </summary>
        /// <returns></returns>
        public virtual IList<Category> GetAllCategoriesDisplayed(int? store)
        {
            try
            {
                var query = from c in CategoriesRepository.Table
                            orderby c.OrderNo
                            where c.Published && c.StoreId == store &&
                            !c.Deleted
                            select c;

                var categories = query.ToList();
                return categories;
            }
            catch (Exception)
            {
                return new List<Category>();
            }
        }

        /// <summary>
        /// ����Ʒ�����Ϣ
        /// </summary>
        /// <param name="store"></param>
        /// <returns></returns>
        public virtual IList<Category> BindCategoryList(int? store)
        {
            var key = DCMSDefaults.BINDCATEGORIES_ALL_STORE_KEY.FillCacheKey(store);
            return _cacheManager.Get(key, () =>
            {
                var query = from c in CategoriesRepository.Table
                            orderby c.OrderNo
                            where c.Published && c.StoreId == store &&
                            !c.Deleted
                            select c;
                var result = query
                .Select(q => new { q.Id, q.ParentId, q.Name })
                .ToList()
                .Select(x => new Category { Id = x.Id, ParentId = x.ParentId, Name = x.Name }).ToList();
                return result;
            });
        }


        /// <summary>
        /// ��ȡ���
        /// </summary>
        /// <param name="categoryId"></param>
        /// <returns></returns>
        public virtual Category GetCategoryById(int? store, int categoryId)
        {
            if (categoryId == 0)
            {
                return null;
            }

            //return CategoriesRepository.ToCachedGetById(categoryId);
            return CategoriesRepository.GetById(categoryId);
        }


        public virtual string GetCategoryName(int? store, int categoryId)
        {
            //var category = GetCategoryById(categoryId);
            //return category != null ? category.Name : "";

            if (categoryId == 0)
            {
                return "";
            }

            var key = DCMSDefaults.CATEGORY_NAME_BY_ID_KEY.FillCacheKey(store ?? 0, categoryId);
            return _cacheManager.Get(key, () =>
            {
                return CategoriesRepository.Table.Where(a => a.Id == categoryId).Select(a => a.Name).FirstOrDefault();
            });

        }

        public virtual int GetCategoryId(int store, string categoryName)
        {
            var query = CategoriesRepository.Table;

            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return 0;
            }

            return query.Where(s => s.StoreId == store && s.Name == categoryName).Select(s => s.Id).FirstOrDefault();
        }

        public virtual IList<Category> GetAllCategoriesByIds(int? store, int[] ids)
        {
            var query = from c in CategoriesRepository.Table
                        orderby c.OrderNo
                        where c.Published && c.StoreId == store &&
                        !c.Deleted && ids.Contains(c.Id)
                        select c;
            var categories = query.ToList();
            return categories;
        }


        public virtual Dictionary<int, string> GetAllCategoriesNames(int? store, int[] ids)
        {
            var categories = CategoriesRepository.TableNoTracking
                        .Where(c => c.Published && c.StoreId == store && c.Deleted == false && ids.Contains(c.Id))
                        .ToDictionary(k => k.Id, v => v.Name);
            return categories;
        }

        public virtual IList<Category> GetCategoriesByCategoryIds(int? store, int[] ids)
        {

            if (ids == null || ids.Length == 0)
            {
                return new List<Category>();
            }

            var key = DCMSDefaults.CATEGORIES_BY_IDS_KEY.FillCacheKey(store, string.Join("_", ids.OrderBy(a => a)));
            return _cacheManager.Get(key, () =>
            {
                var query = from c in CategoriesRepository.Table
                            orderby c.OrderNo
                            where c.StoreId == store &&
                             ids.Contains(c.Id)
                            select c;
                var categories = query.ToList();
                return categories;
            });
        }

        public virtual IList<Category> GetCategoriesIdsByCategoryIds(int? store, int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return new List<Category>();
            }

            var key = DCMSDefaults.CATEGORIES_NOTRACT_BY_IDS_KEY.FillCacheKey(store, string.Join("_", ids.OrderBy(a => a)));
            return _cacheManager.Get(key, () =>
            {
                var query = from c in CategoriesRepository_RO.TableNoTracking
                            orderby c.OrderNo
                            where c.StoreId == store &&
                             ids.Contains(c.Id)
                            select new Category() { Id = c.Id, Name = c.Name };
                var categories = query.ToList();
                return categories;
            });
        }

        public string GetCategoriesName(int? store, int? categoryid)
        {
            if (categoryid.HasValue)
            {
                var cat = CategoriesRepository_RO.TableNoTracking.Where(c => c.StoreId == store && c.Id == categoryid).FirstOrDefault();
                return cat?.Name;
            }
            else
            {
                return "";
            }
        }

        public int GetCategoriesMinId(int? store)
        {
            if (store == 0)
            {
                return 0;
            }

            var query = CategoriesRepository.Table;

            return query.Where(s => s.StoreId == store).Min(s => s.Id);
        }

        /// <summary>
        /// ������
        /// </summary>
        /// <param name="category"></param>
        public virtual void InsertCategory(Category category)
        {
            if (category == null)
            {
                throw new ArgumentNullException("category");
            }

            var uow = CategoriesRepository.UnitOfWork;
            CategoriesRepository.Insert(category);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(category);
        }

        /// <summary>
        /// ���»���
        /// </summary>
        /// <param name="category"></param>
        public virtual void UpdateCategory(Category category)
        {
            if (category == null)
            {
                throw new ArgumentNullException("category");
            }

            var parentCategory = GetCategoryById(category.StoreId, category.ParentId);
            while (parentCategory != null)
            {
                if (category.Id == parentCategory.Id)
                {
                    category.ParentId = 0;
                    break;
                }
                parentCategory = GetCategoryById(parentCategory.StoreId, parentCategory.ParentId);
            }

            var uow = CategoriesRepository.UnitOfWork;
            CategoriesRepository.Update(category);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(category);
        }


        /// <summary>
        /// ɾ����Ʒ���
        /// </summary>
        /// <param name="productCategory"></param>
        public virtual void DeleteProductCategory(ProductCategory productCategory)
        {
            if (productCategory == null)
            {
                throw new ArgumentNullException("productCategory");
            }

            var uow = ProductsCategoryMappingRepository.UnitOfWork;
            ProductsCategoryMappingRepository.Delete(productCategory);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(productCategory);

        }

        /// <summary>
        /// �������ID��ȡ��Ʒ��𼯺ϣ�ע�⣬ͨ����ϵ��Ʒ�Ѿ�������
        /// </summary>
        /// <param name="categoryId">���ID</param>
        /// <param name="pageIndex">ҳ</param>
        /// <param name="pageSize">ҳ��</param>
        /// <param name="showHidden">�Ƿ���ʾ</param>
        /// <returns></returns>
        public virtual IPagedList<ProductCategory> GetProductCategoriesByCategoryId(int categoryId, int? userId, int? storeId, int pageIndex, int pageSize, bool showHidden = false)
        {
            if (pageSize >= 50)
                pageSize = 50;
            if (categoryId == 0)
            {
                return new PagedList<ProductCategory>(new List<ProductCategory>(), pageIndex, pageSize);
            }

            var key = DCMSDefaults.PRODUCTCATEGORIES_ALLBYCATEGORYID_KEY.FillCacheKey(storeId, showHidden, categoryId, pageIndex, pageSize, userId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in ProductsCategoryMappingRepository.Table
                            join p in ProductsRepository.Table on pc.ProductId equals p.Id
                            where pc.CategoryId == categoryId &&
                                  !p.Deleted &&
                                  (showHidden || p.Published)
                            orderby pc.DisplayOrder
                            select pc;

                if (!showHidden)
                {

                    query = query.OrderBy(pc => pc.DisplayOrder);
                }

                //var productCategories = new PagedList<ProductCategory>(query.ToList(), pageIndex, pageSize);
                //return productCategories;

                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<ProductCategory>(plists, pageIndex, pageSize, totalCount);

            });
        }

        /// <summary>
        /// ���ݲ�ƷID��ȡ��Ʒ����𼯺ϣ�һ����Ʒ���Զ�Ӧ������(�������Ʒ)��
        /// </summary>
        /// <param name="productId">��ƷID</param>
        /// <param name="showHidden">�Ƿ���ʾ</param>
        /// <returns></returns>
        public virtual IList<ProductCategory> GetProductCategoriesByProductId(int productId, int? userId, int? storeId, bool showHidden = false)
        {
            if (productId == 0)
            {
                return new List<ProductCategory>();
            }

            var key = DCMSDefaults.PRODUCTCATEGORIES_ALLBYPRODUCTID_KEY.FillCacheKey(storeId, showHidden, productId, userId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pc in ProductsCategoryMappingRepository.Table
                            join c in CategoriesRepository.Table on pc.CategoryId equals c.Id
                            where pc.ProductId == productId &&
                                  !c.Deleted &&
                                  (showHidden || c.Published)
                            orderby pc.DisplayOrder
                            select pc;

                var allProductCategories = query.ToList();
                var result = new List<ProductCategory>();
                if (!showHidden)
                {
                    foreach (var pc in allProductCategories)
                    {
                        result.Add(pc);
                    }
                }
                else
                {
                    result.AddRange(allProductCategories);
                }
                return result;
            });
        }

        /// <summary>
        /// ��ȡ��Ʒ���ӳ��ʵ��
        /// </summary>
        public virtual ProductCategory GetProductCategoryById(int productCategoryId)
        {
            if (productCategoryId == 0)
            {
                return null;
            }

            return ProductsCategoryMappingRepository.ToCachedGetById(productCategoryId);
        }



        /// <summary>
        /// ��Ӳ�Ʒ���ӳ��
        /// </summary>
        /// <param name="productCategory"></param>
        public virtual void InsertProductCategory(ProductCategory productCategory)
        {
            if (productCategory == null)
            {
                throw new ArgumentNullException("productCategory");
            }

            var uow = ProductsCategoryMappingRepository.UnitOfWork;
            ProductsCategoryMappingRepository.Insert(productCategory);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(productCategory);
        }

        /// <summary>
        /// ���²�Ʒ���ӳ��
        /// </summary>
        public virtual void UpdateProductCategory(ProductCategory productCategory)
        {
            if (productCategory == null)
            {
                throw new ArgumentNullException("productCategory");
            }

            var uow = ProductsCategoryMappingRepository.UnitOfWork;
            ProductsCategoryMappingRepository.Update(productCategory);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(productCategory);
        }

        /// <summary>
        /// ��ȡ��ǰ�ڵ㼰������ڵ�
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="categoryId"></param>
        /// <returns></returns>
        public List<int> GetSubCategoryIds(int storeId, int categoryId)
        {
            return GetTreeSubCategoryIds(storeId, categoryId).ToList();
        }



        /// <summary>
        /// ��ȡ����Ʒ�����
        /// </summary>
        /// <param name="store"></param>
        /// <param name="categoryId"></param>
        /// <returns></returns>
        public virtual IList<int> GetTreeSubCategoryIds(int? store, int categoryId)
        {
            try
            {
                var defaultParentId = CategoriesRepository_RO
              .TableNoTracking
              .Where(s => s.StoreId == store && s.ParentId == 0).Select(s => s.Id).FirstOrDefault();

                categoryId = ((categoryId == 0) ? defaultParentId : categoryId);
                //string stringsql = $"SELECT id FROM (SELECT t1.id,IF ( find_in_set( ParentId, @pids ) > 0, @pids := concat( @pids, ',', id ), 0 ) AS ischild FROM ( SELECT  id, ParentId FROM dcms.Categories where StoreId = {store} and Published = 1 ) t1,( SELECT @pids := {categoryId} ) t2 ) t3";
                string sql = $"SELECT id as Value FROM dcms.Categories WHERE StoreId = {store} AND Published = 1 and parentid = {categoryId} union all SELECT id as Value FROM  dcms.Categories WHERE StoreId = {store} AND Published = 1 and id = {categoryId}";
                //WHERE ischild != 0 �������Լ�
                var categoryIds = CategoriesRepository_RO.QueryFromSql<IntQueryType>(sql).Select(s => s.Value ?? 0).ToList();
                return categoryIds;
            }
            catch (Exception)
            {
                return null;
            }

        }


        public virtual Dictionary<int, string> GetAllCategoriesNames(int? store, int categoryId)
        {
            var defaultParentId = CategoriesRepository_RO
                .TableNoTracking
                .Where(s => s.StoreId == store && s.ParentId == 0).Select(s => s.Id).FirstOrDefault();

            categoryId = ((categoryId == 0) ? defaultParentId : categoryId);

            //WHERE ischild != 0 �������Լ�
            var categoryIds = CategoriesRepository_RO.QueryFromSql<DictType>($"SELECT id,name FROM (SELECT t1.id, t1.name,IF ( find_in_set( ParentId, @pids ) > 0, @pids := concat( @pids, ',', id ), 0 ) AS ischild FROM ( SELECT  id,Name, ParentId FROM dcms.Categories where StoreId = {store} and Published = 1 ) t1,( SELECT @pids := {categoryId} ) t2 ) t3;").ToDictionary(k => k.Id, v => v.Name);
            return categoryIds;
        }



        public BaseResult BillCreateOrUpdate(int storeId, int userId, int? billId, Category category, Category data, bool isAdmin = false)
        {
            var uow = ReturnBillsRepository.UnitOfWork;

            ITransaction transaction = null;
            try
            {

                transaction = uow.BeginOrUseTransaction();



                //ҵ��Ա


                if (billId.HasValue && billId.Value != 0)
                {
                    //var category = _productCategoryService.GetCategoryById(id ?? 0);
                    if (category != null)
                    {
                        //var model = data.ToEntity(category);

                        category.StoreId = storeId;

                        UpdateCategory(category);
                    }
                }
                else
                {
                    #region ���

                    //var model = data.ToEntity();

                    category.StoreId = storeId;

                    InsertCategory(category);

                    #endregion
                }
                //����Id
                billId = category.Id;


                //����Ա�����Զ����
                if (isAdmin) //�жϵ�ǰ��¼���Ƿ�Ϊ����Ա,��Ϊ����Ա�������Զ����
                {
                    AuditingNoTran(userId, category);
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
                            BillId = category.Id,
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
        public BaseResult AuditingNoTran(int userId, Category category)
        {
            try
            {

                #region �޸ķ���״̬

                category.Status = 0;
                UpdateCategory(category);
                #endregion



                #region ����֪ͨ
                try
                {
                    //�Ƶ��ˡ�����Ա
                  
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

        /// <summary>
        /// ��ȡ��Ʒ����µ���Ʒ���ѿ�����
        /// </summary>
        /// <param name="store"></param>
        /// <param name="CategoryId"></param>
        /// <param name="showHidden"></param>
        /// <returns></returns>
        public IList<int> GetProductedId(int? store, int CategoryId)
        {
            if (CategoryId == 0)
            {
                return null;
            }
            var producList = from pr in ProductsRepository.Table
                             where pr.StoreId == store && pr.HasSold == true && pr.CategoryId == CategoryId
                             select pr.Id;
            return producList.ToList();
        }
        /// <summary>
        /// ��ȡ��Ʒ����µ���Ʒ
        /// </summary>
        /// <param name="store"></param>
        /// <param name="CategoryId"></param>
        /// <param name="showHidden"></param>
        /// <returns></returns>
        public IList<int> GetProductedId1(int? store, int CategoryId)
        {
            if (CategoryId == 0)
            {
                return null;
            }
            var producList = from pr in ProductsRepository.Table
                             where pr.StoreId == store && pr.CategoryId == CategoryId
                             select pr.Id;
            return producList.ToList();
        }
        #endregion
    }
}
