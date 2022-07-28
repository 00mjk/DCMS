using DCMS.Core.Domain.Products;
using DCMS.Services.Security;
using DCMS.Services.Stores;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DCMS.Services.Products
{
    /// <summary>
    /// ��Ʒ�����չ
    /// </summary>
    public static class CategoryExtensions
    {

        /// <summary>
        /// ���������
        /// </summary>
        /// <param name="source">Դ</param>
        /// <param name="parentId">���ڵ�</param>
        /// <param name="ignoreCategoriesWithoutExistingParent">�Ƿ���Բ����ڵĸ���</param>
        /// <returns></returns>
        public static IList<Category> SortCategoriesForTree(this IList<Category> source, int parentId = 0, bool ignoreCategoriesWithoutExistingParent = false)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var result = new List<Category>();

            foreach (var cat in source.ToList().FindAll(c => c.ParentId == parentId))
            {
                result.Add(cat);
                result.AddRange(SortCategoriesForTree(source, cat.Id, ignoreCategoriesWithoutExistingParent));
            }
            if (!ignoreCategoriesWithoutExistingParent && result.Count != source.Count)
            {
                foreach (var cat in source)
                {
                    if (result.FirstOrDefault(x => x.Id == cat.Id) == null)
                    {
                        if (cat.Id == parentId)
                        {
                            result.Add(cat);
                        }
                    }
                }
            }
            return result;
        }


        /// <summary>
        /// ������Ʒ���������������
        /// </summary>
        /// <param name="source"></param>
        /// <param name="productId"></param>
        /// <param name="categoryId"></param>
        /// <returns></returns>
        public static ProductCategory FindProductCategory(this IList<ProductCategory> source,
            int productId, int categoryId)
        {
            foreach (var productCategory in source)
            {
                if (productCategory.ProductId == productId && productCategory.CategoryId == categoryId)
                {
                    return productCategory;
                }
            }

            return null;
        }

        /// <summary>
        /// ��ʽ����𣨷����������������磺��>>ơ��>>ѩ��ơ�ƣ�
        /// </summary>
        /// <param name="category"></param>
        /// <param name="categoryService"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public static string GetFormattedBreadCrumb(this Category category,
            ICategoryService categoryService,
            string separator = ">>")
        {
            if (category == null)
            {
                throw new ArgumentNullException("category");
            }

            string result = string.Empty;


            var alreadyProcessedCategoryIds = new List<int>() { };

            while (category != null &&
                !alreadyProcessedCategoryIds.Contains(category.Id))
            {
                if (string.IsNullOrEmpty(result))
                {
                    result = category.Name;
                }
                else
                {
                    result = string.Format("{0} {1} {2}", category.Name, separator, result);
                }

                alreadyProcessedCategoryIds.Add(category.Id);

                category = categoryService.GetCategoryById(category.StoreId, category.ParentId);

            }
            return result;
        }

        /// <summary>
        /// ��ȡ����������
        /// </summary>
        /// <param name="category"></param>
        /// <param name="categoryService"></param>
        /// <param name="aclService"></param>
        /// <param name="storeMappingService"></param>
        /// <param name="showHidden"></param>
        /// <returns></returns>
        public static IList<Category> GetCategoryBreadCrumb(this Category category,
            ICategoryService categoryService,
            IAclService aclService,
            IStoreMappingService storeMappingService,
            bool showHidden = false)
        {
            if (category == null)
            {
                throw new ArgumentNullException("category");
            }

            var result = new List<Category>();

            var alreadyProcessedCategoryIds = new List<int>() { };

            while (category != null &&
                (showHidden) &&
                !alreadyProcessedCategoryIds.Contains(category.Id))
            {
                result.Add(category);

                alreadyProcessedCategoryIds.Add(category.Id);

                category = categoryService.GetCategoryById(category.StoreId, category.ParentId);
            }
            result.Reverse();
            return result;
        }

    }
}
