using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Domain.Products;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using DCMS.Services.Caching;

namespace DCMS.Services.Products
{
    /// <summary>
    /// ��Ʒ���Է���
    /// </summary>
    public partial class ProductAttributeService : BaseService, IProductAttributeService
    {
        public ProductAttributeService(
            IServiceGetter getter,
            IStaticCacheManager cacheManager,
            IEventPublisher eventPublisher
            ) : base(getter, cacheManager, eventPublisher)
        {

        }

        #region ����

        #region ��Ʒ����

        /// <summary>
        /// ɾ����Ʒ����
        /// </summary>
        /// <param name="productAttribute"></param>
        public virtual void DeleteProductAttribute(ProductAttribute productAttribute)
        {
            if (productAttribute == null)
            {
                throw new ArgumentNullException("productAttribute");
            }

            var uow = ProductAttributeRepository.UnitOfWork;
            ProductAttributeRepository.Delete(productAttribute);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(productAttribute);
        }

        /// <summary>
        /// ��ȡȫ������
        /// </summary>
        /// <returns></returns>
        public virtual IList<ProductAttribute> GetAllProductAttributes(int? store, string name)
        {
            var key = DCMSDefaults.PRODUCTATTRIBUTES_ALL_KEY.FillCacheKey(store ?? 0, name);
            return _cacheManager.Get(key, () =>
            {
                var query = ProductAttributeRepository.Table;

                if (!string.IsNullOrEmpty(name))
                {
                    query = query.Where(x => x.Name.Contains(name));
                }
                //var query = from pa in _productAttributeRepository.Table
                //            orderby pa.Name
                //            select pa;

                var productAttributes = query.OrderBy(x => x.Name).ToList();

                return productAttributes;
            });
        }

        /// <summary>
        /// ��ȡ��Ʒ����
        /// </summary>
        /// <param name="productAttributeId"></param>
        /// <returns></returns>
        public virtual ProductAttribute GetProductAttributeById(int? store, int productAttributeId)
        {
            if (productAttributeId == 0)
            {
                return null;
            }

            return ProductAttributeRepository.ToCachedGetById(productAttributeId);
        }

        /// <summary>
        /// �����Ʒ����
        /// </summary>
        /// <param name="productAttribute"></param>
        public virtual void InsertProductAttribute(ProductAttribute productAttribute)
        {
            if (productAttribute == null)
            {
                throw new ArgumentNullException("productAttribute");
            }

            var uow = ProductAttributeRepository.UnitOfWork;
            ProductAttributeRepository.Insert(productAttribute);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(productAttribute);
        }

        /// <summary>
        /// ������Ʒ����
        /// </summary>
        /// <param name="productAttribute"></param>
        public virtual void UpdateProductAttribute(ProductAttribute productAttribute)
        {
            if (productAttribute == null)
            {
                throw new ArgumentNullException("productAttribute");
            }

            var uow = ProductAttributeRepository.UnitOfWork;
            ProductAttributeRepository.Update(productAttribute);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(productAttribute);
        }

        #endregion

        #region ��Ʒ��������ӳ�� (ProductVariantAttribute)

        /// <summary>
        /// ɾ��һ����Ʒ��������ӳ��
        /// </summary>
        /// <param name="productVariantAttribute"></param>
        public virtual void DeleteProductVariantAttribute(ProductVariantAttribute productVariantAttribute)
        {
            if (productVariantAttribute == null)
            {
                throw new ArgumentNullException("productVariantAttribute");
            }

            var uow = ProductsProductAttributeMappingRepository.UnitOfWork;
            ProductsProductAttributeMappingRepository.Delete(productVariantAttribute);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(productVariantAttribute);
        }

        /// <summary>
        /// ������ƷID��ȡ��Ʒ��������ӳ��
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        public virtual IList<ProductVariantAttribute> GetProductVariantAttributesByProductId(int? store, int productId)
        {
            var key = DCMSDefaults.PRODUCTVARIANTATTRIBUTES_ALL_KEY.FillCacheKey(store ?? 0, productId);

            return _cacheManager.Get(key, () =>
            {
                var query = from pva in ProductsProductAttributeMappingRepository.Table
                            orderby pva.DisplayOrder
                            where pva.ProductId == productId
                            select pva;
                var productVariantAttributes = query.ToList();
                return productVariantAttributes;
            });
        }

        /// <summary>
        /// ��ȡһ����Ʒ��������ӳ��
        /// </summary>
        /// <param name="productVariantAttributeId"></param>
        /// <returns></returns>
        public virtual ProductVariantAttribute GetProductVariantAttributeById(int? store, int productVariantAttributeId)
        {
            if (productVariantAttributeId == 0)
            {
                return null;
            }

            return ProductsProductAttributeMappingRepository.ToCachedGetById(productVariantAttributeId);
        }

        /// <summary>
        /// �����Ʒ��������ӳ��
        /// </summary>
        /// <param name="productVariantAttribute"></param>
        public virtual void InsertProductVariantAttribute(ProductVariantAttribute productVariantAttribute)
        {
            if (productVariantAttribute == null)
            {
                throw new ArgumentNullException("productVariantAttribute");
            }

            var uow = ProductsProductAttributeMappingRepository.UnitOfWork;
            ProductsProductAttributeMappingRepository.Insert(productVariantAttribute);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(productVariantAttribute);
        }

        /// <summary>
        /// ������Ʒ��������ӳ��
        /// </summary>
        /// <param name="productVariantAttribute"></param>
        public virtual void UpdateProductVariantAttribute(ProductVariantAttribute productVariantAttribute)
        {
            if (productVariantAttribute == null)
            {
                throw new ArgumentNullException("productVariantAttribute");
            }

            var uow = ProductsProductAttributeMappingRepository.UnitOfWork;
            ProductsProductAttributeMappingRepository.Update(productVariantAttribute);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(productVariantAttribute);
        }

        #endregion

        #region ��Ʒ��������ֵ (ProductVariantAttributeValue)

        /// <summary>
        /// ɾ������ֵ
        /// </summary>
        /// <param name="productVariantAttributeValue"></param>
        public virtual void DeleteProductVariantAttributeValue(ProductVariantAttributeValue productVariantAttributeValue)
        {
            if (productVariantAttributeValue == null)
            {
                throw new ArgumentNullException("productVariantAttributeValue");
            }

            var uow = ProductVariantAttributeValueRepository.UnitOfWork;
            ProductVariantAttributeValueRepository.Delete(productVariantAttributeValue);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(productVariantAttributeValue);
        }

        /// <summary>
        /// ��ȡ����ֵ�б�
        /// </summary>
        /// <param name="productVariantAttributeId"></param>
        /// <returns></returns>
        public virtual IList<ProductVariantAttributeValue> GetProductVariantAttributeValues(int? store, int productVariantAttributeId)
        {
            var key = DCMSDefaults.PRODUCTVARIANTATTRIBUTEVALUES_ALL_KEY.FillCacheKey(store ?? 0, productVariantAttributeId);
            return _cacheManager.Get(key, () =>
            {
                var query = from pvav in ProductVariantAttributeValueRepository.Table
                            orderby pvav.DisplayOrder
                            where pvav.ProductVariantAttributeId == productVariantAttributeId
                            select pvav;
                var productVariantAttributeValues = query.ToList();
                return productVariantAttributeValues;
            });
        }

        /// <summary>
        /// ��ȡ����ֵ
        /// </summary>
        /// <param name="productVariantAttributeValueId"></param>
        /// <returns></returns>
        public virtual ProductVariantAttributeValue GetProductVariantAttributeValueById(int? store, int productVariantAttributeValueId)
        {
            if (productVariantAttributeValueId == 0)
            {
                return null;
            }

            return ProductVariantAttributeValueRepository.ToCachedGetById(productVariantAttributeValueId);
        }

        /// <summary>
        /// �������ֵ
        /// </summary>
        /// <param name="productVariantAttributeValue"></param>
        public virtual void InsertProductVariantAttributeValue(ProductVariantAttributeValue productVariantAttributeValue)
        {
            if (productVariantAttributeValue == null)
            {
                throw new ArgumentNullException("productVariantAttributeValue");
            }

            var uow = ProductVariantAttributeValueRepository.UnitOfWork;
            ProductVariantAttributeValueRepository.Insert(productVariantAttributeValue);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(productVariantAttributeValue);
        }

        /// <summary>
        /// ��������ֵ
        /// </summary>
        /// <param name="productVariantAttributeValue"></param>
        public virtual void UpdateProductVariantAttributeValue(ProductVariantAttributeValue productVariantAttributeValue)
        {
            if (productVariantAttributeValue == null)
            {
                throw new ArgumentNullException("productVariantAttributeValue");
            }

            var uow = ProductVariantAttributeValueRepository.UnitOfWork;
            ProductVariantAttributeValueRepository.Update(productVariantAttributeValue);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(productVariantAttributeValue);
        }

        #endregion

        #region ��Ʒ����������� (ProductVariantAttributeCombination)

        /// <summary>
        /// ɾ��������� 
        /// </summary>
        /// <param name="combination"></param>
        public virtual void DeleteProductVariantAttributeCombination(ProductVariantAttributeCombination combination)
        {
            if (combination == null)
            {
                throw new ArgumentNullException("combination");
            }

            var uow = ProductVariantAttributeCombinationRepository.UnitOfWork;
            ProductVariantAttributeCombinationRepository.Delete(combination);
            uow.SaveChanges();
            //֪ͨ
            _eventPublisher.EntityDeleted(combination);
        }

        /// <summary>
        /// ��ȡȫ��������� 
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        public virtual IList<ProductVariantAttributeCombination> GetAllProductVariantAttributeCombinations(int productId)
        {
            if (productId == 0)
            {
                return new List<ProductVariantAttributeCombination>();
            }

            var query = from pvac in ProductVariantAttributeCombinationRepository.Table
                        orderby pvac.Id
                        where pvac.ProductId == productId
                        select pvac;
            var combinations = query.ToList();
            return combinations;
        }

        /// <summary>
        /// ��ȡ������� 
        /// </summary>
        /// <param name="productVariantAttributeCombinationId"></param>
        /// <returns></returns>
        public virtual ProductVariantAttributeCombination GetProductVariantAttributeCombinationById(int productVariantAttributeCombinationId)
        {
            if (productVariantAttributeCombinationId == 0)
            {
                return null;
            }

            return ProductVariantAttributeCombinationRepository.ToCachedGetById(productVariantAttributeCombinationId);
        }

        /// <summary>
        /// ���������� 
        /// </summary>
        /// <param name="combination"></param>
        public virtual void InsertProductVariantAttributeCombination(ProductVariantAttributeCombination combination)
        {
            if (combination == null)
            {
                throw new ArgumentNullException("combination");
            }

            var uow = ProductVariantAttributeCombinationRepository.UnitOfWork;
            ProductVariantAttributeCombinationRepository.Insert(combination);
            uow.SaveChanges();

            //e֪ͨ
            _eventPublisher.EntityInserted(combination);
        }

        /// <summary>
        /// ����������� 
        /// </summary>
        /// <param name="combination"></param>
        public virtual void UpdateProductVariantAttributeCombination(ProductVariantAttributeCombination combination)
        {
            if (combination == null)
            {
                throw new ArgumentNullException("combination");
            }

            var uow = ProductVariantAttributeCombinationRepository.UnitOfWork;
            ProductVariantAttributeCombinationRepository.Update(combination);
            uow.SaveChanges();
            //֪ͨ
            _eventPublisher.EntityUpdated(combination);
        }

        #endregion


        #endregion
    }
}
